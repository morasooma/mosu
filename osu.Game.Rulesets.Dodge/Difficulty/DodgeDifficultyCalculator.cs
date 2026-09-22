// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    public class DodgeDifficultyCalculator : DifficultyCalculator
    {
        public override int Version => 2026082104;

        private IBeatmap? cachedAnalysisBeatmap;
        private DodgeDifficultyAttributes? cachedBaseAttributes;

        internal int AutoplayCalculationCount { get; private set; }
        internal int PersistentCacheHitCount { get; private set; }

        public DodgeDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            // The full-map route is the expensive part. Reuse its complete
            // no-mod result from fork.realm for both normal difficulty lookups
            // (such as beatmap cards) and every progressive live-PP point. A
            // star-only approximation makes Dodge's movement/path/reading
            // weights zero and consequently forces the HUD to show 0 PP.
            bool isProgressiveCalculation = !ReferenceEquals(beatmap, Beatmap);
            IBeatmap analysisBeatmap = isProgressiveCalculation ? Beatmap : beatmap;

            if (!ReferenceEquals(cachedAnalysisBeatmap, analysisBeatmap))
            {
                cachedAnalysisBeatmap = analysisBeatmap;
                bool loadedFromPersistentStore = tryLoadPersistedAttributes(analysisBeatmap, out cachedBaseAttributes);

                if (loadedFromPersistentStore)
                {
                    PersistentCacheHitCount++;
                }
                else
                {
                    cachedBaseAttributes = DodgeAutoplayDifficultyEvaluator.Calculate(analysisBeatmap);
                    AutoplayCalculationCount++;

                    // A gameplay cache miss should be paid for only once. Full
                    // library/background calculations are persisted by their
                    // callers, avoiding a second Realm write for every map.
                    if (isProgressiveCalculation && mods.Length == 0)
                    {
                        cachedBaseAttributes.MaxCombo = CalculateMaxCombo(analysisBeatmap);
                        ForkDataStore.Instance?.SetDodgeDifficulty(
                            analysisBeatmap.BeatmapInfo.ID,
                            analysisBeatmap.BeatmapInfo.MD5Hash,
                            cachedBaseAttributes,
                            Version);
                    }
                }
            }

            DodgeDifficultyAttributes attributes = clone(cachedBaseAttributes!);

            if (isProgressiveCalculation && analysisBeatmap.HitObjects.Count > 0)
            {
                double progress = Math.Clamp((double)beatmap.HitObjects.Count / analysisBeatmap.HitObjects.Count, 0, 1);
                double cumulativeDifficulty = Math.Sqrt(progress);
                attributes.StarRating *= cumulativeDifficulty;
                attributes.MovementDifficulty *= cumulativeDifficulty;
                attributes.PressureDifficulty *= cumulativeDifficulty;
                attributes.PathDifficulty *= cumulativeDifficulty;
                attributes.ReadingDifficulty *= cumulativeDifficulty;
                // These are accumulated quantities rather than peak difficulty.
                // Scaling them linearly keeps live miss, endurance and length
                // factors representative of the portion played so far.
                attributes.DifficultSectionCount *= progress;
                attributes.RelevantPatternCount = (int)Math.Ceiling(attributes.RelevantPatternCount * progress);
                attributes.EffectiveReadingPatternCount *= progress;
            }

            double clockRate = mods.OfType<IApplicableToRate>()
                                   .Aggregate(1.0, (rate, mod) => mod.ApplyToRate(0, rate));

            if (double.IsFinite(clockRate) && clockRate > 0)
            {
                // Keep this exponent in sync with ModMosuTargetDifficulty.
                // Dodge's calibrated star rating scales approximately by rate^1.35.
                double rateDifficultyMultiplier = Math.Pow(clockRate, 1.35);
                attributes.StarRating = Math.Clamp(attributes.StarRating * rateDifficultyMultiplier, 0, 15);
                attributes.MovementDifficulty *= rateDifficultyMultiplier;
                attributes.PressureDifficulty *= rateDifficultyMultiplier;
                attributes.PathDifficulty *= rateDifficultyMultiplier;
                attributes.ReadingDifficulty *= rateDifficultyMultiplier;
                attributes.ProjectileRate *= clockRate;
            }

            attributes.Mods = mods;
            attributes.MaxCombo = CalculateMaxCombo(beatmap);
            return attributes;
        }

        internal static int CalculateMaxCombo(IBeatmap beatmap)
            => beatmap.HitObjects.Sum(hitObject => hitObject switch
            {
                DodgeEmitter emitter => emitter.EffectiveBurstCount,
                DodgeBullet or DodgeBeam => 1,
                _ => 0,
            });

        private bool tryLoadPersistedAttributes(IBeatmap beatmap, out DodgeDifficultyAttributes? attributes)
        {
            attributes = null;
            ForkDataStore? store = ForkDataStore.Instance;
            string checksum = beatmap.BeatmapInfo.MD5Hash;

            if (store == null || !store.HasDodgeDifficultyAttributes(beatmap.BeatmapInfo.ID, Version, checksum))
                return false;

            ForkDataStore.DodgeDifficultyData persisted = store.GetDodgeDifficulty(beatmap.BeatmapInfo.ID);

            try
            {
                attributes = JsonConvert.DeserializeObject<DodgeDifficultyAttributes>(persisted.AttributesJson);
            }
            catch (JsonException)
            {
                return false;
            }

            return attributes != null
                   && finiteNonNegative(attributes.StarRating)
                   && finiteNonNegative(attributes.MovementDifficulty)
                   && finiteNonNegative(attributes.PathDifficulty)
                   && finiteNonNegative(attributes.ReadingDifficulty)
                   && finiteNonNegative(attributes.DifficultSectionCount)
                   && finiteNonNegative(attributes.EffectiveReadingPatternCount)
                   && (attributes.StarRating <= 0.15
                       || attributes.MovementDifficulty + attributes.PathDifficulty + attributes.ReadingDifficulty > 0);
        }

        private static bool finiteNonNegative(double value) => double.IsFinite(value) && value >= 0;

        private static DodgeDifficultyAttributes clone(DodgeDifficultyAttributes source) => new DodgeDifficultyAttributes
        {
            StarRating = source.StarRating,
            MovementDifficulty = source.MovementDifficulty,
            PressureDifficulty = source.PressureDifficulty,
            PathDifficulty = source.PathDifficulty,
            ReadingDifficulty = source.ReadingDifficulty,
            DifficultSectionCount = source.DifficultSectionCount,
            RelevantPatternCount = source.RelevantPatternCount,
            EffectiveReadingPatternCount = source.EffectiveReadingPatternCount,
            ProjectileRate = source.ProjectileRate,
            PeakActiveProjectiles = source.PeakActiveProjectiles,
            PeakConcurrentPatterns = source.PeakConcurrentPatterns,
            AutoplayTeleports = source.AutoplayTeleports,
            AutoplayCollisions = source.AutoplayCollisions,
            IsUnclearable = source.IsUnclearable,
        };

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
            => Enumerable.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods) => Array.Empty<Skill>();
    }
}
