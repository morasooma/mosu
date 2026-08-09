// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ForkAssistRelaxPresetTest
    {
        [TestCase(ForkAssistRelaxPreset.Soft, 0.35, 95f, 10.0, 13f)]
        [TestCase(ForkAssistRelaxPreset.Balanced, 0.56, 120f, 6.0, 18f)]
        [TestCase(ForkAssistRelaxPreset.Sticky, 0.84, 154f, 2.0, 26f)]
        public void TestPresetAppliesExpectedValues(ForkAssistRelaxPreset preset, double expectedAimStrength, float expectedFovRadius, double expectedRelaxBaseOffset, float expectedSyncRadius)
        {
            using var storage = new TemporaryNativeStorage($@"fork-assist-relax-preset-{preset}");
            var config = new OsuConfigManager(storage);

            config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
            config.SetValue(OsuSetting.ForkRelaxEnabled, false);
            config.SetValue(OsuSetting.ForkAimAssistStrength, 0.01);
            config.SetValue(OsuSetting.ForkAimAssistFovRadius, 1f);
            config.SetValue(OsuSetting.ForkRelaxBaseOffset, -10.0);
            config.SetValue(OsuSetting.ForkRelaxSyncRadius, 1f);

            preset.Apply(config);

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkAimAssistEnabled).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkRelaxEnabled).Value, Is.True);
                Assert.That(config.GetBindable<double>(OsuSetting.ForkAimAssistStrength).Value, Is.EqualTo(expectedAimStrength));
                Assert.That(config.GetBindable<float>(OsuSetting.ForkAimAssistFovRadius).Value, Is.EqualTo(expectedFovRadius));
                Assert.That(config.GetBindable<double>(OsuSetting.ForkRelaxBaseOffset).Value, Is.EqualTo(expectedRelaxBaseOffset));
                Assert.That(config.GetBindable<float>(OsuSetting.ForkRelaxSyncRadius).Value, Is.EqualTo(expectedSyncRadius));
            });
        }

        [Test]
        public void TestStickyPresetIsMeaningfullyStrongerThanBalanced()
        {
            using var storage = new TemporaryNativeStorage(@"fork-assist-relax-preset-strength");
            var config = new OsuConfigManager(storage);

            ForkAssistRelaxPreset.Balanced.Apply(config);

            double balancedStrength = config.GetBindable<double>(OsuSetting.ForkAimAssistStrength).Value;
            float balancedFov = config.GetBindable<float>(OsuSetting.ForkAimAssistFovRadius).Value;
            double balancedIntentThreshold = config.GetBindable<double>(OsuSetting.ForkAimAssistIntentThreshold).Value;
            double balancedStableBpm = config.GetBindable<double>(OsuSetting.ForkRelaxStableBpm).Value;

            ForkAssistRelaxPreset.Sticky.Apply(config);

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<double>(OsuSetting.ForkAimAssistStrength).Value, Is.GreaterThan(balancedStrength));
                Assert.That(config.GetBindable<float>(OsuSetting.ForkAimAssistFovRadius).Value, Is.GreaterThan(balancedFov));
                Assert.That(config.GetBindable<double>(OsuSetting.ForkAimAssistIntentThreshold).Value, Is.LessThan(balancedIntentThreshold));
                Assert.That(config.GetBindable<double>(OsuSetting.ForkRelaxStableBpm).Value, Is.GreaterThan(balancedStableBpm));
            });
        }

        /*
        [Test]
        public void TestLegacyAlternateThresholdMigratesToStableBpm()
        {
            using var storage = new TemporaryNativeStorage(@"fork-assist-relax-legacy-bpm");

            var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.Version, "2026.101.0-lazer");
            config.SetValue(OsuSetting.ForkRelaxStableBpm, 180.0);
            config.SetValue(OsuSetting.ForkRelaxAlternateThreshold, 120.0);
            config.Migrate();

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<double>(OsuSetting.ForkRelaxStableBpm).Value, Is.EqualTo(125.0));
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkRelaxStableBpmMigrationComplete).Value, Is.True);
            });
        }

        [Test]
        public void TestLegacyAlternateThresholdMigrationDoesNotOverwriteCustomStableBpm()
        {
            using var storage = new TemporaryNativeStorage(@"fork-assist-relax-custom-stable-bpm");

            var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.Version, "2026.101.0-lazer");
            config.SetValue(OsuSetting.ForkRelaxAlternateThreshold, 120.0);
            config.SetValue(OsuSetting.ForkRelaxStableBpm, 210.0);
            config.Migrate();

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<double>(OsuSetting.ForkRelaxStableBpm).Value, Is.EqualTo(210.0));
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkRelaxStableBpmMigrationComplete).Value, Is.True);
            });

            config.Save();
            var reopenedConfig = new OsuConfigManager(storage);

            Assert.Multiple(() =>
            {
                Assert.That(reopenedConfig.GetBindable<double>(OsuSetting.ForkRelaxStableBpm).Value, Is.EqualTo(210.0));
                Assert.That(reopenedConfig.GetBindable<bool>(OsuSetting.ForkRelaxStableBpmMigrationComplete).Value, Is.True);
            });
        }
        */
    }
}
