// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.MathUtils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.018;

        private readonly bool isForCurrentRuleset;
        private readonly bool applyMappingAntiAbuse;
        private List<DifficultyHitObject>? difficultyHitObjectCache;
        private double vibroCandidateObjectRatio;
        private double longestVibroDuration;
        private bool vibroPenaltyActive;

        internal double VibroCandidateObjectRatio => vibroCandidateObjectRatio;
        internal double LongestVibroDuration => longestVibroDuration;

        public override int Version => 20260722;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
            applyMappingAntiAbuse = !beatmap.BeatmapInfo.GetOnlineStatus().BypassesMappingAntiAbuse();
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            var strain = skills.OfType<Strain>().Single();
            ManiaDifficultyAttributes attributes = new ManiaDifficultyAttributes
            {
                StarRating = strain.DifficultyValue()
                             * difficulty_multiplier
                             * (isForCurrentRuleset && applyMappingAntiAbuse ? CalculateOverallDifficultyPenalty(beatmap.Difficulty.OverallDifficulty) : 1)
                             * CalculateVibroMapPenalty(vibroPenaltyActive ? vibroCandidateObjectRatio : 0),
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
            };

            return attributes;
        }

        /// <summary>
        /// Accounts for the timing lenience of low-OD maps using the actual Perfect window.
        /// OD5 is the neutral reference and higher OD is not given an additional bonus.
        /// </summary>
        internal static double CalculateOverallDifficultyPenalty(double overallDifficulty)
        {
            var referenceWindows = new ManiaHitWindows();
            referenceWindows.SetDifficulty(5);

            var effectiveWindows = new ManiaHitWindows();
            effectiveWindows.SetDifficulty(overallDifficulty);

            return Math.Min(1, referenceWindows.WindowFor(HitResult.Perfect) / Math.Max(1, effectiveWindows.WindowFor(HitResult.Perfect)));
        }

        /// <summary>
        /// Removes residual section-peak inflation from maps made primarily of sustained
        /// repeated/alternating vibro packs. Candidate coverage below 40% is left untouched;
        /// the map-level multiplier reaches 56% at 45% coverage.
        /// </summary>
        internal static double CalculateVibroMapPenalty(double vibroCandidateObjectRatio)
        {
            double progress = Math.Clamp((vibroCandidateObjectRatio - 0.40) / 0.05, 0, 1);
            double smoothProgress = progress * progress * (3 - 2 * progress);
            return 1 - 0.44 * smoothProgress;
        }

        internal static bool ShouldApplyVibroPenalty(double vibroCandidateObjectRatio, double longestVibroDuration) =>
            vibroCandidateObjectRatio > 0.40 && longestVibroDuration >= 1500;

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            if (difficultyHitObjectCache != null)
            {
                List<DifficultyHitObject> cachedObjects = difficultyHitObjectCache;
                difficultyHitObjectCache = null;
                return cachedObjects;
            }

            return createDifficultyHitObjects(beatmap, mods);
        }

        private static List<DifficultyHitObject> createDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            LegacySortHelper<HitObject>.Sort(sortedObjects, Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            return objects;
        }

        private static double calculateVibroCandidateObjectRatio(IReadOnlyList<DifficultyHitObject> objects, int totalObjectCount)
        {
            if (objects.Count == 0 || totalObjectCount == 0)
                return 0;

            int candidateObjectCount = objects.Cast<ManiaDifficultyHitObject>().Sum(o => o.CompletedVibroRowObjectCount);
            var lastObject = (ManiaDifficultyHitObject)objects[^1];
            if (lastObject.VibroDuration > 0)
                candidateObjectCount += lastObject.CurrentRowNoteCount;

            return Math.Clamp(candidateObjectCount / (double)totalObjectCount, 0, 1);
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            difficultyHitObjectCache = createDifficultyHitObjects(beatmap, mods);
            vibroCandidateObjectRatio = calculateVibroCandidateObjectRatio(difficultyHitObjectCache, beatmap.HitObjects.Count);
            longestVibroDuration = difficultyHitObjectCache.Cast<ManiaDifficultyHitObject>().Select(o => o.VibroDuration).DefaultIfEmpty().Max();
            vibroPenaltyActive = applyMappingAntiAbuse && ShouldApplyVibroPenalty(vibroCandidateObjectRatio, longestVibroDuration);

            return new Skill[]
            {
                new Strain(mods, ((ManiaBeatmap)Beatmap).TotalColumns, vibroPenaltyActive ? 1 : 0)
            };
        }

        protected override Mod[] DifficultyAdjustmentMods
        {
            get
            {
                var mods = new Mod[]
                {
                    new ManiaModDoubleTime(),
                    new ManiaModHalfTime(),
                    new ManiaModEasy(),
                    new ManiaModHardRock(),
                };

                if (isForCurrentRuleset)
                    return mods;

                // if we are a convert, we can be played in any key mod.
                return mods.Concat(new Mod[]
                {
                    new ManiaModKey1(),
                    new ManiaModKey2(),
                    new ManiaModKey3(),
                    new ManiaModKey4(),
                    new ManiaModKey5(),
                    new MultiMod(new ManiaModKey5(), new ManiaModDualStages()),
                    new ManiaModKey6(),
                    new MultiMod(new ManiaModKey6(), new ManiaModDualStages()),
                    new ManiaModKey7(),
                    new MultiMod(new ManiaModKey7(), new ManiaModDualStages()),
                    new ManiaModKey8(),
                    new MultiMod(new ManiaModKey8(), new ManiaModDualStages()),
                    new ManiaModKey9(),
                    new MultiMod(new ManiaModKey9(), new ManiaModDualStages()),
                }).ToArray();
            }
        }
    }
}
