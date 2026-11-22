using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class CalculatedBeatmapReuseTest
    {
        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void TestMixedObjectsAndPpMatchFreshConversion(int mode, bool relax)
        {
            Ruleset ruleset = mode switch
            {
                1 => new TaikoRuleset(),
                2 => new CatchRuleset(),
                3 => new ManiaRuleset(),
                _ => new OsuRuleset(),
            };
            var text = new StringBuilder("osu file format v14\n[General]\nMode:0\n[Difficulty]\nHPDrainRate:5\nCircleSize:4\nOverallDifficulty:5\nApproachRate:5\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n");
            for (int i = 0; i < 100; i++)
                text.AppendLine($"{100 + i % 5 * 60},{100 + i % 3 * 60},{i * 500},1,0,0:0:0:0:");
            text.AppendLine("128,192,51000,2,0,B|256:192,2,140\n256,192,54000,8,0,56000,0:0:0:0:");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
            using var reader = new LineBufferedReader(stream);
            var source = new LegacyBeatmapDecoder().Decode(reader);
            var working = new TestWorkingBeatmap(source);
            Mod[] mods = relax ? new[] { ruleset.GetModsFor(ModType.Automation).OfType<ModRelax>().First() } : Array.Empty<Mod>();
            var calculator = ruleset.CreateDifficultyCalculator(working);
            var attributes = calculator.Calculate(mods);
            var reused = ruleset.CreateScoreProcessor();
            reused.Mods.Value = mods;
            reused.ApplyBeatmap(calculator.CalculatedBeatmap);
            var fresh = ruleset.CreateScoreProcessor();
            fresh.Mods.Value = mods;
            fresh.ApplyBeatmap(working.GetPlayableBeatmap(ruleset.RulesetInfo, mods));

            Assert.Multiple(() =>
            {
                Assert.That(reused.MaximumCombo, Is.EqualTo(fresh.MaximumCombo));
                Assert.That(reused.MaximumTotalScore, Is.EqualTo(fresh.MaximumTotalScore));
                Assert.That(reused.MaximumStatistics, Is.EquivalentTo(fresh.MaximumStatistics));
            });

            ScoreInfo score(int combo, long total, System.Collections.Generic.Dictionary<osu.Game.Rulesets.Scoring.HitResult, int> statistics) => new ScoreInfo(source.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true, Accuracy = 1, Mods = mods, MaxCombo = combo, Combo = combo, TotalScore = total,
                Statistics = statistics, MaximumStatistics = statistics,
            };
            var performance = ruleset.CreatePerformanceCalculator();
            if (performance != null)
            {
                double expected = performance.Calculate(score(fresh.MaximumCombo, fresh.MaximumTotalScore, fresh.MaximumStatistics), attributes).Total;
                double actual = performance.Calculate(score(reused.MaximumCombo, reused.MaximumTotalScore, reused.MaximumStatistics), attributes).Total;
                Assert.That(actual, Is.EqualTo(expected).Within(0.000001));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPerfectScoreMatchesFreshConversion(bool relax)
        {
            var ruleset = new OsuRuleset();
            var source = new Beatmap { BeatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata()) };
            for (int i = 0; i < 100; i++)
                source.HitObjects.Add(new HitCircle { StartTime = i * 200, Position = new Vector2(100 + i % 5 * 60, 100 + i % 3 * 60) });

            var working = new TestWorkingBeatmap(source);
            var calculator = ruleset.CreateDifficultyCalculator(working);
            Mod[] mods = relax ? new[] { ruleset.GetModsFor(ModType.Automation).OfType<ModRelax>().First() } : Array.Empty<Mod>();
            calculator.Calculate(mods);
            var reused = calculator.CalculatedBeatmap;
            var fresh = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
            var reusedProcessor = ruleset.CreateScoreProcessor();
            reusedProcessor.Mods.Value = mods;
            reusedProcessor.ApplyBeatmap(reused);
            var freshProcessor = ruleset.CreateScoreProcessor();
            freshProcessor.Mods.Value = mods;
            freshProcessor.ApplyBeatmap(fresh);

            Assert.That(reusedProcessor.MaximumCombo, Is.EqualTo(freshProcessor.MaximumCombo));
            Assert.That(reusedProcessor.MaximumTotalScore, Is.EqualTo(freshProcessor.MaximumTotalScore));
            Assert.That(reusedProcessor.MaximumStatistics, Is.EquivalentTo(freshProcessor.MaximumStatistics));
            Assert.That(reused.HitObjects.Count, Is.EqualTo(fresh.HitObjects.Count));
        }
    }
}
