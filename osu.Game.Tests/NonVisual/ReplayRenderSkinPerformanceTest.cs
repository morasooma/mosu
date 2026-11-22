using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Scoring.Render;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class ReplayRenderSkinPerformanceTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestRenderDisablesPerformanceModeWithoutChangingUserSettings(bool enabled)
        {
            var settings = new (OsuSetting Setting, Func<bool> Get, Action<bool> Set)[]
            {
                (OsuSetting.ForkSkinPerformanceMode, () => SkinPerformanceMode.Enabled, v => SkinPerformanceMode.Enabled = v),
                (OsuSetting.ForkSkinPerformanceFreezeAnimations, () => SkinPerformanceMode.FreezeAnimations, v => SkinPerformanceMode.FreezeAnimations = v),
                (OsuSetting.ForkSkinPerformanceSimplifyEffects, () => SkinPerformanceMode.SimplifyEffects, v => SkinPerformanceMode.SimplifyEffects = v),
                (OsuSetting.ForkSkinPerformanceOptimiseTextures, () => SkinPerformanceMode.OptimiseTextures, v => SkinPerformanceMode.OptimiseTextures = v),
                (OsuSetting.ForkSkinPerformanceSimplifyHud, () => SkinPerformanceMode.SimplifyHud, v => SkinPerformanceMode.SimplifyHud = v),
                (OsuSetting.ForkSkinPerformanceSimplifyCounters, () => SkinPerformanceMode.SimplifyCounters, v => SkinPerformanceMode.SimplifyCounters = v),
                (OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, () => SkinPerformanceMode.DisableKiaiFlashing, v => SkinPerformanceMode.DisableKiaiFlashing = v),
                (OsuSetting.ForkSkinPerformanceBlackBackground, () => SkinPerformanceMode.BlackBackground, v => SkinPerformanceMode.BlackBackground = v),
                (OsuSetting.ForkArgonFollowRing, () => SkinPerformanceMode.ArgonFollowRing, v => SkinPerformanceMode.ArgonFollowRing = v),
            };

            Assert.That(SkinPerformanceMode.HasBenchmarkOverride, Is.False);
            var previous = Array.ConvertAll(settings, setting => setting.Get());
            using var storage = new TemporaryNativeStorage($"render-skin-performance-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            try
            {
                for (int i = 0; i < settings.Length; i++)
                {
                    bool expected = i % 2 == 0 ? enabled : !enabled;
                    config.SetValue(settings[i].Setting, expected);
                    settings[i].Set(!expected);
                }

                ReplayRenderGame.ApplySkinPerformanceSettings(config);

                Assert.Multiple(() =>
                {
                    Assert.That(SkinPerformanceMode.Enabled, Is.False);
                    Assert.That(config.Get<bool>(OsuSetting.ForkSkinPerformanceMode), Is.EqualTo(enabled));
                    for (int i = 1; i < settings.Length; i++)
                        Assert.That(settings[i].Get(), Is.EqualTo(config.Get<bool>(settings[i].Setting)), settings[i].Setting.ToString());
                });
            }
            finally
            {
                for (int i = 0; i < settings.Length; i++)
                    settings[i].Set(previous[i]);
            }
        }
    }
}
