using System;
using System.Linq;
using osu.Game.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Catch.Objects
{
    public static class FruitNinjaPatternGenerator
    {
        public static void Apply(Beatmap<CatchHitObject> beatmap)
        {
            var fruits = beatmap.HitObjects.OfType<FruitNinjaFruit>().Where(f => !f.IsBomb).ToArray();
            int start = 0;
            int pattern = 0;
            while (start < fruits.Length)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(fruits[start].StartTime).BeatLength;
                double maxGap = Math.Clamp(beatLength / 2, 90, 240);
                double maxSpan = Math.Clamp(beatLength * 0.75, 180, 400);
                int end = start + 1;
                while (end < fruits.Length && end - start < 8
                       && fruits[end].ComboIndex == fruits[start].ComboIndex
                       && fruits[end].StartTime - fruits[end - 1].StartTime <= maxGap
                       && fruits[end].StartTime - fruits[start].StartTime <= maxSpan)
                    end++;

                var group = fruits[start..end];
                float left = group.Min(f => f.EffectiveX);
                float right = group.Max(f => f.EffectiveX);
                float centre = Math.Clamp((left + right) / 2, 148, 364);
                float compression = Math.Min(1, 240 / Math.Max(1, right - left));
                float direction = group[^1].EffectiveX - group[0].EffectiveX;
                if (Math.Abs(direction) < 8)
                    direction = pattern % 2 == 0 ? 1 : -1;
                direction = Math.Sign(direction);

                // Keep each source object's AR-based approach duration. Phrase grouping
                // shapes the swipe direction, but must not delay later notes' launches.
                for (int i = 0; i < group.Length; i++)
                {
                    var fruit = group[i];
                    float x = right - left < 32 && group.Length > 1
                        ? centre + direction * (i / (float)(group.Length - 1) - 0.5f) * 112
                        : centre + (fruit.EffectiveX - (left + right) / 2) * compression;
                    fruit.SlicePosition = new Vector2(x, FruitNinjaFruit.SLICE_Y);
                    fruit.ThrowDrift = direction * 38;
                    fruit.PatternIndex = pattern;
                }
                pattern++;
                start = end;
            }

            foreach (var bomb in beatmap.HitObjects.OfType<FruitNinjaFruit>().Where(f => f.IsBomb))
            {
                bomb.SlicePosition = new Vector2(bomb.EffectiveX, FruitNinjaFruit.SLICE_Y + 80);
                bomb.ThrowDrift = bomb.EffectiveX < 256 ? -28 : 28;
            }
        }
    }
}
