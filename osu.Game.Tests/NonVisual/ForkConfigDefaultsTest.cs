// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online;
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
            var gameplayRenderScale = (BindableFloat)config.GetBindable<float>(OsuSetting.ForkGameplayRenderScale);

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkAimAssistShowFlowDebug).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkReduceVolumeOutsideGameplay).Value, Is.False);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkCustomUsername).Value, Is.Empty);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkMorasoomaEndTag).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkEnableModNumericInput).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSongSelectCarouselLazyLoading).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkSeparateSkinsPerRuleset).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkUseSkinCursorOutsideGameplay).Value, Is.False);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkOsuSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkTaikoSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkCatchSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkManiaSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<string>(OsuSetting.ForkDodgeSkin).Value, Is.Empty);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkShowModsInPresetList).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkEnhancedRankingRows).Value, Is.True);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkAutoHideToolbar).Value, Is.False);
                Assert.That(config.GetBindable<DebugHudMode>(OsuSetting.ForkDebugHudMode).Value, Is.EqualTo(DebugHudMode.Disabled));
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkShowMemoryInToolbar).Value, Is.False);
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkDebugFreezeAlerts).Value, Is.False);
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
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkArgonFollowRing).Value, Is.True);
                Assert.That(gameplayRenderScale.Value, Is.EqualTo(1f));
                Assert.That(gameplayRenderScale.MinValue, Is.EqualTo(0.1f));
                Assert.That(gameplayRenderScale.MaxValue, Is.EqualTo(1f));
                Assert.That(gameplayRenderScale.Precision, Is.EqualTo(0.05f));
                Assert.That(config.GetBindable<MosuConnectionRoute>(OsuSetting.ForkConnectionRoute).Value, Is.EqualTo(MosuConnectionRoute.Direct));
                Assert.That(config.GetBindable<ForkMenuLogo>(OsuSetting.ForkMenuLogo).Value, Is.EqualTo(ForkMenuLogo.Random));
                Assert.That(config.GetBindable<ForkMenuLogoGradient>(OsuSetting.ForkMenuLogoGradient).Value, Is.EqualTo(ForkMenuLogoGradient.Random));
                Assert.That(config.GetBindable<bool>(OsuSetting.ForkMenuLogoTriangles).Value, Is.True);
            });
        }

        [Test]
        public void TestLegacyConnectionProxySettingMigratesToProxy2()
        {
            using var storage = new TemporaryNativeStorage($@"fork-connection-route-{Guid.NewGuid():N}");
            writeFile(storage, @"mosu.ini", "ForkUseConnectionProxy = True");

            using var config = new OsuConfigManager(storage);

            Assert.Multiple(() =>
            {
                Assert.That(config.Get<MosuConnectionRoute>(OsuSetting.ForkConnectionRoute), Is.EqualTo(MosuConnectionRoute.Proxy2));
                Assert.That(config.Get<bool>(OsuSetting.ForkUseConnectionProxy), Is.False);
            });
        }

        [TestCase(ForkSongSelectStyle.Modern, false, false)]
        [TestCase(ForkSongSelectStyle.Classic2024, true, false)]
        [TestCase(ForkSongSelectStyle.LegacySkinned, false, true)]
        [TestCase(ForkSongSelectStyle.InfiniteGlass, false, false)]
        public void TestSongSelectStyleRouting(ForkSongSelectStyle style, bool usesV1Screen, bool usesStableStyle)
        {
            Assert.Multiple(() =>
            {
                Assert.That(style.UsesV1Screen(), Is.EqualTo(usesV1Screen));
                Assert.That(style.UsesStableStyle(), Is.EqualTo(usesStableStyle));
                Assert.That(style.UsesInfiniteGlass(), Is.EqualTo(style == ForkSongSelectStyle.InfiniteGlass));
                Assert.That(style.UsesSkinnedLegacyCarousel(), Is.EqualTo(style == ForkSongSelectStyle.LegacySkinned));
            });
        }

        [TestCase(ForkSongSelectStyle.LegacySkinned, true, true)]
        [TestCase(ForkSongSelectStyle.LegacySkinned, false, false)]
        [TestCase(ForkSongSelectStyle.Modern, true, false)]
        public void TestStablePresentationRequiresSupportedScreen(ForkSongSelectStyle style, bool screenSupportsStableStyle, bool expected)
            => Assert.That(style.UsesStableStyleOn(screenSupportsStableStyle), Is.EqualTo(expected));

        [TestCase(ForkSongSelectStyle.LegacySkinned, 1f)]
        [TestCase(ForkSongSelectStyle.Modern, 1f)]
        [TestCase(ForkSongSelectStyle.Classic2024, 1f)]
        public void TestSongSelectLogoAlpha(ForkSongSelectStyle style, float expected)
            => Assert.That(style.GetSongSelectLogoAlpha(), Is.EqualTo(expected));

        [TestCase(ForkSongSelectStyle.LegacySkinned, true, true)]
        [TestCase(ForkSongSelectStyle.LegacySkinned, false, false)]
        [TestCase(ForkSongSelectStyle.Modern, true, false)]
        public void TestSkinnedCarouselBindingHonoursScreenSupport(ForkSongSelectStyle style, bool screenSupportsStableStyle, bool expected)
        {
            using var storage = new TemporaryNativeStorage($@"fork-song-select-screen-support-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            var target = new BindableBool();

            config.SetValue(OsuSetting.ForkSongSelectStyle, style);
            ForkSongSelectStyleBinding.BindSkinnedLegacyCarousel(config, target, () => screenSupportsStableStyle);

            Assert.That(target.Value, Is.EqualTo(expected));
        }

        [Test]
        public void TestExplicitSongSelectStyleIsNotOverriddenByLegacySettings()
        {
            using var storage = new TemporaryNativeStorage($@"fork-song-select-style-{Guid.NewGuid():N}");
            writeFile(storage, @"mosu.ini", string.Join(Environment.NewLine, new[]
            {
                "ForkSongSelectV1Carousel = True",
                "ForkSongSelectStyle = LegacySkinned",
            }));

            using var config = new OsuConfigManager(storage);

            Assert.Multiple(() =>
            {
                Assert.That(config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle), Is.EqualTo(ForkSongSelectStyle.LegacySkinned));
                Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectV1Carousel), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ForkSongSelectSkinnedLegacyCarousel), Is.False);
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
                (OsuSetting.ForkUse8kPollingRate, false),
                (OsuSetting.ForkLargeTextureAtlas, false),
                (OsuSetting.ForkDeferredVertexUploadBatching, false),
                (OsuSetting.ForkDeferredDirectVertexUpload, false),
                (OsuSetting.ForkDeferredDirectUniformUpload, false),
                (OsuSetting.ForkVeldridPipelineLookupCache, false),
                (OsuSetting.ForkStaticChildLifetimeCache, false),
                (OsuSetting.ForkAllowTearing, true),
                (OsuSetting.ForkUpdateThreadSpinWait, false),
                (OsuSetting.ForkSkinPerformanceMode, true),
                (OsuSetting.ForkSkinPerformanceFreezeAnimations, true),
                (OsuSetting.ForkSkinPerformanceSimplifyEffects, true),
                (OsuSetting.ForkSkinPerformanceOptimiseTextures, true),
                (OsuSetting.ForkSkinPerformanceSimplifyHud, true),
                (OsuSetting.ForkSkinPerformanceSimplifyCounters, true),
                (OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, true),
                (OsuSetting.ForkSkinPerformanceBlackBackground, true),
                (OsuSetting.ForkArgonFollowRing, true),
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
        public void TestReleaseStreamMigratesFromGameToMosuConfig()
        {
            using var storage = new TemporaryNativeStorage($@"fork-config-release-stream-{Guid.NewGuid():N}");
            writeFile(storage, @"game.ini", string.Join(Environment.NewLine, new[]
            {
                "ReleaseStream = DevBuild",
                "ShowFpsDisplay = True",
            }));

            using (var config = new OsuConfigManager(storage))
                Assert.That(config.Get<ReleaseStream>(OsuSetting.ReleaseStream), Is.EqualTo(ReleaseStream.DevBuild));

            Assert.Multiple(() =>
            {
                Assert.That(readFile(storage, @"game.ini"), Does.Not.Contain("ReleaseStream"));
                Assert.That(readFile(storage, @"game.ini"), Does.Contain("ShowFpsDisplay = True"));
                Assert.That(readFile(storage, @"mosu.ini"), Does.Contain("ReleaseStream = DevBuild"));
            });
        }

        [Test]
        public void TestMosuReleaseStreamWinsDuringMigration()
        {
            using var storage = new TemporaryNativeStorage($@"fork-config-release-stream-existing-{Guid.NewGuid():N}");
            writeFile(storage, @"game.ini", "ReleaseStream = DevBuild");
            writeFile(storage, @"mosu.ini", "ReleaseStream = Lazer");

            using (var config = new OsuConfigManager(storage))
                Assert.That(config.Get<ReleaseStream>(OsuSetting.ReleaseStream), Is.EqualTo(ReleaseStream.Lazer));

            Assert.Multiple(() =>
            {
                Assert.That(readFile(storage, @"game.ini"), Does.Not.Contain("ReleaseStream"));
                Assert.That(readFile(storage, @"mosu.ini"), Does.Contain("ReleaseStream = Lazer"));
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
