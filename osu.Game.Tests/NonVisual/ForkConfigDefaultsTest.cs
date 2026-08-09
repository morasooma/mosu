// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ForkConfigDefaultsTest
    {
        [Test]
        public void TestNewForkSettingsHaveTypedDefaults()
        {
            using var storage = new TemporaryNativeStorage(@"fork-config-defaults");
            var config = new OsuConfigManager(storage);

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkAimAssistShowFlowDebug).Value, Is.False);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkCustomUsername).Value, Is.Empty);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkEnableModNumericInput).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSeparateSkinsPerRuleset).Value, Is.False);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkOsuSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkTaikoSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkCatchSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkManiaSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkDodgeSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkShowModsInPresetList).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkUseOfficialBeatmapService).Value, Is.False);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkOfficialOsuFailureNotificationKey).Value, Is.Empty);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceMode).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceFreezeAnimations).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceSimplifyEffects).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceOptimiseTextures).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceSimplifyHud).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceSimplifyCounters).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceBlackBackground).Value, Is.True);
            });
        }

        [Test]
        public void TestRecommendedPerformancePresetAppliesBenchmarkedValues()
        {
            using var storage = new TemporaryNativeStorage(@"fork-recommended-performance-preset");
            var config = new OsuConfigManager(storage);
            (OsuSetting Setting, bool Expected)[] presetSettings =
            {
                (OsuSetting.ForkWindowsUltraPerformanceMode, false),
                (OsuSetting.ForkSkinPerformanceMode, true),
                (OsuSetting.ForkSkinPerformanceFreezeAnimations, true),
                (OsuSetting.ForkSkinPerformanceSimplifyEffects, true),
                (OsuSetting.ForkSkinPerformanceOptimiseTextures, true),
                (OsuSetting.ForkSkinPerformanceSimplifyHud, true),
                (OsuSetting.ForkSkinPerformanceSimplifyCounters, true),
                (OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, true),
                (OsuSetting.ForkSkinPerformanceBlackBackground, true),
            };

            foreach ((OsuSetting setting, bool expected) in presetSettings)
                config.SetValue(setting, !expected);

            MosuRecommendedPerformancePreset.Apply(config);

            Assert.Multiple(() =>
            {
                foreach ((OsuSetting setting, bool expected) in presetSettings)
                    Assert.That(config.Get<bool>(setting), Is.EqualTo(expected), setting.ToString());
            });
        }

        [Test]
        public void TestClientVersionIsStoredOnlyInMosuConfig()
        {
            using var storage = new TemporaryNativeStorage($@"fork-config-version-{Guid.NewGuid():N}");

            writeFile(storage, @"game.ini", "Version = 2026.207.0-lazer");

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<string>(OsuSetting.Version), Is.Empty);

                config.SetValue(OsuSetting.Version, "2026.208.0-lazer");
                config.Save();
            }

            using (var config = new OsuConfigManager(storage))
                Assert.That(config.Get<string>(OsuSetting.Version), Is.EqualTo("2026.208.0-lazer"));

            Assert.Multiple(() =>
            {
                Assert.That(readFile(storage, @"game.ini"), Does.Contain("Version = 2026.207.0-lazer"));
                Assert.That(readFile(storage, @"game.ini"), Does.Not.Contain("2026.208.0-lazer"));
                Assert.That(readFile(storage, @"mosu.ini"), Does.Contain("Version = 2026.208.0-lazer"));
            });
        }

        private static void writeFile(TemporaryNativeStorage storage, string filename, string contents)
        {
            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(contents);
        }

        private static string readFile(TemporaryNativeStorage storage, string filename)
        {
            using var stream = storage.GetStream(filename);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
