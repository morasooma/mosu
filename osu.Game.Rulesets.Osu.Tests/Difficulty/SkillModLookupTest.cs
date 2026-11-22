using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class SkillModLookupTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestFlashlightRetainsLiveConfiguredValues(bool enabled)
        {
            var hidden = new OsuModHidden();
            var otherHidden = new OsuModHidden();
            var magnetised = new OsuModMagnetised();
            var deflate = new OsuModDeflate();
            Mod[] mods = new Mod[] { hidden, otherHidden, magnetised, deflate, new OsuModTouchDevice(), new OsuModAutopilot() }
                .Concat(enabled ? new Mod[] { new OsuModFlashlight() } : Array.Empty<Mod>()).ToArray();
            var prepared = new List<DifficultyHitObject>();
            var objects = new List<OsuHitObject>();
            var difficulty = new BeatmapDifficulty { CircleSize = 4, OverallDifficulty = 8, ApproachRate = 9 };
            for (int i = 0; i < 80; i++)
            {
                OsuHitObject obj = i % 19 == 0 ? new Spinner { Duration = 100 } : new HitCircle();
                obj.StartTime = i * 125;
                obj.Position = new Vector2(100 + i % 4 * 100, 100 + i % 3 * 90);
                obj.ApplyDefaults(new ControlPointInfo(), difficulty);
                objects.Add(obj);
                if (i > 0) prepared.Add(new OsuDifficultyHitObject(obj, objects[i - 1], 1, prepared, i - 1));
            }
            var skill = new Flashlight(mods, objects.Count);
            double strain = 0;
            foreach (OsuDifficultyHitObject obj in prepared)
            {
                hidden.OnlyFadeApproachCircles.Value = obj.Index % 3 != 0;
                otherHidden.OnlyFadeApproachCircles.Value = obj.Index % 5 != 0;
                magnetised.AttractionStrength.Value = obj.Index % 10 / 10f;
                deflate.StartScale.Value = 1 + obj.Index % 4;

                // The public evaluator independently resolves current mod settings.
                // The skill's cached identities must still read their live values.
                if (enabled)
                {
                    double value = FlashlightEvaluator.EvaluateDifficultyOf(obj, mods);
                    value = DiffUtils.Pow(value, 0.9);
                    value *= 1.0 - magnetised.AttractionStrength.Value;
                    value *= Math.Clamp(DiffUtils.ReverseLerp(deflate.StartScale.Value, 11, 1), 0.1, 1);
                    value *= 0.4;
                    value *= 0.985 + DiffUtils.Pow(Math.Max(0, obj.OverallDifficulty), 2) / 4000;
                    strain *= DiffUtils.Pow(0.15, obj.DeltaTime / 1000);
                    strain += value * 0.058;
                }
                skill.Process(obj);
                Assert.That(skill.GetObjectDifficulties()[^1], Is.EqualTo(strain), $"object {obj.Index}");
            }
        }
    }
}
