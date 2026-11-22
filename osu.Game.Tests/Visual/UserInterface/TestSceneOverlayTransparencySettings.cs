using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneOverlayTransparencySettings : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private bool previousOverlays;
        private bool previousSongSelect;
        private double previousBlur;
        private double previousDim;
        private bool previousGlobalOverlays;
        private bool previousGlobalSongSelect;
        private double previousGlobalBlur;
        private double previousGlobalDim;

        [SetUpSteps]
        public void SetUpSteps() => AddStep("save settings and shared state", () =>
        {
            previousOverlays = config.Get<bool>(OsuSetting.ForkOverlayTransparency);
            previousSongSelect = config.Get<bool>(OsuSetting.ForkSongSelectTransparency);
            previousBlur = config.Get<double>(OsuSetting.ForkOverlayBlurStrength);
            previousDim = config.Get<double>(OsuSetting.ForkOverlayDimAmount);
            previousGlobalOverlays = OverlayTransparency.Enabled.Value;
            previousGlobalSongSelect = OverlayTransparency.SongSelectEnabled.Value;
            previousGlobalBlur = OverlayTransparency.BlurStrength.Value;
            previousGlobalDim = OverlayTransparency.DimAmount.Value;
        });

        [TearDownSteps]
        public void TearDownSteps() => AddStep("restore settings and shared state", () =>
        {
            config.SetValue(OsuSetting.ForkOverlayTransparency, previousOverlays);
            config.SetValue(OsuSetting.ForkSongSelectTransparency, previousSongSelect);
            config.SetValue(OsuSetting.ForkOverlayBlurStrength, previousBlur);
            config.SetValue(OsuSetting.ForkOverlayDimAmount, previousDim);
            OverlayTransparency.Enabled.Value = previousGlobalOverlays;
            OverlayTransparency.SongSelectEnabled.Value = previousGlobalSongSelect;
            OverlayTransparency.BlurStrength.Value = previousGlobalBlur;
            OverlayTransparency.DimAmount.Value = previousGlobalDim;
        });

        [TestCase(OsuSetting.ForkOverlayTransparency)]
        [TestCase(OsuSetting.ForkSongSelectTransparency)]
        [TestCase(OsuSetting.ForkOverlayBlurStrength)]
        [TestCase(OsuSetting.ForkOverlayDimAmount)]
        public void TestSettingsStillReachSharedEffectAfterCollection(OsuSetting setting)
        {
            AddStep("collect unretained copies", collect);
            switch (setting)
            {
                case OsuSetting.ForkOverlayTransparency:
                case OsuSetting.ForkSongSelectTransparency:
                    AddStep("enable setting", () => config.SetValue(setting, true));
                    AddAssert("enabled reaches consumer", () => setting == OsuSetting.ForkOverlayTransparency
                        ? OverlayTransparency.Enabled.Value : OverlayTransparency.SongSelectEnabled.Value);
                    AddStep("collect again", collect);
                    AddStep("disable setting", () => config.SetValue(setting, false));
                    AddAssert("disabled reaches consumer", () => setting == OsuSetting.ForkOverlayTransparency
                        ? !OverlayTransparency.Enabled.Value : !OverlayTransparency.SongSelectEnabled.Value);
                    break;

                default:
                    AddStep("set first strength", () => config.SetValue(setting, 0.75));
                    AddAssert("first strength reaches consumer", () => (setting == OsuSetting.ForkOverlayBlurStrength
                        ? OverlayTransparency.BlurStrength.Value : OverlayTransparency.DimAmount.Value) == 0.75);
                    AddStep("collect again", collect);
                    AddStep("set second strength", () => config.SetValue(setting, 0.25));
                    AddAssert("second strength reaches consumer", () => (setting == OsuSetting.ForkOverlayBlurStrength
                        ? OverlayTransparency.BlurStrength.Value : OverlayTransparency.DimAmount.Value) == 0.25);
                    break;
            }
        }

        private static void collect()
        {
            // Diagnostic test only: production never forces collection.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
