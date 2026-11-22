using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class AimSharedEvaluationTest
    {
        [Test]
        public void TestSharedAndIndependentEvaluatorsMatchAcrossAllEmbeddedMaps()
        {
            var assembly = typeof(AimSharedEvaluationTest).Assembly;
            int cases = 0;
            foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".osu", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new LineBufferedReader(stream);
                var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                foreach (Mod[] mods in new[]
                         {
                             Array.Empty<Mod>(), new Mod[] { new OsuModHidden(), new OsuModFlashlight() },
                             new Mod[] { new OsuModDoubleTime() }, new Mod[] { new OsuModHardRock(), new OsuModHidden() },
                             new Mod[] { new OsuModTouchDevice() }, new Mod[] { new OsuModAutopilot() },
                             new Mod[] { new OsuModRelax() }, new Mod[] { new OsuModMagnetised() }
                         })
                {
                    var working = new FlatWorkingBeatmap(decoded);
                    var ruleset = new OsuRuleset().RulesetInfo;
                    var expected = new IndependentCalculator(ruleset, working).Calculate(mods);
                    var actual = new OsuDifficultyCalculator(ruleset, working).Calculate(mods);
                    assertEqual(expected, actual);
                    cases++;
                }
            }
            TestContext.Out.WriteLine($"Shared Aim exact parity: {cases} map/mod cases");
            Assert.That(cases, Is.GreaterThanOrEqualTo(200));
        }

        [Test]
        public void TestEveryTimedPrefixMatchesIndependentSkills()
        {
            using var stream = typeof(AimSharedEvaluationTest).Assembly.GetManifestResourceStream(
                "osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.1124896.osu")!;
            using var reader = new LineBufferedReader(stream);
            var working = new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
            var ruleset = new OsuRuleset().RulesetInfo;
            foreach (Mod[] mods in new[] { Array.Empty<Mod>(), new Mod[] { new OsuModHidden(), new OsuModFlashlight() }, new Mod[] { new OsuModRelax() } })
            {
                var expected = new IndependentCalculator(ruleset, working).CalculateTimed(mods);
                var actual = new OsuDifficultyCalculator(ruleset, working).CalculateTimed(mods);
                Assert.That(actual.Count, Is.EqualTo(expected.Count));
                for (int i = 0; i < expected.Count; i++)
                    assertEqual(expected[i].Attributes, actual[i].Attributes);
            }
        }

        private static void assertEqual(DifficultyAttributes expected, DifficultyAttributes actual)
        {
            foreach (var property in typeof(OsuDifficultyAttributes).GetProperties())
            {
                if (property.PropertyType == typeof(double))
                    Assert.That(BitConverter.DoubleToInt64Bits((double)property.GetValue(actual)!), Is.EqualTo(BitConverter.DoubleToInt64Bits((double)property.GetValue(expected)!)), property.Name);
                else if (property.PropertyType == typeof(int))
                    Assert.That(property.GetValue(actual), Is.EqualTo(property.GetValue(expected)), property.Name);
            }
        }

        private class IndependentCalculator : OsuDifficultyCalculator
        {
            public IndependentCalculator(IRulesetInfo ruleset, IWorkingBeatmap working) : base(ruleset, working) { }
            protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
            {
                var result = base.CreateSkills(beatmap, mods);
                result[1] = new Aim(mods, false, !WorkingBeatmap.BeatmapInfo.GetOnlineStatus().BypassesMappingAntiAbuse());
                return result;
            }
        }
    }
}
