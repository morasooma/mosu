// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Replays
{
    /// <summary>
    /// Produces a deterministic collision-free route through a Dodge beatmap.
    /// A time-expanded beam search is used so the bot can start moving before
    /// an incoming pattern reaches its current position.
    /// </summary>
    public class DodgeAutoGenerator : AutoGenerator<DodgeReplayFrame>
    {
        private const float position_grid_size = 4;
        private const float state_bin_size = 8;
        private const float state_cell_size = position_grid_size;
        private const int beam_width = 192;
        private const int recovery_beam_width = 2048;
        private const double minimum_step_duration = 25;
        private const double maximum_step_duration = 100;
        private const double transition_collision_step = 25;
        private const float default_collision_safety_margin = 0;
        private const float threat_bin_size = 32;
        private const float clearance_bin_size = 64;
        private const float scored_clearance = 96;
        private const float local_threat_radius = 128;
        private const float reading_threat_radius = 192;
        private const double reading_urgency_horizon = 1000;
        private const double standalone_pattern_family_interval = 125;
        private const double position_prediction_horizon = 1000;
        private const double minimum_recovery_lookback = 6000;
        private const double viability_horizon = 3500;
        private const int viability_step_stride = 16;
        private const float viability_grid_size = 64;
        private const float diversity_bin_size = 32;
        private const int maximum_diverse_states = 64;

        private const int grid_columns = (int)(DodgePlayfield.WIDTH / state_cell_size) + 1;
        private const int grid_rows = (int)(DodgePlayfield.HEIGHT / state_cell_size) + 1;
        private const int coarse_grid_columns = (int)(DodgePlayfield.WIDTH / state_bin_size) + 1;
        private const int coarse_grid_rows = (int)(DodgePlayfield.HEIGHT / state_bin_size) + 1;
        private const int threat_bin_columns = (int)(DodgePlayfield.WIDTH / threat_bin_size) + 1;
        private const int threat_bin_rows = (int)(DodgePlayfield.HEIGHT / threat_bin_size) + 1;
        private const int clearance_bin_columns = (int)(DodgePlayfield.WIDTH / clearance_bin_size) + 1;
        private const int clearance_bin_rows = (int)(DodgePlayfield.HEIGHT / clearance_bin_size) + 1;
        private const int diversity_bin_columns = (int)(DodgePlayfield.WIDTH / diversity_bin_size) + 1;
        private const int diversity_bin_rows = (int)(DodgePlayfield.HEIGHT / diversity_bin_size) + 1;
        private const int viability_grid_columns = (int)(DodgePlayfield.WIDTH / viability_grid_size) + 1;
        private const int viability_grid_rows = (int)(DodgePlayfield.HEIGHT / viability_grid_size) + 1;

        private readonly DodgeHitObject[] hitObjects;
        private readonly DodgeArenaStateEvaluator arenaEvaluator;
        private readonly DodgeCameraStateEvaluator cameraEvaluator;
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<BeamThreat> beams = new List<BeamThreat>();
        private readonly float movementSpeed;
        private readonly float playerSize;
        private readonly float collisionSafetyMargin;
        private readonly CancellationToken cancellationToken;
        private readonly int[] candidateIndices = new int[grid_columns * grid_rows];
        private readonly int[] candidateIndexVersions = new int[grid_columns * grid_rows];
        // Candidate expansion can fill almost the entire playfield grid, while a
        // normal beam only retains 192 states. Keep one indexed sorting buffer
        // and copy only the retained beam into each history entry; this avoids
        // both dictionary work in the hot loop and oversized history arrays.
        private readonly PlannerState[] candidateSortBuffer = new PlannerState[grid_columns * grid_rows];
        private readonly bool[] selectedCandidateCells = new bool[grid_columns * grid_rows];
        private readonly bool[] occupiedDiversityBins = new bool[diversity_bin_columns * diversity_bin_rows];
        private readonly double[] positionScoreBuffer = new double[grid_columns * grid_rows];
        private readonly int[] positionScoreVersions = new int[grid_columns * grid_rows];
        private readonly bool[] trajectorySafetyBuffer = new bool[threat_bin_columns * threat_bin_rows];
        private readonly int[] trajectorySafetyVersions = new int[threat_bin_columns * threat_bin_rows];
        private readonly List<Projectile>?[] transitionThreatBuffer = new List<Projectile>?[threat_bin_columns * threat_bin_rows];
        private readonly List<Projectile>?[] currentClearanceThreatBuffer = new List<Projectile>?[clearance_bin_columns * clearance_bin_rows];
        private readonly List<Projectile>?[] futureClearanceThreatBuffer = new List<Projectile>?[clearance_bin_columns * clearance_bin_rows];
        private readonly List<Projectile>?[] projectedThreatBuffer = new List<Projectile>?[threat_bin_columns * threat_bin_rows];
        private readonly List<Projectile> projectedProjectiles = new List<Projectile>();
        private readonly List<BeamThreat> projectedBeams = new List<BeamThreat>();
        private readonly HashSet<int> actionCells = new HashSet<int>();
        private readonly Dictionary<double, Vector2> cameraStateCache = new Dictionary<double, Vector2>();
        private readonly bool hasCameraChanges;
        private byte[][] viabilityMap = Array.Empty<byte[]>();
        private byte maximumViability;
        private int candidateIndexVersion;
        private int positionScoreVersion;

        public DodgeAutoplayAnalysis Analysis { get; private set; }

        public IReadOnlyList<DodgeAutoplayRoutePoint> Route { get; private set; } = Array.Empty<DodgeAutoplayRoutePoint>();

        public DodgeAutoGenerator(
            IBeatmap beatmap,
            CancellationToken cancellationToken = default,
            float collisionSafetyMargin = default_collision_safety_margin)
            : base(beatmap)
        {
            hitObjects = beatmap.HitObjects.OfType<DodgeHitObject>().ToArray();
            arenaEvaluator = new DodgeArenaStateEvaluator(hitObjects.OfType<DodgeArenaChange>());
            DodgeCameraChange[] cameraChanges = hitObjects.OfType<DodgeCameraChange>().ToArray();
            cameraEvaluator = new DodgeCameraStateEvaluator(cameraChanges);
            hasCameraChanges = cameraChanges.Length > 0;
            movementSpeed = (float)(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty) / 1000);
            playerSize = DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty);
            this.collisionSafetyMargin = Math.Max(0, collisionSafetyMargin);
            this.cancellationToken = cancellationToken;
            createProjectiles();
        }

        protected override void GenerateFrames()
        {
            Analysis = default;
            Route = Array.Empty<DodgeAutoplayRoutePoint>();
            cancellationToken.ThrowIfCancellationRequested();

            int threatCount = projectiles.Count + beams.Count;
            double firstThreatTime = threatCount == 0
                ? 0
                : Math.Min(
                    projectiles.Select(projectile => projectile.StartTime).DefaultIfEmpty(double.PositiveInfinity).Min(),
                    beams.Select(beam => beam.StartTime).DefaultIfEmpty(double.PositiveInfinity).Min());
            double lastThreatTime = threatCount == 0
                ? 1000
                : Math.Max(
                    projectiles.Select(projectile => projectile.EndTime).DefaultIfEmpty(double.NegativeInfinity).Max(),
                    beams.Select(beam => beam.EndTime).DefaultIfEmpty(double.NegativeInfinity).Max());
            double positioningLeadTime = DodgePlayfield.BASE_SIZE.Length / Math.Max(0.001f, movementSpeed) + 250;
            // There is no useful route to search while the playfield is empty.
            // Start only early enough for the player to reach any point before the
            // first threat; this keeps editor route previews fast for late sections.
            double startTime = threatCount == 0 ? 0 : firstThreatTime - positioningLeadTime;
            double timeStep = Math.Clamp(state_bin_size * Math.Sqrt(2) / Math.Max(0.001f, movementSpeed), minimum_step_duration, maximum_step_duration);

            var times = new List<double> { startTime };

            while (times[^1] < lastThreatTime)
            {
                cancellationToken.ThrowIfCancellationRequested();
                times.Add(Math.Min(lastThreatTime, times[^1] + timeStep));
            }

            if (times.Count == 1)
                times.Add(startTime + timeStep);

            List<Projectile>[] activeProjectiles = buildActiveProjectileLists(times);
            List<BeamThreat>[] activeBeams = buildActiveBeamLists(times);
            List<Vector2>[] movementOffsets = new List<Vector2>[times.Count];
            List<Vector2> regularMovementOffsets = createMovementOffsets(timeStep, state_bin_size);
            List<Vector2> preciseMovementOffsets = createMovementOffsets(timeStep, position_grid_size);

            for (int step = 1; step < times.Count; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double stepDuration = times[step] - times[step - 1];
                bool requiresPreciseMovement = activeProjectiles[step].Count > 0 || activeBeams[step].Count > 0;

                if (Math.Abs(stepDuration - timeStep) < 0.001)
                {
                    movementOffsets[step] = requiresPreciseMovement ? preciseMovementOffsets : regularMovementOffsets;
                }
                else
                {
                    movementOffsets[step] = createMovementOffsets(stepDuration, requiresPreciseMovement ? position_grid_size : state_bin_size);
                }
            }

            DodgeArenaState[] arenas = times.Select(arenaEvaluator.Evaluate).ToArray();
            viabilityMap = buildViabilityMap(times, arenas, activeProjectiles, activeBeams, timeStep);
            var history = new List<List<PlannerState>>(times.Count);

            DodgeArenaState initialArena = arenas[0];
            Vector2 initialPosition = snapToGridWithinArena(
                DodgePlayfield.BASE_SIZE / 2,
                initialArena);
            history.Add(new List<PlannerState>
            {
                createPlannerState(
                    cellKey(initialPosition),
                    initialPosition,
                    0,
                    -1,
                    false,
                    viabilityAt(0, initialPosition)),
            });

            for (int step = 1; step < times.Count; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<PlannerState> nextStates = expandStep(
                    history[^1], times, arenas, activeProjectiles, activeBeams, movementOffsets, step, beam_width);

                if (nextStates.Count > 0)
                {
                    history.Add(nextStates);
                    continue;
                }

                // A locally attractive route can still lead into a pocket. Rebuild
                // the recent route without beam pruning before declaring the
                // pattern impossible; this keeps alternate corridors alive.
                if (history[^1].Any(state => !state.UsedFallback)
                    && tryRecoverRoute(history, times, arenas, activeProjectiles, activeBeams, movementOffsets, step))
                    continue;

                // An actually impossible wall must not cause a teleport through
                // bullets. Keep normal movement and choose the least dangerous
                // reachable cell so autoplay can recover after the wall has passed.
                history.Add(createReachableFallback(
                    history[^1],
                    arenas[step - 1],
                    arenas[step],
                    times[step - 1],
                    times[step],
                    activeProjectiles[step],
                    activeBeams[step],
                    movementOffsets[step],
                    step));
            }

            List<DodgeAutoplayRoutePoint> route = backtrackRoute(history, times);
            Route = route;
            Analysis = analyseRoute(
                route,
                history,
                times,
                arenas,
                activeProjectiles,
                activeBeams,
                movementOffsets,
                countCollisionSections(route, times, arenas, activeProjectiles, activeBeams));
            addReplayFrames(route);
        }

        private List<PlannerState> expandStep(
            IReadOnlyList<PlannerState> previousStates,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<BeamThreat>> activeBeams,
            IReadOnlyList<List<Vector2>> movementOffsets,
            int step,
            int width,
            bool usePreciseStateCells = false)
        {
            double previousTime = times[step - 1];
            double currentTime = times[step];
            DodgeArenaState previousArena = arenas[step - 1];
            DodgeArenaState currentArena = arenas[step];
            int candidateVersion = nextCandidateIndexVersion();
            int candidateCount = 0;
            int scoreVersion = nextPositionScoreVersion();
            float maximumTransitionMovement =
                movementSpeed * (float)(currentTime - previousTime) + maximumArenaBoundaryMovement(previousArena, currentArena);
            List<Projectile>?[] transitionThreats = buildTransitionThreatMap(
                activeProjectiles[step],
                previousTime,
                currentTime,
                maximumTransitionMovement,
                transitionThreatBuffer);
            int futureStep = Math.Min(
                times.Count - 1,
                step + Math.Max(1, (int)Math.Round(position_prediction_horizon / Math.Max(1, currentTime - previousTime))));
            List<Projectile>?[] currentClearanceThreats = buildClearanceThreatMap(activeProjectiles[step], currentTime, currentClearanceThreatBuffer);
            List<Projectile>?[] futureClearanceThreats = buildClearanceThreatMap(activeProjectiles[futureStep], times[futureStep], futureClearanceThreatBuffer);
            projectedProjectiles.Clear();

            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];

                if (projectile.EndTime >= currentTime && projectile.StartTime <= times[futureStep])
                    projectedProjectiles.Add(projectile);
            }

            projectedBeams.Clear();

            for (int i = 0; i < beams.Count; i++)
            {
                BeamThreat beam = beams[i];

                if (beam.EndTime >= currentTime && beam.StartTime <= times[futureStep])
                    projectedBeams.Add(beam);
            }

            List<Projectile>?[] projectedThreats = buildTransitionThreatMap(
                projectedProjectiles,
                currentTime,
                times[futureStep],
                0,
                projectedThreatBuffer);

            for (int parentIndex = 0; parentIndex < previousStates.Count; parentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlannerState previous = previousStates[parentIndex];
                Vector2 from = DodgePlayer.ClampToArena(
                    previous.Position,
                    previousArena.Position,
                    previousArena.Size,
                    playerSize,
                    previousArena.Rotation);
                Vector2 carriedFrom = cameraCarriedPosition(from, previousTime, currentTime, currentArena);

                foreach (Vector2 offset in movementOffsets[step])
                {
                    Vector2 target = snapToGridWithinArena(carriedFrom + offset, currentArena);

                    if ((target - carriedFrom).Length > maximumTransitionMovement + 0.01f)
                        continue;

                    int key = cellKey(target, usePreciseStateCells);
                    int trajectoryKey = threatBinKey(target);
                    double positionScore;

                    // Once a safe candidate has established the score for this
                    // state cell, a lower-scoring transition cannot affect the
                    // retained state. Avoid repeating its comparatively expensive
                    // swept collision test during wide recovery searches.
                    if (positionScoreVersions[key] == scoreVersion)
                    {
                        positionScore = positionScoreBuffer[key];
                        double knownScore = previous.Score + positionScore - offset.Length * 0.08;

                        if (candidateIndexVersions[key] == candidateVersion
                            && knownScore <= candidateSortBuffer[candidateIndices[key]].Score)
                            continue;
                    }

                    IReadOnlyList<Projectile> relevantThreats =
                        transitionThreats[trajectoryKey] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>();

                    if (!isTransitionSafe(from, target, previousTime, currentTime, previousArena, currentArena, relevantThreats, activeBeams[step]))
                        continue;

                    if (positionScoreVersions[key] != scoreVersion)
                    {
                        positionScoreVersions[key] = scoreVersion;

                        if (trajectorySafetyVersions[trajectoryKey] != scoreVersion)
                        {
                            trajectorySafetyVersions[trajectoryKey] = scoreVersion;
                            Vector2 representative = threatBinCentre(trajectoryKey, currentArena);
                            Vector2 carriedRepresentative = cameraCarriedPosition(
                                representative,
                                currentTime,
                                times[futureStep],
                                arenas[futureStep]);
                            trajectorySafetyBuffer[trajectoryKey] = isTransitionSafe(
                                representative,
                                carriedRepresentative,
                                currentTime,
                                times[futureStep],
                                currentArena,
                                arenas[futureStep],
                                projectedThreats[trajectoryKey] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>(),
                                projectedBeams);
                        }

                        positionScoreBuffer[key] = positionScore = scorePosition(
                            target,
                            currentTime,
                            currentArena,
                            currentClearanceThreats[clearanceBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>(),
                            activeBeams[step],
                            times[futureStep],
                            futureClearanceThreats[clearanceBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>(),
                            activeBeams[futureStep]);
                    }
                    else
                        positionScore = positionScoreBuffer[key];

                    // Once the viability tier is equal, prefer the least input
                    // needed to stay safe. A large clearance reward made the bot
                    // roam around easy patterns and inflated their movement
                    // difficulty merely to gain cosmetic extra space.
                    double score = previous.Score + positionScore - offset.Length * 0.08;
                    PlannerState state = createPlannerState(
                        key,
                        target,
                        score,
                        parentIndex,
                        false,
                        viabilityAt(step, target),
                        trajectorySafetyBuffer[trajectoryKey]);

                    addCandidate(state, candidateVersion, ref candidateCount);
                }
            }

            return takeBestCandidates(candidateCount, width);
        }

        private int nextCandidateIndexVersion()
        {
            if (candidateIndexVersion == int.MaxValue)
            {
                Array.Clear(candidateIndexVersions);
                candidateIndexVersion = 0;
            }

            return ++candidateIndexVersion;
        }

        private void addCandidate(PlannerState candidate, int version, ref int count)
        {
            int key = candidate.CellKey;

            if (candidateIndexVersions[key] != version)
            {
                candidateIndexVersions[key] = version;
                candidateIndices[key] = count;
                candidateSortBuffer[count++] = candidate;
                return;
            }

            int index = candidateIndices[key];

            if (candidate.Score > candidateSortBuffer[index].Score)
                candidateSortBuffer[index] = candidate;
        }

        private int nextPositionScoreVersion()
        {
            if (positionScoreVersion == int.MaxValue)
            {
                Array.Clear(positionScoreVersions);
                Array.Clear(trajectorySafetyVersions);
                positionScoreVersion = 0;
            }

            return ++positionScoreVersion;
        }

        private List<PlannerState> takeBestCandidates(int candidateCount, int width)
        {
            Array.Sort(candidateSortBuffer, 0, candidateCount, PlannerStateScoreComparer.Instance);

            int resultCount = Math.Min(width, candidateCount);
            var result = new List<PlannerState>(resultCount);

            if (resultCount == candidateCount)
            {
                for (int i = 0; i < resultCount; i++)
                    result.Add(candidateSortBuffer[i]);

                return result;
            }

            Array.Clear(selectedCandidateCells);
            Array.Clear(occupiedDiversityBins);
            int minimumDiverseViability = Math.Max(0, candidateSortBuffer[0].Viability - 2);

            // A pure global top-N beam rapidly collapses into one attractive
            // pocket. Preserve one strong state per 32px region so an alternate
            // corridor survives until the backward viability signal can
            // distinguish it from a dead end.
            for (int i = 0; i < candidateCount
                            && result.Count < resultCount
                            && result.Count < maximum_diverse_states; i++)
            {
                PlannerState candidate = candidateSortBuffer[i];

                if (candidate.Viability < minimumDiverseViability)
                    break;

                int diversityKey = diversityBinKey(candidate.Position);

                if (occupiedDiversityBins[diversityKey])
                    continue;

                occupiedDiversityBins[diversityKey] = true;
                selectedCandidateCells[candidate.CellKey] = true;
                result.Add(candidate);
            }

            for (int i = 0; i < candidateCount && result.Count < resultCount; i++)
            {
                PlannerState candidate = candidateSortBuffer[i];

                if (selectedCandidateCells[candidate.CellKey])
                    continue;

                selectedCandidateCells[candidate.CellKey] = true;
                result.Add(candidate);
            }

            return result;
        }

        private bool tryRecoverRoute(
            List<List<PlannerState>> history,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<BeamThreat>> activeBeams,
            IReadOnlyList<List<Vector2>> movementOffsets,
            int failedStep)
        {
            int checkpoint = failedStep - 1;
            double recoveryLookback = Math.Max(
                minimum_recovery_lookback,
                DodgePlayfield.BASE_SIZE.Length / Math.Max(0.001f, movementSpeed) + 250);
            double earliestTime = times[failedStep] - recoveryLookback;

            while (checkpoint > 0 && times[checkpoint] > earliestTime)
                checkpoint--;

            return tryRebuildRoute(
                       history,
                       times,
                       arenas,
                       activeProjectiles,
                       activeBeams,
                       movementOffsets,
                       failedStep,
                       checkpoint,
                       true)
                   || tryRebuildRoute(
                       history,
                       times,
                       arenas,
                       activeProjectiles,
                       activeBeams,
                       movementOffsets,
                       failedStep,
                       checkpoint,
                       false);
        }

        private bool tryRebuildRoute(
            List<List<PlannerState>> history,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<BeamThreat>> activeBeams,
            IReadOnlyList<List<Vector2>> movementOffsets,
            int failedStep,
            int checkpoint,
            bool usePreciseStateCells)
        {

            var rebuilt = new List<List<PlannerState>>(failedStep - checkpoint);
            IReadOnlyList<PlannerState> previousStates = history[checkpoint];

            for (int step = checkpoint + 1; step <= failedStep; step++)
            {
                List<PlannerState> states = expandStep(
                    previousStates,
                    times,
                    arenas,
                    activeProjectiles,
                    activeBeams,
                    movementOffsets,
                    step,
                    recovery_beam_width,
                    usePreciseStateCells);

                if (states.Count == 0)
                    return false;

                rebuilt.Add(states);
                previousStates = states;
            }

            history.RemoveRange(checkpoint + 1, history.Count - checkpoint - 1);
            history.AddRange(rebuilt);
            return true;
        }

        private int countCollisionSections(
            IReadOnlyList<DodgeAutoplayRoutePoint> route,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<BeamThreat>> activeBeams)
        {
            int collisionSections = 0;
            bool previousCollided = false;

            for (int step = 1; step < route.Count; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool collided = !isTransitionSafe(
                    route[step - 1].Position,
                    route[step].Position,
                    times[step - 1],
                    times[step],
                    arenas[step - 1],
                    arenas[step],
                    activeProjectiles[step],
                    activeBeams[step]);

                if (collided && !previousCollided)
                    collisionSections++;

                previousCollided = collided;
            }

            return collisionSections;
        }

        private DodgeAutoplayAnalysis analyseRoute(
            IReadOnlyList<DodgeAutoplayRoutePoint> route,
            IReadOnlyList<List<PlannerState>> history,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<BeamThreat>> activeBeams,
            IReadOnlyList<List<Vector2>> movementOffsets,
            int collisionCount)
        {
            int threatCount = projectiles.Count + beams.Count;

            if (threatCount == 0 || route.Count < 2)
                return default;

            double startTime = Math.Min(
                projectiles.Select(projectile => projectile.StartTime).DefaultIfEmpty(double.PositiveInfinity).Min(),
                beams.Select(beam => beam.StartTime).DefaultIfEmpty(double.PositiveInfinity).Min());
            double endTime = Math.Max(
                projectiles.Select(projectile => projectile.EndTime).DefaultIfEmpty(double.NegativeInfinity).Max(),
                beams.Select(beam => beam.EndTime).DefaultIfEmpty(double.NegativeInfinity).Max());
            double duration = Math.Max(1, endTime - startTime);
            double movementRatioSum = 0;
            double pressureSum = 0;
            double peakMovementRatio = 0;
            double peakPressure = 0;
            int peakActiveProjectiles = 0;
            int sampleCount = 0;
            int teleportCount = 0;
            Vector2? previousDirection = null;
            double previousMovementRatio = 0;
            bool hasPreviousMovementRatio = false;
            ushort previousSafeDirectionMask = 0;
            var seenRelevantPatterns = new HashSet<int>();
            var currentRelevantPatterns = new HashSet<int>();
            var currentComplexPatterns = new HashSet<int>();
            var currentPatternUrgencies = new Dictionary<int, double>();
            double effectiveReadingPatternCount = 0;
            double threatUrgencySum = 0;
            double peakThreatUrgency = 0;
            int threatUrgencySampleCount = 0;
            int peakConcurrentPatterns = 0;
            var sectionPeaks = new Dictionary<int, double>();
            var sectionMovementPeaks = new Dictionary<int, double>();
            var sectionPressurePeaks = new Dictionary<int, double>();
            var difficultySections = new Dictionary<int, SectionDifficultyAccumulator>();
            bool[][] successfulRouteStates = buildSuccessfulRouteStateMask(history);

            for (int i = 1; i < route.Count; i++)
            {
                DodgeAutoplayRoutePoint previous = route[i - 1];
                DodgeAutoplayRoutePoint current = route[i];

                if (current.Time < startTime || previous.Time > endTime)
                    continue;

                double segmentStart = Math.Max(startTime, previous.Time);
                double segmentEnd = Math.Min(endTime, current.Time);

                if (segmentEnd <= segmentStart)
                    continue;

                float startProgress = (float)((segmentStart - previous.Time) / Math.Max(0.001, current.Time - previous.Time));
                float endProgress = (float)((segmentEnd - previous.Time) / Math.Max(0.001, current.Time - previous.Time));
                Vector2 startPosition = Vector2.Lerp(previous.Position, current.Position, startProgress);
                Vector2 endPosition = Vector2.Lerp(previous.Position, current.Position, endProgress);
                DodgeArenaState endArena = arenaEvaluator.Evaluate(segmentEnd);
                Vector2 passiveEndPosition = DodgePlayer.ClampToArena(
                    startPosition + evaluateCamera(segmentEnd) - evaluateCamera(segmentStart),
                    endArena.Position,
                    endArena.Size,
                    playerSize,
                    endArena.Rotation);
                // Camera scroll and a moving/rotating arena can carry or clamp the
                // player without any input. Only deliberate displacement is
                // movement strain; otherwise cosmetic motion changes stars.
                Vector2 displacement = endPosition - passiveEndPosition;
                double movementRatio = current.UsedFallback
                    ? 1
                    : displacement.Length / Math.Max(0.001, movementSpeed * (segmentEnd - segmentStart));

                // A fallback can span many planner steps. Count the transition into
                // one contiguous fallback section rather than every sampled frame,
                // otherwise the same impossible pattern gains difficulty merely
                // from being sampled more frequently.
                if (current.UsedFallback && !previous.UsedFallback)
                    teleportCount++;

                movementRatio = Math.Clamp(movementRatio, 0, 1);
                double sampleTime = segmentEnd;
                double clearance = calculateClearanceAndActive(endPosition, sampleTime, out int nearbyThreats);
                double pressure = Math.Clamp(1 - clearance / 96, 0, 1);
                double turn = 0;

                if (displacement.LengthSquared > 0.001f)
                {
                    Vector2 direction = displacement.Normalized();

                    if (previousDirection.HasValue)
                        turn = (1 - Math.Clamp(Vector2.Dot(previousDirection.Value, direction), -1, 1)) / 2;

                    previousDirection = direction;
                }

                double acceleration = hasPreviousMovementRatio
                    ? Math.Abs(movementRatio - previousMovementRatio)
                    : 0;
                previousMovementRatio = movementRatio;
                hasPreviousMovementRatio = true;

                movementRatioSum += movementRatio;
                pressureSum += pressure;
                peakMovementRatio = Math.Max(peakMovementRatio, movementRatio);
                peakPressure = Math.Max(peakPressure, pressure);
                peakActiveProjectiles = Math.Max(peakActiveProjectiles, nearbyThreats);
                sampleCount++;

                double localStrain = movementRatio * 1.35
                                     + pressure * (1.25 + Math.Log2(1 + nearbyThreats) * 0.25)
                                     + turn * 0.45;
                int section = (int)Math.Floor((sampleTime - startTime) / 400);
                double movementSample = movementRatio + turn * 0.35 + acceleration * 0.25;
                // The retained beam describes how many spatially distinct route
                // branches remain, while local action freedom measures how many
                // exact movements from the selected route are collision-free.
                // The latter is what separates a wide, forgiving pattern from a
                // pixel-perfect corridor even when both have one topological
                // route through the map.
                double branchRestriction = calculatePathRestriction(
                    history[i],
                    successfulRouteStates[i],
                    endArena,
                    out double branchInformation);
                double actionRestriction = calculateActionRestriction(
                    previous.Position,
                    times[i - 1],
                    times[i],
                    arenas[i - 1],
                    arenas[i],
                    activeProjectiles[i],
                    activeBeams[i],
                    movementOffsets[i],
                    out double actionInformation,
                    out ushort safeDirectionMask);
                double actionChurn = previousSafeDirectionMask == 0
                    ? 0
                    : maskDistance(previousSafeDirectionMask, safeDirectionMask);
                previousSafeDirectionMask = safeDirectionMask;

                currentRelevantPatterns.Clear();
                currentComplexPatterns.Clear();
                currentPatternUrgencies.Clear();
                ushort trajectoryDirectionMask = 0;
                collectRelevantPatterns(
                    activeProjectiles[i],
                    activeBeams[i],
                    sampleTime,
                    endPosition,
                    endArena,
                    currentRelevantPatterns,
                    currentComplexPatterns,
                    currentPatternUrgencies,
                    ref trajectoryDirectionMask);

                double effectiveActivePatternLoad = currentPatternUrgencies.Values.Sum();
                double sampleThreatUrgency = currentPatternUrgencies.Values.DefaultIfEmpty().Max();
                double averageThreatUrgency = currentRelevantPatterns.Count == 0
                    ? 0
                    : effectiveActivePatternLoad / currentRelevantPatterns.Count;
                double newPatternLoad = 0;

                foreach (int pattern in currentRelevantPatterns)
                {
                    if (seenRelevantPatterns.Add(pattern))
                    {
                        double urgency = currentPatternUrgencies.GetValueOrDefault(pattern);
                        // A new pattern still has a small reading cost when it is
                        // slow, but its full value is earned only when the object
                        // demands an imminent decision along the reference route.
                        newPatternLoad += 0.15 + urgency * 0.85;
                    }
                }

                effectiveReadingPatternCount += newPatternLoad;
                peakConcurrentPatterns = Math.Max(peakConcurrentPatterns, currentRelevantPatterns.Count);
                peakThreatUrgency = Math.Max(peakThreatUrgency, sampleThreatUrgency);

                if (currentRelevantPatterns.Count > 0)
                {
                    threatUrgencySum += averageThreatUrgency;
                    threatUrgencySampleCount++;
                }

                double directionCoverage = System.Numerics.BitOperations.PopCount(trajectoryDirectionMask) / 12.0;
                // Intermediate crossing angles require the most parsing. A
                // perfectly symmetric radial ring is predictable and should not
                // automatically receive maximum reading merely for filling all
                // angular bins.
                double directionComplexity = CalculateDirectionComplexity(directionCoverage, averageThreatUrgency);
                double complexPatternUrgency = currentComplexPatterns.Sum(pattern => currentPatternUrgencies.GetValueOrDefault(pattern));
                double complexPatternRatio = effectiveActivePatternLoad <= 0
                    ? 0
                    : complexPatternUrgency / effectiveActivePatternLoad;
                double pathUrgency = Math.Max(sampleThreatUrgency, pressure);
                double pathRestriction = (actionInformation * 0.85 + branchInformation * 0.15)
                                         * (0.3 + pathUrgency * 0.7);

                if (!difficultySections.TryGetValue(section, out SectionDifficultyAccumulator? difficultySection))
                    difficultySections[section] = difficultySection = new SectionDifficultyAccumulator();

                difficultySection.Add(
                    movementSample,
                    pathRestriction,
                    branchRestriction,
                    actionRestriction,
                    newPatternLoad,
                    effectiveActivePatternLoad,
                    directionComplexity,
                    complexPatternRatio,
                    actionChurn * (0.35 + pathUrgency * 0.65),
                    nearbyThreats > 0 || currentRelevantPatterns.Count > 0);

                if (!sectionPeaks.TryGetValue(section, out double existing) || localStrain > existing)
                    sectionPeaks[section] = localStrain;

                if (!sectionMovementPeaks.TryGetValue(section, out existing) || movementRatio > existing)
                    sectionMovementPeaks[section] = movementRatio;

                if (!sectionPressurePeaks.TryGetValue(section, out existing) || pressure > existing)
                    sectionPressurePeaks[section] = pressure;
            }

            double weightedStrain = weightedPeakAverage(sectionPeaks.Values);
            double weightedMovement = weightedPeakAverage(sectionMovementPeaks.Values);
            double weightedPressure = weightedPeakAverage(sectionPressurePeaks.Values);
            double[] sectionMovementDifficulties = difficultySections.Values
                                                                    .Where(section => section.IsRelevant)
                                                                    .Select(section => section.MovementDifficulty)
                                                                    .ToArray();
            double[] sectionPathDifficulties = difficultySections.Values
                                                                .Where(section => section.IsRelevant)
                                                                .Select(section => section.PathDifficulty)
                                                                .ToArray();
            double[] sectionReadingDifficulties = difficultySections.Values
                                                                   .Where(section => section.IsRelevant)
                                                                   .Select(section => section.ReadingDifficulty)
                                                                   .ToArray();
            double[] sectionBranchDifficulties = difficultySections.Values
                                                                  .Where(section => section.IsRelevant)
                                                                  .Select(section => section.BranchDifficulty)
                                                                  .ToArray();
            double[] sectionActionDifficulties = difficultySections.Values
                                                                  .Where(section => section.IsRelevant)
                                                                  .Select(section => section.ActionDifficulty)
                                                                  .ToArray();
            double movementDifficulty = AggregateSectionDifficulty(sectionMovementDifficulties);
            double pathDifficulty = AggregateSectionDifficulty(sectionPathDifficulties);
            double readingDifficulty = AggregateSectionDifficulty(sectionReadingDifficulties);
            double[] combinedSectionDifficulties = difficultySections.Values
                                                                      .Where(section => section.IsRelevant)
                                                                      .Select(section => section.MovementDifficulty
                                                                                         + section.PathDifficulty
                                                                                         + section.ReadingDifficulty)
                                                                      .ToArray();

            return new DodgeAutoplayAnalysis(
                threatCount,
                duration,
                sampleCount == 0 ? 0 : movementRatioSum / sampleCount,
                peakMovementRatio,
                sampleCount == 0 ? 0 : pressureSum / sampleCount,
                peakPressure,
                peakActiveProjectiles,
                weightedMovement,
                weightedPressure,
                weightedStrain,
                AggregateSectionDifficulty(sectionBranchDifficulties),
                AggregateSectionDifficulty(sectionActionDifficulties),
                pathDifficulty,
                movementDifficulty,
                readingDifficulty,
                sectionPathDifficulties.DefaultIfEmpty().Max(),
                sectionMovementDifficulties.DefaultIfEmpty().Max(),
                sectionReadingDifficulties.DefaultIfEmpty().Max(),
                CalculateDifficultSectionCount(combinedSectionDifficulties),
                seenRelevantPatterns.Count,
                effectiveReadingPatternCount,
                threatUrgencySampleCount == 0 ? 0 : threatUrgencySum / threatUrgencySampleCount,
                peakThreatUrgency,
                peakConcurrentPatterns,
                teleportCount,
                collisionCount,
                playerSize);
        }

        private static bool[][] buildSuccessfulRouteStateMask(IReadOnlyList<List<PlannerState>> history)
        {
            var result = history.Select(states => new bool[states.Count]).ToArray();

            for (int state = 0; state < result[^1].Length; state++)
                result[^1][state] = !history[^1][state].UsedFallback;

            for (int step = history.Count - 1; step > 0; step--)
            {
                for (int state = 0; state < history[step].Count; state++)
                {
                    if (!result[step][state] || history[step][state].UsedFallback)
                        continue;

                    int parent = history[step][state].ParentIndex;

                    if (parent >= 0)
                        result[step - 1][parent] = true;
                }
            }

            return result;
        }

        private double calculatePathRestriction(
            IReadOnlyList<PlannerState> states,
            IReadOnlyList<bool> successfulStates,
            DodgeArenaState arena,
            out double information)
        {
            Array.Clear(occupiedDiversityBins);
            int occupied = 0;

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                if (!successfulStates[stateIndex])
                    continue;

                PlannerState state = states[stateIndex];

                if (state.UsedFallback || state.Viability == 0)
                    continue;

                int key = diversityBinKey(state.Position);

                if (occupiedDiversityBins[key])
                    continue;

                occupiedDiversityBins[key] = true;
                occupied++;
            }

            int capacity = 0;

            for (int row = 0; row < diversity_bin_rows; row++)
            {
                for (int column = 0; column < diversity_bin_columns; column++)
                {
                    Vector2 centre = new Vector2(
                        Math.Min(DodgePlayfield.WIDTH, (column + 0.5f) * diversity_bin_size),
                        Math.Min(DodgePlayfield.HEIGHT, (row + 0.5f) * diversity_bin_size));

                    if (isInsideArena(centre, arena))
                        capacity++;
                }
            }

            if (capacity == 0)
            {
                information = 0;
                return 1;
            }

            int effectiveCapacity = Math.Min(capacity, maximum_diverse_states);
            double freedom = Math.Clamp((double)occupied / effectiveCapacity, 0, 1);
            information = -Math.Log2(Math.Max(freedom, 1.0 / effectiveCapacity));
            return Math.Pow(1 - freedom, 1.2);
        }

        private double calculateActionRestriction(
            Vector2 previousPosition,
            double previousTime,
            double time,
            DodgeArenaState previousArena,
            DodgeArenaState arena,
            IReadOnlyList<Projectile> activeProjectiles,
            IReadOnlyList<BeamThreat> activeBeams,
            IReadOnlyList<Vector2> movementOffsets,
            out double information,
            out ushort safeDirectionMask)
        {
            Vector2 from = DodgePlayer.ClampToArena(
                previousPosition,
                previousArena.Position,
                previousArena.Size,
                playerSize,
                previousArena.Rotation);
            Vector2 carriedFrom = cameraCarriedPosition(from, previousTime, time, arena);
            float maximumMovement = movementSpeed * (float)(time - previousTime)
                                    + maximumArenaBoundaryMovement(previousArena, arena);
            List<Projectile>?[] transitionThreats = buildTransitionThreatMap(
                activeProjectiles,
                previousTime,
                time,
                maximumMovement,
                transitionThreatBuffer);
            int reachableActions = 0;
            int safeActions = 0;
            safeDirectionMask = 0;
            actionCells.Clear();

            foreach (Vector2 offset in movementOffsets)
            {
                Vector2 target = snapToGridWithinArena(carriedFrom + offset, arena);

                if ((target - carriedFrom).Length > maximumMovement + 0.01f)
                    continue;

                if (!actionCells.Add(cellKey(target)))
                    continue;

                reachableActions++;
                IReadOnlyList<Projectile> relevantThreats =
                    transitionThreats[threatBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>();

                if (isTransitionSafe(from, target, previousTime, time, previousArena, arena, relevantThreats, activeBeams))
                {
                    safeActions++;
                    safeDirectionMask |= actionDirectionMask(target - carriedFrom);
                }
            }

            if (reachableActions == 0)
            {
                information = 0;
                return 1;
            }

            double freedom = (double)safeActions / reachableActions;
            information = -Math.Log2(Math.Max(freedom, 1.0 / reachableActions));
            return Math.Pow(1 - freedom, 0.75);
        }

        private void collectRelevantPatterns(
            IReadOnlyList<Projectile> activeProjectiles,
            IReadOnlyList<BeamThreat> activeBeams,
            double time,
            Vector2 routePosition,
            DodgeArenaState arena,
            HashSet<int> patterns,
            HashSet<int> complexPatterns,
            Dictionary<int, double> patternUrgencies,
            ref ushort trajectoryDirectionMask)
        {
            Vector2 cameraOffset = evaluateCamera(time);

            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                Projectile projectile = activeProjectiles[i];

                if (time < projectile.StartTime || time > projectile.EndTime)
                    continue;

                Vector2 position = projectile.PositionAt(time, cameraOffset);
                double clearance = projectileClearance(position, projectile.BulletSize, routePosition);

                if (!projectileIntersectsArena(position, projectile.BulletSize, arena)
                    || clearance > reading_threat_radius)
                    continue;

                patterns.Add(projectile.PatternId);

                if (projectile.MovementType != DodgeMovementType.Linear
                    || projectile.MovementEasing != DodgeMovementEasing.Linear)
                {
                    complexPatterns.Add(projectile.PatternId);
                }

                double directionEndTime = Math.Min(projectile.EndTime, time + 50);
                Vector2 direction = directionEndTime > time
                    ? projectile.PositionAt(directionEndTime, evaluateCamera(directionEndTime)) - position
                    : projectile.ControlEndPosition - projectile.StartPosition;
                double urgency = projectileUrgency(
                    position,
                    routePosition,
                    direction,
                    Math.Max(0.001, directionEndTime - time),
                    clearance);
                addPatternUrgency(patternUrgencies, projectile.PatternId, urgency);
                trajectoryDirectionMask |= trajectoryDirectionBin(direction);
            }

            for (int i = 0; i < activeBeams.Count; i++)
            {
                BeamThreat beam = activeBeams[i];
                double clearance = beamClearance(beam, time, cameraOffset, routePosition);

                if (time < beam.StartTime
                    || time > beam.EndTime
                    || !beamIntersectsArena(beam, time, cameraOffset, arena)
                    || clearance > reading_threat_radius)
                    continue;

                patterns.Add(beam.PatternId);
                double proximity = Math.Clamp(1 - clearance / reading_threat_radius, 0, 1);
                addPatternUrgency(patternUrgencies, beam.PatternId, clearance <= 0 ? 1 : 0.35 + proximity * 0.65);
                trajectoryDirectionMask |= trajectoryDirectionBin(beam.Direction);
            }
        }

        private static double projectileUrgency(
            Vector2 position,
            Vector2 routePosition,
            Vector2 displacement,
            double elapsedTime,
            double clearance)
        {
            if (clearance <= 0)
                return 1;

            Vector2 offset = position - routePosition;
            Vector2 velocity = displacement / (float)elapsedTime;
            double closingSpeed = offset.LengthSquared <= 0.001f
                ? velocity.Length
                : Math.Max(0, -Vector2.Dot(offset.Normalized(), velocity));
            double temporalUrgency = closingSpeed <= 0
                ? 0
                : Math.Clamp(1 - clearance / (closingSpeed * reading_urgency_horizon), 0, 1);
            double proximityUrgency = Math.Clamp(1 - clearance / reading_threat_radius, 0, 1) * 0.4;
            return Math.Max(temporalUrgency, proximityUrgency);
        }

        private static void addPatternUrgency(Dictionary<int, double> patternUrgencies, int patternId, double urgency)
        {
            if (!patternUrgencies.TryGetValue(patternId, out double existing) || urgency > existing)
                patternUrgencies[patternId] = urgency;
        }

        private bool projectileIntersectsArena(Vector2 position, float bulletSize, DodgeArenaState arena)
        {
            Vector2 centre = arena.Position + arena.Size / 2;
            Vector2 local = rotate(position - centre, -arena.Rotation);
            double outsideX = Math.Max(0, Math.Abs(local.X) - arena.Size.X / 2);
            double outsideY = Math.Max(0, Math.Abs(local.Y) - arena.Size.Y / 2);
            double collisionRadius = (bulletSize + playerSize) / 2;
            return outsideX * outsideX + outsideY * outsideY <= collisionRadius * collisionRadius;
        }

        private double projectileClearance(Vector2 position, float bulletSize, Vector2 routePosition)
        {
            Vector2 delta = position - routePosition;
            float collisionDistance = (bulletSize + playerSize) / 2;
            double outsideX = Math.Max(0, Math.Abs(delta.X) - collisionDistance);
            double outsideY = Math.Max(0, Math.Abs(delta.Y) - collisionDistance);
            return Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
        }

        private double beamClearance(BeamThreat beam, double time, Vector2 cameraOffset, Vector2 routePosition)
        {
            Vector2 local = beam.ToLocal(routePosition - beam.PositionAt(time, cameraOffset));
            double outsideX = Math.Max(0, Math.Abs(local.X) - (beam.Length + playerSize) / 2);
            double outsideY = Math.Max(0, Math.Abs(local.Y) - (beam.Width + playerSize) / 2);
            return Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
        }

        private bool beamIntersectsArena(BeamThreat beam, double time, Vector2 cameraOffset, DodgeArenaState arena)
        {
            Vector2 arenaX = rotate(Vector2.UnitX, arena.Rotation);
            Vector2 arenaY = rotate(Vector2.UnitY, arena.Rotation);
            Vector2 beamX = beam.Direction.LengthSquared > 0 ? beam.Direction.Normalized() : Vector2.UnitX;
            Vector2 beamY = beam.PerpendicularDirection.LengthSquared > 0 ? beam.PerpendicularDirection.Normalized() : Vector2.UnitY;
            Vector2 delta = beam.PositionAt(time, cameraOffset) - (arena.Position + arena.Size / 2);
            Vector2 arenaHalfSize = arena.Size / 2;
            Vector2 beamHalfSize = new Vector2(
                beam.Length / 2 + playerSize / 2,
                beam.Width / 2 + playerSize / 2);

            return rectanglesOverlapOnAxis(delta, arenaX, arenaX, arenaY, arenaHalfSize, beamX, beamY, beamHalfSize)
                   && rectanglesOverlapOnAxis(delta, arenaY, arenaX, arenaY, arenaHalfSize, beamX, beamY, beamHalfSize)
                   && rectanglesOverlapOnAxis(delta, beamX, arenaX, arenaY, arenaHalfSize, beamX, beamY, beamHalfSize)
                   && rectanglesOverlapOnAxis(delta, beamY, arenaX, arenaY, arenaHalfSize, beamX, beamY, beamHalfSize);
        }

        private static bool rectanglesOverlapOnAxis(
            Vector2 centreDelta,
            Vector2 axis,
            Vector2 firstX,
            Vector2 firstY,
            Vector2 firstHalfSize,
            Vector2 secondX,
            Vector2 secondY,
            Vector2 secondHalfSize)
        {
            double distance = Math.Abs(Vector2.Dot(centreDelta, axis));
            double firstRadius = Math.Abs(Vector2.Dot(firstX, axis)) * firstHalfSize.X
                                 + Math.Abs(Vector2.Dot(firstY, axis)) * firstHalfSize.Y;
            double secondRadius = Math.Abs(Vector2.Dot(secondX, axis)) * secondHalfSize.X
                                  + Math.Abs(Vector2.Dot(secondY, axis)) * secondHalfSize.Y;
            return distance <= firstRadius + secondRadius;
        }

        private static ushort actionDirectionMask(Vector2 direction)
        {
            if (direction.LengthSquared <= 1)
                return 1;

            double angle = Math.Atan2(direction.Y, direction.X);
            int bin = (int)Math.Floor((angle + Math.PI + Math.PI / 8) / (Math.PI / 4)) & 7;
            return (ushort)(1 << (bin + 1));
        }

        private static ushort trajectoryDirectionBin(Vector2 direction)
        {
            if (direction.LengthSquared <= 0.001f)
                return 0;

            double angle = Math.Atan2(direction.Y, direction.X);
            int bin = (int)Math.Floor((angle + Math.PI + Math.PI / 12) / (Math.PI / 6)) % 12;

            if (bin < 0)
                bin += 12;

            return (ushort)(1 << bin);
        }

        private static double maskDistance(ushort first, ushort second)
        {
            uint union = (uint)(first | second);

            if (union == 0)
                return 0;

            uint intersection = (uint)(first & second);
            return 1 - (double)System.Numerics.BitOperations.PopCount(intersection)
                       / System.Numerics.BitOperations.PopCount(union);
        }

        /// <summary>
        /// Aggregates section difficulty without allowing one isolated maximum
        /// to dominate a long map. The mean and broad percentiles describe
        /// sustained difficulty; the absolute peak can add at most 0.04.
        /// </summary>
        internal static double AggregateSectionDifficulty(IEnumerable<double> sectionDifficulties)
        {
            double[] sorted = sectionDifficulties.Where(double.IsFinite)
                                                 .Select(value => Math.Max(0, value))
                                                 .OrderBy(value => value)
                                                 .ToArray();

            if (sorted.Length == 0)
                return 0;

            double mean = sorted.Average();
            double percentile80 = percentile(sorted, 0.8);
            double percentile95 = percentile(sorted, 0.95);
            double peakBonus = Math.Min(0.04 * Math.Max(1, percentile95), Math.Max(0, sorted[^1] - percentile95) * 0.2);
            return Math.Max(0, mean * 0.3 + percentile80 * 0.45 + percentile95 * 0.25 + peakBonus);
        }

        internal static double CalculateDifficultSectionCount(IEnumerable<double> sectionDifficulties)
        {
            double[] values = sectionDifficulties.Where(double.IsFinite)
                                                 .Select(value => Math.Max(0, value))
                                                 .ToArray();

            if (values.Length == 0)
                return 0;

            double peak = values.Max();

            if (peak <= 0)
                return 0;

            // An effective count rather than a raw section count: sections far
            // below the map's peak contribute little, while repeated difficult
            // patterns add endurance without directly inflating star rating.
            return values.Sum(value => Math.Pow(value / peak, 3));
        }

        internal static double CalculateDirectionComplexity(double directionCoverage, double urgency)
            => Math.Sin(Math.PI * Math.Clamp(directionCoverage, 0, 1)) * Math.Clamp(urgency, 0, 1);

        private static double percentile(IReadOnlyList<double> sorted, double percentile)
        {
            if (sorted.Count == 1)
                return sorted[0];

            double index = Math.Clamp(percentile, 0, 1) * (sorted.Count - 1);
            int lower = (int)Math.Floor(index);
            int upper = Math.Min(sorted.Count - 1, lower + 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
        }

        private static double weightedPeakAverage(IEnumerable<double> values)
        {
            double weighted = 0;
            double weightSum = 0;
            double weight = 1;

            foreach (double value in values.OrderByDescending(value => value))
            {
                if (value <= 0.001)
                    continue;

                weighted += value * weight;
                weightSum += weight;
                weight *= 0.9;
            }

            return weightSum == 0 ? 0 : weighted / weightSum;
        }

        private double calculateClearanceAndActive(Vector2 position, double time, out int activeCount)
        {
            double minimum = 128;
            activeCount = 0;
            Vector2 cameraOffset = evaluateCamera(time);

            foreach (Projectile projectile in projectiles)
            {
                if (time < projectile.StartTime || time > projectile.EndTime)
                    continue;

                Vector2 delta = projectile.PositionAt(time, cameraOffset) - position;
                float collisionDistance = (projectile.BulletSize + playerSize) / 2;
                double outsideX = Math.Max(0, Math.Abs(delta.X) - collisionDistance);
                double outsideY = Math.Max(0, Math.Abs(delta.Y) - collisionDistance);
                double clearance = Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
                minimum = Math.Min(minimum, clearance);

                if (clearance <= local_threat_radius)
                    activeCount++;
            }

            foreach (BeamThreat beam in beams)
            {
                if (time < beam.StartTime || time > beam.EndTime)
                    continue;

                Vector2 local = beam.ToLocal(position - beam.PositionAt(time, cameraOffset));
                double outsideX = Math.Max(0, Math.Abs(local.X) - (beam.Length + playerSize) / 2);
                double outsideY = Math.Max(0, Math.Abs(local.Y) - (beam.Width + playerSize) / 2);
                double clearance = Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
                minimum = Math.Min(minimum, clearance);

                if (clearance <= local_threat_radius)
                    activeCount++;
            }

            return minimum;
        }

        private void createProjectiles()
        {
            if (hitObjects.Length == 0)
                return;

            int nextPatternId = 0;
            var standalonePatternIds = new Dictionary<long, int>();
            DodgeBullet? previousStandaloneBullet = null;
            int previousStandalonePatternId = -1;
            double continuedEndTime = DodgeGameplayTiming.GetContinuedProjectileEndTime(
                hitObjects,
                DodgePlayfield.CONTINUED_BULLET_GRACE_PERIOD);

            foreach (DodgeBullet bullet in hitObjects.OfType<DodgeBullet>().OrderBy(bullet => bullet.StartTime))
            {
                // Individually-authored walls commonly contain dozens of bullets
                // sharing one timestamp. Rapid copies of the same trajectory are
                // also one sustained pattern family rather than a fresh reading
                // decision every few milliseconds. Spatial pressure and route
                // restriction still see every physical projectile.
                long onset = (long)Math.Round(bullet.StartTime);

                if (!standalonePatternIds.TryGetValue(onset, out int patternId))
                {
                    patternId = previousStandaloneBullet != null
                                && belongsToSameStandalonePatternFamily(previousStandaloneBullet, bullet)
                        ? previousStandalonePatternId
                        : nextPatternId++;
                    standalonePatternIds[onset] = patternId;
                }

                previousStandaloneBullet = bullet;
                previousStandalonePatternId = patternId;

                double endTime = bullet.ContinueUntilExit
                    ? (hasCameraChanges
                        ? DodgeTrajectory.CalculateExitTimeWithCamera(
                            bullet.StartTime,
                            bullet.Duration,
                            bullet.Position,
                            bullet.EndPosition,
                            bullet.BulletSize,
                            bullet.MovementType,
                            bullet.WaveAmplitude,
                            bullet.WaveCycles,
                            bullet.WavePhase,
                            evaluateCamera,
                            continuedEndTime,
                            bullet.MovementEasing)
                        : DodgeTrajectory.CalculateExitTime(
                            bullet.StartTime,
                            bullet.Duration,
                            bullet.Position,
                            bullet.EndPosition,
                            bullet.BulletSize,
                            bullet.MovementType,
                            bullet.WaveAmplitude,
                            bullet.WaveCycles,
                            bullet.WavePhase,
                            bullet.MovementEasing))
                    : bullet.EndTime;
                projectiles.Add(Projectile.FromTrajectory(
                    patternId,
                    bullet.StartTime,
                    endTime,
                    bullet.Position,
                    bullet.EndPosition,
                    bullet.Duration,
                    bullet.BulletSize,
                    bullet.MovementType,
                    bullet.MovementEasing,
                    bullet.WaveAmplitude,
                    bullet.WaveCycles,
                    bullet.WavePhase,
                    evaluateCamera(bullet.StartTime)));
            }

            foreach (DodgeEmitter emitter in hitObjects.OfType<DodgeEmitter>())
            {
                for (int burst = 0; burst < emitter.EffectiveBurstCount; burst++)
                {
                    int patternId = nextPatternId++;

                    for (int i = 0; i < emitter.EffectiveBulletCount; i++)
                    {
                        double startTime = emitter.EmissionTimeAt(burst);
                        Vector2 sourcePosition = emitter.SourcePositionAt(burst);
                        Vector2 endPosition = emitter.EndPositionAt(burst, i);
                        double endTime = emitter.ContinueUntilExit
                            ? (hasCameraChanges
                                ? DodgeTrajectory.CalculateExitTimeWithCamera(
                                    startTime,
                                    emitter.Duration,
                                    sourcePosition,
                                    endPosition,
                                    emitter.BulletSize,
                                    emitter.MovementType,
                                    emitter.WaveAmplitude,
                                    emitter.WaveCycles,
                                    emitter.WavePhase,
                                    evaluateCamera,
                                    continuedEndTime,
                                    emitter.MovementEasing)
                                : DodgeTrajectory.CalculateExitTime(
                                    startTime,
                                    emitter.Duration,
                                    sourcePosition,
                                    endPosition,
                                    emitter.BulletSize,
                                    emitter.MovementType,
                                    emitter.WaveAmplitude,
                                    emitter.WaveCycles,
                                    emitter.WavePhase,
                                    emitter.MovementEasing))
                            : startTime + emitter.Duration;
                        projectiles.Add(Projectile.FromTrajectory(
                            patternId,
                            startTime,
                            endTime,
                            sourcePosition,
                            endPosition,
                            emitter.Duration,
                            emitter.BulletSize,
                            emitter.MovementType,
                            emitter.MovementEasing,
                            emitter.WaveAmplitude,
                            emitter.WaveCycles,
                            emitter.WavePhase,
                            evaluateCamera(startTime)));
                    }
                }
            }

            foreach (DodgeBeam beam in hitObjects.OfType<DodgeBeam>())
            {
                beams.Add(new BeamThreat(
                    nextPatternId++,
                    beam.StartTime,
                    beam.EndTime,
                    beam.BeamCenter,
                    beam.BeamDirection,
                    beam.PerpendicularDirection,
                    beam.BeamLength,
                    beam.BeamWidth,
                    evaluateCamera(beam.StartTime)));
            }
        }

        private static bool belongsToSameStandalonePatternFamily(DodgeBullet previous, DodgeBullet current)
        {
            double interval = current.StartTime - previous.StartTime;

            if (interval <= 0 || interval > standalone_pattern_family_interval
                || previous.MovementType != current.MovementType
                || previous.MovementEasing != current.MovementEasing
                || Math.Abs(previous.BulletSize - current.BulletSize) > Math.Max(2, previous.BulletSize * 0.25f)
                || (previous.Position - current.Position).Length > 64)
            {
                return false;
            }

            Vector2 previousDisplacement = previous.EndPosition - previous.Position;
            Vector2 currentDisplacement = current.EndPosition - current.Position;
            double previousSpeed = previousDisplacement.Length / Math.Max(0.001, previous.Duration);
            double currentSpeed = currentDisplacement.Length / Math.Max(0.001, current.Duration);

            if (previousSpeed <= 0.001 || currentSpeed <= 0.001)
                return previousSpeed <= 0.001 && currentSpeed <= 0.001;

            double speedRatio = currentSpeed / previousSpeed;
            return speedRatio is >= 0.8 and <= 1.25
                   && Vector2.Dot(previousDisplacement.Normalized(), currentDisplacement.Normalized()) >= 0.985f;
        }

        private List<Projectile>[] buildActiveProjectileLists(IReadOnlyList<double> times)
        {
            var result = Enumerable.Range(0, times.Count).Select(_ => new List<Projectile>()).ToArray();

            foreach (Projectile projectile in projectiles)
            {
                int first = Math.Max(1, lowerBound(times, projectile.StartTime));
                int last = Math.Min(times.Count - 1, upperBound(times, projectile.EndTime) + 1);

                for (int i = first; i <= last; i++)
                    result[i].Add(projectile);
            }

            return result;
        }

        private List<BeamThreat>[] buildActiveBeamLists(IReadOnlyList<double> times)
        {
            var result = Enumerable.Range(0, times.Count).Select(_ => new List<BeamThreat>()).ToArray();

            foreach (BeamThreat beam in beams)
            {
                int first = Math.Max(1, lowerBound(times, beam.StartTime));
                int last = Math.Min(times.Count - 1, upperBound(times, beam.EndTime) + 1);

                for (int i = first; i <= last; i++)
                    result[i].Add(beam);
            }

            return result;
        }

        /// <summary>
        /// Builds a coarse backward reachability field. Each cell stores how
        /// many future planner steps can still be survived (capped at a fixed
        /// horizon). The forward planner still performs exact swept collision
        /// checks; this field is deliberately cheaper and only prevents the beam
        /// from preferring a locally clear dead end over a viable corridor.
        /// </summary>
        private byte[][] buildViabilityMap(
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<BeamThreat>> activeBeams,
            double timeStep)
        {
            var result = new byte[times.Count][];
            int maximumSurvival = Math.Clamp((int)Math.Ceiling(viability_horizon / Math.Max(1, timeStep)), 1, byte.MaxValue);
            maximumViability = (byte)maximumSurvival;
            var sampledSteps = new List<int>();

            for (int step = 0; step < times.Count; step += viability_step_stride)
                sampledSteps.Add(step);

            if (sampledSteps[^1] != times.Count - 1)
                sampledSteps.Add(times.Count - 1);

            var sampledMaps = new byte[sampledSteps.Count][];

            for (int sample = sampledSteps.Count - 1; sample >= 0; sample--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int step = sampledSteps[sample];
                var current = new byte[viability_grid_columns * viability_grid_rows];
                sampledMaps[sample] = current;
                int nextStep = sample + 1 < sampledSteps.Count ? sampledSteps[sample + 1] : step;
                int survivalIncrement = nextStep - step;
                List<Vector2>? offsets = nextStep == step
                    ? null
                    : createMovementOffsets(times[nextStep] - times[step], viability_grid_size);
                List<Projectile> transitionProjectiles = nextStep == step
                    ? new List<Projectile>()
                    : projectiles.Where(projectile => projectile.EndTime >= times[step] && projectile.StartTime <= times[nextStep]).ToList();
                List<BeamThreat> transitionBeams = nextStep == step
                    ? new List<BeamThreat>()
                    : beams.Where(beam => beam.EndTime >= times[step] && beam.StartTime <= times[nextStep]).ToList();
                float maximumTransitionMovement = nextStep == step
                    ? 0
                    : movementSpeed * (float)(times[nextStep] - times[step])
                      + maximumArenaBoundaryMovement(arenas[step], arenas[nextStep])
                      + viability_grid_size;
                List<Projectile>?[] transitionThreats = buildTransitionThreatMap(
                    transitionProjectiles,
                    times[step],
                    times[nextStep],
                    maximumTransitionMovement,
                    transitionThreatBuffer);
                List<Projectile>?[] clearanceThreats = buildClearanceThreatMap(
                    activeProjectiles[step],
                    times[step],
                    currentClearanceThreatBuffer);

                for (int key = 0; key < current.Length; key++)
                {
                    if ((key & 255) == 0)
                        cancellationToken.ThrowIfCancellationRequested();

                    Vector2 position = viabilityGridPosition(key);

                    if (!isInsideArena(position, arenas[step]))
                        continue;

                    IReadOnlyList<Projectile> localThreats =
                        clearanceThreats[clearanceBinKey(position)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>();

                    if (calculateClearance(position, times[step], localThreats, activeBeams[step]) <= 0.001)
                        continue;

                    if (step == times.Count - 1)
                    {
                        current[key] = 1;
                        continue;
                    }

                    Vector2 carried = cameraCarriedPosition(position, times[step], times[nextStep], arenas[nextStep]);
                    float maximumMovement = movementSpeed * (float)(times[nextStep] - times[step]) + viability_grid_size;
                    int bestNextSurvival = 0;

                    foreach (Vector2 offset in offsets!)
                    {
                        Vector2 target = snapToViabilityGrid(carried + offset);

                        if ((target - carried).Length > maximumMovement + 0.01f || !isInsideArena(target, arenas[nextStep]))
                            continue;

                        IReadOnlyList<Projectile> relevantThreats =
                            transitionThreats[threatBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>();

                        // Viability must follow moving threats throughout the
                        // interval. Comparing only the two endpoint snapshots
                        // makes a fast bullet invisible to route selection and
                        // causes the bot to react after its escape corridor has
                        // already closed.
                        if (!isTransitionSafe(
                                position,
                                target,
                                times[step],
                                times[nextStep],
                                arenas[step],
                                arenas[nextStep],
                                relevantThreats,
                                transitionBeams))
                            continue;

                        bestNextSurvival = Math.Max(bestNextSurvival, sampledMaps[sample + 1][viabilityCellKey(target)]);

                        if (bestNextSurvival >= maximumSurvival)
                            break;
                    }

                    current[key] = (byte)Math.Min(maximumSurvival, bestNextSurvival + survivalIncrement);
                }
            }

            // Forward states between samples use the next sampled map. This is
            // intentionally conservative: it asks whether their current region
            // is still useful at the next coarse planning instant.
            int nextSample = 0;

            for (int step = 0; step < times.Count; step++)
            {
                while (sampledSteps[nextSample] < step)
                    nextSample++;

                result[step] = sampledMaps[nextSample];
            }

            return result;
        }

        private static int lowerBound(IReadOnlyList<double> values, double value)
        {
            int low = 0;
            int high = values.Count;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (values[middle] < value)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private static int upperBound(IReadOnlyList<double> values, double value)
        {
            int low = 0;
            int high = values.Count;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (values[middle] <= value)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low - 1;
        }

        private List<Vector2> createMovementOffsets(double timeStep, float spacing)
        {
            float maximumDistance = movementSpeed * (float)timeStep + 0.01f;
            int cellRadius = Math.Max(1, (int)Math.Ceiling(maximumDistance / spacing));
            var result = new List<Vector2>();

            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    var offset = new Vector2(x * spacing, y * spacing);

                    if (offset.Length <= maximumDistance)
                        result.Add(offset);
                }
            }

            if (result.Count == 0)
                result.Add(Vector2.Zero);

            return result;
        }

        private List<Projectile>?[] buildTransitionThreatMap(
            IReadOnlyList<Projectile> active,
            double startTime,
            double endTime,
            float maximumPlayerMovement,
            List<Projectile>?[] result)
        {
            clearThreatMap(result);

            for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
            {
                Projectile projectile = active[activeIndex];
                double overlapStart = Math.Max(startTime, projectile.StartTime);
                double overlapEnd = Math.Min(endTime, projectile.EndTime);

                if (overlapEnd < overlapStart)
                    continue;

                int pathSubdivisions = projectile.SuggestedSubdivisionCount(overlapStart, overlapEnd);
                Vector2 bulletStart = projectile.PositionAt(overlapStart, evaluateCamera(overlapStart));
                float expansion = (projectile.BulletSize + playerSize) / 2 + collisionSafetyMargin + maximumPlayerMovement;
                float minimumX = bulletStart.X - expansion;
                float maximumX = bulletStart.X + expansion;
                float minimumY = bulletStart.Y - expansion;
                float maximumY = bulletStart.Y + expansion;

                for (int subdivision = 1; subdivision <= pathSubdivisions; subdivision++)
                {
                    double sampleTime = overlapStart + (overlapEnd - overlapStart) * subdivision / pathSubdivisions;
                    Vector2 sample = projectile.PositionAt(sampleTime, evaluateCamera(sampleTime));
                    minimumX = Math.Min(minimumX, sample.X - expansion);
                    maximumX = Math.Max(maximumX, sample.X + expansion);
                    minimumY = Math.Min(minimumY, sample.Y - expansion);
                    maximumY = Math.Max(maximumY, sample.Y + expansion);
                }

                if (maximumX < 0 || minimumX > DodgePlayfield.WIDTH || maximumY < 0 || minimumY > DodgePlayfield.HEIGHT)
                    continue;

                int firstColumn = Math.Clamp((int)MathF.Floor(minimumX / threat_bin_size), 0, threat_bin_columns - 1);
                int lastColumn = Math.Clamp((int)MathF.Floor(maximumX / threat_bin_size), 0, threat_bin_columns - 1);
                int firstRow = Math.Clamp((int)MathF.Floor(minimumY / threat_bin_size), 0, threat_bin_rows - 1);
                int lastRow = Math.Clamp((int)MathF.Floor(maximumY / threat_bin_size), 0, threat_bin_rows - 1);

                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = firstColumn; column <= lastColumn; column++)
                        (result[row * threat_bin_columns + column] ??= new List<Projectile>()).Add(projectile);
                }
            }

            return result;
        }

        private List<Projectile>?[] buildClearanceThreatMap(IReadOnlyList<Projectile> active, double time, List<Projectile>?[] result)
        {
            clearThreatMap(result);

            for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
            {
                Projectile projectile = active[activeIndex];
                if (time < projectile.StartTime || time > projectile.EndTime)
                    continue;

                Vector2 cameraOffset = evaluateCamera(time);
                Vector2 position = projectile.PositionAt(time, cameraOffset);
                float expansion = (projectile.BulletSize + playerSize) / 2 + scored_clearance;
                float minimumX = position.X - expansion;
                float maximumX = position.X + expansion;
                float minimumY = position.Y - expansion;
                float maximumY = position.Y + expansion;

                if (maximumX < 0 || minimumX > DodgePlayfield.WIDTH || maximumY < 0 || minimumY > DodgePlayfield.HEIGHT)
                    continue;

                int firstColumn = Math.Clamp((int)MathF.Floor(minimumX / clearance_bin_size), 0, clearance_bin_columns - 1);
                int lastColumn = Math.Clamp((int)MathF.Floor(maximumX / clearance_bin_size), 0, clearance_bin_columns - 1);
                int firstRow = Math.Clamp((int)MathF.Floor(minimumY / clearance_bin_size), 0, clearance_bin_rows - 1);
                int lastRow = Math.Clamp((int)MathF.Floor(maximumY / clearance_bin_size), 0, clearance_bin_rows - 1);

                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = firstColumn; column <= lastColumn; column++)
                        (result[row * clearance_bin_columns + column] ??= new List<Projectile>()).Add(projectile);
                }
            }

            return result;
        }

        private static void clearThreatMap(List<Projectile>?[] bins)
        {
            for (int i = 0; i < bins.Length; i++)
                bins[i]?.Clear();
        }

        private bool isTransitionSafe(
            Vector2 from,
            Vector2 to,
            double startTime,
            double endTime,
            DodgeArenaState startArena,
            DodgeArenaState endArena,
            IReadOnlyList<Projectile> active,
            IReadOnlyList<BeamThreat> activeBeams)
        {
            bool staticArena = arenaIsStatic(startArena, endArena);
            // Camera easing can curve an otherwise linear projectile between two
            // planner frames. Keep transition samples bounded even in a static
            // arena so the planner checks the same swept path gameplay renders.
            int subdivisions = Math.Max(1, (int)Math.Ceiling((endTime - startTime) / transition_collision_step));

            for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
                subdivisions = Math.Max(subdivisions, active[activeIndex].SuggestedSubdivisionCount(startTime, endTime));

            for (int subdivision = 0; subdivision < subdivisions; subdivision++)
            {
                double segmentStart = startTime + (endTime - startTime) * subdivision / subdivisions;
                double segmentEnd = startTime + (endTime - startTime) * (subdivision + 1) / subdivisions;
                float startProgress = (float)((segmentStart - startTime) / Math.Max(0.001, endTime - startTime));
                float endProgress = (float)((segmentEnd - startTime) / Math.Max(0.001, endTime - startTime));
                DodgeArenaState segmentStartArena = staticArena ? startArena : arenaEvaluator.Evaluate(segmentStart);
                DodgeArenaState segmentEndArena = staticArena ? endArena : arenaEvaluator.Evaluate(segmentEnd);
                Vector2 playerStart = DodgePlayer.ClampToArena(
                    Vector2.Lerp(from, to, startProgress),
                    segmentStartArena.Position,
                    segmentStartArena.Size,
                    playerSize,
                    segmentStartArena.Rotation);
                Vector2 playerEnd = DodgePlayer.ClampToArena(
                    Vector2.Lerp(from, to, endProgress),
                    segmentEndArena.Position,
                    segmentEndArena.Size,
                    playerSize,
                    segmentEndArena.Rotation);
                Vector2 segmentStartCamera = evaluateCamera(segmentStart);
                Vector2 segmentEndCamera = evaluateCamera(segmentEnd);

                for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
                {
                    Projectile projectile = active[activeIndex];
                    double overlapStart = Math.Max(segmentStart, projectile.StartTime);
                    double overlapEnd = Math.Min(segmentEnd, projectile.EndTime);

                    if (overlapEnd < overlapStart)
                        continue;

                    Vector2 startCamera = overlapStart == segmentStart ? segmentStartCamera : evaluateCamera(overlapStart);
                    Vector2 endCamera = overlapEnd == segmentEnd ? segmentEndCamera : evaluateCamera(overlapEnd);
                    Vector2 overlapPlayerStart = overlapStart == segmentStart
                        ? playerStart
                        : playerPositionAtTransition(from, to, startTime, endTime, overlapStart, staticArena ? startArena : arenaEvaluator.Evaluate(overlapStart));
                    Vector2 overlapPlayerEnd = overlapEnd == segmentEnd
                        ? playerEnd
                        : playerPositionAtTransition(from, to, startTime, endTime, overlapEnd, staticArena ? endArena : arenaEvaluator.Evaluate(overlapEnd));
                    Vector2 relativeStart = projectile.PositionAt(overlapStart, startCamera) - overlapPlayerStart;
                    Vector2 relativeEnd = projectile.PositionAt(overlapEnd, endCamera) - overlapPlayerEnd;
                    float collisionDistance = (projectile.BulletSize + playerSize) / 2 + collisionSafetyMargin;

                    if (segmentIntersectsBox(relativeStart, relativeEnd, new Vector2(-collisionDistance), new Vector2(collisionDistance)))
                        return false;
                }

                for (int activeIndex = 0; activeIndex < activeBeams.Count; activeIndex++)
                {
                    BeamThreat beam = activeBeams[activeIndex];
                    double overlapStart = Math.Max(segmentStart, beam.StartTime);
                    double overlapEnd = Math.Min(segmentEnd, beam.EndTime);

                    if (overlapEnd < overlapStart)
                        continue;

                    Vector2 overlapPlayerStart = overlapStart == segmentStart
                        ? playerStart
                        : playerPositionAtTransition(from, to, startTime, endTime, overlapStart, staticArena ? startArena : arenaEvaluator.Evaluate(overlapStart));
                    Vector2 overlapPlayerEnd = overlapEnd == segmentEnd
                        ? playerEnd
                        : playerPositionAtTransition(from, to, startTime, endTime, overlapEnd, staticArena ? endArena : arenaEvaluator.Evaluate(overlapEnd));
                    Vector2 relativeStart = overlapPlayerStart
                                            - beam.PositionAt(overlapStart, overlapStart == segmentStart ? segmentStartCamera : evaluateCamera(overlapStart));
                    Vector2 relativeEnd = overlapPlayerEnd
                                          - beam.PositionAt(overlapEnd, overlapEnd == segmentEnd ? segmentEndCamera : evaluateCamera(overlapEnd));
                    Vector2 localStart = beam.ToLocal(relativeStart);
                    Vector2 localEnd = beam.ToLocal(relativeEnd);
                    Vector2 halfExtent = new Vector2(
                        beam.Length / 2 + playerSize / 2 + collisionSafetyMargin,
                        beam.Width / 2 + playerSize / 2 + collisionSafetyMargin);

                    if (segmentIntersectsBox(localStart, localEnd, -halfExtent, halfExtent))
                        return false;
                }
            }

            return true;
        }

        private Vector2 playerPositionAtTransition(
            Vector2 from,
            Vector2 to,
            double startTime,
            double endTime,
            double time,
            DodgeArenaState arena)
        {
            float progress = (float)((time - startTime) / Math.Max(0.001, endTime - startTime));
            return DodgePlayer.ClampToArena(
                Vector2.Lerp(from, to, progress),
                arena.Position,
                arena.Size,
                playerSize,
                arena.Rotation);
        }

        private double scorePosition(
            Vector2 position,
            double time,
            DodgeArenaState arena,
            IReadOnlyList<Projectile> currentThreats,
            IReadOnlyList<BeamThreat> currentBeams,
            double futureTime,
            IReadOnlyList<Projectile> futureThreats,
            IReadOnlyList<BeamThreat> futureBeams)
        {
            double clearance = calculateClearance(position, time, currentThreats ?? Array.Empty<Projectile>(), currentBeams);
            double futureClearance = calculateClearance(position, futureTime, futureThreats ?? Array.Empty<Projectile>(), futureBeams);
            Vector2 arenaCentre = arena.Position + arena.Size / 2;

            return Math.Min(clearance, scored_clearance) * 0.0125
                   + Math.Min(futureClearance, scored_clearance) * 0.04
                   - (position - arenaCentre).Length * 0.001;
        }

        private double calculateClearance(
            Vector2 position,
            double time,
            IReadOnlyList<Projectile> active,
            IReadOnlyList<BeamThreat>? activeBeams = null)
        {
            double minimum = 128;
            Vector2 cameraOffset = evaluateCamera(time);

            for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
            {
                Projectile projectile = active[activeIndex];
                if (time < projectile.StartTime || time > projectile.EndTime)
                    continue;

                Vector2 delta = projectile.PositionAt(time, cameraOffset) - position;
                float collisionDistance = (projectile.BulletSize + playerSize) / 2;
                double outsideX = Math.Max(0, Math.Abs(delta.X) - collisionDistance);
                double outsideY = Math.Max(0, Math.Abs(delta.Y) - collisionDistance);
                minimum = Math.Min(minimum, Math.Sqrt(outsideX * outsideX + outsideY * outsideY));
            }

            if (activeBeams != null)
            {
                for (int activeIndex = 0; activeIndex < activeBeams.Count; activeIndex++)
                {
                    BeamThreat beam = activeBeams[activeIndex];

                    if (time < beam.StartTime || time > beam.EndTime)
                        continue;

                    Vector2 local = beam.ToLocal(position - beam.PositionAt(time, cameraOffset));
                    double outsideX = Math.Max(0, Math.Abs(local.X) - (beam.Length + playerSize) / 2);
                    double outsideY = Math.Max(0, Math.Abs(local.Y) - (beam.Width + playerSize) / 2);
                    minimum = Math.Min(minimum, Math.Sqrt(outsideX * outsideX + outsideY * outsideY));
                }
            }

            return minimum;
        }

        private List<PlannerState> createReachableFallback(
            IReadOnlyList<PlannerState> previousStates,
            DodgeArenaState previousArena,
            DodgeArenaState currentArena,
            double previousTime,
            double time,
            IReadOnlyList<Projectile> active,
            IReadOnlyList<BeamThreat> activeBeams,
            IReadOnlyList<Vector2> movementOffsets,
            int step)
        {
            int candidateVersion = nextCandidateIndexVersion();
            int candidateCount = 0;
            float maximumTransitionMovement = movementOffsets.Max(offset => offset.Length)
                                              + maximumArenaBoundaryMovement(previousArena, currentArena);

            for (int parentIndex = 0; parentIndex < previousStates.Count; parentIndex++)
            {
                PlannerState previous = previousStates[parentIndex];
                Vector2 from = DodgePlayer.ClampToArena(
                    previous.Position,
                    previousArena.Position,
                    previousArena.Size,
                    playerSize,
                    previousArena.Rotation);
                Vector2 carriedFrom = cameraCarriedPosition(from, previousTime, time, currentArena);

                for (int offsetIndex = 0; offsetIndex < movementOffsets.Count; offsetIndex++)
                {
                    Vector2 offset = movementOffsets[offsetIndex];
                    Vector2 position = snapToGridWithinArena(carriedFrom + offset, currentArena);

                    if ((position - carriedFrom).Length > maximumTransitionMovement + 0.01f)
                        continue;

                    double clearance = calculateClearance(position, time, active, activeBeams);
                    PlannerState state = createPlannerState(
                        cellKey(position),
                        position,
                        previous.Score + clearance - offset.Length * 0.08,
                        parentIndex,
                        true,
                        viabilityAt(step, position));

                    addCandidate(state, candidateVersion, ref candidateCount);
                }
            }

            if (candidateCount > 0)
                return takeBestCandidates(candidateCount, beam_width);

            int bestParent = previousStates.Select((state, index) => (state, index))
                                           .OrderByDescending(item => item.state.Score)
                                           .First().index;
            Vector2 centre = snapToGridWithinArena(
                currentArena.Position + currentArena.Size / 2,
                currentArena);
            return new List<PlannerState>
            {
                createPlannerState(
                    cellKey(centre),
                    centre,
                    previousStates[bestParent].Score,
                    bestParent,
                    true,
                    viabilityAt(step, centre)),
            };
        }

        private static List<DodgeAutoplayRoutePoint> backtrackRoute(IReadOnlyList<List<PlannerState>> history, IReadOnlyList<double> times)
        {
            int index = history[^1].Select((state, stateIndex) => (state, stateIndex))
                                   .OrderByDescending(item => item.state.Score)
                                   .First().stateIndex;
            var route = new DodgeAutoplayRoutePoint[history.Count];

            for (int step = history.Count - 1; step >= 0; step--)
            {
                PlannerState state = history[step][index];
                route[step] = new DodgeAutoplayRoutePoint(times[step], state.Position, state.UsedFallback);
                index = state.ParentIndex;
            }

            return route.ToList();
        }

        private void addReplayFrames(IReadOnlyList<DodgeAutoplayRoutePoint> route)
        {
            for (int i = 0; i < route.Count; i++)
            {
                DodgeAutoplayRoutePoint point = route[i];
                DodgeAction[] actions = Array.Empty<DodgeAction>();

                if (i + 1 < route.Count)
                {
                    DodgeAutoplayRoutePoint next = route[i + 1];
                    DodgeArenaState nextArena = arenaEvaluator.Evaluate(next.Time);
                    Vector2 carriedPosition = cameraCarriedPosition(point.Position, point.Time, next.Time, nextArena);
                    Vector2 inputMovement = next.Position - carriedPosition;
                    double duration = next.Time - point.Time;
                    actions = actionsForMovement(inputMovement, duration);

                }

                Frames.Add(new DodgeReplayFrame(point.Time, point.Position, actions));
            }

            Frames.Add(new DodgeReplayFrame(route[^1].Time + 100, route[^1].Position));
        }

        private DodgeAction[] actionsForMovement(Vector2 movement, double duration)
        {
            float slowMovementLimit = movementSpeed * (float)Math.Max(0, duration) * DodgePlayer.SLOW_MULTIPLIER;
            return actionsForDirection(movement, movement.Length > 0.01f && movement.Length <= slowMovementLimit + 0.25f);
        }

        private static DodgeAction[] actionsForDirection(Vector2 movement, bool slow)
        {
            var actions = new List<DodgeAction>(3);

            if (movement.X < -0.01f) actions.Add(DodgeAction.MoveLeft);
            if (movement.X > 0.01f) actions.Add(DodgeAction.MoveRight);
            if (movement.Y < -0.01f) actions.Add(DodgeAction.MoveUp);
            if (movement.Y > 0.01f) actions.Add(DodgeAction.MoveDown);

            if (slow && actions.Count > 0)
                actions.Add(DodgeAction.Slow);

            return actions.ToArray();
        }

        private Vector2 cameraCarriedPosition(Vector2 position, double startTime, double endTime, DodgeArenaState arena)
        {
            Vector2 cameraDelta = evaluateCamera(endTime) - evaluateCamera(startTime);
            return DodgePlayer.ClampToArena(position + cameraDelta, arena.Position, arena.Size, playerSize, arena.Rotation);
        }

        private Vector2 evaluateCamera(double time)
        {
            if (!hasCameraChanges)
                return Vector2.Zero;

            if (!cameraStateCache.TryGetValue(time, out Vector2 result))
                cameraStateCache[time] = result = cameraEvaluator.Evaluate(time);

            return result;
        }

        private bool isInsideArena(Vector2 position, DodgeArenaState arena)
        {
            Vector2 clamped = DodgePlayer.ClampToArena(position, arena.Position, arena.Size, playerSize, arena.Rotation);
            return (clamped - position).LengthSquared <= 0.01f;
        }

        private static bool arenaIsStatic(DodgeArenaState first, DodgeArenaState second)
            => first.Position == second.Position
               && first.Size == second.Size
               && Math.Abs(first.Rotation - second.Rotation) < 0.001f;

        private static float maximumArenaBoundaryMovement(DodgeArenaState first, DodgeArenaState second)
        {
            Vector2 firstCentre = first.Position + first.Size / 2;
            Vector2 secondCentre = second.Position + second.Size / 2;
            float maximum = 0;

            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 firstLocal = new Vector2(
                    (corner & 1) == 0 ? -first.Size.X / 2 : first.Size.X / 2,
                    (corner & 2) == 0 ? -first.Size.Y / 2 : first.Size.Y / 2);
                Vector2 secondLocal = new Vector2(
                    (corner & 1) == 0 ? -second.Size.X / 2 : second.Size.X / 2,
                    (corner & 2) == 0 ? -second.Size.Y / 2 : second.Size.Y / 2);
                Vector2 firstCorner = firstCentre + rotate(firstLocal, first.Rotation);
                Vector2 secondCorner = secondCentre + rotate(secondLocal, second.Rotation);
                maximum = Math.Max(maximum, (secondCorner - firstCorner).Length);
            }

            return maximum;
        }

        private static Vector2 rotate(Vector2 vector, float degrees)
        {
            float radians = MathHelper.DegreesToRadians(degrees);
            float cosine = MathF.Cos(radians);
            float sine = MathF.Sin(radians);
            return new Vector2(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
        }

        private static int threatBinKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Floor(position.X / threat_bin_size), 0, threat_bin_columns - 1);
            int row = Math.Clamp((int)MathF.Floor(position.Y / threat_bin_size), 0, threat_bin_rows - 1);
            return row * threat_bin_columns + column;
        }

        private Vector2 threatBinCentre(int key, DodgeArenaState arena)
        {
            int column = key % threat_bin_columns;
            int row = key / threat_bin_columns;
            var centre = new Vector2(
                Math.Min(DodgePlayfield.WIDTH, (column + 0.5f) * threat_bin_size),
                Math.Min(DodgePlayfield.HEIGHT, (row + 0.5f) * threat_bin_size));
            return DodgePlayer.ClampToArena(centre, arena.Position, arena.Size, playerSize, arena.Rotation);
        }

        private static int clearanceBinKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Floor(position.X / clearance_bin_size), 0, clearance_bin_columns - 1);
            int row = Math.Clamp((int)MathF.Floor(position.Y / clearance_bin_size), 0, clearance_bin_rows - 1);
            return row * clearance_bin_columns + column;
        }

        private static int diversityBinKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Floor(position.X / diversity_bin_size), 0, diversity_bin_columns - 1);
            int row = Math.Clamp((int)MathF.Floor(position.Y / diversity_bin_size), 0, diversity_bin_rows - 1);
            return row * diversity_bin_columns + column;
        }

        private static Vector2 snapToGrid(Vector2 position) => new Vector2(
            Math.Clamp(MathF.Round(position.X / position_grid_size) * position_grid_size, 0, DodgePlayfield.WIDTH),
            Math.Clamp(MathF.Round(position.Y / position_grid_size) * position_grid_size, 0, DodgePlayfield.HEIGHT));

        private Vector2 snapToGridWithinArena(Vector2 position, DodgeArenaState arena)
            => DodgePlayer.ClampToArena(
                snapToGrid(position),
                arena.Position,
                arena.Size,
                playerSize,
                arena.Rotation);

        private static Vector2 snapToStateGrid(Vector2 position) => new Vector2(
            Math.Clamp(MathF.Round(position.X / state_bin_size) * state_bin_size, 0, DodgePlayfield.WIDTH),
            Math.Clamp(MathF.Round(position.Y / state_bin_size) * state_bin_size, 0, DodgePlayfield.HEIGHT));

        private static Vector2 snapToViabilityGrid(Vector2 position) => new Vector2(
            Math.Clamp(MathF.Round(position.X / viability_grid_size) * viability_grid_size, 0, DodgePlayfield.WIDTH),
            Math.Clamp(MathF.Round(position.Y / viability_grid_size) * viability_grid_size, 0, DodgePlayfield.HEIGHT));

        private static Vector2 viabilityGridPosition(int key)
            => new Vector2((key % viability_grid_columns) * viability_grid_size, (key / viability_grid_columns) * viability_grid_size);

        private static int viabilityCellKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Round(position.X / viability_grid_size), 0, viability_grid_columns - 1);
            int row = Math.Clamp((int)MathF.Round(position.Y / viability_grid_size), 0, viability_grid_rows - 1);
            return row * viability_grid_columns + column;
        }

        private static int cellKey(Vector2 position, bool precise = true)
        {
            float spacing = precise ? state_cell_size : state_bin_size;
            int columns = precise ? grid_columns : coarse_grid_columns;
            int rows = precise ? grid_rows : coarse_grid_rows;
            int column = Math.Clamp((int)MathF.Round(position.X / spacing), 0, columns - 1);
            int row = Math.Clamp((int)MathF.Round(position.Y / spacing), 0, rows - 1);
            return row * columns + column;
        }

        private static bool segmentIntersectsBox(Vector2 start, Vector2 end, Vector2 minimum, Vector2 maximum)
        {
            Vector2 direction = end - start;
            float entry = 0;
            float exit = 1;

            return intersectAxis(start.X, direction.X, minimum.X, maximum.X, ref entry, ref exit)
                   && intersectAxis(start.Y, direction.Y, minimum.Y, maximum.Y, ref entry, ref exit)
                   && exit >= entry;
        }

        private static bool intersectAxis(float origin, float direction, float minimum, float maximum, ref float entry, ref float exit)
        {
            if (direction == 0)
                return origin >= minimum && origin <= maximum;

            float first = (minimum - origin) / direction;
            float second = (maximum - origin) / direction;

            if (first > second)
                (first, second) = (second, first);

            entry = Math.Max(entry, first);
            exit = Math.Min(exit, second);
            return exit >= entry;
        }

        private byte viabilityAt(int step, Vector2 position) => viabilityMap[step][viabilityCellKey(position)];

        private PlannerState createPlannerState(
            int cell,
            Vector2 position,
            double score,
            int parentIndex,
            bool usedFallback,
            byte viability,
            bool projectedSafe = true)
            // Once a state can survive most of the lookahead horizon, exact
            // clearance/movement score is a better discriminator than a few
            // coarse viability samples. This avoids steering the whole beam into
            // expensive dense regions merely to gain one distant sample.
            => new PlannerState(
                cell,
                position,
                score,
                parentIndex,
                usedFallback,
                viability,
                viability >= Math.Max(1, maximumViability * 3 / 4),
                projectedSafe);

        private readonly record struct PlannerState(
            int CellKey,
            Vector2 Position,
            double Score,
            int ParentIndex,
            bool UsedFallback,
            byte Viability,
            bool FullyViable,
            bool ProjectedSafe);

        private sealed class SectionDifficultyAccumulator
        {
            private double movementSum;
            private double pathRestrictionSum;
            private double branchRestrictionSum;
            private double actionRestrictionSum;
            private double readingContextSum;
            private double peakMovement;
            private double peakPathRestriction;
            private double peakReadingContext;
            private double newPatternLoad;
            private int sampleCount;
            private bool hasNearbyThreat;

            public bool IsRelevant => hasNearbyThreat || peakMovement > 0.02;

            public double MovementDifficulty => sampleCount == 0
                ? 0
                : Math.Max(0, movementSum / sampleCount * 0.75 + peakMovement * 0.25);

            public double PathDifficulty => sampleCount == 0
                ? 0
                : Math.Max(0, pathRestrictionSum / sampleCount * 0.85 + peakPathRestriction * 0.15);

            public double ReadingDifficulty
            {
                get
                {
                    if (sampleCount == 0)
                        return 0;

                    double onsetLoad = Math.Log2(1 + newPatternLoad);
                    double sustainedContext = readingContextSum / sampleCount * 0.7 + peakReadingContext * 0.3;
                    return onsetLoad * 0.55 + sustainedContext * 0.45;
                }
            }

            public double BranchDifficulty => sampleCount == 0 ? 0 : Math.Clamp(branchRestrictionSum / sampleCount, 0, 1);

            public double ActionDifficulty => sampleCount == 0 ? 0 : Math.Clamp(actionRestrictionSum / sampleCount, 0, 1);

            public void Add(
                double movement,
                double pathRestriction,
                double branchRestriction,
                double actionRestriction,
                double newPatterns,
                double activePatterns,
                double directionComplexity,
                double complexPatternRatio,
                double actionChurn,
                bool nearbyThreat)
            {
                movementSum += movement;
                pathRestrictionSum += pathRestriction;
                branchRestrictionSum += branchRestriction;
                actionRestrictionSum += actionRestriction;
                double activePatternLoad = Math.Log2(1 + activePatterns);
                double readingContext = activePatternLoad
                                        * (0.22 + directionComplexity * 0.22 + complexPatternRatio * 0.18)
                                        + actionChurn * 1.35;
                readingContextSum += readingContext;
                newPatternLoad += newPatterns;
                peakMovement = Math.Max(peakMovement, movement);
                peakPathRestriction = Math.Max(peakPathRestriction, pathRestriction);
                peakReadingContext = Math.Max(peakReadingContext, readingContext);
                sampleCount++;
                hasNearbyThreat |= nearbyThreat;
            }
        }

        private sealed class PlannerStateScoreComparer : IComparer<PlannerState>
        {
            public static readonly PlannerStateScoreComparer Instance = new PlannerStateScoreComparer();

            public int Compare(PlannerState first, PlannerState second)
            {
                if (first.FullyViable != second.FullyViable)
                    return second.FullyViable.CompareTo(first.FullyViable);

                if (first.ProjectedSafe != second.ProjectedSafe)
                    return second.ProjectedSafe.CompareTo(first.ProjectedSafe);

                int viabilityComparison = first.FullyViable
                    ? 0
                    : second.Viability.CompareTo(first.Viability);

                if (viabilityComparison != 0)
                    return viabilityComparison;

                int scoreComparison = second.Score.CompareTo(first.Score);
                return scoreComparison != 0 ? scoreComparison : first.CellKey.CompareTo(second.CellKey);
            }
        }

        private readonly record struct BeamThreat(
            int PatternId,
            double StartTime,
            double EndTime,
            Vector2 Centre,
            Vector2 Direction,
            Vector2 PerpendicularDirection,
            float Length,
            float Width,
            Vector2 CameraAnchor)
        {
            public Vector2 PositionAt(double time, Vector2 cameraOffset)
                => Centre + cameraOffset - CameraAnchor;

            public Vector2 ToLocal(Vector2 relativePosition)
                => new Vector2(
                    Vector2.Dot(relativePosition, Direction),
                    Vector2.Dot(relativePosition, PerpendicularDirection));
        }

        private readonly record struct Projectile(
            int PatternId,
            double StartTime,
            double EndTime,
            Vector2 StartPosition,
            Vector2 ControlEndPosition,
            double Duration,
            float BulletSize,
            DodgeMovementType MovementType,
            DodgeMovementEasing MovementEasing,
            float WaveAmplitude,
            int WaveCycles,
            float WavePhase,
            Vector2 CameraAnchor)
        {
            public static Projectile FromTrajectory(
                int patternId,
                double startTime,
                double endTime,
                Vector2 startPosition,
                Vector2 controlEnd,
                double duration,
                float bulletSize,
                DodgeMovementType movementType,
                DodgeMovementEasing movementEasing,
                float waveAmplitude,
                int waveCycles,
                float wavePhase,
                Vector2 cameraAnchor)
            {
                Vector2 effectiveStart = duration <= 0 ? controlEnd : startPosition;
                return new Projectile(
                    patternId,
                    startTime,
                    Math.Max(startTime, endTime),
                    effectiveStart,
                    controlEnd,
                    duration,
                    bulletSize,
                    movementType,
                    movementEasing,
                    waveAmplitude,
                    Math.Max(1, waveCycles),
                    wavePhase,
                    cameraAnchor);
            }

            public Vector2 PositionAt(double time, Vector2 cameraOffset)
            {
                Vector2 trajectoryPosition;

                if (Duration <= 0)
                {
                    trajectoryPosition = ControlEndPosition;
                }
                else
                {
                    float progress = (float)Math.Max(0, (Math.Min(time, EndTime) - StartTime) / Duration);
                    trajectoryPosition = DodgeTrajectory.PositionAtProgress(
                        StartPosition,
                        ControlEndPosition,
                        progress,
                        MovementType,
                        WaveAmplitude,
                        WaveCycles,
                        WavePhase,
                        MovementEasing);
                }

                // Scroll drift: ride the field scroll accumulated since this
                // projectile's own spawn, mirroring the gameplay drawables.
                return trajectoryPosition + cameraOffset - CameraAnchor;
            }

            public int SuggestedSubdivisionCount(double startTime, double endTime)
            {
                if (MovementType != DodgeMovementType.Sine || Duration <= 0)
                    return 1;

                double overlapStart = Math.Max(StartTime, startTime);
                double overlapEnd = Math.Min(EndTime, endTime);
                int easingMultiplier = MovementEasing == DodgeMovementEasing.Linear ? 1 : 2;
                return Math.Max(1, (int)Math.Ceiling(Math.Max(0, overlapEnd - overlapStart) / Duration * WaveCycles * 24 * easingMultiplier));
            }
        }
    }

}
