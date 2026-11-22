// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
using osu.Game.Overlays.Settings.Sections.Graphics;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkPerformanceSettings : SettingsSubsection
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IPerformanceDiagnosticsManager? diagnosticsManager { get; set; }

        [Resolved(CanBeNull = true)]
        private ILinuxGamePriority? linuxGamePriority { get; set; }

        private IBindable<LinuxGamePriorityStatus>? linuxPriorityStatus;

        protected override LocalisableString Header => ForkSettingsStrings.PerformanceHeader;

        private readonly PerformanceOptimisationSettingsPanel performanceOptimisationSettings;

        private readonly BackgroundCalculationSettingsPanel? backgroundCalculations;

        public ForkPerformanceSettings(PerformanceOptimisationSettingsPanel performanceOptimisationSettings, BackgroundCalculationSettingsPanel? backgroundCalculations = null)
        {
            this.performanceOptimisationSettings = performanceOptimisationSettings;
            this.backgroundCalculations = backgroundCalculations;
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

            var customFrameLimits = config.GetBindable<bool>(OsuSetting.ForkCustomFrameLimits);
            var customFrameLimitsItem = new SettingsItemV2(new FormCheckBox
            {
                Caption = FrameLimitSettingsStrings.CustomLimits,
                HintText = FrameLimitSettingsStrings.CustomLimitsHint,
                Current = customFrameLimits,
            })
            {
                FavouriteId = "OsuSetting.ForkCustomFrameLimits",
                Keywords = new[] { @"fps", @"update", @"custom", @"limit" },
            };
            var customDrawLimitItem = new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = FrameLimitSettingsStrings.DrawLimit,
                HintText = FrameLimitSettingsStrings.DrawLimitHint,
                Current = config.GetBindable<int>(OsuSetting.ForkCustomDrawHz),
                KeyboardStep = 1,
                TransferValueOnCommit = true,
                LabelFormat = value => value == 0 ? FrameLimitSettingsStrings.Unlimited : value.ToString(),
            })
            {
                FavouriteId = "OsuSetting.ForkCustomDrawHz",
                Keywords = new[] { @"fps", @"draw", @"render", @"limit" },
            };
            var customUpdateLimitItem = new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = FrameLimitSettingsStrings.UpdateLimit,
                HintText = FrameLimitSettingsStrings.UpdateLimitHint,
                Current = config.GetBindable<int>(OsuSetting.ForkCustomUpdateHz),
                KeyboardStep = 1,
                TransferValueOnCommit = true,
                LabelFormat = value => value == 0 ? FrameLimitSettingsStrings.Unlimited : value.ToString(),
            })
            {
                FavouriteId = "OsuSetting.ForkCustomUpdateHz",
                Keywords = new[] { @"ups", @"hz", @"update", @"limit" },
            };

            var customInputLimitItem = new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = FrameLimitSettingsStrings.InputLimit,
                HintText = FrameLimitSettingsStrings.InputLimitHint,
                Current = config.GetBindable<int>(OsuSetting.ForkCustomInputHz),
                KeyboardStep = 1,
                TransferValueOnCommit = true,
                LabelFormat = value => value == 0 ? FrameLimitSettingsStrings.Unlimited : value.ToString(),
            })
            {
                FavouriteId = "OsuSetting.ForkCustomInputHz",
                Keywords = new[] { @"input", @"hz", @"polling", @"limit" },
            };
            var customAudioLimitItem = new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = FrameLimitSettingsStrings.AudioLimit,
                HintText = FrameLimitSettingsStrings.AudioLimitHint,
                Current = config.GetBindable<int>(OsuSetting.ForkCustomAudioHz),
                KeyboardStep = 1,
                TransferValueOnCommit = true,
                LabelFormat = value => value == 0 ? FrameLimitSettingsStrings.Unlimited : value.ToString(),
            })
            {
                FavouriteId = "OsuSetting.ForkCustomAudioHz",
                Keywords = new[] { @"audio", @"hz", @"sound", @"limit" },
            };

            void updateCustomLimitsVisibility()
            {
                customFrameLimitsItem.CanBeShown.Value = uncappedFrameRate.Value;
                bool showLimits = uncappedFrameRate.Value && customFrameLimits.Value;
                customDrawLimitItem.CanBeShown.Value = showLimits;
                customUpdateLimitItem.CanBeShown.Value = showLimits;
                customInputLimitItem.CanBeShown.Value = showLimits;
                customAudioLimitItem.CanBeShown.Value = showLimits;
            }

            uncappedFrameRate.BindValueChanged(_ => updateCustomLimitsVisibility(), true);
            customFrameLimits.BindValueChanged(_ => updateCustomLimitsVisibility());

            var linuxPriorityItem = new SettingsItemV2(new FormCheckBox
            {
                Caption = LinuxGamePriorityStrings.Caption,
                HintText = LinuxGamePriorityStrings.Hint,
                Current = config.GetBindable<bool>(OsuSetting.ForkLinuxGamePriority),
            })
            {
                FavouriteId = "OsuSetting.ForkLinuxGamePriority",
                Keywords = new[] { @"linux", @"priority", @"gamemode", @"cpu", @"stutter" },
            };
            linuxPriorityItem.CanBeShown.Value = OperatingSystem.IsLinux();
            if (OperatingSystem.IsLinux())
            {
                if (linuxGamePriority != null)
                {
                    linuxPriorityStatus = linuxGamePriority.Status.GetBoundCopy();
                    linuxPriorityStatus.BindValueChanged(v => Schedule(() =>
                        linuxPriorityItem.Note.Value = new SettingsNote.Data(LinuxGamePriorityStrings.Status(v.NewValue),
                            v.NewValue is LinuxGamePriorityStatus.Unavailable or LinuxGamePriorityStatus.RestoreFailed
                                ? SettingsNote.Type.Warning : SettingsNote.Type.Informational)), true);
                }
                else
                    linuxPriorityItem.Note.Value = new SettingsNote.Data(LinuxGamePriorityStrings.Status(LinuxGamePriorityStatus.Unavailable), SettingsNote.Type.Warning);
            }

            Children = new Drawable[]
            {
                new GraphicsAdapterSettings(),
                linuxPriorityItem,
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
                    FavouriteId = "OsuSetting.ForkLimitMenuFps2x",
                    Keywords = new[] { @"fps", @"framerate", @"menu", @"2x", @"limit" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UncappedFpsCaption,
                    HintText = ForkSettingsStrings.UncappedFpsHint,
                    Current = uncappedFrameRate
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.UncappedFpsCaption",
                    Keywords = new[] { @"fps", @"framerate", @"frametime", @"latency", @"unlimited" },
                },
                customFrameLimitsItem,
                customDrawLimitItem,
                customUpdateLimitItem,
                customInputLimitItem,
                customAudioLimitItem,
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = BackgroundCalculationStrings.ModdedCarouselRatings,
                    HintText = BackgroundCalculationStrings.ModdedCarouselRatingsHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectModdedRatings),
                })
                {
                    FavouriteId = "OsuSetting.ForkSongSelectModdedRatings",
                    Keywords = new[] { @"carousel", @"sort", @"filter", @"mods", @"sr", @"pp", @"experimental" },
                },
                new SettingsButtonV2
                {
                    Text = BackgroundCalculationStrings.Title,
                    Action = () => backgroundCalculations?.ToggleVisibility(),
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.SkinPerfCaption,
                    HintText = ForkSettingsStrings.SkinPerfHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceMode)
                })
                {
                    FavouriteId = "OsuSetting.ForkSkinPerformanceMode",
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

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                linuxPriorityStatus?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
