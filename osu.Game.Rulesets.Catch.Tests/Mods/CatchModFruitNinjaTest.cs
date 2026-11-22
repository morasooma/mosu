using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Catch.Judgements;
using osu.Game.Rulesets.Catch.Scoring;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Catch.Tests.Mods
{
    [TestFixture]
    public class CatchModFruitNinjaTest
    {
        [Test]
        public void TestRegistrationAndRulesetIsolation()
        {
            var ruleset = new CatchRuleset();
            var mod = ruleset.GetModsFor(ModType.Mosu).OfType<CatchModFruitNinja>().Single();
            Assert.That(mod.HasImplementation, Is.True);
            Assert.That(mod.Ranked, Is.False);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(null, new Mod[] { mod }), Is.False);
            Assert.That(mod.ValidForMultiplayer, Is.False);
            Assert.That(ruleset.AllMods.GroupBy(m => m.Acronym).Where(g => g.Count() > 1), Is.Empty);
            Assert.That(ruleset.CreateModFromAcronym("FN"), Is.TypeOf<CatchModFlatline>());
            Assert.That(ruleset.CreateModFromAcronym("FNI"), Is.TypeOf<CatchModFruitNinja>());
            using var ordinary = ruleset.CreateDrawableRulesetWith(new Beatmap<CatchHitObject>());
            using var ninja = ruleset.CreateDrawableRulesetWith(new Beatmap<CatchHitObject>(), new Mod[] { mod });
            Assert.That(ordinary, Is.TypeOf<DrawableCatchRuleset>());
            Assert.That(ninja, Is.TypeOf<DrawableFruitNinjaRuleset>());
        }

        [Test]
        public void TestEveryDropletAndBananaBecomesFruit()
        {
            var beatmap = new Beatmap<CatchHitObject>
            {
                HitObjects = new List<CatchHitObject>
                {
                    new Fruit { StartTime = 1000, X = 100, NewCombo = true },
                    new JuiceStream
                    {
                        StartTime = 2000,
                        X = 200,
                        NewCombo = true,
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(250, 0) }),
                    },
                    new BananaShower { StartTime = 5000, Duration = 1000 },
                },
            };
            var processor = new CatchBeatmapProcessor(beatmap);
            processor.PreProcess();
            foreach (var h in beatmap.HitObjects)
                h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            processor.PostProcess();

            var original = beatmap.HitObjects.SelectMany(h => h is PalpableCatchHitObject ? new HitObject[] { h } : h.NestedHitObjects.AsEnumerable())
                                  .OfType<PalpableCatchHitObject>().ToArray();
            Assert.That(original.OfType<TinyDroplet>().Any(), Is.True);
            Assert.That(original.OfType<Droplet>().Any(), Is.True);
            Assert.That(original.OfType<Banana>().Any(), Is.True);
            int bombs = beatmap.HitObjects.Count(h => h.NewCombo && h is not BananaShower);

            new CatchModFruitNinja().ApplyToBeatmap(beatmap);

            var fruits = beatmap.HitObjects.Cast<FruitNinjaFruit>().ToArray();
            Assert.That(fruits.Count(f => !f.IsBomb), Is.EqualTo(original.Length));
            Assert.That(fruits.Count(f => f.IsBomb), Is.EqualTo(bombs));
            Assert.That(fruits.All(f => f.NestedHitObjects.Count == 0), Is.True);
            Assert.That(fruits.All(f => f.HitWindows != null), Is.True);
            Assert.That(fruits.Select(f => f.StartTime), Is.Ordered);
            Assert.That(fruits.Where(f => !f.IsBomb).Select(f => f.StartTime),
                Is.EquivalentTo(original.Select(f => f.StartTime)));
            foreach (var fruit in fruits)
            {
                Assert.That(fruit.PositionAt(fruit.StartTime - fruit.TimePreempt).Y, Is.EqualTo(420).Within(0.001));
                Assert.That(fruit.PositionAt(fruit.FlightEndTime).Y, Is.EqualTo(420).Within(0.001));
                Assert.That(fruit.PositionAt(fruit.ApexTime).Y, Is.EqualTo(fruit.IsBomb ? FruitNinjaFruit.SLICE_Y + 80 : FruitNinjaFruit.SLICE_Y));
                Assert.That(fruit.TimePreempt, Is.EqualTo(fruit.SourceObject!.TimePreempt));
                Assert.That(fruit.PositionAt(fruit.StartTime + 1).Y, Is.LessThan(fruit.PositionAt(fruit.StartTime).Y));
            }
        }

        [Test]
        public void TestSweptBladeAndStationaryBlade()
        {
            Assert.That(FruitNinjaFruit.IntersectsSwipe(new Vector2(0, 100), new Vector2(512, 100),
                new Vector2(256, 100), new Vector2(256, 100), 20), Is.True);
            Assert.That(FruitNinjaFruit.IntersectsSwipe(new Vector2(0, 10), new Vector2(512, 10),
                new Vector2(256, 100), new Vector2(256, 100), 20), Is.False);
            Assert.That(FruitNinjaFruit.IntersectsSwipe(new Vector2(256, 100), new Vector2(256, 100),
                new Vector2(256, 150), new Vector2(256, 100), 20), Is.False);
            Assert.That(FruitNinjaFruit.IntersectsSwipe(new Vector2(100, 100), new Vector2(200, 100),
                new Vector2(150, 150), new Vector2(150, 50), 20), Is.True);
        }

        [Test]
        public void TestBombJudgement()
        {
            var judgement = new FruitNinjaFruit { IsBomb = true }.Judgement;
            HitResultExtensions.ValidateHitResultPair(judgement.MaxResult, judgement.MinResult);
            Assert.That(judgement.MaxResult, Is.EqualTo(HitResult.IgnoreHit));
            Assert.That(judgement.MinResult, Is.EqualTo(HitResult.ComboBreak));
            Assert.That(new CatchRuleset().GetValidHitResults(), Does.Contain(HitResult.ComboBreak));
        }

        [Test]
        public void TestBombBreaksComboAndRemovesHealth()
        {
            var fruit = new FruitNinjaFruit();
            var bomb = new FruitNinjaFruit { IsBomb = true, StartTime = 1000 };
            var beatmap = new Beatmap<CatchHitObject> { HitObjects = new List<CatchHitObject> { fruit, bomb } };
            var score = new CatchScoreProcessor();
            score.ApplyBeatmap(beatmap);
            score.ApplyResult(new CatchJudgementResult(fruit, fruit.Judgement) { Type = HitResult.Great });
            Assert.That(score.Combo.Value, Is.EqualTo(1));
            score.ApplyResult(new CatchJudgementResult(bomb, bomb.Judgement) { Type = HitResult.ComboBreak });
            Assert.That(score.Combo.Value, Is.Zero);

            var health = new CatchModFruitNinja().CreateHealthProcessor(0);
            health.ApplyBeatmap(beatmap);
            health.Health.Value = 0.5;
            health.ApplyResult(new CatchJudgementResult(bomb, bomb.Judgement) { Type = HitResult.ComboBreak });
            Assert.That(health.Health.Value, Is.EqualTo(0.42).Within(0.0001));
        }

        [TestCase(0, HitResult.Great, 1)]
        [TestCase(50, HitResult.Great, 1)]
        [TestCase(-75, HitResult.Ok, 1d / 3)]
        [TestCase(75, HitResult.Ok, 1d / 3)]
        [TestCase(125, HitResult.Meh, 1d / 6)]
        [TestCase(-550, HitResult.Meh, 1d / 6)]
        [TestCase(-300, HitResult.Meh, 1d / 6)]
        [TestCase(151, HitResult.Meh, 1d / 6)]
        [TestCase(500, HitResult.Meh, 1d / 6)]
        [TestCase(599, HitResult.Meh, 1d / 6)]
        [TestCase(600, HitResult.Meh, 1d / 6)]
        [TestCase(999, HitResult.Meh, 1d / 6)]
        [TestCase(1000, HitResult.Miss, 0)]
        [TestCase(-1001, HitResult.Miss, 0)]
        public void TestTimingAffectsActualAccuracy(double offset, HitResult expected, double accuracy)
        {
            var windows = new FruitNinjaHitWindows();
            windows.SetDifficulty(5);
            var fruit = new FruitNinjaFruit { StartTime = 2000, HitWindows = windows };
            var score = new CatchScoreProcessor();
            score.ApplyBeatmap(new Beatmap<CatchHitObject> { HitObjects = { fruit } });
            var result = fruit.SliceResultAt(2000 + offset);
            Assert.That(result, Is.EqualTo(expected));
            score.ApplyResult(new CatchJudgementResult(fruit, fruit.Judgement) { Type = result });
            Assert.That(score.Accuracy.Value, Is.EqualTo(accuracy).Within(0.0001));
            Assert.That(score.Combo.Value, Is.EqualTo(expected == HitResult.Miss ? 0 : 1));
        }

        [Test]
        public void TestMultiplierAndLegacyScore()
        {
            var ruleset = new CatchRuleset();
            var mods = new Mod[] { new CatchModFruitNinja() };
            var map = new Beatmap<CatchHitObject> { HitObjects = { new FruitNinjaFruit { StartTime = 1000 } } };
            Assert.That(ruleset.CreateScoreMultiplierCalculator(new ScoreMultiplierContext(map.Difficulty)).CalculateFor(mods), Is.EqualTo(0.1));
            var score = new CatchScoreProcessor();
            score.Mods.Value = mods;
            score.ApplyBeatmap(map);
            score.ApplyResult(new CatchJudgementResult(map.HitObjects[0], map.HitObjects[0].Judgement) { Type = HitResult.Great });
            Assert.That(score.TotalScore.Value, Is.EqualTo(100000));
            var legacy = new CatchLegacyScoreV1Processor(map, mods);
            legacy.ApplyResult(new CatchJudgementResult(map.HitObjects[0], map.HitObjects[0].Judgement) { Type = HitResult.Great });
            Assert.That(legacy.TotalScore, Is.EqualTo(30));
            var bomb = new FruitNinjaFruit { IsBomb = true };
            legacy.ApplyResult(new CatchJudgementResult(bomb, bomb.Judgement) { Type = HitResult.IgnoreHit });
            Assert.That(legacy.TotalScore, Is.EqualTo(30));
            legacy.RevertResult(new CatchJudgementResult(bomb, bomb.Judgement));
            Assert.That(legacy.TotalScore, Is.EqualTo(30));
        }

        [Test]
        public void TestPatternLaunchesPreserveRhythmAndDirection()
        {
            var map = new Beatmap<CatchHitObject>();
            foreach (var (time, x) in new[] { (1000, 400), (1125, 300), (1250, 200), (2000, 100), (2125, 200) })
            {
                var fruit = new Fruit { StartTime = time, X = x };
                fruit.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
                map.HitObjects.Add(fruit);
            }
            new CatchModFruitNinja().ApplyToBeatmap(map);
            var targets = map.HitObjects.Cast<FruitNinjaFruit>().ToArray();
            Assert.That(targets.Select(f => f.TimePreempt), Is.All.EqualTo(targets[0].SourceObject!.TimePreempt));
            Assert.That(targets.Take(3).Select(f => f.LaunchTime), Is.Ordered);
            Assert.That(targets.Take(3).Select(f => f.PatternIndex).Distinct().Count(), Is.EqualTo(1));
            Assert.That(targets[3].PatternIndex, Is.Not.EqualTo(targets[0].PatternIndex));
            Assert.That(targets.Take(3).Select(f => f.PositionAt(f.StartTime).X), Is.Ordered.Descending);
            Assert.That(targets.Skip(3).Select(f => f.PositionAt(f.StartTime).X), Is.Ordered);
            Assert.That(targets.Select(f => f.StartTime), Is.EqualTo(new[] { 1000, 1125, 1250, 2000, 2125 }));
            foreach (var target in targets)
            {
                Assert.That(target.IsInFlight(target.StartTime - target.TimePreempt), Is.True);
                Assert.That(target.PositionAt(target.StartTime - target.TimePreempt).Y, Is.EqualTo(420).Within(0.001));
                Assert.That(target.PositionAt(target.StartTime - 1).Y, Is.GreaterThan(target.PositionAt(target.StartTime).Y));
            }
        }

        [TestCase(0, 1800)]
        [TestCase(5, 1200)]
        [TestCase(9, 600)]
        [TestCase(10, 450)]
        [TestCase(11, 300)]
        public void TestApproachRateControlsLaunchAndApexIsLate(float ar, double expectedPreempt)
        {
            var map = new Beatmap<CatchHitObject>
            {
                Difficulty = new BeatmapDifficulty { ApproachRate = ar },
                HitObjects = { new Fruit { StartTime = 4000, X = 256, NewCombo = true } },
            };
            map.HitObjects[0].ApplyDefaults(map.ControlPointInfo, map.Difficulty);
            new CatchModFruitNinja().ApplyToBeatmap(map);
            foreach (var fruit in map.HitObjects.Cast<FruitNinjaFruit>())
            {
                Assert.That(fruit.TimePreempt, Is.EqualTo(expectedPreempt));
                Assert.That(fruit.LaunchTime, Is.EqualTo(4000 - expectedPreempt));
                Assert.That(fruit.PositionAt(fruit.LaunchTime).Y, Is.EqualTo(420).Within(0.001));
                Assert.That(fruit.PositionAt(fruit.StartTime - 1).Y, Is.GreaterThan(fruit.PositionAt(fruit.StartTime).Y));
                Assert.That(fruit.PositionAt(fruit.StartTime + 1).Y, Is.LessThan(fruit.PositionAt(fruit.StartTime).Y));
                Assert.That(fruit.SliceResultAt(fruit.StartTime), Is.EqualTo(HitResult.Great));
                Assert.That(fruit.ApexTime - fruit.StartTime, Is.GreaterThan(fruit.HitWindows.WindowFor(HitResult.Ok)));
                Assert.That(fruit.SliceResultAt(fruit.ApexTime), Is.EqualTo(HitResult.Meh));
                Assert.That(fruit.PositionAt(fruit.ApexTime - 1).Y, Is.GreaterThan(fruit.PositionAt(fruit.ApexTime).Y));
                Assert.That(fruit.PositionAt(fruit.ApexTime + 1).Y, Is.GreaterThan(fruit.PositionAt(fruit.ApexTime).Y));
                Assert.That(fruit.PositionAt(fruit.FlightEndTime).Y, Is.EqualTo(420).Within(0.001));
                Assert.That(fruit.IsInFlight(fruit.FlightEndTime), Is.False);
            }
        }

        [Test]
        public void TestDenseDropletsDoNotCauseInstantFailure()
        {
            var map = new Beatmap<CatchHitObject>();
            var health = new CatchModFruitNinja().CreateHealthProcessor(0);
            Assert.That(health, Is.TypeOf<FruitNinjaHealthProcessor>());
            Assert.That(health, Is.Not.InstanceOf<DrainingHealthProcessor>());
            health.ApplyBeatmap(map);
            var fruit = new FruitNinjaFruit { SourceObject = new Fruit() };
            for (int i = 0; i < 2; i++)
                health.ApplyResult(new CatchJudgementResult(fruit, fruit.Judgement) { Type = HitResult.Miss });
            var droplet = new FruitNinjaFruit { SourceObject = new TinyDroplet() };
            for (int i = 0; i < 100; i++)
                health.ApplyResult(new CatchJudgementResult(droplet, droplet.Judgement) { Type = HitResult.Miss });
            Assert.That(health.HasFailed, Is.False);
            Assert.That(health.Health.Value, Is.EqualTo(0.75).Within(0.0001));
            var hit = new CatchJudgementResult(fruit, fruit.Judgement) { Type = HitResult.Great };
            health.ApplyResult(hit);
            Assert.That(health.Health.Value, Is.EqualTo(0.775).Within(0.0001));
            health.RevertResult(hit);
            Assert.That(health.Health.Value, Is.EqualTo(0.75).Within(0.0001));
            for (int i = 0; i < 50; i++)
                health.ApplyResult(new CatchJudgementResult(fruit, fruit.Judgement) { Type = HitResult.Miss });
            Assert.That(health.HasFailed, Is.True);
        }

        [Test]
        public void TestTwoDimensionalReplayRoundTrip()
        {
            var beatmap = new Beatmap();
            var frame = new CatchReplayFrame(1000, 123, true) { BladeY = 234 };
            var restored = new CatchReplayFrame();
            var legacy = frame.ToLegacy(beatmap);
            restored.Time = legacy.Time;
            restored.FromLegacy(legacy, beatmap);
            Assert.That(restored.IsEquivalentTo(frame), Is.True);
            restored.BladeY = 235;
            Assert.That(restored.IsEquivalentTo(frame), Is.False);
            var ordinary = new CatchReplayFrame(1000, 123);
            restored = new CatchReplayFrame { Time = ordinary.Time };
            restored.FromLegacy(ordinary.ToLegacy(beatmap), beatmap);
            Assert.That(restored.BladeY, Is.Null);
            Assert.That(restored.IsEquivalentTo(ordinary), Is.True);
        }
    }
}
