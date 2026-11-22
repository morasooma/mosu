using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Navigation
{
    [HeadlessTest]
    public partial class TestSceneSkinPerformanceSettings : OsuGameTestScene
    {
        [Test]
        public void TestSettingsStillReloadSkinAfterGarbageCollection()
        {
            var settings = new (OsuSetting setting, Func<bool> effective)[]
            {
                (OsuSetting.ForkSkinPerformanceMode, () => SkinPerformanceMode.Enabled),
                (OsuSetting.ForkSkinPerformanceFreezeAnimations, () => SkinPerformanceMode.FreezeAnimations),
                (OsuSetting.ForkSkinPerformanceSimplifyEffects, () => SkinPerformanceMode.SimplifyEffects),
                (OsuSetting.ForkSkinPerformanceOptimiseTextures, () => SkinPerformanceMode.OptimiseTextures),
                (OsuSetting.ForkSkinPerformanceSimplifyHud, () => SkinPerformanceMode.SimplifyHud),
                (OsuSetting.ForkSkinPerformanceSimplifyCounters, () => SkinPerformanceMode.SimplifyCounters),
                (OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, () => SkinPerformanceMode.DisableKiaiFlashing),
                (OsuSetting.ForkSkinPerformanceBlackBackground, () => SkinPerformanceMode.BlackBackground),
                (OsuSetting.ForkArgonFollowRing, () => SkinPerformanceMode.ArgonFollowRing),
            };

            SkinManager manager = null!;
            SkinnableDrawable consumer = null!;
            AddStep("add skin consumer", () =>
            {
                manager = Game.Dependencies.Get<SkinManager>();
                Add(new SkinProvidingContainer(manager)
                {
                    Child = consumer = new SkinnableDrawable(new TestLookup(), _ => new Box()),
                });
            });
            AddUntilStep("consumer loaded", () => consumer.IsLoaded);

            // Exercise both directions repeatedly, including turning the mode off after it was enabled.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var (setting, effective) in settings)
                {
                    bool target = false;
                    Skin previousSkin = null!;
                    object previousDrawable = null!;

                    AddStep($"collect before changing {setting}", () =>
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    });
                    AddStep($"toggle {setting}", () =>
                    {
                        target = !Game.LocalConfig.Get<bool>(setting);
                        previousSkin = manager.CurrentSkin.Value;
                        previousDrawable = consumer.Drawable;
                        Game.LocalConfig.SetValue(setting, target);
                    });
                    AddAssert("effective setting updated", () => effective() == target);
                    AddUntilStep("skin recreated", () => manager.CurrentSkin.Value != previousSkin);
                    AddUntilStep("existing drawable reloaded", () => consumer.Drawable != previousDrawable);
                }
            }
        }

        private class TestLookup : ISkinComponentLookup
        {
        }
    }
}
