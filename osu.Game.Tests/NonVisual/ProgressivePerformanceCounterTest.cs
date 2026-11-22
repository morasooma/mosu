using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ProgressivePerformanceCounterTest
    {
        [Test]
        public void TestActualCounterLoadUsesOnDemandWorkerAndPublishesAfterRewind()
        {
            var ruleset = new OsuRuleset();
            var map = new Beatmap<OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 180).Select(i => (OsuHitObject)new HitCircle
                {
                    StartTime = i * 150,
                    Position = new Vector2(100 + i % 3 * 150, 100 + i % 2 * 150)
                }).ToList()
            };
            var prepared = new FlatWorkingBeatmap(map).GetPlayableBeatmap(ruleset.RulesetInfo);
            var state = new GameplayState(prepared, ruleset);
            var processor = ruleset.CreateScoreProcessor();
            processor.ApplyBeatmap(prepared);
            using var counter = new TestCounter();
            Type type = typeof(PerformancePointsCounter);
            type.GetProperty("gameplayState", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, state);
            type.GetProperty("scoreProcessor", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, processor);
            // No difficultyCache supplied: ordinary HUD must not request the all-prefix table.
            type.GetMethod("load", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(counter, new object?[] { null });
            Assert.That(type.GetField("progressivePerformance", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(counter), Is.Not.Null);
            Assert.That(type.GetField("timedAttributes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(counter), Is.Null);
            var attributes = new OsuDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(map)).CalculateTimed();
            foreach (int index in new[] { 30, 90, 10, 179 })
            {
                var judgement = new OsuJudgementResult(prepared.HitObjects[index], new OsuJudgement()) { Type = HitResult.Great };
                counter.IsValid = false;
                type.GetMethod("onJudgementChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(counter, new object[] { judgement, index == 10 });
                Assert.That(SpinWait.SpinUntil(() => { counter.Pump(); return counter.IsValid; }, TimeSpan.FromSeconds(5)), Is.True);
                var score = new ScoreInfo();
                processor.PopulateScore(score);
                int expected = (int)Math.Round(new OsuPerformanceCalculator().Calculate(score, attributes[index].Attributes).Total, MidpointRounding.AwayFromZero);
                Assert.That(counter.Current.Value, Is.EqualTo(expected));
            }
        }

        [Test]
        public void TestActualScoringWithMissesAndTwentyJudgementReverts()
        {
            var ruleset = new OsuRuleset();
            var map = new Beatmap<OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 150).Select(i => (OsuHitObject)new HitCircle
                {
                    StartTime = i * 130,
                    Position = new Vector2(80 + i % 3 * 140, 80 + i % 2 * 150)
                }).ToList()
            };
            var prepared = new FlatWorkingBeatmap(map).GetPlayableBeatmap(ruleset.RulesetInfo);
            var state = new GameplayState(prepared, ruleset);
            var processor = ruleset.CreateScoreProcessor();
            processor.ApplyBeatmap(prepared);
            using var counter = new TestCounter();
            Type type = typeof(PerformancePointsCounter);
            type.GetProperty("gameplayState", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, state);
            type.GetProperty("scoreProcessor", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, processor);
            type.GetMethod("load", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(counter, new object?[] { null });
            var changed = type.GetMethod("onJudgementChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var timed = new OsuDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(map)).CalculateTimed();
            var results = prepared.HitObjects.Select((obj, i) => new OsuJudgementResult(obj, new OsuJudgement())
            {
                Type = i % 19 == 0 ? HitResult.Miss : i % 7 == 0 ? HitResult.Ok : HitResult.Great
            }).ToArray();
            var reference = new OsuPerformanceCalculator();
            for (int i = 0; i < 120; i++)
            {
                processor.ApplyResult(results[i]);
                check(i, false);
            }
            for (int i = 119; i >= 100; i--)
            {
                processor.RevertResult(results[i]);
                check(i, true);
            }
            for (int i = 100; i < 150; i++)
            {
                processor.ApplyResult(results[i]);
                check(i, false);
            }
            Assert.That(counter.Current.Value, Is.GreaterThan(0), "non-zero real score must be verified");

            void check(int index, bool reverting)
            {
                counter.IsValid = false;
                changed.Invoke(counter, new object[] { results[index], reverting });
                Assert.That(SpinWait.SpinUntil(() => { counter.Pump(); return counter.IsValid; }, TimeSpan.FromSeconds(5)), Is.True);
                var score = new ScoreInfo(prepared.BeatmapInfo, ruleset.RulesetInfo) { Mods = Array.Empty<osu.Game.Rulesets.Mods.Mod>() };
                processor.PopulateScore(score);
                int expected = (int)Math.Round(reference.Calculate(score, timed[index].Attributes).Total, MidpointRounding.AwayFromZero);
                Assert.That(counter.Current.Value, Is.EqualTo(expected), $"judgement {index}, reverting={reverting}");
            }
        }

        private partial class TestCounter : PerformancePointsCounter
        {
            public void Pump() => Scheduler.Update();
        }
    }
}
