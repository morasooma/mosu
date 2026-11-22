using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online.API;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeSegmentRateModsTest
    {
        [Test]
        public void TestBothModsRegisteredAndSettingsSurviveReplaySerialization()
        {
            var ruleset = new DodgeRuleset();
            foreach (var mod in ruleset.AllMods.OfType<ModSegmentRate>())
            {
                Assert.That(mod.Ranked, Is.False);
                Assert.That(new APIMod(mod).ToMod(ruleset), Is.TypeOf(mod.GetType()));
            }
            Assert.That(ruleset.AllMods.OfType<ModSegmentRate>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void TestProjectileLifetimesDoNotDisableGlitch()
        {
            var map = createMap();
            var mod = new DodgeModGlitch { BakedTiming = { Value = false }, Amount = { Value = 100 } };
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments.Count, Is.GreaterThan(5));
            Assert.That(mod.RatePlan.Segments.Any(s => s.Start > 1000 && s.End < 2000), Is.True);
        }

        [Test]
        public void TestBakedGlitchPreservesProjectileAndBurstTiming()
        {
            var map = createMap();
            var emitter = (DodgeEmitter)map.HitObjects[1];
            emitter.BurstCount = 4;
            foreach (var obj in map.HitObjects)
                obj.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
            var originalBurstTimes = Enumerable.Range(0, emitter.EffectiveBurstCount).Select(emitter.EmissionTimeAt).ToArray();
            double end = emitter.EndTime;
            double bulletEnd = ((DodgeBullet)map.HitObjects[0]).EndTime;
            var mod = new DodgeModGlitch { Amount = { Value = 100 } };
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments, Is.Not.Empty);
            Assert.That(((DodgeBullet)map.HitObjects[0]).EndTime, Is.EqualTo(mod.RatePlan.Map(bulletEnd)).Within(1e-6));
            for (int i = 0; i < originalBurstTimes.Length; i++)
                Assert.That(emitter.EmissionTimeAt(i), Is.EqualTo(mod.RatePlan.Map(originalBurstTimes[i])).Within(1e-6));
            Assert.That(emitter.EndTime, Is.EqualTo(mod.RatePlan.Map(end)).Within(1e-6));
        }

        [TestCase(FlatlineDirection.Harder)]
        [TestCase(FlatlineDirection.Easier)]
        [TestCase(FlatlineDirection.Both)]
        public void TestFlatlineUsesRouteDifficulty(FlatlineDirection direction)
        {
            var ruleset = new DodgeRuleset();
            var raw = createMap();
            var working = new FlatWorkingBeatmap(raw);
            var mod = new DodgeModFlatline { Direction = { Value = direction } };
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, new Mod[] { mod });
            var calculator = ruleset.CreateDifficultyCalculator(working);
            var samples = calculator.CalculateLocalDifficulty(playable);
            Assert.That(samples, Is.Not.Empty);
            Assert.That(samples.Any(s => s.Difficulty > 0), Is.True);
            Assert.That(samples.All(s => double.IsFinite(s.Difficulty)), Is.True);
            Assert.That(mod.RatePlan.Segments, Is.Not.Empty);
            if (direction == FlatlineDirection.Harder)
                Assert.That(mod.RatePlan.Segments.All(s => s.Rate >= 1), Is.True);
            if (direction == FlatlineDirection.Easier)
                Assert.That(mod.RatePlan.Segments.All(s => s.Rate <= 1), Is.True);
            TestContext.Out.WriteLine($"Dodge/{direction}: {samples.Count} route difficulty samples, {mod.RatePlan.Segments.Count} rate segments");
        }

        private static Beatmap<DodgeHitObject> createMap()
        {
            var map = new Beatmap<DodgeHitObject>();
            map.BeatmapInfo.Ruleset = new DodgeRuleset().RulesetInfo;
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            map.HitObjects.Add(new DodgeBullet
            {
                StartTime = 1000, Duration = 1000,
                Position = new Vector2(0, 64), EndPosition = new Vector2(512, 64),
            });
            map.HitObjects.Add(new DodgeEmitter
            {
                StartTime = 2000, Duration = 1800,
                Position = new Vector2(256, 192), AimPosition = new Vector2(512, 192),
                BulletCount = 8, SpreadAngle = 180,
            });
            return map;
        }
    }
}
