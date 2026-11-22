using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Overlays.Toolbar;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>
    /// Nested settings panel for creating, editing, and using custom color themes live.
    /// </summary>
    public partial class CustomThemeSettingsPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader() =>
            new SettingsHeader(ForkSettingsStrings.CustomThemeSettingsHeader, ForkSettingsStrings.CustomThemeSettingsDescription);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            AddSection(new CommunityThemeSection());
            AddSection(new CustomThemeModeSection(config, colours));
            AddSection(new ThemeInterfaceSection());
            AddSection(new CustomThemeAccentsSection(config));
            AddSection(new CustomThemeTextSection(config));
            AddSection(new CustomThemeSurfacesSection(config));
            AddSection(new CustomThemeContextColoursSection(config));
            AddSection(new CustomThemeRadiiSection(config));
            AddSection(new CustomThemeTopBarSection(config));
        }

        private partial class CustomThemeModeSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeBaseModeCaption;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.Palette
            };

            private readonly OsuConfigManager config;
            private readonly OsuColour colours;
            private readonly IBindable<CustomThemeDefinition> customThemeBindable;
            private SettingsButtonV2 applyButton = null!;
            private OsuSpriteText statusText = null!;
            private readonly Bindable<string> themeName = new Bindable<string>();
            private readonly IBindable<int> libraryChanged;
            private readonly IBindable<string> workingThemeJson;
            private SettingsButtonV2 deleteButton = null!;

            [Resolved(canBeNull: true)]
            private Storage? storage { get; set; }

            [Resolved(canBeNull: true)]
            private IDialogOverlay? dialogOverlay { get; set; }

            [Resolved(canBeNull: true)]
            private INotificationOverlay? notificationOverlay { get; set; }

            public CustomThemeModeSection(OsuConfigManager config, OsuColour colours)
            {
                this.config = config;
                this.colours = colours;
                customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
                libraryChanged = config.CustomThemes.Changed.GetBoundCopy();
                workingThemeJson = config.GetBindable<string>(OsuSetting.ForkCustomThemeJson);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var themeMode = config.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);
                // Also support an isolated settings hierarchy (without OsuGameBase's binding).
                workingThemeJson.BindValueChanged(e => OverlayColourProvider.CustomTheme.Value = CustomThemeDefinition.FromJson(e.NewValue));

                var baseThemeBindable = new Bindable<ThemeMode>(
                    OverlayColourProvider.CustomTheme.Value?.BaseTheme ?? ThemeMode.Default);

                baseThemeBindable.BindValueChanged(e =>
                {
                    if (e.NewValue == ThemeMode.Custom)
                    {
                        baseThemeBindable.Value = ThemeMode.Default;
                        return;
                    }

                    var current = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();

                    if (current.BaseTheme != e.NewValue)
                    {
                        current.BaseTheme = e.NewValue;
                        config.SetValue(OsuSetting.ForkCustomThemeJson, current.ToJson());
                        OverlayColourProvider.CustomTheme.Value = current;
                    }
                });

                // Keep local base theme bindable updated if theme changes externally
                customThemeBindable.BindValueChanged(e =>
                {
                    if (e.NewValue != null && baseThemeBindable.Value != e.NewValue.BaseTheme && e.NewValue.BaseTheme != ThemeMode.Custom)
                        baseThemeBindable.Value = e.NewValue.BaseTheme;
                });

                var baseDropdown = new FormEnumDropdown<ThemeMode>
                {
                    Caption = ForkSettingsStrings.CustomThemeBaseModeCaption,
                    HintText = ForkSettingsStrings.CustomThemeBaseModeHint,
                    Current = baseThemeBindable,
                    Items = new[] { ThemeMode.Default, ThemeMode.Light, ThemeMode.Dark },
                };

                applyButton = new SettingsButtonV2
                {
                    Action = () => themeMode.Value = ThemeMode.Custom,
                };

                statusText = new OsuSpriteText
                {
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_PADDING.Left, Vertical = 4 },
                    Font = OsuFont.GetFont(weight: FontWeight.Regular, size: 13),
                };

                themeMode.BindValueChanged(e =>
                {
                    bool isCustom = e.NewValue == ThemeMode.Custom;
                    applyButton.Text = isCustom
                        ? ForkSettingsStrings.CustomThemeActiveButton
                        : ForkSettingsStrings.CustomThemeApplyButton;
                    applyButton.BackgroundColour = isCustom ? colours.Green : colours.Pink3;
                    statusText.Text = isCustom
                        ? ForkSettingsStrings.CustomThemeStatusActive
                        : ForkSettingsStrings.CustomThemeStatusInactive;
                    statusText.Colour = isCustom ? colours.Green : colours.Yellow;
                }, true);

                themeName.Value = config.CustomThemes.Active.Name;
                libraryChanged.BindValueChanged(_ =>
                {
                    themeName.Value = config.CustomThemes.Active.Name;
                    deleteButton.Enabled.Value = config.CustomThemes.Entries.Count > 1;
                });

                Children = new Drawable[]
                {
                    new SettingsItemV2(new NamedThemeDropdown(config)
                    {
                        Caption = NamedThemeStrings.SavedThemes,
                    })
                    {
                        FavouriteId = "ui:CustomThemeSettingsPanel:savedThemes",
                    },
                    new SettingsItemV2(new FormTextBox
                    {
                        Caption = NamedThemeStrings.Name,
                        HintText = NamedThemeStrings.AutoSaveHint,
                        PlaceholderText = ForkSettingsStrings.ThemeModeCustom,
                        Current = themeName,
                        LengthLimit = CustomThemeLibrary.MAX_NAME_LENGTH,
                    })
                    {
                        FavouriteId = "ui:CustomThemeSettingsPanel:themeName",
                    },
                    new SettingsButtonV2
                    {
                        Text = NamedThemeStrings.Create,
                        Action = () =>
                        {
                            if (config.CustomThemes.Create(themeName.Value))
                                themeMode.Value = ThemeMode.Custom;
                            else
                                showInvalidName();
                        },
                    },
                    new SettingsButtonV2
                    {
                        Text = NamedThemeStrings.Rename,
                        Action = () =>
                        {
                            if (!config.CustomThemes.Rename(themeName.Value))
                                showInvalidName();
                        },
                    },
                    deleteButton = new SettingsButtonV2
                    {
                        Text = NamedThemeStrings.Delete,
                        Enabled = { Value = config.CustomThemes.Entries.Count > 1 },
                        Action = () =>
                        {
                            dialogOverlay?.Push(new DeleteNamedThemeDialog(
                                string.IsNullOrEmpty(config.CustomThemes.Active.Name)
                                    ? ForkSettingsStrings.ThemeModeCustom
                                    : config.CustomThemes.Active.Name,
                                config.CustomThemes.DeleteActive));
                        },
                    },
                    applyButton,
                    statusText,
                    new SettingsItemV2(baseDropdown)
                    {
                        FavouriteId = "ui:CustomThemeSettingsPanel:baseDropdown",
                    },
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetAllButton,
                        Action = resetAllOverrides,
                    },
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeExportButton,
                        Action = exportTheme,
                    },
                    new ImportThemeButton(importThemeFromFile, () => storage?.GetStorageForDirectory("exports")?.GetFullPath(string.Empty)),
                };
            }

            private void showInvalidName() => notificationOverlay?.Post(new SimpleErrorNotification
            {
                Text = NamedThemeStrings.InvalidName,
            });

            private partial class DeleteNamedThemeDialog : PopupDialog
            {
                public DeleteNamedThemeDialog(LocalisableString name, Action onConfirm)
                {
                    HeaderText = NamedThemeStrings.Delete;
                    BodyText = NamedThemeStrings.DeleteConfirmation(name);
                    Icon = FontAwesome.Solid.Trash;
                    Buttons = new PopupDialogButton[]
                    {
                        new PopupDialogOkButton { Text = NamedThemeStrings.Delete, Action = onConfirm },
                        new PopupDialogCancelButton(),
                    };
                }
            }

            private void exportTheme()
            {
                if (storage == null)
                    return;

                var currentTheme = config.CustomThemes.GetActiveThemeForSharing();
                var exportStorage = storage.GetStorageForDirectory("exports");

                if (ThemeShareManager.TryExportTheme(currentTheme, exportStorage, config.CustomThemes.Active.Name, out string fileName, out string? error))
                {
                    notificationOverlay?.Post(new SimpleNotification
                    {
                        Text = ForkSettingsStrings.CustomThemeExportSuccess(fileName),
                        Icon = FontAwesome.Solid.Check,
                        Activated = () =>
                        {
                            exportStorage.PresentFileExternally(fileName);
                            return true;
                        }
                    });
                }
                else
                {
                    notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = ForkSettingsStrings.CustomThemeExportFailed(error ?? "Unknown error"),
                    });
                }
            }

            private void importThemeFromFile(FileInfo file)
            {
                try
                {
                    using var stream = file.OpenRead();
                    if (ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out string themeName, out var errorMessage))
                    {
                        if (dialogOverlay == null)
                        {
                            notificationOverlay?.Post(new SimpleErrorNotification
                            {
                                Text = ForkSettingsStrings.CustomThemeImportDialogUnavailable,
                            });
                            return;
                        }

                        int totalOverrides = (importedTheme!.Overrides?.Count ?? 0)
                            + (importedTheme.ContextOverrides?.Values.Sum(v => v?.Count ?? 0) ?? 0)
                            + (importedTheme.ModTypeOverrides?.Count ?? 0);

                        if (importedTheme.Geometry != null)
                        {
                            if (importedTheme.Geometry.ButtonRadius.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.InputRadius.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.CardRadius.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.PanelRadius.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.PopupRadius.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.TopBarRadius.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.TopBarVariant.HasValue) totalOverrides++;
                            if (importedTheme.Geometry.EnableGradients.HasValue) totalOverrides++;
                        }

                        dialogOverlay.Push(new ThemeImportConfirmationDialog(
                            themeName,
                            importedTheme.BaseTheme,
                            totalOverrides,
                            () => applyImportedTheme(importedTheme, themeName)));
                    }
                    else
                    {
                        notificationOverlay?.Post(new SimpleErrorNotification
                        {
                            Text = ForkSettingsStrings.CustomThemeImportFailed(errorMessage),
                        });
                    }
                }
                catch (Exception ex)
                {
                    notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = ForkSettingsStrings.CustomThemeImportFailed(ex.Message),
                    });
                }
            }

            private void applyImportedTheme(CustomThemeDefinition theme, string themeName)
            {
                if (!config.CustomThemes.Create(themeName, theme))
                {
                    showInvalidName();
                    return;
                }

                OverlayColourProvider.CustomTheme.Value = theme;
                config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);

                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = ForkSettingsStrings.CustomThemeImportSuccess(themeName),
                    Icon = FontAwesome.Solid.Check,
                });
            }

            private partial class ImportThemeButton : SettingsButtonV2, IHasPopover
            {
                private readonly Bindable<FileInfo?> selectedFile = new Bindable<FileInfo?>();
                private readonly Action<FileInfo> onFileSelected;
                private readonly Func<string?> initialPathProvider;

                public ImportThemeButton(Action<FileInfo> onFileSelected, Func<string?> initialPathProvider)
                {
                    Text = ForkSettingsStrings.CustomThemeImportButton;
                    this.onFileSelected = onFileSelected;
                    this.initialPathProvider = initialPathProvider;

                    Action = this.ShowPopover;

                    selectedFile.BindValueChanged(e =>
                    {
                        if (e.NewValue != null)
                        {
                            this.HidePopover();
                            onFileSelected(e.NewValue);
                            selectedFile.Value = null;
                        }
                    });
                }

                public Popover GetPopover()
                {
                    return new FormFileSelector.FileChooserPopover(new[] { ".json" }, selectedFile, initialPathProvider(), allowClear: false);
                }
            }

            private void resetAllOverrides()
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.ClearAllOverrides();
                config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                OverlayColourProvider.CustomTheme.Value = theme;
            }
        }

        private partial class CustomThemeAccentsSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeSectionAccents;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.Magic
            };

            public CustomThemeAccentsSection(OsuConfigManager config)
            {
                Children = new Drawable[]
                {
                    new CustomThemeColourRow(
                        OverlayColour.Colour1,
                        ForkSettingsStrings.CustomThemeColourAccent,
                        ForkSettingsStrings.CustomThemeColourAccentHint,
                        config),
                    new CustomThemeColourRow(
                        OverlayColour.Highlight1,
                        ForkSettingsStrings.CustomThemeColourHighlight,
                        ForkSettingsStrings.CustomThemeColourHighlightHint,
                        config),
                };
            }
        }

        private partial class CustomThemeTextSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeSectionText;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.Font
            };

            public CustomThemeTextSection(OsuConfigManager config)
            {
                Children = new Drawable[]
                {
                    new CustomThemeColourRow(
                        OverlayColour.Content1,
                        ForkSettingsStrings.CustomThemeColourContent1,
                        ForkSettingsStrings.CustomThemeColourContent1Hint,
                        config),
                    new CustomThemeColourRow(
                        OverlayColour.Content2,
                        ForkSettingsStrings.CustomThemeColourContent2,
                        ForkSettingsStrings.CustomThemeColourContent2Hint,
                        config),
                };
            }
        }

        private partial class CustomThemeSurfacesSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeSectionSurfaces;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.LayerGroup
            };

            public CustomThemeSurfacesSection(OsuConfigManager config)
            {
                Children = new Drawable[]
                {
                    new CustomThemeColourRow(
                        OverlayColour.Background6,
                        ForkSettingsStrings.CustomThemeColourBackground6,
                        ForkSettingsStrings.CustomThemeColourBackground6Hint,
                        config),
                    new CustomThemeColourRow(
                        OverlayColour.Background4,
                        ForkSettingsStrings.CustomThemeColourBackground4,
                        ForkSettingsStrings.CustomThemeColourBackground4Hint,
                        config),
                    new CustomThemeColourRow(
                        OverlayColour.Background3,
                        ForkSettingsStrings.CustomThemeColourBackground3,
                        ForkSettingsStrings.CustomThemeColourBackground3Hint,
                        config),
                    new CustomThemeColourRow(
                        OverlayColour.Background2,
                        ForkSettingsStrings.CustomThemeColourBackground2,
                        ForkSettingsStrings.CustomThemeColourBackground2Hint,
                        config),
                    new CustomThemeColourRow(
                        OverlayColour.Foreground1,
                        ForkSettingsStrings.CustomThemeColourForeground1,
                        ForkSettingsStrings.CustomThemeColourForeground1Hint,
                        config),
                };
            }
        }

        private partial class CustomThemeContextColoursSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeSectionContexts;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.Desktop
            };

            private readonly OsuConfigManager config;
            private readonly IBindable<CustomThemeDefinition> customThemeBindable;

            public CustomThemeContextColoursSection(OsuConfigManager config)
            {
                this.config = config;
                customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();

                Children = new Drawable[]
                {
                    // Top Bar
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextTopBar),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextTopBarBg, ForkSettingsStrings.CustomThemeContextTopBarBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceElevated,
                        ForkSettingsStrings.CustomThemeContextTopBarElevated, ForkSettingsStrings.CustomThemeContextTopBarElevatedHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.TopBar, SurfaceColourRole.ControlHover,
                        ForkSettingsStrings.CustomThemeContextTopBarControlHover, ForkSettingsStrings.CustomThemeContextTopBarControlHoverHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.TopBar, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextTopBarAccent, ForkSettingsStrings.CustomThemeContextTopBarAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.TopBar, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeContextTopBarTextPrimary, ForkSettingsStrings.CustomThemeContextTopBarTextPrimaryHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.TopBar, SurfaceColourRole.TextSecondary,
                        ForkSettingsStrings.CustomThemeContextTopBarTextSecondary, ForkSettingsStrings.CustomThemeContextTopBarTextSecondaryHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.TopBar),
                    },

                    // Song Select
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextSongSelect),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextSongSelectPanelBg, ForkSettingsStrings.CustomThemeContextSongSelectPanelBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceElevated,
                        ForkSettingsStrings.CustomThemeContextSongSelectElevated, ForkSettingsStrings.CustomThemeContextSongSelectElevatedHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.SongSelect, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeContextSongSelectControlIdle, ForkSettingsStrings.CustomThemeContextSongSelectControlIdleHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.SongSelect, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextSongSelectAccent, ForkSettingsStrings.CustomThemeContextSongSelectAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.SongSelect, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeContextSongSelectTextPrimary, ForkSettingsStrings.CustomThemeContextSongSelectTextPrimaryHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.SongSelect, SurfaceColourRole.TextSecondary,
                        ForkSettingsStrings.CustomThemeContextSongSelectTextSecondary, ForkSettingsStrings.CustomThemeContextSongSelectTextSecondaryHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.SongSelect),
                    },

                    // Song Select Footer & Action Buttons
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeSectionFooter),
                    new SettingsCheckbox
                    {
                        LabelText = ForkSettingsStrings.CustomThemeEnableGradientsCaption,
                        TooltipText = ForkSettingsStrings.CustomThemeEnableGradientsHint,
                        Current = createGradientsBindable(config),
                    },
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Footer, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeFooterBgCaption, ForkSettingsStrings.CustomThemeFooterBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterBack, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeFooterBackBgCaption, ForkSettingsStrings.CustomThemeFooterBackBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterBack, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeFooterBackTextCaption, ForkSettingsStrings.CustomThemeFooterBackTextHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterMods, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeFooterModsBgCaption, ForkSettingsStrings.CustomThemeFooterModsBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterMods, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeFooterModsAccentCaption, ForkSettingsStrings.CustomThemeFooterModsAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterRandom, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeFooterRandomBgCaption, ForkSettingsStrings.CustomThemeFooterRandomBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterRandom, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeFooterRandomAccentCaption, ForkSettingsStrings.CustomThemeFooterRandomAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterOptions, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeFooterOptionsBgCaption, ForkSettingsStrings.CustomThemeFooterOptionsBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.FooterOptions, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeFooterOptionsAccentCaption, ForkSettingsStrings.CustomThemeFooterOptionsAccentHint, config),

                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeFooterReset,
                        Action = () =>
                        {
                            var c = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                            c.ClearFooterOverrides();
                            c.EnableGradients = true;
                            OverlayColourProvider.CustomTheme.Value = c;
                            config.SetValue(OsuSetting.ForkCustomThemeJson, c.ToJson());
                        }
                    },

                    // Results
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextResults),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Results, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextResultsBg, ForkSettingsStrings.CustomThemeContextResultsBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Results, SurfaceColourRole.SurfaceElevated,
                        ForkSettingsStrings.CustomThemeContextResultsElevated, ForkSettingsStrings.CustomThemeContextResultsElevatedHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Results, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextResultsAccent, ForkSettingsStrings.CustomThemeContextResultsAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Results, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeContextResultsTextPrimary, ForkSettingsStrings.CustomThemeContextResultsTextPrimaryHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Results, SurfaceColourRole.TextSecondary,
                        ForkSettingsStrings.CustomThemeContextResultsTextSecondary, ForkSettingsStrings.CustomThemeContextResultsTextSecondaryHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.Results),
                    },

                    // Mod Select
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextModSelect),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextModCardBg, ForkSettingsStrings.CustomThemeContextModCardBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeContextModCardInnerBg, ForkSettingsStrings.CustomThemeContextModCardInnerBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceElevated,
                        ForkSettingsStrings.CustomThemeContextModColumnBg, ForkSettingsStrings.CustomThemeContextModColumnBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.HeaderBackground,
                        ForkSettingsStrings.CustomThemeContextModHeaderBg, ForkSettingsStrings.CustomThemeContextModHeaderBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.Backdrop,
                        ForkSettingsStrings.CustomThemeContextModBackdrop, ForkSettingsStrings.CustomThemeContextModBackdropHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.ControlHover,
                        ForkSettingsStrings.CustomThemeContextModControlHover, ForkSettingsStrings.CustomThemeContextModControlHoverHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextModAccent, ForkSettingsStrings.CustomThemeContextModAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeContextModTextPrimary, ForkSettingsStrings.CustomThemeContextModTextPrimaryHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.ModSelect, SurfaceColourRole.TextSecondary,
                        ForkSettingsStrings.CustomThemeContextModTextSecondary, ForkSettingsStrings.CustomThemeContextModTextSecondaryHint, config),

                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeModCategoriesHeader),
                    new CustomThemeModTypeColourRow(ModType.DifficultyReduction, ForkSettingsStrings.CustomThemeModTypeDifficultyReduction, config),
                    new CustomThemeModTypeColourRow(ModType.DifficultyIncrease, ForkSettingsStrings.CustomThemeModTypeDifficultyIncrease, config),
                    new CustomThemeModTypeColourRow(ModType.Automation, ForkSettingsStrings.CustomThemeModTypeAutomation, config),
                    new CustomThemeModTypeColourRow(ModType.Conversion, ForkSettingsStrings.CustomThemeModTypeConversion, config),
                    new CustomThemeModTypeColourRow(ModType.Fun, ForkSettingsStrings.CustomThemeModTypeFun, config),
                    new CustomThemeModTypeColourRow(ModType.System, ForkSettingsStrings.CustomThemeModTypeSystem, config),

                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () =>
                        {
                            resetContext(ThemeSurfaceContext.ModSelect);
                            var c = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                            c.ClearModTypeOverrides();
                            OverlayColourProvider.CustomTheme.Value = c;
                            config.SetValue(OsuSetting.ForkCustomThemeJson, c.ToJson());
                        },
                    },

                    // Editor
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextEditor),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.SurfaceElevated,
                        ForkSettingsStrings.CustomThemeContextEditorSidebars, ForkSettingsStrings.CustomThemeContextEditorSidebarsHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextEditorTimeline, ForkSettingsStrings.CustomThemeContextEditorTimelineHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.ControlIdle,
                        ForkSettingsStrings.CustomThemeContextEditorControlIdle, ForkSettingsStrings.CustomThemeContextEditorControlIdleHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.ControlHover,
                        ForkSettingsStrings.CustomThemeContextEditorControlHover, ForkSettingsStrings.CustomThemeContextEditorControlHoverHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.ControlSelected,
                        ForkSettingsStrings.CustomThemeContextEditorControlSelected, ForkSettingsStrings.CustomThemeContextEditorControlSelectedHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextEditorAccent, ForkSettingsStrings.CustomThemeContextEditorAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeContextEditorTextPrimary, ForkSettingsStrings.CustomThemeContextEditorTextPrimaryHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Editor, SurfaceColourRole.TextSecondary,
                        ForkSettingsStrings.CustomThemeContextEditorTextSecondary, ForkSettingsStrings.CustomThemeContextEditorTextSecondaryHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.Editor),
                    },

                    // Beatmap Library
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextLibrary),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.BeatmapLibrary, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextCardBg, ForkSettingsStrings.CustomThemeContextCardBgHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.BeatmapLibrary, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextCardAccent, ForkSettingsStrings.CustomThemeContextCardAccentHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.BeatmapLibrary, SurfaceColourRole.TextSecondary,
                        ForkSettingsStrings.CustomThemeContextCardTextSecondary, ForkSettingsStrings.CustomThemeContextCardTextSecondaryHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.BeatmapLibrary),
                    },

                    // Settings
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextSettings),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Settings, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextSettingsSurface, ForkSettingsStrings.CustomThemeContextSettingsSurfaceHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.Settings, SurfaceColourRole.SurfaceElevated,
                        ForkSettingsStrings.CustomThemeContextSettingsElevated, ForkSettingsStrings.CustomThemeContextSettingsElevatedHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.Settings),
                    },

                    // Infinite Glass
                    new CustomThemeSubsectionHeader(ForkSettingsStrings.CustomThemeContextInfiniteGlass),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.SurfaceBackground,
                        ForkSettingsStrings.CustomThemeContextInfiniteGlassCard, ForkSettingsStrings.CustomThemeContextInfiniteGlassCardHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.TextPrimary,
                        ForkSettingsStrings.CustomThemeContextInfiniteGlassText, ForkSettingsStrings.CustomThemeContextInfiniteGlassTextHint, config),
                    new CustomThemeContextColourRow(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.Accent,
                        ForkSettingsStrings.CustomThemeContextInfiniteGlassAccent, ForkSettingsStrings.CustomThemeContextInfiniteGlassAccentHint, config),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeResetContextButton,
                        Action = () => resetContext(ThemeSurfaceContext.InfiniteGlass),
                    },
                };
            }

            private BindableBool createGradientsBindable(OsuConfigManager cfg)
            {
                var bindable = new BindableBool(OverlayColourProvider.CustomTheme.Value?.EnableGradients ?? true);
                bool updatingInternally = false;

                bindable.BindValueChanged(e =>
                {
                    if (updatingInternally)
                        return;

                    var c = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                    c.EnableGradients = e.NewValue;
                    OverlayColourProvider.CustomTheme.Value = c;
                    cfg.SetValue(OsuSetting.ForkCustomThemeJson, c.ToJson());
                });

                customThemeBindable.BindValueChanged(e =>
                {
                    updatingInternally = true;
                    try
                    {
                        bindable.Value = e.NewValue?.EnableGradients ?? true;
                    }
                    finally
                    {
                        updatingInternally = false;
                    }
                });

                return bindable;
            }

            private void resetContext(ThemeSurfaceContext context)
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                if (theme.ClearContextOverrides(context))
                {
                    config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                    OverlayColourProvider.CustomTheme.Value = theme;
                }
            }

            private partial class CustomThemeSubsectionHeader : CompositeDrawable, IFilterable
            {
                private readonly LocalisableString text;
                private OsuSpriteText label = null!;

                [Resolved]
                private OverlayColourProvider colourProvider { get; set; } = null!;

                public CustomThemeSubsectionHeader(LocalisableString text)
                {
                    this.text = text;
                    RelativeSizeAxes = Axes.X;
                    AutoSizeAxes = Axes.Y;
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS, Top = 16, Bottom = 4 };
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    InternalChild = label = new OsuSpriteText
                    {
                        Text = text,
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                    };

                    var content1 = colourProvider.GetColourBindable(OverlayColour.Content1);
                    content1.BindValueChanged(c => label.Colour = c.NewValue, true);
                }

                public bool MatchingFilter { set => this.FadeTo(value ? 1 : 0); }
                public System.Collections.Generic.IEnumerable<LocalisableString> FilterTerms => new[] { text };
                public bool FilteringActive { get; set; }
            }
        }

        private partial class CustomThemeRadiiSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeSectionGeometry;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.Shapes
            };

            private readonly OsuConfigManager config;

            public CustomThemeRadiiSection(OsuConfigManager config)
            {
                this.config = config;

                Children = new Drawable[]
                {
                    new CustomThemeRadiusRow(
                        GeometryRole.Button,
                        ForkSettingsStrings.CustomThemeGeometryButtonCaption,
                        ForkSettingsStrings.CustomThemeGeometryButtonHint,
                        config,
                        minRadius: 0f,
                        maxRadius: 20f,
                        stepRadius: 1f),
                    new CustomThemeRadiusRow(
                        GeometryRole.Input,
                        ForkSettingsStrings.CustomThemeGeometryInputCaption,
                        ForkSettingsStrings.CustomThemeGeometryInputHint,
                        config,
                        minRadius: 0f,
                        maxRadius: 20f,
                        stepRadius: 1f),
                    new CustomThemeRadiusRow(
                        GeometryRole.Card,
                        ForkSettingsStrings.CustomThemeGeometryCardCaption,
                        ForkSettingsStrings.CustomThemeGeometryCardHint,
                        config,
                        minRadius: 0f,
                        maxRadius: 30f,
                        stepRadius: 1f),
                    new CustomThemeRadiusRow(
                        GeometryRole.Panel,
                        ForkSettingsStrings.CustomThemeGeometryPanelCaption,
                        ForkSettingsStrings.CustomThemeGeometryPanelHint,
                        config,
                        minRadius: 0f,
                        maxRadius: 30f,
                        stepRadius: 1f),
                    new CustomThemeRadiusRow(
                        GeometryRole.Popup,
                        ForkSettingsStrings.CustomThemeGeometryPopupCaption,
                        ForkSettingsStrings.CustomThemeGeometryPopupHint,
                        config,
                        minRadius: 0f,
                        maxRadius: 30f,
                        stepRadius: 1f),
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeGeometryResetAll,
                        Action = resetAllRadii,
                    }
                };
            }

            private void resetAllRadii()
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.ClearAllRadii();
                config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                OverlayColourProvider.CustomTheme.Value = theme;
            }
        }

        public partial class CustomThemeTopBarSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CustomThemeSectionTopBar;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.WindowMaximize
            };

            private readonly OsuConfigManager config;
            private readonly IBindable<CustomThemeDefinition> customThemeBindable;
            private readonly Bindable<TopBarVariant> variantBindable = new Bindable<TopBarVariant>();
            private readonly BindableFloat radiusBindable = new BindableFloat
            {
                MinValue = 0f,
                MaxValue = 24f,
                Precision = 1f,
                Default = 10f,
                Value = 10f,
            };
            private bool isUpdatingInternally;

            public CustomThemeTopBarSection(OsuConfigManager config)
            {
                this.config = config;
                customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                syncFromTheme();

                variantBindable.BindValueChanged(e =>
                {
                    if (isUpdatingInternally)
                        return;

                    var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                    if (theme.Geometry?.TopBarVariant != e.NewValue)
                    {
                        theme.TopBarVariant = e.NewValue;
                        config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                        OverlayColourProvider.CustomTheme.Value = theme;
                    }
                });

                radiusBindable.BindValueChanged(e =>
                {
                    if (isUpdatingInternally)
                        return;

                    var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                    if (theme.Geometry?.TopBarRadius != e.NewValue)
                    {
                        theme.TopBarRadius = e.NewValue;
                        config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                        OverlayColourProvider.CustomTheme.Value = theme;
                    }
                });

                customThemeBindable.BindValueChanged(_ => syncFromTheme());

                var dropdown = new FormEnumDropdown<TopBarVariant>
                {
                    Caption = ForkSettingsStrings.CustomThemeTopBarVariantCaption,
                    HintText = ForkSettingsStrings.CustomThemeTopBarVariantHint,
                    Current = variantBindable,
                };

                var radiusSlider = new FormSliderBar<float>
                {
                    Caption = ForkSettingsStrings.CustomThemeTopBarRadiusCaption,
                    HintText = ForkSettingsStrings.CustomThemeTopBarRadiusHint,
                    Current = radiusBindable,
                    TransferValueOnCommit = false,
                };

                Children = new Drawable[]
                {
                    new TopBarPreview(),
                    new SettingsItemV2(dropdown)
                    {
                        FavouriteId = "ui:CustomThemeSettingsPanel:dropdown",
                    },
                    new SettingsItemV2(radiusSlider)
                    {
                        FavouriteId = "ui:CustomThemeSettingsPanel:radiusSlider",
                    },
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.CustomThemeTopBarReset,
                        Action = resetTopBar,
                    }
                };
            }

            private void syncFromTheme()
            {
                isUpdatingInternally = true;
                try
                {
                    var theme = OverlayColourProvider.CustomTheme.Value;
                    if (theme != null)
                    {
                        var targetVariant = theme.Geometry?.TopBarVariant ?? TopBarVariant.Flat;
                        if (variantBindable.Value != targetVariant)
                            variantBindable.Value = targetVariant;

                        float effRadius = theme.Geometry?.TopBarRadius ?? OverlayColourProvider.CalculateEffectiveRadius(GeometryRole.Panel, 10f);
                        float clampedRadius = Math.Clamp(effRadius, 0f, 24f);
                        if (Math.Abs(radiusBindable.Value - clampedRadius) > 0.01f)
                            radiusBindable.Value = clampedRadius;
                    }
                }
                finally
                {
                    isUpdatingInternally = false;
                }
            }

            private void resetTopBar()
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                if (theme.Geometry != null)
                {
                    theme.Geometry.TopBarVariant = null;
                    theme.Geometry.TopBarRadius = null;
                }
                config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
                OverlayColourProvider.CustomTheme.Value = theme;
            }

            public partial class TopBarPreview : CompositeDrawable
            {
                private Container previewOuter = null!;
                private Container previewBar = null!;
                private Container leftPod = null!;
                private Container rightPod = null!;
                private Box previewBarBackground = null!;
                private InverseCornerFillet leftFillet = null!;
                private InverseCornerFillet rightFillet = null!;
                private IBindable<TopBarVariant> variant = null!;
                private IBindable<float> radius = null!;

                public TopBarPreview()
                {
                    RelativeSizeAxes = Axes.X;
                    Height = 64;
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS, Vertical = 4 };
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    InternalChild = previewOuter = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 6,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Colour4.FromHex("#15151b"),
                            },
                            leftFillet = new InverseCornerFillet(FilletDirection.Left)
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Alpha = 0,
                            },
                            rightFillet = new InverseCornerFillet(FilletDirection.Right)
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Alpha = 0,
                            },
                            previewBar = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    previewBarBackground = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Colour4.FromHex("#222228"),
                                    },
                                    leftPod = new Container
                                    {
                                        Anchor = Anchor.TopLeft,
                                        Origin = Anchor.TopLeft,
                                        RelativeSizeAxes = Axes.Y,
                                        Width = 40,
                                        Child = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = Colour4.FromHex("#2a2a32"),
                                        }
                                    },
                                    rightPod = new Container
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.Y,
                                        Width = 70,
                                        Child = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = Colour4.FromHex("#2a2a32"),
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(3, 0),
                                        Padding = new MarginPadding { Left = 6 },
                                        Children = new Drawable[]
                                        {
                                            new Circle { Size = new Vector2(8), Colour = Colour4.FromHex("#ff66aa") },
                                            new Circle { Size = new Vector2(8), Colour = Colour4.White.Opacity(0.7f) },
                                        }
                                    },
                                    new Container
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(36, 6),
                                        Masking = true,
                                        CornerRadius = 3,
                                        Child = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = Colour4.White.Opacity(0.3f),
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(3, 0),
                                        Padding = new MarginPadding { Right = 6 },
                                        Children = new Drawable[]
                                        {
                                            new Circle { Size = new Vector2(8), Colour = Colour4.White.Opacity(0.6f) },
                                            new Circle { Size = new Vector2(8), Colour = Colour4.White.Opacity(0.6f) },
                                            new Circle { Size = new Vector2(8), Colour = Colour4.FromHex("#55bbff") },
                                        }
                                    }
                                }
                            }
                        }
                    };

                    variant = OverlayColourProvider.GetTopBarVariant();
                    radius = OverlayColourProvider.GetTopBarRadius();

                    variant.BindValueChanged(_ => updatePreview());
                    radius.BindValueChanged(_ => updatePreview());
                    updatePreview();
                }

                private void updatePreview()
                {
                    var currentVariant = variant?.Value ?? TopBarVariant.Flat;
                    float currentRadius = Math.Clamp((radius?.Value ?? 10f) * 0.7f, 0f, 16f);

                    leftPod.Alpha = currentVariant == TopBarVariant.Attached ? 1 : 0;
                    rightPod.Alpha = currentVariant == TopBarVariant.Attached ? 1 : 0;
                    leftFillet.Alpha = 0;
                    rightFillet.Alpha = 0;

                    switch (currentVariant)
                    {
                        case TopBarVariant.Flat:
                            previewBar.RelativeSizeAxes = Axes.Both;
                            previewBar.Size = Vector2.One;
                            previewBar.Position = Vector2.Zero;
                            previewBar.Masking = false;
                            previewBar.CornerRadius = 0;
                            previewBar.EdgeEffect = default;
                            previewBarBackground.Colour = Colour4.FromHex("#222228");
                            leftPod.Masking = false;
                            leftPod.CornerRadius = 0;
                            rightPod.Masking = false;
                            rightPod.CornerRadius = 0;
                            break;

                        case TopBarVariant.Rounded:
                            previewBar.RelativeSizeAxes = Axes.X;
                            previewBar.Width = 1f;
                            previewBar.Height = 36 + currentRadius;
                            previewBar.Position = new Vector2(0, -currentRadius);
                            previewBar.Masking = currentRadius > 0;
                            previewBar.CornerRadius = currentRadius;
                            previewBar.EdgeEffect = currentRadius > 0 ? new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = Colour4.Black.Opacity(0.4f),
                                Radius = 6f,
                            } : default;
                            previewBarBackground.Colour = Colour4.FromHex("#222228");
                            leftPod.Masking = false;
                            leftPod.CornerRadius = 0;
                            rightPod.Masking = false;
                            rightPod.CornerRadius = 0;
                            break;

                        case TopBarVariant.DownwardRounded:
                            previewBar.RelativeSizeAxes = Axes.X;
                            previewBar.Width = 1f;
                            previewBar.Height = 32;
                            previewBar.Position = Vector2.Zero;
                            previewBar.Masking = false;
                            previewBar.CornerRadius = 0;
                            previewBar.EdgeEffect = default;
                            previewBarBackground.Colour = Colour4.FromHex("#222228");
                            leftPod.Masking = false;
                            leftPod.CornerRadius = 0;
                            rightPod.Masking = false;
                            rightPod.CornerRadius = 0;

                            if (currentRadius > 0f)
                            {
                                leftFillet.Position = new Vector2(0, 32);
                                leftFillet.UpdateFillet(currentRadius, Colour4.FromHex("#222228"));

                                rightFillet.Position = new Vector2(0, 32);
                                rightFillet.UpdateFillet(currentRadius, Colour4.FromHex("#222228"));
                            }
                            break;

                        case TopBarVariant.Floating:
                            previewBar.RelativeSizeAxes = Axes.X;
                            previewBar.Width = 0.94f;
                            previewBar.Height = 28;
                            previewBar.Anchor = Anchor.TopCentre;
                            previewBar.Origin = Anchor.TopCentre;
                            previewBar.Position = new Vector2(0, 4);
                            previewBar.Masking = currentRadius > 0;
                            previewBar.CornerRadius = currentRadius;
                            previewBar.EdgeEffect = currentRadius > 0 ? new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = Colour4.Black.Opacity(0.5f),
                                Radius = 8f,
                            } : default;
                            previewBarBackground.Colour = Colour4.FromHex("#222228");
                            leftPod.Masking = false;
                            leftPod.CornerRadius = 0;
                            rightPod.Masking = false;
                            rightPod.CornerRadius = 0;
                            break;

                        case TopBarVariant.Attached:
                            previewBar.RelativeSizeAxes = Axes.Both;
                            previewBar.Size = Vector2.One;
                            previewBar.Position = Vector2.Zero;
                            previewBar.Masking = false;
                            previewBar.CornerRadius = 0;
                            previewBar.EdgeEffect = default;
                            previewBarBackground.Colour = Colour4.FromHex("#191920");

                            leftPod.Masking = currentRadius > 0;
                            leftPod.CornerRadius = currentRadius;
                            leftPod.Height = 36 + currentRadius;
                            leftPod.Position = new Vector2(0, -currentRadius);

                            rightPod.Masking = currentRadius > 0;
                            rightPod.CornerRadius = currentRadius;
                            rightPod.Height = 36 + currentRadius;
                            rightPod.Position = new Vector2(0, -currentRadius);
                            break;
                    }
                }
            }
        }

    }
}
