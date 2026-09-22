// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeModsTest
    {
        [Test]
        public void TestExpectedStandardModsAreAvailable()
        {
            var ruleset = new DodgeRuleset();

            assertContains<DodgeModMosuStaticBpm>(ruleset.GetModsFor(ModType.Mosu));
            assertContains<DodgeModMosuTargetDifficulty>(ruleset.GetModsFor(ModType.Mosu));
            assertContains<DodgeModAudioEffects>(ruleset.GetModsFor(ModType.Mosu));
            assertContains<DodgeModFullPaths>(ruleset.GetModsFor(ModType.Mosu));

            assertContains<DodgeModNoFail>(ruleset.GetModsFor(ModType.DifficultyReduction));
            assertContains<DodgeModHalfTime>(ruleset.GetModsFor(ModType.DifficultyReduction));
            assertContains<DodgeModDaycore>(ruleset.GetModsFor(ModType.DifficultyReduction));

            assertContains<DodgeModSuddenDeath>(ruleset.GetModsFor(ModType.DifficultyIncrease));
            assertContains<DodgeModPerfect>(ruleset.GetModsFor(ModType.DifficultyIncrease));
            assertContains<DodgeModDoubleTime>(ruleset.GetModsFor(ModType.DifficultyIncrease));
            assertContains<DodgeModNightcore>(ruleset.GetModsFor(ModType.DifficultyIncrease));

            assertContains<DodgeModAutoplay>(ruleset.GetModsFor(ModType.Automation));
            assertContains<DodgeModCinema>(ruleset.GetModsFor(ModType.Automation));
            assertContains<ModWindUp>(ruleset.GetModsFor(ModType.Fun));
            assertContains<ModWindDown>(ruleset.GetModsFor(ModType.Fun));
            assertContains<ModAdaptiveSpeed>(ruleset.GetModsFor(ModType.Fun));
            assertContains<ModScoreV2>(ruleset.GetModsFor(ModType.System));
        }

        [Test]
        public void TestFullPathsIsUnranked()
        {
            var mod = new DodgeModFullPaths();

            Assert.That(mod.Ranked, Is.False);
            Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(null, new[] { mod }), Is.False);
        }

        [Test]
        public void TestAllSpeedChangingModsAreUnranked()
        {
            var ruleset = new DodgeRuleset();
            var beatmapInfo = new BeatmapInfo { Ruleset = ruleset.RulesetInfo };
            Mod[] speedChangingMods = System.Enum.GetValues<ModType>()
                                                   .SelectMany(type => flatten(ruleset.GetModsFor(type)))
                                                   .Where(mod => mod is IApplicableToRate)
                                                   .ToArray();

            Assert.That(speedChangingMods.Select(mod => mod.Acronym), Is.EquivalentTo(new[]
            {
                "HT", "DC", "DT", "NC", "SB", "TS", "WU", "WD", "AS",
            }));
            Assert.Multiple(() =>
            {
                foreach (Mod mod in speedChangingMods)
                {
                    Assert.That(mod.Ranked, Is.False, mod.Acronym);
                    Assert.That(ModPerformancePointHelper.ModsAwardPerformancePoints(beatmapInfo, new[] { mod }), Is.False, mod.Acronym);
                }
            });
        }

        [TestCase(BeatmapOnlineStatus.Pending, false)]
        [TestCase(BeatmapOnlineStatus.Qualified, false)]
        [TestCase(BeatmapOnlineStatus.Loved, false)]
        [TestCase(BeatmapOnlineStatus.Ranked, true)]
        [TestCase(BeatmapOnlineStatus.Approved, true)]
        public void TestBeatmapPerformanceEligibilityRequiresRankedDodgeStatus(BeatmapOnlineStatus status, bool expected)
        {
            var ruleset = new DodgeRuleset();
            var beatmapInfo = new BeatmapInfo
            {
                Ruleset = ruleset.RulesetInfo,
                Status = status,
            };

            Assert.That(ModPerformancePointHelper.BeatmapAwardsPerformancePoints(ruleset.RulesetInfo, beatmapInfo), Is.EqualTo(expected));
        }

        [Test]
        public void TestAutoplayCannotFail()
        {
            var autoplay = new DodgeModAutoplay();

            Assert.Multiple(() =>
            {
                Assert.That(autoplay, Is.AssignableTo<IApplicableFailOverride>());
                Assert.That(autoplay.PerformFail(), Is.False);
                Assert.That(autoplay.RestartOnFail, Is.False);
            });
        }

        [Test]
        public void TestAudioEffectsSerialisesForScoreSubmission()
        {
            var mod = new DodgeModAudioEffects
            {
                Preset = { Value = AudioEffectPreset.Radio },
                Intensity = { Value = 0.8 },
                Pitch = { Value = -3 },
                AffectHitSounds = { Value = true },
            };

            string serialised = JsonConvert.SerializeObject(new APIMod(mod));
            var deserialised = JsonConvert.DeserializeObject<APIMod>(serialised)!;
            var restored = (DodgeModAudioEffects)deserialised.ToMod(new DodgeRuleset());

            Assert.Multiple(() =>
            {
                Assert.That(deserialised.Acronym, Is.EqualTo("FX"));
                Assert.That(deserialised.Settings.Keys, Is.EquivalentTo(new[]
                {
                    "preset",
                    "intensity",
                    "pitch",
                    "affect_hit_sounds",
                }));
                Assert.That(restored.Preset.Value, Is.EqualTo(AudioEffectPreset.Radio));
                Assert.That(restored.Intensity.Value, Is.EqualTo(0.8).Within(0.0001));
                Assert.That(restored.Pitch.Value, Is.EqualTo(-3));
                Assert.That(restored.AffectHitSounds.Value, Is.True);
            });
        }

        [Test]
        public void TestFullPathsAppliesDirectlyToDrawables()
        {
            var mod = new DodgeModFullPaths();
            var bullet = new DrawableDodgeHitObject(new DodgeBullet());
            var emitter = new DrawableDodgeEmitter(new DodgeEmitter());

            mod.ApplyToDrawableHitObject(bullet);
            mod.ApplyToDrawableHitObject(emitter);

            Assert.That(bullet.ShowFullTrajectory, Is.True);
            Assert.That(emitter.ShowFullTrajectories, Is.True);
        }

        [Test]
        public void TestTrajectoryGuidesAreNotAllocatedWithoutFullPaths()
        {
            var bullet = new DrawableDodgeHitObject(new DodgeBullet());
            var emitter = new DrawableDodgeEmitter(new DodgeEmitter
            {
                BulletCount = DodgeEmitter.MAX_BULLET_COUNT,
                BurstCount = DodgeEmitter.MAX_BURST_COUNT,
            });

            Assert.Multiple(() =>
            {
                Assert.That(bullet.TrajectoryGuideCount, Is.Zero);
                Assert.That(emitter.BulletVisualCount, Is.Zero);
                Assert.That(emitter.TrajectoryGuideCount, Is.Zero);
            });
        }

        [Test]
        public void TestBulletOutlineDrawableIsAllocatedOnDemand()
        {
            var bullet = new DodgeBulletVisual();

            Assert.That(bullet.HasOutlineDrawable, Is.False);

            bullet.OutlineThickness = 2;
            Assert.That(bullet.HasOutlineDrawable, Is.True);

            var previousOutline = bullet.OutlineDrawable;
            bullet.Shape = DodgeBulletShape.Square;
            Assert.That(bullet.OutlineDrawable, Is.Not.SameAs(previousOutline));

            bullet.OutlineThickness = 0;
            Assert.That(bullet.HasOutlineDrawable, Is.False);
        }

        [TestCaseSource(nameof(score_multiplier_cases))]
        public void TestScoreMultipliers(Mod mod, double expected)
        {
            ScoreMultiplierCalculator calculator =
                new DodgeRuleset().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(new BeatmapDifficulty()));
            Assert.That(calculator.CalculateFor(new[] { mod }), Is.EqualTo(expected).Within(0.0001));
        }

        private static readonly object[][] score_multiplier_cases =
        {
            new object[] { new DodgeModNoFail(), 0.5 },
            new object[] { new DodgeModHalfTime(), 0.3 },
            new object[] { new DodgeModDoubleTime(), 1.1 },
            new object[] { new DodgeModSuddenDeath(), 1.0 },
            new object[] { new DodgeModFullPaths(), 0.7 },
        };

        private static void assertContains<T>(IEnumerable<Mod> mods)
            where T : Mod
            => Assert.That(flatten(mods), Has.Some.TypeOf<T>());

        private static IEnumerable<Mod> flatten(IEnumerable<Mod> mods)
            => mods.SelectMany(mod => mod is MultiMod multiMod ? multiMod.Mods : new[] { mod });
    }
}
