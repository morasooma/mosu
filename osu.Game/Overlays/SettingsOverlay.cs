// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Cursor;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Rulesets;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class SettingsOverlay : SettingsPanel, INamedOverlayComponent
    {
        public IconUsage Icon => OsuIcon.Settings;
        public LocalisableString Title => SettingsStrings.HeaderTitle;
        public LocalisableString Description => SettingsStrings.HeaderDescription;

        private readonly List<SettingsSubPanel> subPanels = new List<SettingsSubPanel>();

        private SettingsSubPanel? lastOpenedSubPanel;
        // Historical sound-pack data remains readable, but no editor is mounted.
        [Obsolete("The sound editor has been replaced by resource packs.")]
        public void ShowSoundEditor() { }

        protected override bool StaggerSectionFirstUpdates => true;

        protected override bool HasOpaqueBackground => base.HasOpaqueBackground
                                                      && (lastOpenedSubPanel?.State.Value != Visibility.Visible || lastOpenedSubPanel.CanUseOpaqueDrawPrepass);

        protected override Drawable CreateHeader() => new SettingsHeader(Title, Description);

        protected override Drawable CreateFooter() => new OsuContextMenuContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Child = new SettingsFooter()
        };

        public SettingsOverlay()
            : base(false)
        {
        }

        public override bool AcceptsFocus => lastOpenedSubPanel == null || lastOpenedSubPanel.State.Value == Visibility.Hidden;

        public void ShowAtControl<T>() where T : Drawable
        {
            // if search isn't cleared then the target control won't be visible if it doesn't match the query
            SearchTextBox.Current.SetDefault();

            Show();

            // wait for load of sections
            if (!SectionsContainer.Any())
            {
                Scheduler.Add(ShowAtControl<T>);
                return;
            }

            SectionsContainer.ScrollTo(SectionsContainer.ChildrenOfType<T>().Single());
        }

        protected override float ExpandedPosition => lastOpenedSubPanel?.State.Value == Visibility.Visible ? -PANEL_WIDTH : base.ExpandedPosition;

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets, osu.Game.Configuration.OsuConfigManager config)
        {
            Favourites = new FavouriteSettingsStore(config.GetBindable<string>(osu.Game.Configuration.OsuSetting.ForkFavouriteSettings));
            favouritePanel = createSubPanel(new FavouriteSettingsPanel(this));
            SectionsLoaded += () => registerFavouriteRows(this);
            var skinVisibility = createSubPanel(new SkinVisibilitySettingsPanel());

            var sections = new List<SettingsSection>
            {
                // This list should be kept in sync with ScreenBehaviour.
                new ForkSection(createSubPanel(new PerformanceOptimisationSettingsPanel()), createSubPanel(new CustomThemeSettingsPanel()), skinVisibility, createSubPanel(new BackgroundCalculationSettingsPanel()), createSubPanel(new AiFunctionSettingsPanel())),
                new GeneralSection(),
                new SkinSection(skinVisibility),
                new InputSection(createSubPanel(new KeyBindingPanel())),
                new UserInterfaceSection(),
                new GameplaySection()
            };

            foreach (Ruleset ruleset in rulesets.AvailableRulesets.Select(info => info.CreateInstance()))
            {
                try
                {
                    SettingsSubsection? section = ruleset.CreateSettings();

                    if (section != null)
                        sections.Add(new RulesetSection(ruleset, section));
                }
                catch (Exception e)
                {
                    RulesetStore.LogRulesetFailure(ruleset.RulesetInfo, e);
                }
            }

            sections.AddRange(new SettingsSection[]
            {
                new AudioSection(),
                new GraphicsSection(),
                new OnlineSection(),
                new MaintenanceSection(),
                new DebugSection()
            });

            var today = DateTimeOffset.Now;
            if (today.Month == 4 && today.Day == 1)
                sections.Insert(9, new AfToggleSection());

            foreach (var s in sections)
                AddSection(s);

            foreach (var s in subPanels)
                ContentContainer.Add(s);
        }

        private T createSubPanel<T>(T subPanel)
            where T : SettingsSubPanel
        {
            subPanel.Depth = 1;
            subPanel.Anchor = Anchor.TopRight;
            subPanel.State.ValueChanged += e => subPanelStateChanged(subPanel, e);

            if (subPanel is not FavouriteSettingsPanel)
                subPanel.SectionsLoaded += () => registerFavouriteRows(subPanel);
            subPanels.Add(subPanel);

            return subPanel;
        }

        private void subPanelStateChanged(SettingsSubPanel panel, ValueChangedEvent<Visibility> state)
        {
            switch (state.NewValue)
            {
                case Visibility.Visible:
                    foreach (var other in subPanels.Where(p => p != panel && p.State.Value == Visibility.Visible))
                        other.Hide();
                    Sidebar.Expanded.Value = false;
                    Sidebar.FadeColour(Color4.DarkGray, 300, Easing.OutQuint);

                    SectionsContainer.FadeOut(InterfaceAnimation.TransitionFadeDuration(300), InterfaceAnimation.TransitionEasing(Easing.OutQuint));
                    ContentContainer.MoveToX(-PANEL_WIDTH, InterfaceAnimation.TransitionDuration(500), InterfaceAnimation.TransitionEasing(Easing.OutQuint));

                    lastOpenedSubPanel = panel;

                    break;

                case Visibility.Hidden:
                    Sidebar.Expanded.Value = true;
                    Sidebar.FadeColour(Color4.White, 300, Easing.OutQuint);

                    SectionsContainer.FadeIn(InterfaceAnimation.TransitionFadeDuration(500), InterfaceAnimation.TransitionEasing(Easing.OutQuint));
                    ContentContainer.MoveToX(0, InterfaceAnimation.TransitionDuration(500), InterfaceAnimation.TransitionEasing(Easing.OutQuint));
                    break;
            }
        }
    }
}
