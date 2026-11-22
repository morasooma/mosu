// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Difficulty
{
    public abstract class DifficultyCalculator
    {
        /// <summary>
        /// The beatmap for which difficulty will be calculated.
        /// </summary>
        protected IBeatmap Beatmap { get; set; }

        /// <summary>
        /// The prepared playable beatmap from the most recent calculation. Consume before
        /// starting another calculation, which replaces this instance.
        /// </summary>
        internal IBeatmap CalculatedBeatmap => Beatmap;

        /// <summary>
        /// The working beatmap for which difficulty will be calculated.
        /// </summary>
        protected readonly IWorkingBeatmap WorkingBeatmap;

        private Mod[] playableMods;

        private readonly IRulesetInfo ruleset;

        /// <summary>
        /// A yymmdd version which is used to discern when reprocessing is required.
        /// </summary>
        public virtual int Version => 0;

        protected DifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
        {
            this.ruleset = ruleset;
            WorkingBeatmap = beatmap;
        }

        /// <summary>
        /// Calculates the difficulty of the beatmap with no mods applied.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        public DifficultyAttributes Calculate(CancellationToken cancellationToken = default)
            => Calculate(Array.Empty<Mod>(), cancellationToken);

        /// <summary>
        /// Calculates the difficulty of the beatmap using a specific mod combination.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        public DifficultyAttributes Calculate([NotNull] IEnumerable<Mod> mods, CancellationToken cancellationToken = default)
        {
            using var timedCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            if (!cancellationToken.CanBeCanceled)
                cancellationToken = timedCancellationSource.Token;

            cancellationToken.ThrowIfCancellationRequested();
            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
            preProcess(mods, cancellationToken);

            var skills = CalculateSkills(Beatmap, playableMods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return CreateDifficultyAttributes(Beatmap, playableMods, skills);
        }

        protected static void CalculationCheckpoint() => BackgroundCalculationLimiter.Checkpoint();

        protected virtual Skill[] CalculateSkills(IBeatmap beatmap, Mod[] mods, CancellationToken cancellationToken)
        {
            var skills = CreateSkills(beatmap, mods);
            if (beatmap.HitObjects.Count == 0)
                return skills;

            foreach (var hitObject in getDifficultyHitObjects(cancellationToken))
            {
                BackgroundCalculationLimiter.Checkpoint();
                foreach (var skill in skills)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    skill.Process(hitObject);
                }
            }

            return skills;
        }

        /// <summary>
        /// Calculates the difficulty of the beatmap with no mods applied and returns a set of <see cref="TimedDifficultyAttributes"/> representing the difficulty at every relevant time value in the beatmap.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The set of <see cref="TimedDifficultyAttributes"/>.</returns>
        /// <summary>
        /// Whether this calculation should also prepare ruleset-specific state required by a
        /// subsequent performance calculation. Visual star-rating-only callers may disable this.
        /// </summary>
        public bool PreparePerformanceCalculation { get; set; } = true;

        public List<TimedDifficultyAttributes> CalculateTimed(CancellationToken cancellationToken = default)
            => CalculateTimed(Array.Empty<Mod>(), cancellationToken);

        /// <summary>
        /// Calculates the difficulty of the beatmap using a specific mod combination and returns a set of <see cref="TimedDifficultyAttributes"/> representing the difficulty at every relevant time value in the beatmap.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The set of <see cref="TimedDifficultyAttributes"/>.</returns>
        public List<TimedDifficultyAttributes> CalculateTimed([NotNull] IEnumerable<Mod> mods, CancellationToken cancellationToken = default)
        {
            using var timedCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            if (!cancellationToken.CanBeCanceled)
                cancellationToken = timedCancellationSource.Token;

            cancellationToken.ThrowIfCancellationRequested();
            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
            preProcess(mods, cancellationToken);

            var attribs = new List<TimedDifficultyAttributes>();

            if (!Beatmap.HitObjects.Any())
                return attribs;

            var skills = CreateSkills(Beatmap, playableMods);
            var progressiveBeatmap = new ProgressiveCalculationBeatmap(Beatmap);
            var difficultyObjects = getDifficultyHitObjects(cancellationToken).ToArray();

            int currentIndex = 0;
            bool preparePerformance = PreparePerformanceCalculation;

            // A timed calculation creates one attributes instance per hit object. Ruleset-specific
            // performance preparation must run once for the complete map, not once for every prefix.
            PreparePerformanceCalculation = false;

            try
            {
                BeginTimedCalculation(Beatmap, playableMods);
                foreach (var obj in Beatmap.HitObjects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progressiveBeatmap.HitObjects.Add(obj);
                    OnTimedPrefixAdded(progressiveBeatmap, obj);

                    while (currentIndex < difficultyObjects.Length && difficultyObjects[currentIndex].BaseObject.GetEndTime() <= obj.GetEndTime())
                    {
                        foreach (var skill in skills)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            skill.Process(difficultyObjects[currentIndex]);
                        }

                        currentIndex++;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    attribs.Add(new TimedDifficultyAttributes(obj.GetEndTime(), CreateDifficultyAttributes(progressiveBeatmap, playableMods, skills)));
                }

                if (preparePerformance)
                    PrepareTimedPerformanceCalculation(Beatmap, playableMods, attribs);

                return attribs;
            }
            finally
            {
                PreparePerformanceCalculation = preparePerformance;
                EndTimedCalculation();
            }
        }

        /// <summary>
        /// Returns an on-demand cursor only for rulesets/mods which explicitly support it.
        /// Use a dedicated calculator instance; the cursor owns its timed lifecycle.
        /// </summary>
        public virtual bool SupportsProgressiveCalculation(IEnumerable<Mod> mods) => false;

        public virtual IProgressiveDifficultyCalculator CreateProgressiveCalculation(IEnumerable<Mod> mods, CancellationToken cancellationToken = default)
            => null;

        protected IProgressiveDifficultyCalculator CreateProgressiveCalculationCore(IEnumerable<Mod> mods, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            preProcess(mods, cancellationToken);
            return new ProgressiveCalculation(this, cancellationToken);
        }

        private sealed class ProgressiveCalculation : IProgressiveDifficultyCalculator
        {
            private readonly DifficultyCalculator calculator;
            private readonly DifficultyHitObject[] difficultyObjects;
            private readonly double[] prefixTimes;
            private readonly bool preparePerformance;
            private ProgressiveCalculationBeatmap progressiveBeatmap;
            private Skill[] skills;
            private int objectIndex;
            private int difficultyIndex;
            private int lastIndex = -1;
            private DifficultyAttributes lastAttributes;
            // Keep a small recent window, not an eager all-prefix table. Nested slider
            // judgements/rewinds can revisit an already calculated prefix many times.
            private const int recent_prefix_limit = 64;
            private readonly Dictionary<int, DifficultyAttributes> recentAttributes = new Dictionary<int, DifficultyAttributes>();
            private readonly Queue<int> recentOrder = new Queue<int>();
            private bool disposed;

            public ProgressiveCalculation(DifficultyCalculator calculator, CancellationToken token)
            {
                this.calculator = calculator;
                difficultyObjects = calculator.getDifficultyHitObjects(token).ToArray();
                prefixTimes = calculator.Beatmap.HitObjects.Select(obj => obj.GetEndTime()).ToArray();
                preparePerformance = calculator.PreparePerformanceCalculation;
                calculator.PreparePerformanceCalculation = false;
                try
                {
                    reset();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private bool timedStarted;

            private void reset()
            {
                if (timedStarted)
                    calculator.EndTimedCalculation();
                skills = calculator.CreateSkills(calculator.Beatmap, calculator.playableMods);
                progressiveBeatmap = new ProgressiveCalculationBeatmap(calculator.Beatmap);
                objectIndex = difficultyIndex = 0;
                lastIndex = -1;
                lastAttributes = null;
                timedStarted = true;
                calculator.BeginTimedCalculation(calculator.Beatmap, calculator.playableMods);
            }

            public DifficultyAttributes GetAttributesAtTime(double time, CancellationToken cancellationToken = default)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(ProgressiveCalculation));
                cancellationToken.ThrowIfCancellationRequested();
                if (prefixTimes.Length == 0)
                    return null;

                // Deliberately match the existing HUD lookup, including duplicate timestamps
                // and maps whose overlapping sliders make end times non-monotonic.
                int index = Array.BinarySearch(prefixTimes, time);
                if (index < 0)
                    index = ~index - 1;
                index = Math.Clamp(index, 0, prefixTimes.Length - 1);
                if (index == lastIndex)
                    return lastAttributes;
                if (recentAttributes.TryGetValue(index, out var cached))
                    return cached;
                if (index < objectIndex - 1)
                    reset();

                try
                {
                    while (objectIndex <= index)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var obj = calculator.Beatmap.HitObjects[objectIndex++];
                        progressiveBeatmap.HitObjects.Add(obj);
                        calculator.OnTimedPrefixAdded(progressiveBeatmap, obj);
                        while (difficultyIndex < difficultyObjects.Length && difficultyObjects[difficultyIndex].BaseObject.GetEndTime() <= obj.GetEndTime())
                        {
                            foreach (var skill in skills)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                skill.Process(difficultyObjects[difficultyIndex]);
                            }
                            difficultyIndex++;
                        }
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    lastAttributes = calculator.CreateDifficultyAttributes(progressiveBeatmap, calculator.playableMods, skills);
                    lastIndex = index;
                    if (recentAttributes.Count == recent_prefix_limit)
                        recentAttributes.Remove(recentOrder.Dequeue());
                    recentAttributes.Add(index, lastAttributes);
                    recentOrder.Enqueue(index);
                    return lastAttributes;
                }
                catch
                {
                    // Cancellation may interrupt processing halfway through one object's skills.
                    // Discard that state before a retry rather than using a partially advanced cursor.
                    reset();
                    throw;
                }
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                recentAttributes.Clear();
                recentOrder.Clear();
                if (timedStarted)
                    calculator.EndTimedCalculation();
                calculator.PreparePerformanceCalculation = preparePerformance;
            }
        }

        /// <summary>
        /// Called once before timed prefixes are calculated, and after each prefix object is appended.
        /// Allows rulesets to maintain prefix-invariant aggregates without bypassing virtual attribute creation.
        /// </summary>
        protected virtual void BeginTimedCalculation(IBeatmap beatmap, Mod[] mods) { }

        protected virtual void OnTimedPrefixAdded(IBeatmap prefixBeatmap, HitObject addedObject) { }

        protected virtual void EndTimedCalculation() { }

        protected virtual void PrepareTimedPerformanceCalculation(IBeatmap beatmap, Mod[] mods, IReadOnlyList<TimedDifficultyAttributes> attributes)
        {
        }

        /// <summary>
        /// Processes native ruleset skills on an already prepared beatmap without converting it again.
        /// This is intentionally separate from cumulative timed star ratings, which are not local difficulty.
        /// No hit object timestamps are modified.
        /// </summary>
        public virtual IReadOnlyList<LocalDifficultySample> CalculateLocalDifficulty(IBeatmap beatmap, double clockRate = 1, CancellationToken cancellationToken = default)
        {
            if (!double.IsFinite(clockRate) || clockRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(clockRate));
            Mod[] mods = clockRate == 1 ? Array.Empty<Mod>() : new Mod[] { new LocalDifficultyRateMod(clockRate) };
            IBeatmap previousBeatmap = Beatmap;
            Beatmap = beatmap;
            try
            {
                var skills = CreateLocalDifficultySkills(beatmap, mods);
                var result = new List<LocalDifficultySample>();
                foreach (var obj in SortObjects(CreateDifficultyHitObjects(beatmap, mods, cancellationToken)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    double sum = 0;
                    foreach (var skill in skills)
                    {
                        skill.Process(obj);
                        double local = skill.LocalDifficulty;
                        if (double.IsFinite(local))
                            sum += Math.Max(0, local);
                    }

                    result.Add(new LocalDifficultySample(obj.BaseObject.StartTime, Math.Sqrt(sum)));
                }

                return result;
            }
            finally
            {
                Beatmap = previousBeatmap;
            }
        }

        protected virtual Skill[] CreateLocalDifficultySkills(IBeatmap beatmap, Mod[] mods) => CreateSkills(beatmap, mods);

        /// <summary>
        /// Calculates the difficulty of the beatmap using all mod combinations applicable to the beatmap.
        /// </summary>
        /// <remarks>
        /// This can only be used to compute difficulties for legacy mod combinations.
        /// </remarks>
        /// <returns>A collection of structures describing the difficulty of the beatmap for each mod combination.</returns>
        public IEnumerable<DifficultyAttributes> CalculateAllLegacyCombinations(CancellationToken cancellationToken = default)
            => CalculateBatch(createLegacyCombinations(), cancellationToken);

        private IEnumerable<IEnumerable<Mod>> createLegacyCombinations()
        {
            var rulesetInstance = ruleset.CreateInstance();

            foreach (var combination in CreateDifficultyAdjustmentModCombinations())
            {
                Mod classicMod = rulesetInstance.CreateMod<ModClassic>();

                var finalCombination = ModUtils.FlattenMod(combination);
                if (classicMod != null)
                    finalCombination = finalCombination.Append(classicMod);

                yield return finalCombination.ToArray();
            }
        }

        /// <summary>
        /// Calculates a sequential batch on this dedicated calculator, preserving request order.
        /// Enumerate/dispose before using this calculator again. Rulesets may reuse preparation
        /// inside this bounded lifetime, but not globally or between gameplay owners.
        /// </summary>
        public virtual IEnumerable<DifficultyAttributes> CalculateBatch(IEnumerable<IEnumerable<Mod>> combinations, CancellationToken cancellationToken = default)
        {
            foreach (var combination in combinations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Calculate(combination, cancellationToken);
            }
        }

        /// <summary>
        /// Retrieves the <see cref="DifficultyHitObject"/>s to calculate against.
        /// </summary>
        private IEnumerable<DifficultyHitObject> getDifficultyHitObjects(CancellationToken cancellationToken) => SortObjects(CreateDifficultyHitObjects(Beatmap, playableMods, cancellationToken));

        /// <summary>
        /// Performs required tasks before every calculation.
        /// </summary>
        /// <param name="mods">The original list of <see cref="Mod"/>s.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private void preProcess([NotNull] IEnumerable<Mod> mods, CancellationToken cancellationToken)
        {
            playableMods = mods.Select(m => m.DeepClone()).ToArray();
            Beatmap = GetPlayableBeatmap(ruleset, playableMods, cancellationToken);
        }

        protected virtual IBeatmap GetPlayableBeatmap(IRulesetInfo rulesetInfo, Mod[] mods, CancellationToken cancellationToken)
            => WorkingBeatmap.GetPlayableBeatmap(rulesetInfo, mods, cancellationToken);

        /// <summary>
        /// Sorts a given set of <see cref="DifficultyHitObject"/>s.
        /// </summary>
        /// <param name="input">The <see cref="DifficultyHitObject"/>s to sort.</param>
        /// <returns>The sorted <see cref="DifficultyHitObject"/>s.</returns>
        protected virtual IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input)
            => input.OrderBy(h => h.BaseObject.StartTime);

        /// <summary>
        /// Creates all <see cref="Mod"/> combinations which adjust the <see cref="Beatmaps.Beatmap"/> difficulty.
        /// </summary>
        public Mod[] CreateDifficultyAdjustmentModCombinations()
        {
            return createDifficultyAdjustmentModCombinations(DifficultyAdjustmentMods, Array.Empty<Mod>()).ToArray();

            static IEnumerable<Mod> createDifficultyAdjustmentModCombinations(ReadOnlyMemory<Mod> remainingMods, IEnumerable<Mod> currentSet, int currentSetCount = 0)
            {
                // Return the current set.
                switch (currentSetCount)
                {
                    case 0:
                        // Initial-case: Empty current set
                        yield return new ModNoMod();

                        break;

                    case 1:
                        yield return currentSet.Single();

                        break;

                    default:
                        yield return new MultiMod(currentSet.ToArray());

                        break;
                }

                // Apply the rest of the remaining mods recursively.
                for (int i = 0; i < remainingMods.Length; i++)
                {
                    (var nextSet, int nextCount) = flatten(remainingMods.Span[i]);

                    // Check if any mods in the next set are incompatible with any of the current set.
                    if (currentSet.SelectMany(m => m.IncompatibleMods).Any(c => nextSet.Any(c.IsInstanceOfType)))
                        continue;

                    // Check if any mods in the next set are the same type as the current set. Mods of the exact same type are not incompatible with themselves.
                    if (currentSet.Any(c => nextSet.Any(n => c.GetType() == n.GetType())))
                        continue;

                    // If all's good, attach the next set to the current set and recurse further.
                    foreach (var combo in createDifficultyAdjustmentModCombinations(remainingMods.Slice(i + 1), currentSet.Concat(nextSet), currentSetCount + nextCount))
                        yield return combo;
                }
            }

            // Flattens a mod hierarchy (through MultiMod) as an IEnumerable<Mod>
            static (IEnumerable<Mod> set, int count) flatten(Mod mod)
            {
                if (!(mod is MultiMod multi))
                    return (mod.Yield(), 1);

                IEnumerable<Mod> set = Enumerable.Empty<Mod>();
                int count = 0;

                foreach (var nested in multi.Mods)
                {
                    (var nestedSet, int nestedCount) = flatten(nested);
                    set = set.Concat(nestedSet);
                    count += nestedCount;
                }

                return (set, count);
            }
        }

        /// <summary>
        /// Retrieves all <see cref="Mod"/>s which adjust the <see cref="Beatmaps.Beatmap"/> difficulty.
        /// </summary>
        protected virtual Mod[] DifficultyAdjustmentMods => Array.Empty<Mod>();

        /// <summary>
        /// Creates <see cref="DifficultyAttributes"/> to describe beatmap's calculated difficulty.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> whose difficulty was calculated.
        /// This may differ from <see cref="Beatmap"/> in the case of timed calculation.</param>
        /// <param name="mods">The <see cref="Mod"/>s that difficulty was calculated with.</param>
        /// <param name="skills">The skills which processed the beatmap.</param>
        protected abstract DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills);

        /// <summary>
        /// Enumerates <see cref="DifficultyHitObject"/>s to be processed from <see cref="HitObject"/>s in the <see cref="IBeatmap"/>.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> providing the <see cref="HitObject"/>s to enumerate.</param>
        /// <param name="mods">Mods to create difficulty objects with.</param>
        /// <returns>The enumerated <see cref="DifficultyHitObject"/>s.</returns>
        protected abstract IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods);

        /// <summary>
        /// Creates difficulty hitobjects while observing the active calculation cancellation token.
        /// Existing rulesets remain compatible through the original overload.
        /// </summary>
        protected virtual IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreateDifficultyHitObjects(beatmap, mods);
        }

        /// <summary>
        /// Creates the <see cref="Skill"/>s to calculate the difficulty of an <see cref="IBeatmap"/>.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> whose difficulty will be calculated.
        /// This may differ from <see cref="Beatmap"/> in the case of timed calculation.</param>
        /// <param name="mods">Mods to calculate difficulty with.</param>
        /// <returns>The <see cref="Skill"/>s.</returns>
        protected abstract Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods);

        /// <summary>
        /// Used to calculate timed difficulty attributes, where only a subset of hitobjects should be visible at any point in time.
        /// </summary>
        private class ProgressiveCalculationBeatmap : IBeatmap
        {
            private readonly IBeatmap baseBeatmap;

            public ProgressiveCalculationBeatmap(IBeatmap baseBeatmap)
            {
                this.baseBeatmap = baseBeatmap;
            }

            public readonly List<HitObject> HitObjects = new List<HitObject>();

            IReadOnlyList<HitObject> IBeatmap.HitObjects => HitObjects;

            #region Delegated IBeatmap implementation

            public BeatmapInfo BeatmapInfo
            {
                get => baseBeatmap.BeatmapInfo;
                set => baseBeatmap.BeatmapInfo = value;
            }

            public ControlPointInfo ControlPointInfo
            {
                get => baseBeatmap.ControlPointInfo;
                set => baseBeatmap.ControlPointInfo = value;
            }

            public BeatmapMetadata Metadata => baseBeatmap.Metadata;

            public BeatmapDifficulty Difficulty
            {
                get => baseBeatmap.Difficulty;
                set => baseBeatmap.Difficulty = value;
            }

            public SortedList<BreakPeriod> Breaks
            {
                get => baseBeatmap.Breaks;
                set => baseBeatmap.Breaks = value;
            }

            public double TotalBreakTime => baseBeatmap.TotalBreakTime;
            public IEnumerable<BeatmapStatistic> GetStatistics() => baseBeatmap.GetStatistics();
            public double GetMostCommonBeatLength() => baseBeatmap.GetMostCommonBeatLength();
            public int BeatmapVersion => baseBeatmap.BeatmapVersion;
            public IBeatmap Clone() => new ProgressiveCalculationBeatmap(baseBeatmap.Clone());

            public double AudioLeadIn
            {
                get => baseBeatmap.AudioLeadIn;
                set => baseBeatmap.AudioLeadIn = value;
            }

            public float StackLeniency
            {
                get => baseBeatmap.StackLeniency;
                set => baseBeatmap.StackLeniency = value;
            }

            public bool SpecialStyle
            {
                get => baseBeatmap.SpecialStyle;
                set => baseBeatmap.SpecialStyle = value;
            }

            public bool LetterboxInBreaks
            {
                get => baseBeatmap.LetterboxInBreaks;
                set => baseBeatmap.LetterboxInBreaks = value;
            }

            public bool WidescreenStoryboard
            {
                get => baseBeatmap.WidescreenStoryboard;
                set => baseBeatmap.WidescreenStoryboard = value;
            }

            public bool EpilepsyWarning
            {
                get => baseBeatmap.EpilepsyWarning;
                set => baseBeatmap.EpilepsyWarning = value;
            }

            public bool SamplesMatchPlaybackRate
            {
                get => baseBeatmap.SamplesMatchPlaybackRate;
                set => baseBeatmap.SamplesMatchPlaybackRate = value;
            }

            public double DistanceSpacing
            {
                get => baseBeatmap.DistanceSpacing;
                set => baseBeatmap.DistanceSpacing = value;
            }

            public int GridSize
            {
                get => baseBeatmap.GridSize;
                set => baseBeatmap.GridSize = value;
            }

            public double TimelineZoom
            {
                get => baseBeatmap.TimelineZoom;
                set => baseBeatmap.TimelineZoom = value;
            }

            public CountdownType Countdown
            {
                get => baseBeatmap.Countdown;
                set => baseBeatmap.Countdown = value;
            }

            public int CountdownOffset
            {
                get => baseBeatmap.CountdownOffset;
                set => baseBeatmap.CountdownOffset = value;
            }

            public int[] Bookmarks
            {
                get => baseBeatmap.Bookmarks;
                set => baseBeatmap.Bookmarks = value;
            }

            public double[] SliderVelocityPresets
            {
                get => baseBeatmap.SliderVelocityPresets;
                set => baseBeatmap.SliderVelocityPresets = value;
            }

            #endregion
        }
    }
}
