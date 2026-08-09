// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Configuration;
using osu.Game;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkPerformanceSettings : SettingsSubsection
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IPerformanceDiagnosticsManager? diagnosticsManager { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.PerformanceHeader;

        private readonly PerformanceOptimisationSettingsPanel performanceOptimisationSettings;

        public ForkPerformanceSettings(PerformanceOptimisationSettingsPanel performanceOptimisationSettings)
        {
            this.performanceOptimisationSettings = performanceOptimisationSettings;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, FrameworkConfigManager frameworkConfig, OsuColour colours)
        {
            var frameSync = frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
            var restoreFrameSync = config.GetBindable<FrameSync>(OsuSetting.ForkFrameLimiterRestoreMode);
            var uncappedFrameRate = config.GetBindable<bool>(OsuSetting.ForkUncappedFrameRate);
            bool syncingFrameLimiter = false;

            frameSync.BindValueChanged(v =>
            {
                if (syncingFrameLimiter)
                    return;

                syncingFrameLimiter = true;

                if (v.NewValue != FrameSync.Unlimited)
                {
                    restoreFrameSync.Value = v.NewValue;
                    uncappedFrameRate.Value = false;
                }

                syncingFrameLimiter = false;
            }, true);

            uncappedFrameRate.BindValueChanged(v =>
            {
                if (syncingFrameLimiter)
                    return;

                syncingFrameLimiter = true;

                if (v.NewValue)
                {
                    if (frameSync.Value != FrameSync.Unlimited)
                        restoreFrameSync.Value = frameSync.Value;

                    frameSync.Value = FrameSync.Unlimited;
                }
                else if (frameSync.Value == FrameSync.Unlimited)
                {
                    if (restoreFrameSync.Value == FrameSync.Unlimited)
                        frameSync.SetDefault();
                    else
                        frameSync.Value = restoreFrameSync.Value;
                }

                syncingFrameLimiter = false;
            });

            Children = new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.DiagnosticsBenchmarkBtn,
                    Action = () =>
                    {
                        if (diagnosticsManager != null)
                            diagnosticsManager.ShowSetup();
                        else
                            game.ShowPerformanceDiagnosticsSetup();
                    },
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.RecommendedRendererPresetBtn,
                    Action = () => MosuRecommendedPerformancePreset.Apply(config),
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.LimitMenuFps2xCaption,
                    HintText = ForkSettingsStrings.LimitMenuFps2xHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkLimitMenuFps2x)
                })
                {
                    Keywords = new[] { @"fps", @"framerate", @"menu", @"2x", @"limit" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UncappedFpsCaption,
                    HintText = ForkSettingsStrings.UncappedFpsHint,
                    Current = uncappedFrameRate
                })
                {
                    Keywords = new[] { @"fps", @"framerate", @"frametime", @"latency", @"unlimited" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.SkinPerfCaption,
                    HintText = ForkSettingsStrings.SkinPerfHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceMode)
                })
                {
                    Keywords = new[] { @"skin", @"performance", @"hud", @"animation", @"argon", @"fps" },
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.PerformanceOptimisationSettingsButton,
                    Action = performanceOptimisationSettings.ToggleVisibility,
                    BackgroundColour = colours.Pink3,
                    Height = 60,
                },
            };
        }
    }
}
