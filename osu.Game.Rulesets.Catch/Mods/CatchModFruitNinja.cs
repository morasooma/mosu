using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Catch.Mods
{
    public class CatchModFruitNinja : Mod, IApplicableToBeatmap, IApplicableHealthProcessor
    {
        public override string Name => "Fruity Slash";
        public override string Acronym => "FNI";
        public override ModType Type => ModType.Mosu;
        public override IconUsage? Icon => FontAwesome.Solid.Cut;
        public override LocalisableString Description => global::osu.Game.Localisation.ClientTranslationStrings.J3620;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;

        public HealthProcessor CreateHealthProcessor(double drainStartTime) => new FruitNinjaHealthProcessor();
        public override bool ValidForMultiplayerAsFreeMod => false;
        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay), typeof(ModCinema), typeof(ModRelax), typeof(CatchModFloatingFruits),
            typeof(ModHidden), typeof(ModFlashlight), typeof(CatchModNoScope), typeof(CatchModMovingFast), typeof(ModClassic),
            typeof(ModSegmentRate)
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            // Defaults and Catch post-processing have already generated slider droplets and bananas.
            // Flatten only here, preserving every playable object, including tiny droplets.
            var converted = new List<HitObject>();
            int index = 0;

            foreach (var source in beatmap.HitObjects.Cast<CatchHitObject>())
            {
                var fruits = flatten(source).ToArray();
                foreach (var fruit in fruits)
                    converted.Add(createFruit(fruit, false, index++, beatmap.Difficulty));

                if (source.NewCombo && fruits.Length > 0)
                {
                    var bomb = createFruit(fruits[0], true, index++, beatmap.Difficulty);
                    // Launch on the opposite side, not inside the mandatory fruit.
                    bomb.X = fruits[0].EffectiveX < 256 ? 440 : 72;
                    converted.Add(bomb);
                }
            }

            var catchBeatmap = (Beatmap<CatchHitObject>)beatmap;
            catchBeatmap.HitObjects = converted.Cast<CatchHitObject>().OrderBy(h => h.StartTime).ToList();
            FruitNinjaPatternGenerator.Apply(catchBeatmap);
        }

        private static IEnumerable<PalpableCatchHitObject> flatten(CatchHitObject source)
        {
            if (source is PalpableCatchHitObject fruit)
                yield return fruit;

            foreach (var nested in source.NestedHitObjects.Cast<CatchHitObject>())
            foreach (var child in flatten(nested))
                yield return child;
        }

        private static FruitNinjaFruit createFruit(PalpableCatchHitObject source, bool bomb, int index, IBeatmapDifficultyInfo difficulty)
        {
            var windows = new FruitNinjaHitWindows();
            windows.SetDifficulty(difficulty.OverallDifficulty);
            return new FruitNinjaFruit
            {
                StartTime = source.StartTime,
                SourceObject = source,
                X = source.EffectiveX,
                IsBomb = bomb,
                Scale = Math.Clamp(source.Scale, 0.28f, 0.42f),
                IndexInBeatmap = index,
                ComboIndex = source.ComboIndex,
                ComboIndexWithOffsets = source.ComboIndexWithOffsets,
                Samples = bomb ? new() : source.Samples.ToList(),
                TimePreempt = source.TimePreempt,
                HitWindows = windows,
            };
        }
    }
}
