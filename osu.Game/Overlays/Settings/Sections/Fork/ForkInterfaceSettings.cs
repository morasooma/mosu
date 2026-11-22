// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Settings;
using System;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkInterfaceSettings : SettingsSubsection
    {
        private readonly CustomThemeSettingsPanel? customThemeSettings;
        private readonly SkinVisibilitySettingsPanel? skinVisibility;

        [Resolved]
        private OsuGame? game { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.InterfaceHeader;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        public ForkInterfaceSettings(CustomThemeSettingsPanel? customThemeSettings = null, SkinVisibilitySettingsPanel? skinVisibility = null)
        {
            this.customThemeSettings = customThemeSettings;
            this.skinVisibility = skinVisibility;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, osu.Framework.Platform.Storage storage, IAPIProvider api, OsuColour colours)
        {
            var themeMode = config.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);
            var customThemeJson = config.GetBindable<string>(OsuSetting.ForkCustomThemeJson);

            void syncCustomTheme()
            {
                OverlayColourProvider.CustomTheme.Value = CustomThemeDefinition.FromJson(customThemeJson.Value);
            }

            syncCustomTheme();
            customThemeJson.BindValueChanged(_ => syncCustomTheme());

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

            var songSelectTransparency = config.GetBindable<bool>(OsuSetting.ForkSongSelectTransparency);
            songSelectTransparency.BindValueChanged(e => OverlayTransparency.SongSelectEnabled.Value = e.NewValue, true);
            var songSelectTransparencyItem = new SettingsItemV2(new FormCheckBox
            {
                Caption = SongSelectTransparencyStrings.Caption,
                HintText = SongSelectTransparencyStrings.Hint,
                Current = songSelectTransparency,
            })
            {
                FavouriteId = "ui:SongSelectTransparencyStrings.Caption",
                Keywords = new[] { "transparency", "carousel", "toolbar", "footer", "прозрачность", "карусель" },
            };
            // Carousel glass is independent from overlay glass and remains configurable.

            var themeDropdown = new NamedThemeDropdown(config, includeBuiltInThemes: true)
            {
                Caption = ForkSettingsStrings.ThemeModeCaption,
                HintText = ForkSettingsStrings.ThemeModeHint,
            };

            var disableShear = config.GetBindable<bool>(OsuSetting.ForkDisableInterfaceShear);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = RussianLayoutSearchStrings.Caption,
                    HintText = RussianLayoutSearchStrings.Hint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkRussianLayoutSearch),
                })
                {
                    FavouriteId = "OsuSetting.ForkRussianLayoutSearch",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ServerInvitationStrings.Enabled,
                    HintText = ServerInvitationStrings.EnabledHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkToolbarInvitations),
                })
                {
                    FavouriteId = "OsuSetting.ForkToolbarInvitations",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ServerInvitationStrings.AllModes,
                    HintText = ServerInvitationStrings.AllModesHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkRankedInvitationsAllModes),
                })
                {
                    FavouriteId = "OsuSetting.ForkRankedInvitationsAllModes",
                },
                new FontSettingsPanel(),
                new GameResourceSettings(),
                new SettingsItemV2(new FormEnumDropdown<InterfaceAnimationMode>
                {
                    Caption = InterfaceAnimationStrings.Caption,
                    HintText = InterfaceAnimationStrings.Hint,
                    Current = config.GetBindable<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations),
                })
                {
                    FavouriteId = "OsuSetting.ForkInterfaceAnimations",
                    Keywords = new[] { "animation", "motion", "анимации", "движение" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = InterfaceAnimationStrings.EnhancedCaption,
                    HintText = InterfaceAnimationStrings.EnhancedHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkEnhancedAnimations),
                })
                {
                    FavouriteId = "OsuSetting.ForkEnhancedAnimations",
                    Keywords = new[] { "animation", "settings", "expand", "анимации", "параметры", "раскрытие" },
                },
                new SettingsItemV2(new FormEnumDropdown<ForkBreakStyle>
                {
                    Caption = BreakStyleStrings.Caption,
                    HintText = BreakStyleStrings.Hint,
                    Current = config.GetBindable<ForkBreakStyle>(OsuSetting.ForkBreakStyle),
                })
                {
                    FavouriteId = "OsuSetting.ForkBreakStyle",
                    Keywords = new[] { "break", "legacy", "section", "брейк", "перерыв" },
                },
                new SettingsItemV2(themeDropdown)
                {
                    FavouriteId = "ui:ForkInterfaceSettings:themeDropdown",
                },
                new SettingsButtonV2
                {
                    Text = ImageThemeStrings.Open,
                    Keywords = new[] { "theme", "image", "avatar", "banner", "background", "тема", "фото", "аватар", "баннер", "фон" },
                    Action = () => dialogOverlay?.Push(new ImageThemeDialog()),
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.CustomThemeSettingsButton,
                    Keywords = new[] { @"theme", @"custom", @"custom theme", @"тема", @"кастомная тема", @"цвета", @"colours", @"colors" },
                    Action = () => customThemeSettings?.ToggleVisibility(),
                    BackgroundColour = colours.Pink3,
                    Height = 60,
                },
                new SettingsButtonV2
                {
                    Text = ThemeLibraryStrings.Library,
                    Keywords = new[] { "themes", "library", "community", "темы", "библиотека" },
                    Action = () => game?.ShowThemeLibrary(),
                },
                new SettingsButtonV2
                {
                    Text = SkinVisibilityStrings.Title,
                    Keywords = new[] { "skin", "visibility", "elements", "скин", "видимость", "элементы" },
                    Action = () => skinVisibility?.ToggleVisibility(),
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UseSkinCursorOutsideGameplayCaption,
                    HintText = ForkSettingsStrings.UseSkinCursorOutsideGameplayHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkUseSkinCursorOutsideGameplay)
                })
                {
                    FavouriteId = "OsuSetting.ForkUseSkinCursorOutsideGameplay",
                    Keywords = new[] { @"cursor", @"skin", @"menu", @"gameplay" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OverlayTransparencyCaption,
                    HintText = ForkSettingsStrings.OverlayTransparencyHint,
                    Current = overlayTransparency
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.OverlayTransparencyCaption",
                    Keywords = new[] { @"transparency", @"transparent", @"glass", @"blur", @"overlay" },
                },
                songSelectTransparencyItem,
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.OverlayBlurStrengthCaption,
                    HintText = ForkSettingsStrings.OverlayBlurStrengthHint,
                    Current = overlayBlurStrength,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.OverlayBlurStrengthCaption",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.OverlayDimCaption,
                    HintText = ForkSettingsStrings.OverlayDimHint,
                    Current = overlayDimAmount,
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.OverlayDimCaption",
                },
                new SettingsItemV2(new FormEnumDropdown<ForkMenuLogo>
                {
                    Caption = ForkSettingsStrings.MenuLogoCaption,
                    HintText = ForkSettingsStrings.MenuLogoHint,
                    Current = config.GetBindable<ForkMenuLogo>(OsuSetting.ForkMenuLogo)
                })
                {
                    FavouriteId = "OsuSetting.ForkMenuLogo",
                },
                new SettingsItemV2(new FormEnumDropdown<ForkMenuLogoGradient>
                {
                    Caption = ForkSettingsStrings.MenuLogoGradientCaption,
                    HintText = ForkSettingsStrings.MenuLogoGradientHint,
                    Current = config.GetBindable<ForkMenuLogoGradient>(OsuSetting.ForkMenuLogoGradient)
                })
                {
                    FavouriteId = "OsuSetting.ForkMenuLogoGradient",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.MenuLogoTrianglesCaption,
                    HintText = ForkSettingsStrings.MenuLogoTrianglesHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkMenuLogoTriangles)
                })
                {
                    FavouriteId = "OsuSetting.ForkMenuLogoTriangles",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.DisableInterfaceShearCaption,
                    HintText = ForkSettingsStrings.DisableInterfaceShearHint,
                    Current = disableShear
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.DisableInterfaceShearCaption",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.VisualOD11Caption,
                    HintText = ForkSettingsStrings.VisualOD11Hint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkVisualOD11)
                })
                {
                    FavouriteId = "OsuSetting.ForkVisualOD11",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.HitErrorMeterPositionalMissesCaption,
                    HintText = ForkSettingsStrings.HitErrorMeterPositionalMissesHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkHitErrorMeterShowPositionalMisses)
                })
                {
                    FavouriteId = "OsuSetting.ForkHitErrorMeterShowPositionalMisses",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.DifficultyAdditionalInfoCaption,
                    HintText = ForkSettingsStrings.DifficultyAdditionalInfoHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkDifficultyAdditionalInfo)
                })
                {
                    FavouriteId = "OsuSetting.ForkDifficultyAdditionalInfo",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ShowModsInPresetListCaption,
                    HintText = ForkSettingsStrings.ShowModsInPresetListHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkShowModsInPresetList)
                })
                {
                    FavouriteId = "OsuSetting.ForkShowModsInPresetList",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ModPresetMergeStrings.Caption,
                    HintText = ModPresetMergeStrings.Hint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkMergeModPresetsWithShift)
                })
                {
                    FavouriteId = "OsuSetting.ForkMergeModPresetsWithShift",
                    Keywords = new[] { @"mods", @"presets", @"shift", @"merge", @"моды", @"пресеты", @"объединение" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.EnhancedRankingRowsCaption,
                    HintText = ForkSettingsStrings.EnhancedRankingRowsHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkEnhancedRankingRows)
                })
                {
                    FavouriteId = "OsuSetting.ForkEnhancedRankingRows",
                    Keywords = new[] { @"ranking", @"leaderboard", @"avatar", @"rows", @"top" },
                },

                new SettingsItemV2(new FormEnumDropdown<ForkSongSelectStyle>
                {
                    Caption = ForkSettingsStrings.SongSelectStyleCaption,
                    HintText = ForkSettingsStrings.SongSelectStyleHint,
                    Current = config.GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle)
                })
                {
                    FavouriteId = "OsuSetting.ForkSongSelectStyle",
                    Keywords = new[] { @"song select", @"carousel", @"preview", @"legacy", @"old", @"v1", @"2024", @"beatmap cards", @"classic", @"skin", @"infinite", @"glass", @"zoom", @"pan", @"drift" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OldCarouselPreviewCaption,
                    HintText = ForkSettingsStrings.OldCarouselPreviewHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews)
                })
                {
                    FavouriteId = "OsuSetting.ForkSongSelectOldCarouselPreviews",
                    Keywords = new[] { @"song select", @"carousel", @"preview", @"legacy", @"old", @"beatmap cards" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.AutoHideToolbarCaption,
                    HintText = ForkSettingsStrings.AutoHideToolbarHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAutoHideToolbar)
                })
                {
                    FavouriteId = "OsuSetting.ForkAutoHideToolbar",
                    Keywords = new[] { @"toolbar", @"top bar", @"auto", @"hide", @"reveal", @"hover", @"legacy", @"stable" },
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.CarouselBgDim,
                    Current = config.GetBindable<double>(OsuSetting.ForkSongSelectCarouselBackgroundDim),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                })
                {
                    FavouriteId = "OsuSetting.ForkSongSelectCarouselBackgroundDim",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.StoryboardBgCaption,
                    HintText = ForkSettingsStrings.StoryboardBgHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectStoryboardBackground)
                })
                {
                    FavouriteId = "OsuSetting.ForkSongSelectStoryboardBackground",
                    Keywords = new[] { @"song select", @"storyboard", @"video", @"background", @"sb bg" },
                },
            };
        }

    }
}
