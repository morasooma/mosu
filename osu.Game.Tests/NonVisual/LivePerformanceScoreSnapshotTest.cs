using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LivePerformanceScoreSnapshotTest
    {
        [Test]
        public void TestIndependentDynamicStateAndPrivateConfiguration()
        {
            var dt = new OsuModDoubleTime();
            var source = new ScoreInfo(new BeatmapInfo(), new OsuRuleset().RulesetInfo)
            {
                Mods = new Mod[] { dt }, Combo = 10, MaxCombo = 20, Accuracy = .98,
                TotalScore = 123, TotalScoreWithoutMods = 100, LegacyTotalScore = 110, IsLegacyScore = true,
                Rank = ScoreRank.A
            };
            source.Statistics[HitResult.Great] = 9;
            source.MaximumStatistics[HitResult.Great] = 10;
            var template = new LivePerformanceScoreSnapshot(source);
            var first = template.Create(source);
            source.Statistics[HitResult.Great] = 50;
            source.MaximumStatistics[HitResult.Great] = 60;
            source.Combo = 30;
            dt.SpeedChange.Value = 1.7;
            var second = template.Create(source);
            Assert.That(first.Statistics[HitResult.Great], Is.EqualTo(9));
            Assert.That(first.MaximumStatistics[HitResult.Great], Is.EqualTo(10));
            Assert.That(first.Combo, Is.EqualTo(10));
            Assert.That(second.Combo, Is.EqualTo(30));
            Assert.That(second.Statistics[HitResult.Great], Is.EqualTo(50));
            Assert.That(first.Mods[0], Is.Not.SameAs(dt));
            Assert.That(((OsuModDoubleTime)first.Mods[0]).SpeedChange.Value, Is.EqualTo(1.5));
            Assert.That(first.HitEvents, Is.Empty);
            Assert.That(second.HitEvents, Is.Empty);
            Assert.That(first.BeatmapInfo, Is.Not.SameAs(source.BeatmapInfo));
            Assert.That(first.Ruleset, Is.Not.SameAs(source.Ruleset));
            Assert.That(first.Accuracy, Is.EqualTo(source.Accuracy));
            Assert.That(first.Rank, Is.EqualTo(source.Rank));
            Assert.That(first.TotalScore, Is.EqualTo(123));
            Assert.That(first.TotalScoreWithoutMods, Is.EqualTo(100));
            Assert.That(first.LegacyTotalScore, Is.EqualTo(110));
            Assert.That(first.IsLegacyScore, Is.True);
        }

        [Test]
        public void TestAllNumericPerformanceFieldsMatchOriginalSnapshot()
        {
            var map = new Beatmap<osu.Game.Rulesets.Osu.Objects.OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 120).Select(i => (osu.Game.Rulesets.Osu.Objects.OsuHitObject)new osu.Game.Rulesets.Osu.Objects.HitCircle
                {
                    StartTime = i * 130,
                    Position = new osuTK.Vector2(80 + i % 3 * 140, 80 + i % 2 * 150)
                }).ToList()
            };
            var original = typeof(PerformancePointsCounter).GetMethod("createScoreSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!
                                                         .CreateDelegate<Func<ScoreInfo, ScoreInfo>>();
            foreach (Mod[] mods in new[]
                     {
                         Array.Empty<Mod>(), new Mod[] { new OsuModHidden(), new OsuModFlashlight() },
                         new Mod[] { new OsuModHardRock(), new OsuModDoubleTime() },
                         new Mod[] { new OsuModClassic(), new OsuModNoFail() },
                         new Mod[] { new OsuModEasy(), new ModScoreV2() },
                         new Mod[] { new OsuModDaycore() }, new Mod[] { new OsuModNightcore(), new OsuModSpunOut() }
                     })
            {
                var ruleset = new OsuRuleset().RulesetInfo;
                var attributes = new OsuDifficultyCalculator(ruleset, new FlatWorkingBeatmap(map)).Calculate(mods);
                var source = new ScoreInfo(map.BeatmapInfo, ruleset) { Mods = mods };
                var template = new LivePerformanceScoreSnapshot(source);
                var oldCalculator = new OsuPerformanceCalculator();
                var newCalculator = new OsuPerformanceCalculator();
                for (int i = 0; i < 100; i++)
                {
                    source.Combo = i;
                    source.MaxCombo = i + 10;
                    source.Accuracy = .7 + .003 * i;
                    source.TotalScore = 10000 + i * 1000;
                    source.TotalScoreWithoutMods = 9000 + i * 1000;
                    source.LegacyTotalScore = source.TotalScore;
                    source.Statistics = new Dictionary<HitResult, int>
                    {
                        [HitResult.Great] = i, [HitResult.Ok] = i % 5, [HitResult.Meh] = i % 3, [HitResult.Miss] = i % 7
                    };
                    var expected = oldCalculator.Calculate(original(source), attributes);
                    var actual = newCalculator.Calculate(template.Create(source), attributes);
                    foreach (var property in typeof(OsuPerformanceAttributes).GetProperties().Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(double?)))
                    {
                        object? a = property.GetValue(expected);
                        object? b = property.GetValue(actual);
                        Assert.That(a == null ? b == null : b is double value && BitConverter.DoubleToInt64Bits((double)a) == BitConverter.DoubleToInt64Bits(value), Is.True, property.Name);
                    }
                }
            }
        }
    }
}
