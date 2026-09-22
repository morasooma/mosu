// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using System;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkInterfaceSettings : SettingsSubsection
    {
        [Resolved]
        private OsuGame? game { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.InterfaceHeader;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, osu.Framework.Platform.Storage storage, IAPIProvider api)
        {
            var fontBindable = config.GetBindable<string>(OsuSetting.ForkCustomUIFont);
            var russianFontFix = config.GetBindable<bool>(OsuSetting.ForkRussianFontFix);

            fontBindable.BindValueChanged(e =>
            {
                if (e.NewValue.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || e.NewValue.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                {
                    if (dialogOverlay != null)
                    {
                        Scheduler.Add(() =>
                        {
                            dialogOverlay.Push(new UnsupportedFontDialog(() => fontBindable.Value = e.OldValue ?? "Default"));
                        });
                    }

                    return;
                }

                if (e.OldValue != null && e.NewValue != e.OldValue)
                    russianFontFix.Value = false;
            });

            var themeMode = config.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);

            void applyEffectiveTheme()
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeModeResolver.Resolve(
                    themeMode.Value,
                    api.LocalUser.Value.ForceLightTheme,
                    MosuServerEnvironment.IsThirdPartyServer);
            }

            // Keep the global palette in sync when this subsection is hosted in an
            // isolated settings/test hierarchy, without bypassing the server policy.
            themeMode.BindValueChanged(_ => applyEffectiveTheme(), true);

            // Same reasoning as above for the overlay transparency state.
            var overlayTransparency = config.GetBindable<bool>(OsuSetting.ForkOverlayTransparency);
            var overlayBlurStrength = config.GetBindable<double>(OsuSetting.ForkOverlayBlurStrength);
            var overlayDimAmount = config.GetBindable<double>(OsuSetting.ForkOverlayDimAmount);

            overlayTransparency.BindValueChanged(e => OverlayTransparency.Enabled.Value = e.NewValue, true);
            overlayBlurStrength.BindValueChanged(e => OverlayTransparency.BlurStrength.Value = e.NewValue, true);
            overlayDimAmount.BindValueChanged(e => OverlayTransparency.DimAmount.Value = e.NewValue, true);

            var themeDropdown = new FormEnumDropdown<ThemeMode>
            {
                Caption = ForkSettingsStrings.ThemeModeCaption,
                HintText = ForkSettingsStrings.ThemeModeHint,
                Current = themeMode
            };

            var disableShear = config.GetBindable<bool>(OsuSetting.ForkDisableInterfaceShear);

            fontDropdown = new CustomUIFontDropdown
            {
                Caption = ForkSettingsStrings.CustomUIFontCaption,
                HintText = ForkSettingsStrings.CustomUIFontHint,
                Items = OsuGameBase.AvailableCustomUIFonts,
                Current = config.GetBindable<string>(OsuSetting.ForkCustomUIFont),
            };

            var russianFontFixCheckbox = new FormCheckBox
            {
                Caption = ForkSettingsStrings.RussianFontFixCaption,
                HintText = ForkSettingsStrings.RussianFontFixHint,
                Current = russianFontFix,
            };

            Children = new Drawable[]
            {
                new SettingsItemV2(fontDropdown),
                new SettingsItemV2(russianFontFixCheckbox),
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.FontsFolderBtn,
                    Action = () => storage.GetStorageForDirectory("Fonts").PresentExternally()
                },
                new SettingsItemV2(themeDropdown),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UseSkinCursorOutsideGameplayCaption,
                    HintText = ForkSettingsStrings.UseSkinCursorOutsideGameplayHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkUseSkinCursorOutsideGameplay)
                })
                {
                    Keywords = new[] { @"cursor", @"skin", @"menu", @"gameplay" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OverlayTransparencyCaption,
                    HintText = ForkSettingsStrings.OverlayTransparencyHint,
                    Current = overlayTransparency
                })
                {
                    Keywords = new[] { @"transparency", @"transparent", @"glass", @"blur", @"overlay" },
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.OverlayBlurStrengthCaption,
                    HintText = ForkSettingsStrings.OverlayBlurStrengthHint,
                    Current = overlayBlurStrength,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.OverlayDimCaption,
                    HintText = ForkSettingsStrings.OverlayDimHint,
                    Current = overlayDimAmount,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                new SettingsItemV2(new FormEnumDropdown<ForkMenuLogo>
                {
                    Caption = ForkSettingsStrings.MenuLogoCaption,
                    HintText = ForkSettingsStrings.MenuLogoHint,
                    Current = config.GetBindable<ForkMenuLogo>(OsuSetting.ForkMenuLogo)
                }),
                new SettingsItemV2(new FormEnumDropdown<ForkMenuLogoGradient>
                {
                    Caption = ForkSettingsStrings.MenuLogoGradientCaption,
                    HintText = ForkSettingsStrings.MenuLogoGradientHint,
                    Current = config.GetBindable<ForkMenuLogoGradient>(OsuSetting.ForkMenuLogoGradient)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.MenuLogoTrianglesCaption,
                    HintText = ForkSettingsStrings.MenuLogoTrianglesHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkMenuLogoTriangles)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.DisableInterfaceShearCaption,
                    HintText = ForkSettingsStrings.DisableInterfaceShearHint,
                    Current = disableShear
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.VisualOD11Caption,
                    HintText = ForkSettingsStrings.VisualOD11Hint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkVisualOD11)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.HitErrorMeterPositionalMissesCaption,
                    HintText = ForkSettingsStrings.HitErrorMeterPositionalMissesHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkHitErrorMeterShowPositionalMisses)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.DifficultyAdditionalInfoCaption,
                    HintText = ForkSettingsStrings.DifficultyAdditionalInfoHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkDifficultyAdditionalInfo)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ShowModsInPresetListCaption,
                    HintText = ForkSettingsStrings.ShowModsInPresetListHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkShowModsInPresetList)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.EnhancedRankingRowsCaption,
                    HintText = ForkSettingsStrings.EnhancedRankingRowsHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkEnhancedRankingRows)
                })
                {
                    Keywords = new[] { @"ranking", @"leaderboard", @"avatar", @"rows", @"top" },
                },

                new SettingsItemV2(new FormEnumDropdown<ForkSongSelectStyle>
                {
                    Caption = ForkSettingsStrings.SongSelectStyleCaption,
                    HintText = ForkSettingsStrings.SongSelectStyleHint,
                    Current = config.GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle)
                })
                {
                    Keywords = new[] { @"song select", @"carousel", @"preview", @"legacy", @"old", @"v1", @"2024", @"beatmap cards", @"classic", @"skin", @"infinite", @"glass", @"zoom", @"pan", @"drift" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OldCarouselPreviewCaption,
                    HintText = ForkSettingsStrings.OldCarouselPreviewHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews)
                })
                {
                    Keywords = new[] { @"song select", @"carousel", @"preview", @"legacy", @"old", @"beatmap cards" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.AutoHideToolbarCaption,
                    HintText = ForkSettingsStrings.AutoHideToolbarHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAutoHideToolbar)
                })
                {
                    Keywords = new[] { @"toolbar", @"top bar", @"auto", @"hide", @"reveal", @"hover", @"legacy", @"stable" },
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.CarouselBgDim,
                    Current = config.GetBindable<double>(OsuSetting.ForkSongSelectCarouselBackgroundDim),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.StoryboardBgCaption,
                    HintText = ForkSettingsStrings.StoryboardBgHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectStoryboardBackground)
                })
                {
                    Keywords = new[] { @"song select", @"storyboard", @"video", @"background", @"sb bg" },
                },
            };
        }

        private FormDropdown<string> fontDropdown = null!;

        private partial class CustomUIFontDropdown : FormDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item)
                => OsuGameBase.BuiltInCustomUIFonts.Contains(item) ? $"{item} (built-in)" : base.GenerateItemText(item);
        }

        protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
        {
            if (game != null)
            {
                game.RefreshCustomFontList();
                fontDropdown.Items = OsuGameBase.AvailableCustomUIFonts;
            }
            return base.OnHover(e);
        }
    }
}
