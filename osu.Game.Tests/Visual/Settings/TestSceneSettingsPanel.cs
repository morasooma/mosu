// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Overlays.Settings.Sections.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneSettingsPanel : OsuManualInputManagerTestScene
    {
        // Fork controls need OsuGame for button callbacks; this fixture does
        // not launch those actions or load a second game.
        [Cached]
        private readonly OsuGame game = new OsuGame();

        // The cached game is only a callback target, not a running game for the menu cursor.
        protected override bool DisplayCursorForManualInput => false;

        private SettingsPanel settings;
        private DialogOverlay dialogOverlay;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create settings", () =>
            {
                settings?.Expire();

                Add(settings = new SettingsOverlay
                {
                    State = { Value = Visibility.Visible }
                });
            });
        }

        [Test]
        public void TestSmokeTagFieldVisibilityAndBinding()
        {
            SettingsItemV2 textRow = null;
            SettingsItemV2 toggleRow = null;
            osu.Game.Configuration.OsuConfigManager config = null;
            AddStep("open fork settings with smoke disabled", () =>
            {
                config = Dependencies.Get<osu.Game.Configuration.OsuConfigManager>();
                config.SetValue(osu.Game.Configuration.OsuSetting.ForkMorasoomaEndTag, false);
                config.SetValue(osu.Game.Configuration.OsuSetting.ForkMorasoomaEndTagText, "MORASOOMA");
                ((SettingsOverlay)settings).ShowAtControl<osu.Game.Overlays.Settings.Sections.Fork.ForkSettings>();
            });
            AddUntilStep("smoke controls loaded", () =>
            {
                var rows = settings.ChildrenOfType<SettingsItemV2>().ToArray();
                textRow = rows.SingleOrDefault(r => r.FavouriteId == "OsuSetting.ForkMorasoomaEndTagText");
                toggleRow = rows.SingleOrDefault(r => r.FavouriteId == "OsuSetting.ForkMorasoomaEndTag");
                return textRow?.IsLoaded == true && toggleRow?.IsLoaded == true;
            });
            AddUntilStep("text field hidden when disabled", () => !textRow.IsPresent);
            AddStep("enable through actual toggle", () => ((osu.Game.Graphics.UserInterfaceV2.FormCheckBox)toggleRow.Control).Current.Value = true);
            AddUntilStep("text field appears below toggle", () => textRow.IsPresent && textRow.DrawHeight > 0
                && textRow.ScreenSpaceDrawQuad.AABBFloat.Top >= toggleRow.ScreenSpaceDrawQuad.AABBFloat.Bottom - 1);
            AddAssert("old text is the default", () => ((osu.Game.Graphics.UserInterfaceV2.FormTextBox)textRow.Control).Current.Value == "MORASOOMA");
            AddStep("edit tag", () => ((osu.Game.Graphics.UserInterfaceV2.FormTextBox)textRow.Control).Current.Value = "ПРИВЕТ 123");
            AddAssert("text saved to config", () => config.Get<string>(osu.Game.Configuration.OsuSetting.ForkMorasoomaEndTagText) == "ПРИВЕТ 123");
            AddStep("disable tag", () => ((osu.Game.Graphics.UserInterfaceV2.FormCheckBox)toggleRow.Control).Current.Value = false);
            AddUntilStep("text field disappears", () => !textRow.IsPresent);
            AddStep("enable again", () => ((osu.Game.Graphics.UserInterfaceV2.FormCheckBox)toggleRow.Control).Current.Value = true);
            AddUntilStep("custom text preserved", () => textRow.IsPresent
                && ((osu.Game.Graphics.UserInterfaceV2.FormTextBox)textRow.Control).Current.Value == "ПРИВЕТ 123");
            AddStep("reset text through existing reset action", () => textRow.ApplyDefault());
            AddAssert("old tag restored", () => config.Get<string>(osu.Game.Configuration.OsuSetting.ForkMorasoomaEndTagText) == "MORASOOMA");
            AddStep("restore disabled default", () => config.SetValue(osu.Game.Configuration.OsuSetting.ForkMorasoomaEndTag, false));
        }

        [Test]
        public void TestBasic()
        {
            AddStep("do nothing", () => { });
            AddToggleStep("toggle visibility", visible => settings.State.Value = visible ? Visibility.Visible : Visibility.Hidden);
        }

        [Test]
        public void TestSectionFirstUpdatesAreSpreadAcrossFrames()
        {
            var initialized = new HashSet<SettingsSection>();
            var initializedSubsections = new HashSet<SettingsSubsection>();
            int maximumPerFrame = 0;
            int maximumSubsectionsPerFrame = 0;
            AddStep("create isolated multi-section panel", () =>
            {
                settings.Expire();
                Add(settings = new SectionLoadingTestPanel { State = { Value = Visibility.Visible } });
                settings.OnUpdate += _ =>
                {
                    int added = 0;
                    foreach (var section in settings.SectionsContainer.Children.Where(s => s.IsLoaded))
                    {
                        if (initialized.Add(section))
                            added++;
                    }
                    maximumPerFrame = System.Math.Max(maximumPerFrame, added);
                    int addedSubsections = 0;
                    foreach (var subsection in settings.SectionsContainer.ChildrenOfType<SettingsSubsection>().Where(s => s.IsLoaded))
                    {
                        if (initializedSubsections.Add(subsection))
                            addedSubsections++;
                    }
                    maximumSubsectionsPerFrame = System.Math.Max(maximumSubsectionsPerFrame, addedSubsections);
                };
            });
            AddUntilStep("all section data published", () => settings.SectionsContainer.Children.Count == SectionLoadingTestPanel.SECTION_COUNT);
            AddUntilStep("every section initialized", () => initialized.Count == SectionLoadingTestPanel.SECTION_COUNT);
            for (int i = 0; i < SectionLoadingTestPanel.SECTION_COUNT; i++)
            {
                int index = i;
                AddStep($"scroll to section {index}", () => settings.SectionsContainer.ScrollTo(settings.SectionsContainer.Children[index]));
                AddUntilStep("visible subsections initialized", () => settings.SectionsContainer.Children[index]
                    .ChildrenOfType<SettingsSubsection>().All(s => s.IsLoaded));
            }
            AddUntilStep("every subsection initialized", () => initializedSubsections.Count == SectionLoadingTestPanel.SECTION_COUNT * 3);
            AddAssert("at most one section initialized per frame", () => maximumPerFrame == 1);
            AddAssert("at most one subsection initialized per frame", () => maximumSubsectionsPerFrame == 1);
            AddStep("hide panel", () => settings.Hide());
            AddWaitStep("finish hiding", 5);
            AddStep("reopen panel", () => settings.Show());
            AddAssert("loaded sections retained", () => settings.SectionsContainer.Children.All(initialized.Contains));
        }

        [Test]
        public void TestFilteringDuringStagedLoad()
        {
            SectionLoadingTestPanel panel = null;
            bool filteredBeforeInitialization = false;

            AddStep("search as section data is published", () =>
            {
                settings.Expire();
                Add(settings = panel = new SectionLoadingTestPanel { State = { Value = Visibility.Visible } });
                panel.OnUpdate += _ =>
                {
                    if (filteredBeforeInitialization || panel.SectionsContainer.Children.Count == 0)
                        return;

                    filteredBeforeInitialization = panel.SectionsContainer.Children.Any(s => !s.IsLoaded);
                    panel.SearchTerm = "Subsection 23";
                };
            });
            AddUntilStep("matching subsection initialized", () => panel.SectionsContainer.Children.Count == SectionLoadingTestPanel.SECTION_COUNT
                && panel.SectionsContainer.Children.Last().Children.OfType<SettingsSubsection>().Last().IsLoaded);
            AddAssert("searched before first updates completed", () => filteredBeforeInitialization);
            AddUntilStep("only matching section is present", () => panel.SectionsContainer.Children.Count(s => s.IsPresent) == 1
                && panel.SectionsContainer.Children.Last().IsPresent);
            AddUntilStep("only matching subsection is present", () => panel.SectionsContainer.ChildrenOfType<SettingsSubsection>().Count(s => s.IsPresent) == 1);
            AddUntilStep("matching control is visible", () => panel.SectionsContainer.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(
                panel.SectionsContainer.Children.Last().Children.OfType<SettingsSubsection>().Last().ScreenSpaceDrawQuad.AABBFloat));
            AddStep("clear search", () => panel.SearchTerm = string.Empty);
            AddUntilStep("all sections present again", () => panel.SectionsContainer.Children.All(s => s.IsPresent));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestShowAtControlDuringStagedLoad(bool beforeSectionsPublished)
        {
            LayoutSettings target = null;
            bool requested = false;

            AddStep("request direct navigation during first load", () =>
            {
                settings.Expire();
                Add(settings = new SettingsOverlay { State = { Value = Visibility.Visible } });
                settings.OnUpdate += _ =>
                {
                    if (requested || (settings.SectionsContainer.Children.Count == 0) != beforeSectionsPublished)
                        return;

                    // In the second case the complete searchable tree is attached,
                    // but its sections have not all received their first update.
                    if (!beforeSectionsPublished && settings.SectionsContainer.Children.All(s => s.IsLoaded))
                        return;

                    requested = true;
                    ((SettingsOverlay)settings).ShowAtControl<LayoutSettings>();
                };
            });
            AddUntilStep("navigation requested at expected stage", () => requested);
            AddUntilStep("target initialized", () => (target = settings.SectionsContainer.ChildrenOfType<LayoutSettings>().SingleOrDefault())?.IsLoaded == true);
            AddUntilStep("target section selected", () => settings.CurrentSection.Value is GraphicsSection);
            AddUntilStep("control visible below fixed search", controlVisible);
            AddWaitStep("allow remaining layout to settle", 5);
            AddAssert("control remains visible after layout", controlVisible);

            bool controlVisible() => target != null && target.IsLoaded && target.IsPresent
                && target.ScreenSpaceDrawQuad.TopLeft.Y >= settings.SectionsContainer.FixedHeader.ScreenSpaceDrawQuad.BottomLeft.Y - 1
                && target.ScreenSpaceDrawQuad.TopLeft.Y < settings.SectionsContainer.ChildrenOfType<UserTrackingScrollContainer>().First().ScreenSpaceDrawQuad.BottomLeft.Y;
        }

        [Test]
        public void TestForkSubsectionNavigation()
        {
            ForkSidebarSubsections navigation = null;
            SidebarIconButton[] buttons = null;

            AddStep("create isolated navigation panel", () =>
            {
                settings.Expire();
                Add(settings = new ForkNavigationTestPanel { State = { Value = Visibility.Visible } });
            });
            AddUntilStep("fork navigation loaded", () =>
                (navigation = settings.ChildrenOfType<ForkSidebarSubsections>().FirstOrDefault())?.IsLoaded == true);
            AddStep("get subsection buttons", () => buttons = navigation.Children.OfType<SidebarIconButton>().ToArray());
            AddAssert("collapsed by default", () => !navigation.Expanded);
            AddAssert("only current and expand visible", () => buttons.Count(b => b.IsPresent) == 2);
            AddStep("expand subsections", () => buttons.Last().TriggerClick());
            AddUntilStep("all subsections visible", () => navigation.Expanded && buttons.All(b => b.IsPresent));
            AddStep("open interface", () => buttons[3].TriggerClick());
            AddUntilStep("fork section selected", () => settings.CurrentSection.Value is ForkSection);
            AddUntilStep("interface selected", () => navigation.Current == settings.SectionsContainer
                .ChildrenOfType<ForkSection>().Single().Children.OfType<SettingsSubsection>().ElementAt(3));
            AddStep("collapse subsections", () => buttons.Last().TriggerClick());
            AddUntilStep("only interface and expand visible", () => !navigation.Expanded && buttons[3].IsPresent && buttons.Count(b => b.IsPresent) == 2);
            AddStep("scroll content to another subsection", () => settings.SectionsContainer.ScrollTo(
                settings.SectionsContainer.ChildrenOfType<ForkSection>().Single().Children.OfType<SettingsSubsection>().ElementAt(5)));
            AddUntilStep("compact navigation follows scrolling", () => buttons[5].Selected && buttons[5].IsPresent && !buttons[3].IsPresent);
            AddStep("expand again", () => buttons.Last().TriggerClick());
            AddStep("close settings", () => settings.Hide());
            AddStep("reopen settings", () => settings.Show());
            AddUntilStep("collapsed on reopen", () => !navigation.Expanded && buttons.Count(b => b.IsPresent) == 2);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestForkSubsectionSelectionClearedWhenLeavingSection(bool clickSidebar)
        {
            ForkSidebarSubsections navigation = null;
            SidebarIconButton[] buttons = null;
            SidebarIconButton otherSectionButton = null;
            SettingsSubsection current = null;

            AddStep("create isolated navigation panel", () =>
            {
                settings.Expire();
                Add(settings = new ForkNavigationTestPanel { State = { Value = Visibility.Visible } });
            });
            AddUntilStep("fork navigation loaded", () =>
                (navigation = settings.ChildrenOfType<ForkSidebarSubsections>().FirstOrDefault())?.IsLoaded == true);
            AddStep("get navigation buttons", () =>
            {
                buttons = navigation.Children.OfType<SidebarIconButton>().ToArray();
                otherSectionButton = settings.ChildrenOfType<SettingsSidebar>().Single().Children.OfType<SidebarIconButton>()
                    .Single(b => b.Section is not ForkSection);
            });
            AddStep("expand subsections", () => buttons.Last().TriggerClick());
            AddStep("select fork subsection", () => buttons[3].TriggerClick());
            AddUntilStep("fork subsection selected", () => settings.CurrentSection.Value is ForkSection && buttons[3].Selected);
            AddStep("leave fork section", () =>
            {
                if (clickSidebar)
                    otherSectionButton.TriggerClick();
                else
                    settings.SectionsContainer.ScrollTo(otherSectionButton.Section.Children.OfType<SettingsSubsection>().Last());
            });
            AddUntilStep("other section selected", () => settings.CurrentSection.Value == otherSectionButton.Section && otherSectionButton.Selected);
            AddUntilStep("fork subsection highlight cleared", () => buttons.All(b => !b.Selected));
            AddStep("remember inactive subsection", () => current = navigation.Current);
            AddWaitStep("allow layout to settle", 5);
            AddAssert("inactive subsection remembered without highlight", () => navigation.Current == current && buttons.All(b => !b.Selected));
            AddStep("return to remembered subsection", () => buttons[3].TriggerClick());
            AddUntilStep("fork subsection selected again", () => settings.CurrentSection.Value is ForkSection && buttons[3].Selected);
            AddAssert("other section highlight cleared", () => !otherSectionButton.Selected);
        }

        [Test]
        public void TestFiltering([Values] bool beforeLoad)
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            if (beforeLoad)
                AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");

            AddUntilStep("wait for items to load", () => settings.SectionsContainer.ChildrenOfType<IFilterable>().Any());

            if (!beforeLoad)
                AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");

            AddAssert("ensure all items match filter", () => settings.SectionsContainer
                                                                     .ChildrenOfType<SettingsSection>().Where(f => f.IsPresent)
                                                                     .All(section =>
                                                                         section.Children.Where(f => f.IsPresent)
                                                                                .OfType<ISettingsItem>()
                                                                                .OfType<IFilterable>()
                                                                                .All(f => f.FilterTerms.Any(t => t.ToString().Contains("scaling")))
                                                                     ));

            AddAssert("ensure section is current", () => settings.CurrentSection.Value is GraphicsSection);
            AddAssert("ensure section is placed first", () => settings.CurrentSection.Value.Y == 0);
        }

        [TestCase("ru", nameof(ForkSettingsStrings.FontsFolderBtn), false)]
        [TestCase("en", nameof(ForkSettingsStrings.FontsFolderBtn), false)]
        [TestCase("ru", nameof(ForkSettingsStrings.DiagnosticsBenchmarkBtn), false)]
        [TestCase("ru", nameof(ForkSettingsStrings.PerformanceOptimisationSettingsButton), false)]
        [TestCase("ru", nameof(ForkSettingsStrings.CustomThemeSettingsButton), false)]
        [TestCase("ru", nameof(ForkSettingsStrings.FontsFolderBtn), true)]
        [TestCase("ru", nameof(ForkSettingsStrings.DiagnosticsBenchmarkBtn), true)]
        [TestCase("ru", nameof(ForkSettingsStrings.PerformanceOptimisationSettingsButton), true)]
        [TestCase("ru", nameof(ForkSettingsStrings.CustomThemeSettingsButton), true)]
        public void TestSearchByButtonTitle(string language, string stringProperty, bool beforeLoad)
        {
            LocalisableString title = (LocalisableString)typeof(ForkSettingsStrings).GetProperty(stringProperty).GetValue(null);
            SettingsButtonV2 button = null;
            string previousLocale = null;
            FrameworkConfigManager frameworkConfig = null;
            LocalisationManager localisation = null;

            AddStep("set language", () =>
            {
                frameworkConfig = Dependencies.Get<FrameworkConfigManager>();
                localisation = Dependencies.Get<LocalisationManager>();
                previousLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
                frameworkConfig.SetValue(FrameworkSetting.Locale, language);
            });
            if (beforeLoad)
                AddStep("search title before sections load", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>()
                    .First().Current.Value = localisation.GetLocalisedString(title));
            AddUntilStep("button loaded", () => (button = settings.SectionsContainer.ChildrenOfType<SettingsButtonV2>()
                .SingleOrDefault(b => b.Text.Equals(title)))?.IsLoaded == true);
            if (!beforeLoad)
                AddStep("search visible button title", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>()
                    .First().Current.Value = localisation.GetLocalisedString(title));
            AddUntilStep("button and its section match", () => button.IsPresent
                && settings.SectionsContainer.Children.Single(s => s.ChildrenOfType<SettingsButtonV2>().Contains(button)).IsPresent
                && settings.SectionsContainer.ChildrenOfType<SettingsSubsection>().Single(s => s.ChildrenOfType<SettingsButtonV2>().Contains(button)).IsPresent);
            AddUntilStep("button is inside visible scroll area", () => button.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(
                settings.SectionsContainer.ChildrenOfType<UserTrackingScrollContainer>().First().ScreenSpaceDrawQuad.AABBFloat));
            AddStep("restore language", () => frameworkConfig.SetValue(FrameworkSetting.Locale, previousLocale));
        }

        [Test]
        public void TestSearchEveryButtonTitle()
        {
            SettingsButtonV2[] buttons = null;
            string previousLocale = null;
            int index = 0;
            bool searchPending = false;
            AddStep("use Russian", () =>
            {
                var config = Dependencies.Get<FrameworkConfigManager>();
                previousLocale = config.Get<string>(FrameworkSetting.Locale);
                config.SetValue(FrameworkSetting.Locale, "ru");
            });
            AddUntilStep("all sections attached", () => settings.SectionsContainer.Children.Count > 0);
            AddStep("collect buttons", () => buttons = settings.SectionsContainer.ChildrenOfType<SettingsButtonV2>()
                .Where(b => b.CanBeShown.Value).ToArray());
            AddAssert("buttons found", () => buttons.Length > 0);
            AddUntilStep("every button found by its localised title", () =>
            {
                if (index == buttons.Length)
                    return true;

                var button = buttons[index];
                if (!searchPending)
                {
                    settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value =
                        Dependencies.Get<LocalisationManager>().GetLocalisedString(button.Text);
                    searchPending = true;
                    return false;
                }

                Assert.That(button.IsPresent, Is.True, button.Text.ToString());
                Assert.That(settings.SectionsContainer.ChildrenOfType<SettingsSubsection>()
                    .Where(s => s.ChildrenOfType<SettingsButtonV2>().Contains(button)).All(s => s.IsPresent), Is.True, button.Text.ToString());
                Assert.That(settings.SectionsContainer.Children.Where(s => s.ChildrenOfType<SettingsButtonV2>().Contains(button))
                    .All(s => s.IsPresent), Is.True, button.Text.ToString());
                index++;
                searchPending = false;
                return index == buttons.Length;
            });
            AddStep("restore locale", () => Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, previousLocale));
        }

        [Test]
        public void TestFilterAfterLoad()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("wait for items to load", () => settings.SectionsContainer.ChildrenOfType<IFilterable>().Any());

            AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");
        }

        [Test]
        public void ToggleVisibility()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddWaitStep("wait some", 5);
            AddToggleStep("toggle visibility", _ => settings.ToggleVisibility());
        }

        [Test]
        public void TestTextboxFocusAfterNestedPanelBackButton()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("sections loaded", () => settings.SectionsContainer.Children.Count > 0);
            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("open key binding subpanel", () =>
            {
                settings.SectionsContainer
                        .ChildrenOfType<InputSection>().FirstOrDefault()?
                        .ChildrenOfType<OsuButton>().FirstOrDefault()?
                        .TriggerClick();
            });

            AddUntilStep("binding panel textbox focused", () => settings
                                                                .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                                                .ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("Press back", () => settings
                                        .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                        .ChildrenOfType<SettingsSidebar.BackButton>().FirstOrDefault()?.TriggerClick());

            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);
        }

        [Test]
        public void TestTextboxFocusAfterNestedPanelEscape()
        {
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("sections loaded", () => settings.SectionsContainer.Children.Count > 0);
            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("open key binding subpanel", () =>
            {
                settings.SectionsContainer
                        .ChildrenOfType<InputSection>().FirstOrDefault()?
                        .ChildrenOfType<OsuButton>().FirstOrDefault()?
                        .TriggerClick();
            });

            AddUntilStep("binding panel textbox focused", () => settings
                                                                .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                                                .ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("Escape", () => InputManager.Key(Key.Escape));

            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);
        }

        [Test]
        public void TestSearchTextBoxSelectedOnShow()
        {
            SettingsSearchTextBox searchTextBox = null!;

            AddStep("set text", () => (searchTextBox = settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First()).Current.Value = "some text");
            AddAssert("no text selected", () => searchTextBox.SelectedText == string.Empty);
            AddRepeatStep("toggle visibility", () => settings.ToggleVisibility(), 2);
            AddAssert("search text selected", () => searchTextBox.SelectedText == searchTextBox.Current.Value);
        }

        private partial class SectionLoadingTestPanel : SettingsPanel
        {
            public const int SECTION_COUNT = 8;
            protected override bool StaggerSectionFirstUpdates => true;

            public string SearchTerm
            {
                set => SearchTextBox.Current.Value = value;
            }

            public SectionLoadingTestPanel()
                : base(false)
            {
                for (int i = 0; i < SECTION_COUNT; i++)
                    AddSection(new SectionLoadingTestSection(i));
            }
        }

        private partial class SectionLoadingTestSection : SettingsSection
        {
            private readonly int index;
            public override osu.Framework.Localisation.LocalisableString Header => $"Section {index}";
            public override osu.Framework.Graphics.Drawable CreateIcon() => new osu.Framework.Graphics.Containers.Container();

            public SectionLoadingTestSection(int index)
            {
                this.index = index;
                for (int i = 0; i < 3; i++)
                    Add(new NavigationTestSubsection(index * 3 + i));
            }
        }

        private partial class ForkNavigationTestPanel : SettingsPanel
        {
            protected override bool StaggerSectionFirstUpdates => true;

            public ForkNavigationTestPanel()
                : base(false)
            {
                var fork = new ForkSection();
                // Exercise real navigation without unrelated runtime services required by fork controls.
                fork.Clear();
                for (int i = 0; i < 8; i++)
                    fork.Add(new NavigationTestSubsection(i));
                AddSection(fork);
                AddSection(new SectionLoadingTestSection(8));
            }
        }

        private partial class NavigationTestSubsection : SettingsSubsection
        {
            private readonly int index;
            protected override osu.Framework.Localisation.LocalisableString Header => $"Subsection {index}";

            public NavigationTestSubsection(int index)
            {
                this.index = index;
                Add(new osu.Framework.Graphics.Containers.Container
                {
                    RelativeSizeAxes = osu.Framework.Graphics.Axes.X,
                    Height = 500
                });
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(dialogOverlay = new DialogOverlay
            {
                Depth = -1
            });

            Dependencies.CacheAs<IDialogOverlay>(dialogOverlay);
        }
    }
}
