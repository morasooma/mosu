using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BackgroundCalculationPolicyTest
    {
        [Test]
        public void TestIndependentFlagsAndImportGate()
        {
            using var storage = new TemporaryNativeStorage($"calculation-policy-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            var policy = new BackgroundCalculationPolicy(config);
            policy.Set("osu", new BackgroundCalculationPolicy.Mode { Calculations = LibraryCalculation.Performance | LibraryCalculation.RelaxDifficulty });
            Assert.Multiple(() =>
            {
                Assert.That(policy.Allows("osu", LibraryCalculation.Difficulty), Is.False);
                Assert.That(policy.Allows("osu", LibraryCalculation.Performance), Is.True);
                Assert.That(policy.Allows("osu", LibraryCalculation.RelaxDifficulty), Is.True);
                Assert.That(policy.Allows("osu", LibraryCalculation.RelaxPerformance), Is.False);
                Assert.That(policy.Allows("osu", LibraryCalculation.Performance, true), Is.False);
                Assert.That(policy.Allows("taiko", LibraryCalculation.Difficulty), Is.True);
            });
            var mode = policy.Get("osu");
            mode.Calculations |= LibraryCalculation.Import;
            policy.Set("osu", mode);
            Assert.That(policy.Allows("osu", LibraryCalculation.Performance, true), Is.True);
            mode.Enabled = false;
            policy.Set("osu", mode);
            Assert.That(policy.Allows("osu", LibraryCalculation.Performance), Is.False);
        }

        [Test]
        public void TestPersistenceAndSharedBudget()
        {
            using var storage = new TemporaryNativeStorage($"calculation-policy-save-{Guid.NewGuid():N}");
            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.ForkBackgroundCalculationConcurrency, 2);
                var policy = new BackgroundCalculationPolicy(config);
                policy.Set("custom", new BackgroundCalculationPolicy.Mode { Enabled = false, Concurrency = 4 });
                Assert.That(policy.Concurrency("custom"), Is.EqualTo(2));
                policy.Set("osu", new BackgroundCalculationPolicy.Mode { Concurrency = 1 });
                Assert.That(policy.Concurrency("osu"), Is.EqualTo(1));
                config.Save();
            }
            using var reopened = new OsuConfigManager(storage);
            Assert.That(new BackgroundCalculationPolicy(reopened).Get("custom").Enabled, Is.False);
            Assert.That(new BackgroundCalculationPolicy(reopened).Concurrency("osu"), Is.EqualTo(1));
        }

        [Test]
        public void TestInvalidJsonUsesDefaultsAndReloads()
        {
            using var storage = new TemporaryNativeStorage($"calculation-policy-invalid-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            var policy = new BackgroundCalculationPolicy(config);
            config.SetValue(OsuSetting.ForkBackgroundCalculationModes, "not json");
            Assert.That(policy.Allows("osu", LibraryCalculation.Performance), Is.True);
            config.SetValue(OsuSetting.ForkBackgroundCalculationModes, "{\"osu\":{\"Enabled\":false}}");
            Assert.That(policy.Allows("osu", LibraryCalculation.Performance), Is.False);
        }

        [Test]
        [NonParallelizable]
        public void TestRelaxStarsDoNotMarkPpAsCalculatedAndSurviveReload()
        {
            using var storage = new TemporaryNativeStorage($"calculation-rx-sr-{Guid.NewGuid():N}");
            Guid id = Guid.NewGuid();
            int version = ForkDataStore.GetRelaxCalculationVersion(RelaxPpSystemSelection.Current);
            using (var store = new ForkDataStore(storage))
            {
                store.SetRelaxDifficulty(id, "checksum", version, 4.5);
                Assert.That(store.HasRelaxData(id), Is.False);
                Assert.That(store.HasRelaxDifficulty(id, "checksum", version), Is.True);
                Assert.That(store.HasRelaxDifficulty(id, "changed", version), Is.False);
            }
            using var reloaded = new ForkDataStore(storage);
            Assert.That(reloaded.GetRelaxStarRating(id), Is.EqualTo(4.5));
            Assert.That(reloaded.HasRelaxData(id), Is.False);
            Assert.That(reloaded.HasRelaxDifficulty(id, "checksum", version), Is.True);
            reloaded.SetRelaxData(id, 4.5, 123);
            reloaded.SetRelaxDifficulty(id, "checksum", version, 5);
            Assert.That(reloaded.GetRelaxPP(id), Is.EqualTo(123));
            Assert.That(reloaded.HasRelaxData(id), Is.True);
        }
    }
}
