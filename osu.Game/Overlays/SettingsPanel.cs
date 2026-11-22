// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    [Cached]
    public abstract partial class SettingsPanel : OsuFocusedOverlayContainer
    {
        public const float CONTENT_MARGINS = 20;

        // extra right padding to give room to the revert-to-default button in settings controls.
        public static readonly MarginPadding CONTENT_PADDING = new MarginPadding { Left = 12, Right = 22 };

        public const float TRANSITION_LENGTH = 600;

        private const float sidebar_width = SettingsSidebar.EXPANDED_WIDTH;

        /// <summary>
        /// The width of the settings panel content, excluding the sidebar.
        /// </summary>
        public const float PANEL_WIDTH = 400;

        /// <summary>
        /// The full width of the settings panel, including the sidebar.
        /// </summary>
        public const float WIDTH = sidebar_width + PANEL_WIDTH;

        protected Container<Drawable> ContentContainer;

        protected override Container<Drawable> Content => ContentContainer;

        protected SettingsSidebar Sidebar;
        private SidebarIconButton selectedSidebarButton;

        public SettingsSectionsContainer SectionsContainer { get; private set; }

        protected SeekLimitedSearchTextBox SearchTextBox { get; private set; }

        protected override string PopInSampleName => "UI/settings-pop-in";
        protected override double PopInOutSampleBalance => -OsuGameBase.SFX_STEREO_STRENGTH;

        private readonly bool showBackButton;

        private LoadingLayer loading;
        private BackdropBlurSurface contentBackground;
        private IBindable<Colour4> themeColour;

        protected override BackdropBlurSurface OpaqueBackground => contentBackground;

        private readonly List<SettingsSection> loadableSections = new List<SettingsSection>();

        private Task sectionsLoadingTask;
        private bool sectionFirstUpdateAdmitted;
        private bool subsectionFirstUpdateAdmitted;

        protected virtual bool StaggerSectionFirstUpdates => false;

        internal bool AdmitSectionFirstUpdate()
        {
            if (!StaggerSectionFirstUpdates)
                return true;

            if (sectionFirstUpdateAdmitted)
                return false;

            sectionFirstUpdateAdmitted = true;
            return true;
        }

        internal bool AdmitSubsectionFirstUpdate()
        {
            if (!StaggerSectionFirstUpdates)
                return true;

            if (subsectionFirstUpdateAdmitted)
                return false;

            subsectionFirstUpdateAdmitted = true;
            return true;
        }

        public IBindable<SettingsSection> CurrentSection = new Bindable<SettingsSection>();

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple, ThemeSurfaceContext.Settings);

        protected SettingsPanel(bool showBackButton)
        {
            this.showBackButton = showBackButton;
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = ContentContainer = new NonMaskedContent
            {
                X = -WIDTH + ExpandedPosition,
                Width = PANEL_WIDTH,
                RelativeSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    contentBackground = new BackdropBlurSurface
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Scale = new Vector2(2, 1), // over-extend to the left for transitions
                        RelativeSizeAxes = Axes.Both,
                        SurfaceColour = colourProvider.Background4,
                        Alpha = 1,
                        Depth = float.MaxValue,
                    },
                    loading = new LoadingLayer
                    {
                        State = { Value = Visibility.Visible }
                    }
                }
            };

            Add(new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = SectionsContainer = new SettingsSectionsContainer
                {
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Colour = Color4.Black.Opacity(0),
                        Type = EdgeEffectType.Shadow,
                        Hollow = true,
                        Radius = 10
                    },
                    MaskingSmoothness = 0,
                    RelativeSizeAxes = Axes.Both,
                    ExpandableHeader = CreateHeader(),
                    SelectedSection = { BindTarget = CurrentSection },
                    FixedHeader = new FillFlowContainer
                    {
                        Direction = FillDirection.Vertical,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding
                        {
                            Vertical = 6,
                            Left = CONTENT_PADDING.Left,
                            Right = CONTENT_PADDING.Right,
                        },
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Children = new Drawable[]
                        {
                            SearchTextBox = new SettingsSearchTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                            CreateSelectionControls(),
                        }
                    },
                    Footer = CreateFooter().With(f => f.Alpha = 0)
                }
            });

            AddInternal(Sidebar = new SettingsSidebar(showBackButton)
            {
                BackButtonAction = Hide,
                Width = sidebar_width
            });

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background4);
            themeColour.BindValueChanged(_ => contentBackground.SurfaceColour = colourProvider.Background4, true);
        }

        protected void AddSection(SettingsSection section)
        {
            if (IsLoaded)
                // just to keep things simple. can be accommodated for if we ever need it.
                throw new InvalidOperationException("All sections must be added before the panel is loaded.");

            loadableSections.Add(section);
        }

        internal IReadOnlyList<SettingsSection> AvailableSections => loadableSections;
        internal bool SectionsReady { get; private set; }
        internal event Action SectionsLoaded;
        internal void EnsureSectionsLoaded(SettingsPanel loader = null) => loadSections(loader);

        protected virtual Drawable CreateSelectionControls() => new Container();

        protected virtual Drawable CreateHeader() => new Container();

        protected virtual Drawable CreateFooter() => new Container();

        protected override void PopIn()
        {
            foreach (var subsections in Sidebar.Children.OfType<ForkSidebarSubsections>())
                subsections.Collapse();

            ContentContainer.MoveToX(ExpandedPosition, InterfaceAnimation.TransitionDuration(TRANSITION_LENGTH), InterfaceAnimation.TransitionEasing(Easing.OutQuint));

            SectionsContainer.FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, InterfaceAnimation.FadeDuration(WaveContainer.APPEAR_DURATION), Easing.Out);

            // delay load enough to ensure it doesn't overlap with the initial animation.
            // this is done as there is still a brief stutter during load completion which is more visible if the transition is in progress.
            // the eventual goal would be to remove the need for this by splitting up load into smaller work pieces, or fixing the remaining
            // load complete overheads.
            Scheduler.AddDelayed(() => loadSections(), InterfaceAnimation.MovementDuration(TRANSITION_LENGTH) / 3);

            Sidebar?.MoveToX(0, InterfaceAnimation.TransitionDuration(TRANSITION_LENGTH), InterfaceAnimation.TransitionEasing(Easing.OutQuint));
            this.FadeTo(1, InterfaceAnimation.TransitionFadeDuration(TRANSITION_LENGTH / 2), InterfaceAnimation.TransitionEasing(Easing.OutQuint));

            SearchTextBox.TakeFocus();
            SearchTextBox.HoldFocus = true;
        }

        protected virtual float ExpandedPosition => 0;

        protected override void PopOut()
        {
            base.PopOut();

            SectionsContainer.FadeEdgeEffectTo(0, InterfaceAnimation.FadeDuration(WaveContainer.DISAPPEAR_DURATION), Easing.In);
            ContentContainer.MoveToX(-WIDTH + ExpandedPosition, InterfaceAnimation.TransitionDuration(TRANSITION_LENGTH), InterfaceAnimation.TransitionEasing(Easing.OutQuint));

            Sidebar?.MoveToX(-sidebar_width, InterfaceAnimation.TransitionDuration(TRANSITION_LENGTH), InterfaceAnimation.TransitionEasing(Easing.OutQuint));
            this.FadeTo(0, InterfaceAnimation.TransitionFadeDuration(TRANSITION_LENGTH / 2), InterfaceAnimation.TransitionEasing(Easing.OutQuint));

            SearchTextBox.HoldFocus = false;
            if (SearchTextBox.HasFocus)
                GetContainingFocusManager()!.ChangeFocus(null);
        }

        public override bool AcceptsFocus => true;

        protected override void OnFocus(FocusEvent e)
        {
            SearchTextBox.TakeFocus();
            base.OnFocus(e);
        }

        protected override void Update()
        {
            base.Update();
            sectionFirstUpdateAdmitted = false;
            subsectionFirstUpdateAdmitted = false;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            ContentContainer.Margin = new MarginPadding { Left = Sidebar?.DrawWidth ?? 0 };
        }

        private const double fade_in_duration = 500;

        private void loadSections(SettingsPanel loader = null)
        {
            if (sectionsLoadingTask != null)
                return;

            sectionsLoadingTask = LoadComponentsAsync(loadableSections, sections =>
            {
                SectionsContainer.AddRange(sections);
                SectionsContainer.Footer.FadeInFromZero(InterfaceAnimation.FadeDuration(fade_in_duration), Easing.OutQuint);
                SectionsContainer.SearchContainer.FadeInFromZero(InterfaceAnimation.FadeDuration(fade_in_duration), Easing.OutQuint);
                loading.Hide();
                SectionsReady = true;
                SectionsLoaded?.Invoke();
                SearchTextBox.Current.BindValueChanged(term => SectionsContainer.SearchTerm = term.NewValue, true);
                loadSidebarButtons();
            }, scheduler: loader?.Scheduler);
        }

        private void loadSidebarButtons()
        {
            if (Sidebar == null)
                return;

            LoadComponentsAsync(createSidebarButtons(), buttons =>
            {
                float delay = 0;

                foreach (var button in buttons)
                {
                    Sidebar.Add(button);

                    button.FadeOut()
                          .Delay(InterfaceAnimation.MovementDuration(delay))
                          .FadeInFromZero(InterfaceAnimation.FadeDuration(fade_in_duration), Easing.OutQuint);

                    delay += 40;
                }

                SectionsContainer.SelectedSection.BindValueChanged(section =>
                {
                    selectedSidebarButton?.Selected = false;

                    selectedSidebarButton = Sidebar.Children.OfType<SidebarIconButton>().FirstOrDefault(b => b.Section == section.NewValue);

                    selectedSidebarButton?.Selected = true;
                }, true);
            });
        }

        private IEnumerable<Drawable> createSidebarButtons()
        {
            foreach (var section in SectionsContainer)
            {
                yield return new SidebarIconButton
                {
                    Section = section,
                    Action = () =>
                    {
                        if (!SectionsContainer.IsLoaded)
                            return;

                        SectionsContainer.ScrollTo(section);
                    },
                };

                if (section is ForkSection fork)
                    yield return new ForkSidebarSubsections(fork, SectionsContainer);
            }
        }

        private partial class NonMaskedContent : Container<Drawable>
        {
            // masking breaks the pan-out transform with nested sub-settings panels.
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
        }

        public partial class SettingsSectionsContainer : SectionsContainer<SettingsSection>
        {
            public SearchContainer<SettingsSection> SearchContainer;

            internal void RefreshSearch()
            {
                string term = SearchTerm;
                SearchTerm = term + " ";
                SearchTerm = term;
            }

            public string SearchTerm
            {
                get => SearchContainer.SearchTerm;
                set => SearchContainer.SearchTerm = value;
            }

            protected override FlowContainer<SettingsSection> CreateScrollContentContainer()
                => SearchContainer = new SearchContainer<SettingsSection>
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Vertical,
                };

            private IBindable<Colour4> headerBgColour;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                HeaderBackground = new Box
                {
                    Colour = colourProvider.Background5,
                    RelativeSizeAxes = Axes.Both
                };

                headerBgColour = colourProvider.GetColourBindable(OverlayColour.Background5);
                headerBgColour.BindValueChanged(c => HeaderBackground.Colour = c.NewValue, true);

                SearchContainer.FilterCompleted += InvalidateScrollPosition;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // no null check because the usage of this class is strict
                HeaderBackground!.Alpha = -ExpandableHeader!.Y / ExpandableHeader.LayoutSize.Y;
            }
        }
    }
}
