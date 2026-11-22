using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class TazikPpLiveCounterTest
    {
        [TestCase(ForkRelaxPpSystem.TazikPp)]
        [TestCase(ForkRelaxPpSystem.MosuPpPrerelease)]
        public void BackgroundCounterMatchesFullEvidenceThroughMissesAndRewind(ForkRelaxPpSystem system)
        {
            ForkRelaxPpSystem previous = RelaxPpSystemSelection.Current;
            RelaxPpSystemSelection.Current = system;
            try
            {
                var ruleset = new OsuRuleset();
                Mod[] mods = { new OsuModRelax(), new OsuModDoubleTime(), new OsuModDifficultyAdjust { CircleSize = { Value = 7 } } };
                var map = new Beatmap<OsuHitObject>
                {
                    HitObjects = Enumerable.Range(0, 100).Select(i => (OsuHitObject)new HitCircle
                    {
                        StartTime = i * 140,
                        Position = new Vector2(80 + i % 3 * 150, 80 + i % 2 * 150)
                    }).ToList()
                };
                var working = new FlatWorkingBeatmap(map);
                var prepared = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
                var state = new GameplayState(prepared, ruleset, mods);
                var processor = ruleset.CreateScoreProcessor();
                processor.ApplyBeatmap(prepared);
                using var counter = new TestCounter();
                Type type = typeof(PerformancePointsCounter);
                void set(string name, object value) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, value);
                type.GetProperty("gameplayState", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, state);
                type.GetProperty("scoreProcessor", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, processor);
                set("scoreInfo", new ScoreInfo(prepared.BeatmapInfo, ruleset.RulesetInfo) { Mods = mods });
                set("performanceCalculator", new OsuPerformanceCalculator());
                var timed = new OsuDifficultyCalculator(ruleset.RulesetInfo, working).CalculateTimed(mods);
                // Exercise the HUD's gameplay-beatmap wrapper separately from the regular working beatmap.
                var wrapperType = type.GetNestedType("GameplayWorkingBeatmap", BindingFlags.NonPublic)!;
                var wrapper = (IWorkingBeatmap)Activator.CreateInstance(wrapperType, prepared, mods, working)!;
                var gameplayTimed = new OsuDifficultyCalculator(ruleset.RulesetInfo, wrapper).CalculateTimed(mods);
                set("timedAttributes", gameplayTimed);

                // DT's rate lives on the mod rather than the beatmap, so also use DA here to
                // ensure the reference really does use the original circle size.
                var rxOnly = wrapper.GetPlayableBeatmap(ruleset.RulesetInfo, new Mod[] { new OsuModRelax() });
                Assert.That(rxOnly.Difficulty.CircleSize, Is.EqualTo(working.Beatmap.Difficulty.CircleSize));
                Assert.That(rxOnly.Difficulty.CircleSize, Is.Not.EqualTo(prepared.Difficulty.CircleSize));
                var changed = type.GetMethod("onJudgementChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var results = prepared.HitObjects.Select((obj, i) => new OsuHitCircleJudgementResult(obj, new OsuJudgement())
                {
                    Type = i % 13 == 0 ? HitResult.Miss : HitResult.Great,
                    CursorPositionAtHit = ((OsuHitObject)obj).StackedPosition + new Vector2((float)((OsuHitObject)obj).Radius + 1, 0)
                }).ToArray();
                for (int i = 0; i < 70; i++)
                {
                    processor.ApplyResult(results[i]);
                    if (i % 10 == 0 || i == 69)
                        check(i, false);
                }
                for (int i = 69; i >= 50; i--)
                {
                    processor.RevertResult(results[i]);
                    check(i, true);
                }
                for (int i = 50; i < 100; i++)
                {
                    processor.ApplyResult(results[i]);
                    if (i % 10 == 0 || i == 99)
                        check(i, false);
                }

                void check(int index, bool reverting)
                {
                    // Exercise the real async/background queue while bypassing only the wall-clock throttle.
                    set("lastLivePerformanceUpdateTime", double.NegativeInfinity);
                    counter.IsValid = false;
                    changed.Invoke(counter, new object[] { results[index], reverting });
                    Assert.That(SpinWait.SpinUntil(() => { counter.Pump(); return counter.IsValid; }, TimeSpan.FromSeconds(5)), Is.True);
                    var score = new ScoreInfo(prepared.BeatmapInfo, ruleset.RulesetInfo) { Mods = mods };
                    processor.PopulateScore(score);
                    int attributeIndex = timed.ToList().FindLastIndex(a => a.Time <= results[index].HitObject.StartTime);
                    var attributes = timed[Math.Max(0, attributeIndex)].Attributes;
                    var expected = new OsuPerformanceCalculator().Calculate(score, attributes);
                    Assert.That(counter.Current.Value, Is.EqualTo((int)Math.Round(expected.Total, MidpointRounding.AwayFromZero)),
                        $"judgement {index}, reverting={reverting}");
                }
            }
            finally
            {
                RelaxPpSystemSelection.Current = previous;
            }
        }

        private partial class TestCounter : PerformancePointsCounter
        {
            public void Pump() => Scheduler.Update();
        }
    }
}
