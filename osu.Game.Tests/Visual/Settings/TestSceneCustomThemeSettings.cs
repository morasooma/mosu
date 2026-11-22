using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Overlays.Volume;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Tests.Visual;
using osu.Game.Users;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneCustomThemeSettings : OsuManualInputManagerTestScene
    {
        private CustomThemeSettingsPanel panel = null!;
        private OverlayColourProvider colourProvider = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(new OsuConfigManager(LocalStorage));
            colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);
            Dependencies.Cache(colourProvider);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("Reset theme mode and custom theme", () =>
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
                OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition();
            });

            AddStep("Create settings panel", () =>
            {
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = panel = new CustomThemeSettingsPanel()
                };
                panel.Show();
            });

            AddUntilStep("Wait for panel to load", () => panel.IsLoaded && panel.ChildrenOfType<FormEnumDropdown<ThemeMode>>().Any());
        }

        [Test]
        public void TestCommunityLibraryIsOnlyAnEntryPoint()
        {
            AddAssert("library entry exists", () => panel.ChildrenOfType<CommunityThemeSection>().Single()
                .ChildrenOfType<SettingsButtonV2>().Single().Text == ThemeLibraryStrings.Library);
            AddAssert("catalogue is not embedded in settings", () => !panel.ChildrenOfType<SettingsButtonV2>()
                .Any(b => b.Text == ThemeLibraryStrings.Install || b.Text == ThemeLibraryStrings.Publish));
        }

        [Test]
        public void TestThemeEditorRemainsASettingsDrawer()
        {
            ForkInterfaceSettings original = null!;
            AddAssert("editor keeps original drawer geometry", () => panel.RelativeSizeAxes == Axes.Y
                && panel.AutoSizeAxes == Axes.X && panel.Anchor != Anchor.Centre
                && panel.SectionsContainer.DrawWidth == SettingsPanel.PANEL_WIDTH);
            AddStep("load original settings entry", () => Add(original = new ForkInterfaceSettings(panel)));
            AddUntilStep("entry loaded", () => original.IsLoaded);
            AddStep("close editor", () => panel.Hide());
            AddStep("open via settings", () => original.ChildrenOfType<SettingsButtonV2>().Single(b => b.Text == ForkSettingsStrings.CustomThemeSettingsButton).Action.Invoke());
            AddAssert("existing entry opens drawer", () => panel.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestForkAppearanceControlsAreMirroredAndSavedInTheme()
        {
            ForkInterfaceSettings original = null!;
            AddStep("load original settings beside panel", () => Add(original = new ForkInterfaceSettings()));
            AddUntilStep("original settings loaded", () => original.IsLoaded);
            AddAssert("both locations contain all requested controls", () =>
                original.ChildrenOfType<FormCheckBox>().Any(c => c.Caption == ForkSettingsStrings.DisableInterfaceShearCaption)
                && panel.ChildrenOfType<ThemeInterfaceSection>().Count() == 1
                && panel.ChildrenOfType<FormCheckBox>().Any(c => c.Caption == ForkSettingsStrings.DisableInterfaceShearCaption)
                && panel.ChildrenOfType<FormEnumDropdown<ForkSongSelectStyle>>().Any()
                && panel.ChildrenOfType<FormEnumDropdown<InterfaceAnimationMode>>().Any()
                && panel.ChildrenOfType<FormCheckBox>().Any(c => c.Caption == SongSelectTransparencyStrings.Caption)
                && panel.ChildrenOfType<FormDropdown<string>>().Any(c => c.Caption == ForkSettingsStrings.CustomUIFontCaption));
            AddStep("change shear in theme panel", () => panel.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.DisableInterfaceShearCaption).Current.Value = true);
            AddAssert("original control and stored theme match", () =>
                original.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.DisableInterfaceShearCaption).Current.Value
                && Dependencies.Get<OsuConfigManager>().CustomThemes.Active.Theme.Interface!.DisableShear == true);
            AddStep("enable old previews in theme panel", () => panel.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.OldCarouselPreviewCaption).Current.Value = true);
            AddAssert("old preview setting mirrored and exported", () =>
                original.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.OldCarouselPreviewCaption).Current.Value
                && Dependencies.Get<OsuConfigManager>().CustomThemes.GetActiveThemeForSharing().Interface!.OldCarouselPreviews == true);
            AddStep("disable old previews in original location", () => original.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.OldCarouselPreviewCaption).Current.Value = false);
            AddAssert("theme panel receives original change", () => !panel.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.OldCarouselPreviewCaption).Current.Value
                && Dependencies.Get<OsuConfigManager>().CustomThemes.Active.Theme.Interface!.OldCarouselPreviews == false);
            AddStep("change song select in original location", () => original.ChildrenOfType<FormEnumDropdown<ForkSongSelectStyle>>().Single().Current.Value = ForkSongSelectStyle.InfiniteGlass);
            AddAssert("theme panel and saved value match", () => panel.ChildrenOfType<FormEnumDropdown<ForkSongSelectStyle>>().Single().Current.Value == ForkSongSelectStyle.InfiniteGlass
                && Dependencies.Get<OsuConfigManager>().CustomThemes.Active.Theme.Interface!.SongSelectStyle == ForkSongSelectStyle.InfiniteGlass);
            AddStep("change transparency and animations", () =>
            {
                panel.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.OverlayTransparencyCaption).Current.Value = true;
                panel.ChildrenOfType<FormEnumDropdown<InterfaceAnimationMode>>().Single().Current.Value = InterfaceAnimationMode.Minimal;
                panel.ChildrenOfType<FormDropdown<string>>().Single(c => c.Caption == ForkSettingsStrings.CustomUIFontCaption).Current.Value = "Inter";
            });
            AddAssert("original values and portable theme match", () =>
            {
                var theme = Dependencies.Get<OsuConfigManager>().CustomThemes.GetActiveThemeForSharing();
                return original.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == ForkSettingsStrings.OverlayTransparencyCaption).Current.Value
                       && original.ChildrenOfType<FormEnumDropdown<InterfaceAnimationMode>>().Single().Current.Value == InterfaceAnimationMode.Minimal
                       && original.ChildrenOfType<FormDropdown<string>>().Single(c => c.Caption == ForkSettingsStrings.CustomUIFontCaption).Current.Value == "Inter"
                       && theme.Interface!.OverlayTransparency == true && theme.Interface.Animations == InterfaceAnimationMode.Minimal && theme.Interface.Font == "Inter";
            });
        }

        [Test]
        public void TestNamedThemeCreationRenameAndSwitch()
        {
            OsuConfigManager config = null!;
            string originalId = string.Empty;
            string createdId = string.Empty;
            AddStep("remember original theme", () =>
            {
                config = Dependencies.Get<OsuConfigManager>();
                originalId = config.CustomThemes.ActiveId;
                panel.ChildrenOfType<FormTextBox>().Single(t => t.Caption == NamedThemeStrings.Name).Current.Value = "Моя ночная тема";
            });
            AddStep("create named copy", () => panel.ChildrenOfType<SettingsButtonV2>().Single(b => b.Text == NamedThemeStrings.Create).Action.Invoke());
            AddAssert("new named theme selected", () =>
            {
                createdId = config.CustomThemes.ActiveId;
                return createdId != originalId && config.CustomThemes.Active.Name == "Моя ночная тема"
                                               && panel.ChildrenOfType<NamedThemeDropdown>().Single().Current.Value == createdId;
            });
            AddStep("edit new theme", () =>
            {
                var dropdown = panel.ChildrenOfType<FormEnumDropdown<ThemeMode>>().First();
                dropdown.Current.Value = ThemeMode.Dark;
                panel.ChildrenOfType<FormTextBox>().Single(t => t.Caption == NamedThemeStrings.Name).Current.Value = "Тёмная тема";
                panel.ChildrenOfType<SettingsButtonV2>().Single(b => b.Text == NamedThemeStrings.Rename).Action.Invoke();
            });
            AddAssert("renamed dropdown label", () => panel.ChildrenOfType<NamedThemeDropdown>().Single().FilterTerms.Any(t => t == "Тёмная тема"));
            AddStep("switch to original", () => panel.ChildrenOfType<NamedThemeDropdown>().Single().Current.Value = originalId);
            AddStep("switch back", () => panel.ChildrenOfType<NamedThemeDropdown>().Single().Current.Value = createdId);
            AddAssert("saved theme and editor restored", () =>
                OverlayColourProvider.CustomTheme.Value.BaseTheme == ThemeMode.Dark
                && panel.ChildrenOfType<FormEnumDropdown<ThemeMode>>().First().Current.Value == ThemeMode.Dark
                && panel.ChildrenOfType<FormTextBox>().Single(t => t.Caption == NamedThemeStrings.Name).Current.Value == "Тёмная тема");
        }

        [Test]
        public void TestGeneralThemeDropdownIncludesNamedThemes()
        {
            NamedThemeDropdown dropdown = null!;
            OsuConfigManager config = null!;
            AddStep("create general theme dropdown", () =>
            {
                config = Dependencies.Get<OsuConfigManager>();
                Child = dropdown = new NamedThemeDropdown(config, includeBuiltInThemes: true);
            });
            AddUntilStep("dropdown loaded", () => dropdown.IsLoaded);
            AddStep("select built-in dark theme", () => dropdown.Current.Value = "builtin:Dark");
            AddAssert("built-in mode applied", () => config.Get<ThemeMode>(OsuSetting.ForkThemeMode) == ThemeMode.Dark);
            AddStep("create named theme", () => config.CustomThemes.Create("Из списка"));
            AddAssert("built-in selection retained", () => dropdown.Current.Value == "builtin:Dark");
            AddStep("select named theme", () => dropdown.Current.Value = config.CustomThemes.ActiveId);
            AddAssert("custom mode applied", () => config.Get<ThemeMode>(OsuSetting.ForkThemeMode) == ThemeMode.Custom);
            AddStep("external mode change", () => config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Light));
            AddAssert("selection synchronised", () => dropdown.Current.Value == "builtin:Light");
        }

        [Test]
        public void TestPanelComponentsExist()
        {
            AddAssert("Panel is visible", () => panel.State.Value == Visibility.Visible);
            AddAssert("Contains base theme dropdown", () => panel.ChildrenOfType<FormEnumDropdown<ThemeMode>>().Any());
            AddAssert("Contains colour rows", () => panel.ChildrenOfType<CustomThemeColourRow>().Count() == 9);
            AddAssert("Contains 51 context colour rows", () => panel.ChildrenOfType<CustomThemeContextColourRow>().Count() == 51);
            AddAssert("Contains 6 mod type colour rows", () => panel.ChildrenOfType<CustomThemeModTypeColourRow>().Count() == 6);
            AddAssert("Contains ModSelect Header and Backdrop rows", () => panel.ChildrenOfType<CustomThemeContextColourRow>().Any(r => r.Context == ThemeSurfaceContext.ModSelect && r.Role == SurfaceColourRole.HeaderBackground)
                                                                           && panel.ChildrenOfType<CustomThemeContextColourRow>().Any(r => r.Context == ThemeSurfaceContext.ModSelect && r.Role == SurfaceColourRole.Backdrop));
            AddAssert("Contains TopBar ControlHover row", () => panel.ChildrenOfType<CustomThemeContextColourRow>().Any(r => r.Context == ThemeSurfaceContext.TopBar && r.Role == SurfaceColourRole.ControlHover));
            AddAssert("Contains Footer and FooterButton rows", () => panel.ChildrenOfType<CustomThemeContextColourRow>().Any(r => r.Context == ThemeSurfaceContext.Footer)
                                                                    && panel.ChildrenOfType<CustomThemeContextColourRow>().Any(r => r.Context == ThemeSurfaceContext.FooterMods));
            AddAssert("Contains 5 radius rows", () => panel.ChildrenOfType<CustomThemeRadiusRow>().Count() == 5);
            AddAssert("Contains top bar section", () => panel.ChildrenOfType<CustomThemeSettingsPanel.CustomThemeTopBarSection>().Any());
            AddAssert("Contains top bar preview", () => panel.ChildrenOfType<CustomThemeSettingsPanel.CustomThemeTopBarSection.TopBarPreview>().Any());
            AddAssert("Contains reset all radii button", () =>
                panel.ChildrenOfType<SettingsButtonV2>().Any(b => b.Text == ForkSettingsStrings.CustomThemeGeometryResetAll));
        }

        [Test]
        public void TestChangeBaseThemeUpdatesCustomDefinition()
        {
            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Change base theme to Light", () =>
            {
                var dropdown = panel.ChildrenOfType<FormEnumDropdown<ThemeMode>>().First();
                dropdown.Current.Value = ThemeMode.Light;
            });

            AddAssert("CustomTheme definition has Light base", () =>
                OverlayColourProvider.CustomTheme.Value.BaseTheme == ThemeMode.Light);
            AddAssert("Provider contrast polarity matches Light", () =>
                OverlayColourProvider.IsLightTheme);
            AddAssert("Row caption text is dark in Light mode", () =>
                panel.ChildrenOfType<CustomThemeColourRow>().First().ChildrenOfType<OsuSpriteText>().First().Colour.TopLeft.SRGB.R < 0.5f);
        }

        [Test]
        public void TestResetAllClearsOverrides()
        {
            AddStep("Add custom override", () =>
            {
                var def = OverlayColourProvider.CustomTheme.Value.DeepClone();
                def.SetOverride(OverlayColour.Colour1, Colour4.Red);
                OverlayColourProvider.CustomTheme.Value = def;
            });

            AddAssert("Override exists", () =>
                OverlayColourProvider.CustomTheme.Value.TryGetOverride(OverlayColour.Colour1, out _));

            AddStep("Click reset all button", () =>
            {
                var resetButton = panel.ChildrenOfType<SettingsButtonV2>().FirstOrDefault(b => b.Text == ForkSettingsStrings.CustomThemeResetAllButton);
                resetButton?.TriggerClick();
            });

            AddAssert("Overrides are empty", () =>
                OverlayColourProvider.CustomTheme.Value.Overrides.Count == 0);
        }

        [Test]
        public void TestContextColourAdjustmentAndReset()
        {
            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Set SongSelect SurfaceBackground context override", () =>
            {
                var row = panel.ChildrenOfType<CustomThemeContextColourRow>().First(r => r.Context == ThemeSurfaceContext.SongSelect && r.Role == SurfaceColourRole.SurfaceBackground);
                var button = row.ChildrenOfType<CustomThemeColourRow.ColourButton>().First();
                button.Current.Value = Colour4.FromHex("#AABBCC");
            });

            AddAssert("CustomTheme has SongSelect SurfaceBackground override", () =>
                OverlayColourProvider.CustomTheme.Value.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out var col)
                && col == Colour4.FromHex("#AABBCC"));

            AddStep("Click reset button on that context row", () =>
            {
                var row = panel.ChildrenOfType<CustomThemeContextColourRow>().First(r => r.Context == ThemeSurfaceContext.SongSelect && r.Role == SurfaceColourRole.SurfaceBackground);
                var resetButton = row.ChildrenOfType<ThemeResetButton>().First();
                resetButton.TriggerClick();
            });

            AddAssert("SongSelect SurfaceBackground override is removed", () =>
                !OverlayColourProvider.CustomTheme.Value.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _));
        }

        [Test]
        public void TestRadiusLiveAdjustmentAndReset()
        {
            RoundedButton? testButton = null;

            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Add test rounded button", () =>
            {
                Add(testButton = new RoundedButton
                {
                    Width = 120,
                    Height = 40,
                    Text = "Test",
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding(20)
                });
            });

            AddUntilStep("Wait for test button to load", () => testButton!.IsLoaded);

            AddAssert("Initial button radius is default (5)", () =>
                testButton!.CornerRadius == 5f);

            AddStep("Adjust Button radius slider to 16", () =>
            {
                var buttonRow = panel.ChildrenOfType<CustomThemeRadiusRow>().First(r => r.Role == GeometryRole.Button);
                var slider = buttonRow.ChildrenOfType<FormSliderBar<float>>().First();
                slider.Current.Value = 16f;
            });

            AddAssert("CustomTheme definition has Button radius 16", () =>
                OverlayColourProvider.CustomTheme.Value.Geometry?.ButtonRadius == 16f);
            AddAssert("OverlayColourProvider reports Button radius 16", () =>
                OverlayColourProvider.GetGeometryRadius(GeometryRole.Button).Value == 16f);
            AddAssert("Test button corner radius updated reactively to 16", () =>
                testButton!.CornerRadius == 16f);

            AddStep("Click Button row revert button", () =>
            {
                var buttonRow = panel.ChildrenOfType<CustomThemeRadiusRow>().First(r => r.Role == GeometryRole.Button);
                var revertBtn = buttonRow.ChildrenOfType<ThemeResetButton>().First();
                revertBtn.Action?.Invoke();
            });

            AddAssert("Button radius override cleared in definition", () =>
                OverlayColourProvider.CustomTheme.Value.Geometry?.ButtonRadius == null);
            AddAssert("OverlayColourProvider reports default Button radius (5)", () =>
                OverlayColourProvider.GetGeometryRadius(GeometryRole.Button).Value == 5f);
            AddAssert("Test button corner radius reverted to 5", () =>
                testButton!.CornerRadius == 5f);

            AddStep("Remove test button", () =>
            {
                if (testButton != null)
                    Remove(testButton, true);
            });
        }

        [Test]
        public void TestResetAllRadiiClearsAllGeometryOverrides()
        {
            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Set multiple radii overrides", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Button, 12f);
                theme.SetRadius(GeometryRole.Card, 20f);
                theme.SetRadius(GeometryRole.Panel, 25f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("Multiple overrides present", () =>
                OverlayColourProvider.CustomTheme.Value.Geometry?.ButtonRadius == 12f &&
                OverlayColourProvider.CustomTheme.Value.Geometry?.CardRadius == 20f &&
                OverlayColourProvider.CustomTheme.Value.Geometry?.PanelRadius == 25f);

            AddStep("Click reset all radii button", () =>
            {
                var resetRadiiButton = panel.ChildrenOfType<SettingsButtonV2>().FirstOrDefault(b => b.Text == ForkSettingsStrings.CustomThemeGeometryResetAll);
                resetRadiiButton?.TriggerClick();
            });

            AddAssert("All radii overrides cleared", () =>
                OverlayColourProvider.CustomTheme.Value.Geometry == null || !OverlayColourProvider.CustomTheme.Value.Geometry.HasOverrides);
            AddAssert("OverlayColourProvider reports default Card radius (8)", () =>
                OverlayColourProvider.GetGeometryRadius(GeometryRole.Card).Value == 8f);
        }

        [Test]
        public void TestMuteButtonRetainsCircularRadius()
        {
            MuteButton? muteButton = null;

            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Add mute button", () =>
            {
                Add(muteButton = new MuteButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 70, Right = 20 }
                });
            });

            AddUntilStep("Wait for mute button to load", () => muteButton!.IsLoaded);

            AddAssert("Mute button retains circular radius (15)", () =>
                muteButton!.CornerRadius == 15f);

            AddStep("Adjust Button radius to 2", () =>
            {
                var buttonRow = panel.ChildrenOfType<CustomThemeRadiusRow>().First(r => r.Role == GeometryRole.Button);
                var slider = buttonRow.ChildrenOfType<FormSliderBar<float>>().First();
                slider.Current.Value = 2f;
            });

            AddAssert("Mute button still retains circular radius (15)", () =>
                muteButton!.CornerRadius == 15f);

            AddStep("Remove mute button", () =>
            {
                if (muteButton != null)
                    Remove(muteButton, true);
            });
        }

        [Test]
        public void TestThemeResetButtonStylingAndPresence()
        {
            AddAssert("All radius rows contain ThemeResetButton", () =>
                panel.ChildrenOfType<CustomThemeRadiusRow>().All(r => r.ChildrenOfType<ThemeResetButton>().Any()));
            AddAssert("All colour rows contain ThemeResetButton", () =>
                panel.ChildrenOfType<CustomThemeColourRow>().All(r => r.ChildrenOfType<ThemeResetButton>().Any()));
            AddAssert("ThemeResetButton has correct size (26x24)", () =>
                panel.ChildrenOfType<ThemeResetButton>().All(b => b.DrawSize == new osuTK.Vector2(26, 24)));
        }

        [Test]
        public void TestBackdropBlurSurfaceMaskingCondition()
        {
            BackdropBlurSurface blur = null!;

            AddStep("Create BackdropBlurSurface", () => blur = new BackdropBlurSurface());
            AddAssert("Glass view is clipped even with square corners", () => blur.Masking && blur.CornerRadius == 0f);
            AddStep("Set CornerRadius to 12", () => blur.CornerRadius = 12f);
            AddAssert("Rounded glass view remains clipped", () => blur.Masking && blur.CornerRadius == 12f);
            AddStep("Set CornerRadius back to 0", () => blur.CornerRadius = 0f);
            AddAssert("Square glass view remains clipped", () => blur.Masking && blur.CornerRadius == 0f);
        }

        [Test]
        public void TestAllSemanticRolesAffectLiveComponents()
        {
            DialogButton? dialogButton = null;
            UserGridPanel? userPanel = null;
            ShearedSearchTextBox? searchTextBox = null;
            ModColumn? modColumn = null;
            ConfirmDialog? confirmDialog = null;

            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Add components", () =>
            {
                Add(dialogButton = new DialogButton
                {
                    Text = "Test Dialog Button",
                    Width = 200,
                    Height = 40,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 10, Right = 20 }
                });

                Add(userPanel = new UserGridPanel(new osu.Game.Online.API.Requests.Responses.APIUser { Username = "TestUser", Id = 100 })
                {
                    Width = 200,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 60, Right = 20 }
                });

                Add(searchTextBox = new ShearedSearchTextBox
                {
                    Width = 200,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 190, Right = 20 }
                });

                Add(modColumn = new ModColumn(ModType.DifficultyReduction, false)
                {
                    Width = 200,
                    Height = 100,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 240, Right = 20 }
                });

                Add(confirmDialog = new ConfirmDialog("Confirm test", () => { })
                {
                    Width = 300,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 350, Right = 20 }
                });
            });

            AddUntilStep("Wait for components to load", () =>
                dialogButton!.IsLoaded && userPanel!.IsLoaded && searchTextBox!.IsLoaded && modColumn!.IsLoaded && confirmDialog!.IsLoaded);

            AddStep("Adjust Button radius to 14", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Button, 14f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("DialogButton updated ColourContainer corner radius to 14", () =>
                dialogButton!.ColourContainer.CornerRadius == 14f);

            AddStep("Adjust Card radius to 18", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Card, 18f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("UserGridPanel updated corner radius to 18", () =>
                userPanel!.CornerRadius == 18f);

            AddStep("Adjust Input radius to 8", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Input, 8f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("ShearedSearchTextBox updated corner radius (clamped to 6 when sheared)", () =>
                searchTextBox!.CornerRadius == 6f);

            AddStep("Adjust Panel radius to 12", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Panel, 12f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("ModColumn TopLevelContent updated corner radius to 12", () =>
                modColumn!.TopLevelContent.CornerRadius == 12f);

            AddStep("Adjust Popup radius to 22", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Popup, 22f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("ConfirmDialog ContentContainer updated corner radius to 22", () =>
                confirmDialog!.ContentContainer.CornerRadius == 22f);

            AddStep("Clean up components", () =>
            {
                if (dialogButton != null) Remove(dialogButton, true);
                if (userPanel != null) Remove(userPanel, true);
                if (searchTextBox != null) Remove(searchTextBox, true);
                if (modColumn != null) Remove(modColumn, true);
                if (confirmDialog != null) Remove(confirmDialog, true);
            });
        }

        [Test]
        public void TestDropdownLiveRadiusAndZeroClamping()
        {
            FormDropdown<string>? dropdown = null;

            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Add FormDropdown", () =>
            {
                Add(dropdown = new FormDropdown<string>
                {
                    Caption = "Theme Dropdown",
                    Width = 250,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 20, Right = 20 },
                    Items = new[] { "Option A", "Option B", "Option C" }
                });
            });

            AddUntilStep("Wait for dropdown to load", () => dropdown!.IsLoaded);
            AddStep("Open dropdown menu", () =>
            {
                dropdown!.ChildrenOfType<OsuDropdown<string>.OsuDropdownMenu>().First().Open();
            });
            AddUntilStep("Dropdown menu is visible", () =>
                dropdown!.ChildrenOfType<OsuDropdown<string>.OsuDropdownMenu>().First().State == osu.Framework.Graphics.UserInterface.MenuState.Open);

            AddStep("Set Popup radius to 12", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Popup, 12f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("Dropdown menu CornerRadius is 12", () =>
                dropdown!.ChildrenOfType<OsuDropdown<string>.OsuDropdownMenu>().First().CornerRadius == 12f);
            AddAssert("Dropdown menu item CornerRadius is 10 (12 - 2)", () =>
                dropdown!.ChildrenOfType<OsuDropdown<string>.OsuDropdownMenu.DrawableOsuDropdownMenuItem>().First().CornerRadius == 10f);

            AddStep("Set Popup radius to 0 (sharp)", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Popup, 0f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("Dropdown menu CornerRadius is 0", () =>
                dropdown!.ChildrenOfType<OsuDropdown<string>.OsuDropdownMenu>().First().CornerRadius == 0f);
            AddAssert("Dropdown menu item CornerRadius is 0 (not clamped to 2)", () =>
                dropdown!.ChildrenOfType<OsuDropdown<string>.OsuDropdownMenu.DrawableOsuDropdownMenuItem>().First().CornerRadius == 0f);

            AddStep("Clean up dropdown", () =>
            {
                if (dropdown != null) Remove(dropdown, true);
            });
        }

        [Test]
        public void TestCarouselPanelRadiusAndBackgroundMasking()
        {
            PanelSetBackground? panelSetBg = null;

            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Add PanelSetBackground", () =>
            {
                Add(panelSetBg = new PanelSetBackground
                {
                    Width = 300,
                    Height = 80,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 20, Right = 20 }
                });
            });

            AddUntilStep("Wait for PanelSetBackground to load", () => panelSetBg!.IsLoaded);

            AddStep("Set Panel radius to 16", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Panel, 16f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("PanelSetBackground CornerRadius updated to 16", () =>
                panelSetBg!.CornerRadius == 16f);

            AddStep("Set Panel radius to 0", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetRadius(GeometryRole.Panel, 0f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("PanelSetBackground CornerRadius updated to 0", () =>
                panelSetBg!.CornerRadius == 0f);

            AddStep("Clean up PanelSetBackground", () =>
            {
                if (panelSetBg != null) Remove(panelSetBg, true);
            });
        }

        [Test]
        public void TestTopBarSectionUpdatesDefinitionAndResets()
        {
            AddStep("Set theme mode to Custom", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("Set top bar variant to Floating and radius to 18", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Floating;
                theme.TopBarRadius = 18f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("TopBarVariant is Floating in theme", () =>
                OverlayColourProvider.CustomTheme.Value.TopBarVariant == TopBarVariant.Floating);
            AddAssert("TopBarRadius is 18 in theme", () =>
                OverlayColourProvider.CustomTheme.Value.TopBarRadius == 18f);

            AddStep("Click reset top bar button", () =>
            {
                var topBarReset = panel.ChildrenOfType<SettingsButtonV2>().FirstOrDefault(b => b.Text == ForkSettingsStrings.CustomThemeTopBarReset);
                topBarReset?.TriggerClick();
            });

            AddAssert("TopBarVariant reset to Flat", () =>
                OverlayColourProvider.CustomTheme.Value.TopBarVariant == TopBarVariant.Flat);
            AddAssert("TopBarRadius override cleared", () =>
                OverlayColourProvider.CustomTheme.Value.Geometry?.TopBarRadius == null);
        }

        [Test]
        public void TestFooterLocatedInContextSectionAndNoStandaloneFooterSection()
        {
            AddAssert("No standalone CustomThemeFooterSection", () =>
                !panel.ChildrenOfType<SettingsSection>().Any(s => s.Header == ForkSettingsStrings.CustomThemeSectionFooter));

            AddAssert("Footer controls exist under context section", () =>
            {
                var contextSection = panel.ChildrenOfType<SettingsSection>().FirstOrDefault(s => s.Header == ForkSettingsStrings.CustomThemeSectionContexts);
                return contextSection != null &&
                       contextSection.ChildrenOfType<SettingsCheckbox>().Any(c => c.LabelText == ForkSettingsStrings.CustomThemeEnableGradientsCaption) &&
                       contextSection.ChildrenOfType<CustomThemeContextColourRow>().Any(r => r.Context == ThemeSurfaceContext.Footer);
            });
        }

        [Test]
        public void TestRowLayoutAndWrapping()
        {
            AddAssert("Colour rows use OsuTextFlowContainer for hints", () =>
                panel.ChildrenOfType<CustomThemeColourRow>().All(r => r.ChildrenOfType<OsuTextFlowContainer>().Any()));

            AddAssert("Context colour rows use OsuTextFlowContainer for hints", () =>
                panel.ChildrenOfType<CustomThemeContextColourRow>().All(r => r.ChildrenOfType<OsuTextFlowContainer>().Any()));

            AddAssert("Radius rows use OsuTextFlowContainer for hints", () =>
                panel.ChildrenOfType<CustomThemeRadiusRow>().All(r => r.ChildrenOfType<OsuTextFlowContainer>().Any()));

            AddAssert("TopBarPreview height is at least 60", () =>
                panel.ChildrenOfType<CustomThemeSettingsPanel.CustomThemeTopBarSection.TopBarPreview>().All(p => p.Height >= 60));
        }
    }
}
