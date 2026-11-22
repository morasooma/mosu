using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Overlays.Settings.Sections;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// Compact navigation for Morasooma's subsections, using the same buttons as ruleset navigation.
    /// </summary>
    public partial class ForkSidebarSubsections : FillFlowContainer
    {
        private readonly ForkSection section;
        private readonly SettingsPanel.SettingsSectionsContainer sections;
        private SubsectionButton[] buttons = Array.Empty<SubsectionButton>();
        private SubsectionButton toggle = null!;
        private SettingsSubsection? current;
        private bool expanded;

        public bool Expanded => expanded;
        public SettingsSubsection? Current => current;

        public ForkSidebarSubsections(ForkSection section, SettingsPanel.SettingsSectionsContainer sections)
        {
            this.section = section;
            this.sections = sections;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            buttons = section.Children.OfType<SettingsSubsection>().Select(subsection => new SubsectionButton
            {
                Section = section,
                Subsection = subsection,
                Action = () =>
                {
                    current = subsection;
                    sections.ScrollTo(subsection);
                    updateButtons();
                }
            }).ToArray();

            current = buttons.FirstOrDefault()?.Subsection;
            AddRange(buttons);
            Add(toggle = new SubsectionButton
            {
                Section = section,
                Action = () =>
                {
                    expanded = !expanded;
                    updateButtons();
                }
            });
            sections.SelectedSection.BindValueChanged(_ => updateButtons(), true);
        }

        public void Collapse()
        {
            expanded = false;
            if (IsLoaded)
                updateButtons();
        }

        protected override void Update()
        {
            base.Update();

            if (sections.SelectedSection.Value != section)
                return;

            // Match the content viewport's selection line, below the fixed search header.
            float selectionY = sections.ToScreenSpace(new osuTK.Vector2(0,
                (sections.FixedHeader?.LayoutSize.Y ?? 0) + sections.DrawHeight * 0.1f)).Y;
            SettingsSubsection? visible = null;
            foreach (var button in buttons)
            {
                var subsection = button.Subsection!;
                if (!subsection.IsPresent)
                    continue;

                visible ??= subsection;
                if (subsection.ScreenSpaceDrawQuad.TopLeft.Y <= selectionY)
                    visible = subsection;
            }

            if (visible != null && visible != current)
            {
                current = visible;
                updateButtons();
            }
        }

        private void updateButtons()
        {
            bool sectionSelected = sections.SelectedSection.Value == section;
            foreach (var button in buttons)
            {
                button.SetExpanded(expanded || button.Subsection == current);
                button.Selected = sectionSelected && button.Subsection == current;
            }

            toggle.Expanded = expanded;
        }

        private partial class SubsectionButton : SidebarIconButton
        {
            public SettingsSubsection? Subsection { get; init; }
            private bool expanded;
            private bool visible = true;
            private SpriteIcon? icon;
            private const float expanded_height = 46 * 0.8f;

            public void SetExpanded(bool value)
            {
                if (visible == value)
                    return;

                visible = value;
                if (IsLoaded)
                    animateVisibility();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                // Keep vertical padding out of the animated height so the row can fully collapse.
                Padding = new MarginPadding { Horizontal = Padding.Left };
                Height = visible ? expanded_height : 0;
                Alpha = visible ? 1 : 0;
            }

            private void animateVisibility()
            {
                this.ResizeHeightTo(visible ? expanded_height : 0, InterfaceAnimation.MovementDuration(260), Easing.OutQuint);
                this.FadeTo(visible ? 1 : 0, InterfaceAnimation.FadeDuration(180), Easing.OutQuint);
            }

            public bool Expanded
            {
                get => expanded;
                set
                {
                    expanded = value;
                    RefreshLabel();
                    icon?.RotateTo(value ? 180 : 0, InterfaceAnimation.MovementDuration(260), Easing.OutQuint);
                }
            }

            // Unlike the top-level buttons, hidden subsections must not occupy flow space.
            public override bool IsPresent => Alpha > 0 || Height > 0;
            protected override bool UseSmallerButton => true;
            protected override LocalisableString Label => Subsection?.SidebarHeader
                ?? (Expanded ? SettingsNavigationStrings.Collapse : SettingsNavigationStrings.Expand);

            protected override Drawable CreateIcon() => icon = new SpriteIcon
            {
                Icon = Subsection switch
                {
                    ForkConnectionSettings => OsuIcon.NavigationPlug,
                    ForkSettings => OsuIcon.NavigationSliders,
                    ForkPerformanceSettings => OsuIcon.NavigationGauge,
                    ForkInterfaceSettings => OsuIcon.NavigationPalette,
                    ForkAiSettings => OsuIcon.NavigationBrain,
                    ForkDebugSettings => OsuIcon.NavigationBug,
                    ForkCommunitySettings => OsuIcon.NavigationUsers,
                    ConfigurationBackupSettings => OsuIcon.NavigationBackup,
                    ServerProfilesSettings => OsuIcon.NavigationServers,
                    null => OsuIcon.NavigationExpand,
                    _ => OsuIcon.NavigationSettings
                },
                Rotation = Subsection == null && Expanded ? 180 : 0
            };

        }
    }
}
