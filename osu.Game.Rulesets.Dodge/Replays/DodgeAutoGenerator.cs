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
        private const float grid_size = 8;
        private const int beam_width = 256;
        private const double minimum_step_duration = 25;
        private const double maximum_step_duration = 100;
        private const double transition_collision_step = 25;
        private const float threat_bin_size = 32;
        private const float clearance_bin_size = 64;
        private const float scored_clearance = 96;
        private const double recovery_lookback = 1500;

        private const int grid_columns = (int)(DodgePlayfield.WIDTH / grid_size) + 1;
        private const int grid_rows = (int)(DodgePlayfield.HEIGHT / grid_size) + 1;
        private const int threat_bin_columns = (int)(DodgePlayfield.WIDTH / threat_bin_size) + 1;
        private const int threat_bin_rows = (int)(DodgePlayfield.HEIGHT / threat_bin_size) + 1;
        private const int clearance_bin_columns = (int)(DodgePlayfield.WIDTH / clearance_bin_size) + 1;
        private const int clearance_bin_rows = (int)(DodgePlayfield.HEIGHT / clearance_bin_size) + 1;

        private readonly DodgeHitObject[] hitObjects;
        private readonly DodgeArenaStateEvaluator arenaEvaluator;
        private readonly DodgeCameraStateEvaluator cameraEvaluator;
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly float movementSpeed;
        private readonly float playerSize;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<int, PlannerState> candidateBuffer = new Dictionary<int, PlannerState>(grid_columns * grid_rows);
        // Candidate expansion can fill almost the entire playfield grid, while a
        // normal beam only retains 256 states. Building a List directly from the
        // dictionary used to leave every history entry with the much larger
        // backing array even after RemoveRange(), retaining hundreds of megabytes
        // on long maps and eventually forcing a Gen2 collection during gameplay.
        // Keep one full-size sorting buffer and copy only the retained beam into
        // each history entry.
        private readonly PlannerState[] candidateSortBuffer = new PlannerState[grid_columns * grid_rows];
        private readonly double[] positionScoreBuffer = new double[grid_columns * grid_rows];
        private readonly int[] positionScoreVersions = new int[grid_columns * grid_rows];
        private readonly List<Projectile>?[] transitionThreatBuffer = new List<Projectile>?[threat_bin_columns * threat_bin_rows];
        private readonly List<Projectile>?[] currentClearanceThreatBuffer = new List<Projectile>?[clearance_bin_columns * clearance_bin_rows];
        private readonly List<Projectile>?[] futureClearanceThreatBuffer = new List<Projectile>?[clearance_bin_columns * clearance_bin_rows];
        private int positionScoreVersion;

        public DodgeAutoplayAnalysis Analysis { get; private set; }

        public IReadOnlyList<DodgeAutoplayRoutePoint> Route { get; private set; } = Array.Empty<DodgeAutoplayRoutePoint>();

        public DodgeAutoGenerator(IBeatmap beatmap, CancellationToken cancellationToken = default)
            : base(beatmap)
        {
            hitObjects = beatmap.HitObjects.OfType<DodgeHitObject>().ToArray();
            arenaEvaluator = new DodgeArenaStateEvaluator(hitObjects.OfType<DodgeArenaChange>());
            cameraEvaluator = new DodgeCameraStateEvaluator(hitObjects.OfType<DodgeCameraChange>());
            movementSpeed = (float)(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty) / 1000);
            playerSize = DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty);
            this.cancellationToken = cancellationToken;
            createProjectiles();
        }

        protected override void GenerateFrames()
        {
            Analysis = default;
            Route = Array.Empty<DodgeAutoplayRoutePoint>();
            cancellationToken.ThrowIfCancellationRequested();

            double firstProjectileTime = projectiles.Count == 0 ? 0 : projectiles.Min(projectile => projectile.StartTime);
            double lastProjectileTime = projectiles.Count == 0 ? Math.Max(1000, hitObjects.Select(h => h.GetEndTime()).DefaultIfEmpty(1000).Max()) : projectiles.Max(projectile => projectile.EndTime);
            double positioningLeadTime = DodgePlayfield.BASE_SIZE.Length / Math.Max(0.001f, movementSpeed) + 250;
            // There is no useful route to search while the playfield is empty.
            // Start only early enough for the player to reach any point before the
            // first threat; this keeps editor route previews fast for late sections.
            double startTime = projectiles.Count == 0 ? 0 : firstProjectileTime - positioningLeadTime;
            double timeStep = Math.Clamp(grid_size * Math.Sqrt(2) / Math.Max(0.001f, movementSpeed), minimum_step_duration, maximum_step_duration);

            var times = new List<double> { startTime };

            while (times[^1] < lastProjectileTime)
            {
                cancellationToken.ThrowIfCancellationRequested();
                times.Add(Math.Min(lastProjectileTime, times[^1] + timeStep));
            }

            if (times.Count == 1)
                times.Add(startTime + timeStep);

            List<Projectile>[] activeProjectiles = buildActiveProjectileLists(times);
            List<Vector2>[] movementOffsets = new List<Vector2>[times.Count];
            List<Vector2> regularMovementOffsets = createMovementOffsets(timeStep);

            for (int step = 1; step < times.Count; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double stepDuration = times[step] - times[step - 1];
                movementOffsets[step] = Math.Abs(stepDuration - timeStep) < 0.001
                    ? regularMovementOffsets
                    : createMovementOffsets(stepDuration);
            }

            DodgeArenaState[] arenas = times.Select(arenaEvaluator.Evaluate).ToArray();
            var history = new List<List<PlannerState>>(times.Count);

            DodgeArenaState initialArena = arenas[0];
            Vector2 initialPosition = DodgePlayer.ClampToArena(DodgePlayfield.BASE_SIZE / 2, initialArena.Position, initialArena.Size, playerSize);
            history.Add(new List<PlannerState>
            {
                new PlannerState(cellKey(initialPosition), snapToGrid(initialPosition), 0, -1, false),
            });

            for (int step = 1; step < times.Count; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<PlannerState> nextStates = expandStep(
                    history[^1], times, arenas, activeProjectiles, movementOffsets, step, beam_width);

                if (nextStates.Count > 0)
                {
                    history.Add(nextStates);
                    continue;
                }

                // A locally attractive route can still lead into a pocket. Rebuild
                // the recent route without beam pruning before declaring the
                // pattern impossible; this keeps alternate corridors alive.
                if (history[^1].Any(state => !state.UsedFallback)
                    && tryRecoverRoute(history, times, arenas, activeProjectiles, movementOffsets, step))
                    continue;

                // An actually impossible wall must not cause a teleport through
                // bullets. Keep normal movement and choose the least dangerous
                // reachable cell so autoplay can recover after the wall has passed.
                history.Add(createReachableFallback(
                    history[^1],
                    arenas[step - 1],
                    arenas[step],
                    times[step],
                    activeProjectiles[step],
                    movementOffsets[step]));
            }

            List<DodgeAutoplayRoutePoint> route = backtrackRoute(history, times);
            Route = route;
            Analysis = analyseRoute(route);
            addReplayFrames(route);
        }

        private List<PlannerState> expandStep(
            IReadOnlyList<PlannerState> previousStates,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<Vector2>> movementOffsets,
            int step,
            int width)
        {
            double previousTime = times[step - 1];
            double currentTime = times[step];
            DodgeArenaState previousArena = arenas[step - 1];
            DodgeArenaState currentArena = arenas[step];
            Dictionary<int, PlannerState> candidates = candidateBuffer;
            candidates.Clear();
            int scoreVersion = nextPositionScoreVersion();
            float maximumTransitionMovement =
                movementSpeed * (float)(currentTime - previousTime) + maximumArenaBoundaryMovement(previousArena, currentArena);
            List<Projectile>?[] transitionThreats = buildTransitionThreatMap(
                activeProjectiles[step],
                previousTime,
                currentTime,
                maximumTransitionMovement,
                transitionThreatBuffer);
            int futureStep = Math.Min(times.Count - 1, step + Math.Max(1, (int)Math.Round(300 / Math.Max(1, currentTime - previousTime))));
            List<Projectile>?[] currentClearanceThreats = buildClearanceThreatMap(activeProjectiles[step], currentTime, currentClearanceThreatBuffer);
            List<Projectile>?[] futureClearanceThreats = buildClearanceThreatMap(activeProjectiles[futureStep], times[futureStep], futureClearanceThreatBuffer);

            for (int parentIndex = 0; parentIndex < previousStates.Count; parentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlannerState previous = previousStates[parentIndex];
                Vector2 from = DodgePlayer.ClampToArena(previous.Position, previousArena.Position, previousArena.Size, playerSize);

                foreach (Vector2 offset in movementOffsets[step])
                {
                    Vector2 target = snapToGrid(from + offset);

                    if (!isInsideArena(target, currentArena))
                        continue;

                    if ((target - from).Length > maximumTransitionMovement + 0.01f)
                        continue;

                    IReadOnlyList<Projectile> relevantThreats =
                        transitionThreats[threatBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>();

                    if (!isTransitionSafe(from, target, previousTime, currentTime, previousArena, currentArena, relevantThreats))
                        continue;

                    int key = cellKey(target);
                    double positionScore;

                    if (positionScoreVersions[key] != scoreVersion)
                    {
                        positionScoreVersions[key] = scoreVersion;
                        positionScoreBuffer[key] = positionScore = scorePosition(
                            target,
                            currentTime,
                            currentArena,
                            currentClearanceThreats[clearanceBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>(),
                            times[futureStep],
                            futureClearanceThreats[clearanceBinKey(target)] ?? (IReadOnlyList<Projectile>)Array.Empty<Projectile>());
                    }
                    else
                        positionScore = positionScoreBuffer[key];

                    double score = previous.Score + positionScore - offset.Length * 0.0125;
                    var state = new PlannerState(key, target, score, parentIndex, false);

                    if (!candidates.TryGetValue(key, out PlannerState existing) || state.Score > existing.Score)
                        candidates[key] = state;
                }
            }

            return takeBestCandidates(candidates, width);
        }

        private int nextPositionScoreVersion()
        {
            if (positionScoreVersion == int.MaxValue)
            {
                Array.Clear(positionScoreVersions);
                positionScoreVersion = 0;
            }

            return ++positionScoreVersion;
        }

        private List<PlannerState> takeBestCandidates(Dictionary<int, PlannerState> candidates, int width)
        {
            int candidateCount = 0;

            foreach (PlannerState candidate in candidates.Values)
                candidateSortBuffer[candidateCount++] = candidate;

            Array.Sort(candidateSortBuffer, 0, candidateCount, PlannerStateScoreComparer.Instance);

            int resultCount = Math.Min(width, candidateCount);
            var result = new List<PlannerState>(resultCount);

            for (int i = 0; i < resultCount; i++)
                result.Add(candidateSortBuffer[i]);

            return result;
        }

        private bool tryRecoverRoute(
            List<List<PlannerState>> history,
            IReadOnlyList<double> times,
            IReadOnlyList<DodgeArenaState> arenas,
            IReadOnlyList<List<Projectile>> activeProjectiles,
            IReadOnlyList<List<Vector2>> movementOffsets,
            int failedStep)
        {
            int checkpoint = failedStep - 1;
            double earliestTime = times[failedStep] - recovery_lookback;

            while (checkpoint > 0 && times[checkpoint] > earliestTime)
                checkpoint--;

            var rebuilt = new List<List<PlannerState>>(failedStep - checkpoint);
            IReadOnlyList<PlannerState> previousStates = history[checkpoint];

            for (int step = checkpoint + 1; step <= failedStep; step++)
            {
                List<PlannerState> states = expandStep(
                    previousStates,
                    times,
                    arenas,
                    activeProjectiles,
                    movementOffsets,
                    step,
                    grid_columns * grid_rows);

                if (states.Count == 0)
                    return false;

                rebuilt.Add(states);
                previousStates = states;
            }

            history.RemoveRange(checkpoint + 1, history.Count - checkpoint - 1);
            history.AddRange(rebuilt);
            return true;
        }

        private DodgeAutoplayAnalysis analyseRoute(IReadOnlyList<DodgeAutoplayRoutePoint> route)
        {
            if (projectiles.Count == 0 || route.Count < 2)
                return default;

            double startTime = projectiles.Min(projectile => projectile.StartTime);
            double endTime = projectiles.Max(projectile => projectile.EndTime);
            double duration = Math.Max(1, endTime - startTime);
            double movementRatioSum = 0;
            double pressureSum = 0;
            double peakMovementRatio = 0;
            double peakPressure = 0;
            int peakActiveProjectiles = 0;
            int sampleCount = 0;
            int teleportCount = 0;
            Vector2? previousDirection = null;
            var sectionPeaks = new Dictionary<int, double>();

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
                Vector2 displacement = endPosition - startPosition;
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
                double clearance = calculateClearanceAndActive(endPosition, sampleTime, out int activeProjectiles);
                double pressure = Math.Clamp(1 - clearance / 96, 0, 1);
                double turn = 0;

                if (displacement.LengthSquared > 0.001f)
                {
                    Vector2 direction = displacement.Normalized();

                    if (previousDirection.HasValue)
                        turn = (1 - Math.Clamp(Vector2.Dot(previousDirection.Value, direction), -1, 1)) / 2;

                    previousDirection = direction;
                }

                movementRatioSum += movementRatio;
                pressureSum += pressure;
                peakMovementRatio = Math.Max(peakMovementRatio, movementRatio);
                peakPressure = Math.Max(peakPressure, pressure);
                peakActiveProjectiles = Math.Max(peakActiveProjectiles, activeProjectiles);
                sampleCount++;

                double localStrain = movementRatio * 1.35
                                     + pressure * (1.25 + Math.Log2(1 + activeProjectiles) * 0.25)
                                     + turn * 0.45;
                int section = (int)Math.Floor((sampleTime - startTime) / 400);

                if (!sectionPeaks.TryGetValue(section, out double existing) || localStrain > existing)
                    sectionPeaks[section] = localStrain;
            }

            double weightedStrain = 0;
            double weightSum = 0;
            double weight = 1;

            foreach (double strain in sectionPeaks.Values.OrderByDescending(value => value))
            {
                weightedStrain += strain * weight;
                weightSum += weight;
                weight *= 0.9;
            }

            if (weightSum > 0)
                weightedStrain /= weightSum;

            return new DodgeAutoplayAnalysis(
                projectiles.Count,
                duration,
                sampleCount == 0 ? 0 : movementRatioSum / sampleCount,
                peakMovementRatio,
                sampleCount == 0 ? 0 : pressureSum / sampleCount,
                peakPressure,
                peakActiveProjectiles,
                weightedStrain,
                teleportCount);
        }

        private double calculateClearanceAndActive(Vector2 position, double time, out int activeCount)
        {
            double minimum = 128;
            activeCount = 0;

            foreach (Projectile projectile in projectiles)
            {
                if (time < projectile.StartTime || time > projectile.EndTime)
                    continue;

                activeCount++;
                Vector2 delta = projectile.PositionAt(time, cameraEvaluator.Evaluate(time)) - position;
                float collisionDistance = (projectile.BulletSize + playerSize) / 2;
                double outsideX = Math.Max(0, Math.Abs(delta.X) - collisionDistance);
                double outsideY = Math.Max(0, Math.Abs(delta.Y) - collisionDistance);
                minimum = Math.Min(minimum, Math.Sqrt(outsideX * outsideX + outsideY * outsideY));
            }

            return minimum;
        }

        private void createProjectiles()
        {
            if (hitObjects.Length == 0)
                return;

            double gameplayEndTime = DodgeGameplayTiming.GetGameplayEndTime(hitObjects);
            double continuedEndTime = gameplayEndTime + DodgePlayfield.CONTINUED_BULLET_GRACE_PERIOD;

            foreach (DodgeBullet bullet in hitObjects.OfType<DodgeBullet>())
            {
                double endTime = Math.Min(bullet.MovementEndTime, continuedEndTime);
                projectiles.Add(Projectile.FromTrajectory(
                    bullet.StartTime,
                    endTime,
                    bullet.Position,
                    bullet.EndPosition,
                    bullet.Duration,
                    bullet.BulletSize,
                    bullet.MovementType,
                    bullet.WaveAmplitude,
                    bullet.WaveCycles,
                    bullet.WavePhase,
                    cameraEvaluator.Evaluate(bullet.StartTime)));
            }

            foreach (DodgeEmitter emitter in hitObjects.OfType<DodgeEmitter>())
            {
                for (int burst = 0; burst < emitter.EffectiveBurstCount; burst++)
                {
                    for (int i = 0; i < emitter.EffectiveBulletCount; i++)
                    {
                        double startTime = emitter.EmissionTimeAt(burst);
                        double endTime = Math.Min(emitter.ExitTimeAt(burst, i), continuedEndTime);
                        projectiles.Add(Projectile.FromTrajectory(
                            startTime,
                            endTime,
                            emitter.SourcePositionAt(burst),
                            emitter.EndPositionAt(burst, i),
                            emitter.Duration,
                            emitter.BulletSize,
                            emitter.MovementType,
                            emitter.WaveAmplitude,
                            emitter.WaveCycles,
                            emitter.WavePhase,
                            cameraEvaluator.Evaluate(startTime)));
                    }
                }
            }
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

        private List<Vector2> createMovementOffsets(double timeStep)
        {
            float maximumDistance = movementSpeed * (float)timeStep + 0.01f;
            int cellRadius = Math.Max(1, (int)Math.Ceiling(maximumDistance / grid_size));
            var result = new List<Vector2>();

            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    var offset = new Vector2(x * grid_size, y * grid_size);

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
                Vector2 bulletStart = projectile.PositionAt(overlapStart, cameraEvaluator.Evaluate(overlapStart));
                float expansion = (projectile.BulletSize + playerSize) / 2 + maximumPlayerMovement;
                float minimumX = bulletStart.X - expansion;
                float maximumX = bulletStart.X + expansion;
                float minimumY = bulletStart.Y - expansion;
                float maximumY = bulletStart.Y + expansion;

                for (int subdivision = 1; subdivision <= pathSubdivisions; subdivision++)
                {
                    double sampleTime = overlapStart + (overlapEnd - overlapStart) * subdivision / pathSubdivisions;
                    Vector2 sample = projectile.PositionAt(sampleTime, cameraEvaluator.Evaluate(sampleTime));
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

                Vector2 cameraOffset = cameraEvaluator.Evaluate(time);
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
            IReadOnlyList<Projectile> active)
        {
            bool staticArena = arenaIsStatic(startArena, endArena);
            int subdivisions = staticArena ? 1 : Math.Max(1, (int)Math.Ceiling((endTime - startTime) / transition_collision_step));

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
                Vector2 playerStart = DodgePlayer.ClampToArena(Vector2.Lerp(from, to, startProgress), segmentStartArena.Position, segmentStartArena.Size, playerSize);
                Vector2 playerEnd = DodgePlayer.ClampToArena(Vector2.Lerp(from, to, endProgress), segmentEndArena.Position, segmentEndArena.Size, playerSize);

                for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
                {
                    Projectile projectile = active[activeIndex];
                    double overlapStart = Math.Max(segmentStart, projectile.StartTime);
                    double overlapEnd = Math.Min(segmentEnd, projectile.EndTime);

                    if (overlapEnd < overlapStart)
                        continue;

                    float overlapStartProgress = (float)((overlapStart - segmentStart) / Math.Max(0.001, segmentEnd - segmentStart));
                    float overlapEndProgress = (float)((overlapEnd - segmentStart) / Math.Max(0.001, segmentEnd - segmentStart));
                    Vector2 relativeStart = projectile.PositionAt(overlapStart, cameraEvaluator.Evaluate(overlapStart)) - Vector2.Lerp(playerStart, playerEnd, overlapStartProgress);
                    Vector2 relativeEnd = projectile.PositionAt(overlapEnd, cameraEvaluator.Evaluate(overlapEnd)) - Vector2.Lerp(playerStart, playerEnd, overlapEndProgress);
                    float collisionDistance = (projectile.BulletSize + playerSize) / 2;

                    if (segmentIntersectsBox(relativeStart, relativeEnd, new Vector2(-collisionDistance), new Vector2(collisionDistance)))
                        return false;
                }
            }

            return true;
        }

        private double scorePosition(
            Vector2 position,
            double time,
            DodgeArenaState arena,
            IReadOnlyList<Projectile> currentThreats,
            double futureTime,
            IReadOnlyList<Projectile> futureThreats)
        {
            double clearance = calculateClearance(position, time, currentThreats ?? Array.Empty<Projectile>());
            double futureClearance = calculateClearance(position, futureTime, futureThreats ?? Array.Empty<Projectile>());
            Vector2 arenaCentre = arena.Position + arena.Size / 2;

            return Math.Min(clearance, scored_clearance) * 0.08
                   + Math.Min(futureClearance, scored_clearance) * 0.12
                   - (position - arenaCentre).Length * 0.001;
        }

        private double calculateClearance(Vector2 position, double time, IReadOnlyList<Projectile> active)
        {
            double minimum = 128;

            for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
            {
                Projectile projectile = active[activeIndex];
                if (time < projectile.StartTime || time > projectile.EndTime)
                    continue;

                Vector2 delta = projectile.PositionAt(time, cameraEvaluator.Evaluate(time)) - position;
                float collisionDistance = (projectile.BulletSize + playerSize) / 2;
                double outsideX = Math.Max(0, Math.Abs(delta.X) - collisionDistance);
                double outsideY = Math.Max(0, Math.Abs(delta.Y) - collisionDistance);
                minimum = Math.Min(minimum, Math.Sqrt(outsideX * outsideX + outsideY * outsideY));
            }

            return minimum;
        }

        private List<PlannerState> createReachableFallback(
            IReadOnlyList<PlannerState> previousStates,
            DodgeArenaState previousArena,
            DodgeArenaState currentArena,
            double time,
            IReadOnlyList<Projectile> active,
            IReadOnlyList<Vector2> movementOffsets)
        {
            Dictionary<int, PlannerState> candidates = candidateBuffer;
            candidates.Clear();
            float maximumTransitionMovement = movementOffsets.Max(offset => offset.Length)
                                              + maximumArenaBoundaryMovement(previousArena, currentArena);

            for (int parentIndex = 0; parentIndex < previousStates.Count; parentIndex++)
            {
                PlannerState previous = previousStates[parentIndex];
                Vector2 from = DodgePlayer.ClampToArena(previous.Position, previousArena.Position, previousArena.Size, playerSize);

                for (int offsetIndex = 0; offsetIndex < movementOffsets.Count; offsetIndex++)
                {
                    Vector2 offset = movementOffsets[offsetIndex];
                    Vector2 position = snapToGrid(from + offset);

                    if (!isInsideArena(position, currentArena))
                        continue;

                    if ((position - from).Length > maximumTransitionMovement + 0.01f)
                        continue;

                    double clearance = calculateClearance(position, time, active);
                    var state = new PlannerState(
                        cellKey(position),
                        position,
                        previous.Score + clearance - offset.Length * 0.0125,
                        parentIndex,
                        true);

                    if (!candidates.TryGetValue(state.CellKey, out PlannerState existing) || state.Score > existing.Score)
                        candidates[state.CellKey] = state;
                }
            }

            if (candidates.Count > 0)
                return takeBestCandidates(candidates, beam_width);

            int bestParent = previousStates.Select((state, index) => (state, index))
                                           .OrderByDescending(item => item.state.Score)
                                           .First().index;
            Vector2 centre = snapToGrid(DodgePlayer.ClampToArena(
                currentArena.Position + currentArena.Size / 2,
                currentArena.Position,
                currentArena.Size,
                playerSize));
            return new List<PlannerState>
            {
                new PlannerState(cellKey(centre), centre, previousStates[bestParent].Score, bestParent, true),
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
                DodgeAction[] actions = i + 1 < route.Count
                    ? actionsForMovement(route[i + 1].Position - point.Position)
                    : Array.Empty<DodgeAction>();

                Frames.Add(new DodgeReplayFrame(point.Time, point.Position, actions));
            }

            Frames.Add(new DodgeReplayFrame(route[^1].Time + 100, route[^1].Position));
        }

        private static DodgeAction[] actionsForMovement(Vector2 movement)
        {
            var actions = new List<DodgeAction>(2);

            if (movement.X < -0.01f) actions.Add(DodgeAction.MoveLeft);
            if (movement.X > 0.01f) actions.Add(DodgeAction.MoveRight);
            if (movement.Y < -0.01f) actions.Add(DodgeAction.MoveUp);
            if (movement.Y > 0.01f) actions.Add(DodgeAction.MoveDown);

            return actions.ToArray();
        }

        private bool isInsideArena(Vector2 position, DodgeArenaState arena)
        {
            float halfPlayer = playerSize / 2;
            return position.X >= arena.Position.X + halfPlayer
                   && position.X <= arena.Position.X + arena.Size.X - halfPlayer
                   && position.Y >= arena.Position.Y + halfPlayer
                   && position.Y <= arena.Position.Y + arena.Size.Y - halfPlayer;
        }

        private static bool arenaIsStatic(DodgeArenaState first, DodgeArenaState second)
            => first.Position == second.Position && first.Size == second.Size;

        private static float maximumArenaBoundaryMovement(DodgeArenaState first, DodgeArenaState second)
        {
            Vector2 topLeftMovement = Vector2.ComponentMax(first.Position - second.Position, second.Position - first.Position);
            Vector2 firstBottomRight = first.Position + first.Size;
            Vector2 secondBottomRight = second.Position + second.Size;
            Vector2 bottomRightMovement = Vector2.ComponentMax(firstBottomRight - secondBottomRight, secondBottomRight - firstBottomRight);
            return Math.Max(topLeftMovement.Length, bottomRightMovement.Length);
        }

        private static int threatBinKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Floor(position.X / threat_bin_size), 0, threat_bin_columns - 1);
            int row = Math.Clamp((int)MathF.Floor(position.Y / threat_bin_size), 0, threat_bin_rows - 1);
            return row * threat_bin_columns + column;
        }

        private static int clearanceBinKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Floor(position.X / clearance_bin_size), 0, clearance_bin_columns - 1);
            int row = Math.Clamp((int)MathF.Floor(position.Y / clearance_bin_size), 0, clearance_bin_rows - 1);
            return row * clearance_bin_columns + column;
        }

        private static Vector2 snapToGrid(Vector2 position) => new Vector2(
            Math.Clamp(MathF.Round(position.X / grid_size) * grid_size, 0, DodgePlayfield.WIDTH),
            Math.Clamp(MathF.Round(position.Y / grid_size) * grid_size, 0, DodgePlayfield.HEIGHT));

        private static int cellKey(Vector2 position)
        {
            int column = Math.Clamp((int)MathF.Round(position.X / grid_size), 0, grid_columns - 1);
            int row = Math.Clamp((int)MathF.Round(position.Y / grid_size), 0, grid_rows - 1);
            return row * grid_columns + column;
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

        private readonly record struct PlannerState(int CellKey, Vector2 Position, double Score, int ParentIndex, bool UsedFallback);

        private sealed class PlannerStateScoreComparer : IComparer<PlannerState>
        {
            public static readonly PlannerStateScoreComparer Instance = new PlannerStateScoreComparer();

            public int Compare(PlannerState first, PlannerState second) => second.Score.CompareTo(first.Score);
        }

        private readonly record struct Projectile(
            double StartTime,
            double EndTime,
            Vector2 StartPosition,
            Vector2 ControlEndPosition,
            double Duration,
            float BulletSize,
            DodgeMovementType MovementType,
            float WaveAmplitude,
            int WaveCycles,
            float WavePhase,
            Vector2 CameraAnchor)
        {
            public static Projectile FromTrajectory(
                double startTime,
                double endTime,
                Vector2 startPosition,
                Vector2 controlEnd,
                double duration,
                float bulletSize,
                DodgeMovementType movementType,
                float waveAmplitude,
                int waveCycles,
                float wavePhase,
                Vector2 cameraAnchor)
            {
                Vector2 effectiveStart = duration <= 0 ? controlEnd : startPosition;
                return new Projectile(
                    startTime,
                    Math.Max(startTime, endTime),
                    effectiveStart,
                    controlEnd,
                    duration,
                    bulletSize,
                    movementType,
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
                        WavePhase);
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
                return Math.Max(1, (int)Math.Ceiling(Math.Max(0, overlapEnd - overlapStart) / Duration * WaveCycles * 24));
            }
        }
    }
}
