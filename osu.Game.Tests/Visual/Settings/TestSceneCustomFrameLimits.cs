using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Online.API;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneCustomFrameLimits : OsuGameTestScene
    {
        private GameHost host => Dependencies.Get<GameHost>();
        private FrameworkConfigManager frameworkConfig => Dependencies.Get<FrameworkConfigManager>();

        protected override TestOsuGame CreateTestGame() => new FrameLimitTestGame(LocalStorage, API);

        private partial class FrameLimitTestGame : TestOsuGame
        {
            public FrameLimitTestGame(Storage storage, IAPIProvider api)
                : base(storage, api)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                LocalConfig.SetValue(OsuSetting.ForkLibraryLocationChosen, true);
            }
        }

        [Test]
        public void TestLiveLimitsAndRestoringFrameworkMode()
        {
            AddStep("enable uncapped mode", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkLimitMenuFps2x, false);
                frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);
                Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, true);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, false);
            });
            AddUntilStep("original uncapped limits", () => host.MaximumDrawHz == int.MaxValue && host.MaximumUpdateHz == 8000);
            AddStep("set independent custom limits", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomDrawHz, 237);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomUpdateHz, 2000);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, true);
            });
            AddUntilStep("custom limits applied", () => host.MaximumDrawHz == 237 && host.MaximumUpdateHz == 2000);
            AddStep("change limits live", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomDrawHz, 1440);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomUpdateHz, 4000);
            });
            AddUntilStep("above framework ceiling", () => host.MaximumDrawHz == 1440 && host.MaximumUpdateHz == 4000);
            AddStep("zero means unlimited", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomDrawHz, 0);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomUpdateHz, 0);
            });
            AddUntilStep("both unlimited", () => host.MaximumDrawHz == int.MaxValue && host.MaximumUpdateHz == int.MaxValue);
            AddStep("menu cap takes priority", () => Game.LocalConfig.SetValue(OsuSetting.ForkLimitMenuFps2x, true));
            AddUntilStep("menu capped", () => !host.AllowBenchmarkUnlimitedFrames && host.MaximumDrawHz == refreshRate * 2 && host.MaximumUpdateHz == refreshRate * 4);
            AddStep("disable menu cap", () => Game.LocalConfig.SetValue(OsuSetting.ForkLimitMenuFps2x, false));
            AddUntilStep("custom unlimited restored", () => host.AllowBenchmarkUnlimitedFrames && host.MaximumDrawHz == int.MaxValue && host.MaximumUpdateHz == int.MaxValue);
            AddStep("disable custom limits", () => Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, false));
            AddUntilStep("original limits restored", () => host.MaximumDrawHz == int.MaxValue && host.MaximumUpdateHz == 8000);
            AddStep("use low custom draw limit", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomDrawHz, 120);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, true);
            });
            AddUntilStep("low custom limit applied", () => host.MaximumDrawHz == 120);
            AddStep("disable uncapped mode", () => Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, false));
            AddUntilStep("framework unlimited ceiling restored", () => !host.AllowBenchmarkUnlimitedFrames && host.MaximumDrawHz == 1000 && host.MaximumUpdateHz == 1000);
            AddStep("select standard 2x mode", () => frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Limit2x));
            AddUntilStep("standard mode restored", () => !host.AllowBenchmarkUnlimitedFrames && (host.Window == null
                ? host.MaximumDrawHz == 1000 && host.MaximumUpdateHz == 1000
                : host.MaximumDrawHz == Math.Min(1000, refreshRate * 2) && host.MaximumUpdateHz == Math.Min(1000, refreshRate * 4)));
        }

        [Test]
        public void TestInputAndAudioLimitsSurvivePolicyChangesAndRestore()
        {
            double originalAudioHz = 0, inputInactiveHz = 0, audioInactiveHz = 0;
            AddStep("capture defaults and enable all thread limits", () =>
            {
                originalAudioHz = host.AudioThread.ActiveHz;
                inputInactiveHz = host.InputThread.InactiveHz;
                audioInactiveHz = host.AudioThread.InactiveHz;
                frameworkConfig.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.MultiThreaded);
                frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);
                Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, true);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomDrawHz, 300);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomUpdateHz, 2000);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomInputHz, 4321);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomAudioHz, 500);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, true);
            });
            AddUntilStep("Input and Audio rates applied", () => host.MaximumInputHz == 4321 && host.InputThread.ActiveHz == 4321 && host.AudioThread.ActiveHz == 500);
            AddUntilStep("real throttling clocks updated", () => host.InputThread.Clock.MaximumUpdateHz == 4321 && host.AudioThread.Clock.MaximumUpdateHz == 500);
            AddAssert("background rates unchanged", () => host.InputThread.InactiveHz == inputInactiveHz && host.AudioThread.InactiveHz == audioInactiveHz);
            AddStep("collect temporary config bindings", () => GC.Collect());
            AddStep("enable 8k input option", () => Game.LocalConfig.SetValue(OsuSetting.ForkUse8kPollingRate, true));
            AddAssert("8k option reached runner", () => osu.Framework.Platform.ThreadRunner.Use8kInputRate);
            AddAssert("custom Input still wins", () => host.InputThread.ActiveHz == 4321);
            AddStep("change Update independently", () => Game.LocalConfig.SetValue(OsuSetting.ForkCustomUpdateHz, 3000));
            AddUntilStep("Input independent of Update", () => host.MaximumUpdateHz == 3000 && host.InputThread.ActiveHz == 4321);
            AddStep("enable menu cap", () => Game.LocalConfig.SetValue(OsuSetting.ForkLimitMenuFps2x, true));
            AddUntilStep("menu changes only Draw and Update", () => host.MaximumDrawHz == refreshRate * 2 && host.InputThread.ActiveHz == 4321 && host.AudioThread.ActiveHz == 500);
            AddStep("disable menu cap", () => Game.LocalConfig.SetValue(OsuSetting.ForkLimitMenuFps2x, false));
            AddStep("use shared single-thread loop", () => frameworkConfig.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.SingleThread));
            AddUntilStep("shared loop follows Update", () => host.InputThread.ActiveHz == 3000);
            AddStep("return to independent threads", () => frameworkConfig.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.MultiThreaded));
            AddUntilStep("Input override survives mode switch", () => host.InputThread.ActiveHz == 4321 && host.AudioThread.ActiveHz == 500);
            AddStep("unlimited Input and Audio", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomInputHz, 0);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomAudioHz, 0);
            });
            AddUntilStep("zero reaches thread clocks", () => host.InputThread.ActiveHz == 0 && host.AudioThread.ActiveHz == 0
                && host.InputThread.Clock.MaximumUpdateHz == 0 && host.AudioThread.Clock.MaximumUpdateHz == 0);
            AddStep("disable custom limits", () => Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, false));
            AddUntilStep("Input override cleared", () => host.MaximumInputHz == null);
            AddAssert("normal 8k Input restored", () => host.InputThread.ActiveHz, () => Is.EqualTo(8000));
            AddAssert("original Audio restored", () => host.AudioThread.ActiveHz, () => Is.EqualTo(originalAudioHz));
            AddStep("restore ordinary input option", () => Game.LocalConfig.SetValue(OsuSetting.ForkUse8kPollingRate, false));
            AddUntilStep("normal 1k input restored", () => host.InputThread.ActiveHz == 1000);
            AddStep("reenable custom limits", () => Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, true));
            AddUntilStep("stored unlimited rates reapplied", () => host.InputThread.ActiveHz == 0 && host.AudioThread.ActiveHz == 0);
            AddStep("disable uncapped parent", () => Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, false));
            AddUntilStep("parent restores Input and Audio", () => host.MaximumInputHz == null && host.InputThread.ActiveHz == 1000 && host.AudioThread.ActiveHz == originalAudioHz);
            AddStep("enable parent again", () => Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, true));
            AddUntilStep("parent reapplies custom rates", () => host.InputThread.ActiveHz == 0 && host.AudioThread.ActiveHz == 0);
            AddStep("leave unlimited frame sync", () => frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Limit2x));
            AddUntilStep("frame sync restores Input and Audio", () => host.MaximumInputHz == null && host.InputThread.ActiveHz == 1000 && host.AudioThread.ActiveHz == originalAudioHz);
        }

        private int refreshRate => Math.Max(1, (int)MathF.Round(host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60));

        [Test]
        public void TestConditionalControlsAndConfigurationBinding()
        {
            ForkPerformanceSettings subsection = null!;
            SettingsItemV2 custom = null!;
            SettingsItemV2[] limits = Array.Empty<SettingsItemV2>();
            var settings = new[] { OsuSetting.ForkCustomDrawHz, OsuSetting.ForkCustomUpdateHz, OsuSetting.ForkCustomInputHz, OsuSetting.ForkCustomAudioHz };
            AddStep("reset options", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, false);
                Game.LocalConfig.SetValue(OsuSetting.ForkCustomFrameLimits, false);
            });
            AddUntilStep("settings ready", () => Game.Settings.IsLoaded);
            AddStep("open settings", () => Game.Settings.Show());
            AddUntilStep("subsection loaded", () =>
            {
                subsection = Game.Settings.ChildrenOfType<ForkPerformanceSettings>().SingleOrDefault()!;
                return subsection?.IsLoaded == true;
            });
            AddStep("find conditional rows", () =>
            {
                custom = subsection.ChildrenOfType<SettingsItemV2>().Single(i => i.FavouriteId == "OsuSetting.ForkCustomFrameLimits");
                limits = settings.Select(setting => subsection.ChildrenOfType<SettingsItemV2>().Single(i => i.FavouriteId == $"OsuSetting.{setting}")).ToArray();
            });
            AddAssert("rows hidden without uncapped mode", () => !custom.CanBeShown.Value && limits.All(i => !i.CanBeShown.Value));
            AddStep("enable uncapped mode", () => Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, true));
            AddAssert("only custom toggle available", () => custom.CanBeShown.Value && limits.All(i => !i.CanBeShown.Value));
            AddStep("enable via checkbox", () => ((FormCheckBox)custom.Control).Current.Value = true);
            AddAssert("all four limits available", () => limits.All(i => i.CanBeShown.Value));
            AddStep("set controls", () =>
            {
                for (int i = 0; i < limits.Length; i++)
                    ((FormSliderBar<int>)limits[i].Control).Current.Value = 321 + i;
            });
            AddAssert("all four values reach configuration", () => settings.Select((setting, i) => Game.LocalConfig.Get<int>(setting) == 321 + i).All(matches => matches));
            AddStep("disable custom checkbox", () => ((FormCheckBox)custom.Control).Current.Value = false);
            AddAssert("limits hidden but retained", () => limits.All(i => !i.CanBeShown.Value) && Game.LocalConfig.Get<int>(OsuSetting.ForkCustomInputHz) == 323 && Game.LocalConfig.Get<int>(OsuSetting.ForkCustomAudioHz) == 324);
            AddStep("disable uncapped mode", () => Game.LocalConfig.SetValue(OsuSetting.ForkUncappedFrameRate, false));
            AddAssert("all conditional rows hidden", () => !custom.CanBeShown.Value && limits.All(i => !i.CanBeShown.Value));
        }
    }
}
