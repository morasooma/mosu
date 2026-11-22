// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osuTK;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Rulesets;
using osu.Framework.Input.Bindings;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.CustomTheme;
using osu.Framework.Graphics.Effects;

namespace osu.Game.Overlays.Toolbar
{
    public partial class Toolbar : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        public const float HEIGHT = 40;
        public const float FLOATING_HEIGHT = 58;
        public const float FLOATING_TOP_MARGIN = 6;
        public const float FLOATING_HORIZONTAL_MARGIN = 12;
        public const float TOOLTIP_HEIGHT = 30;

        /// <summary>
        /// Whether the user hid this <see cref="Toolbar"/> with <see cref="GlobalAction.ToggleToolbar"/>.
        /// In this state, automatic toggles should not occur, respecting the user's preference to have no toolbar.
        /// </summary>
        private bool hiddenByUser;

        /// <summary>
        /// Whether visibility is currently controlled by the edge-reveal auto-hide behaviour.
        /// While active, a previous manual Ctrl+T hide must not prevent revealing the toolbar.
        /// </summary>
        public bool AutoHideActive { get; set; }

        public bool SongSelectActive { get; set; }
        private float surfaceAlpha = 1f;

        public Action OnHome;

        // Offscreen theme captures omit the wall clock so publication thumbnails contain no local time.
        internal bool HideClockForCapture { get; init; }

        private ToolbarUserButton userButton;
        private ToolbarRulesetSelector rulesetSelector;

        private Container wrapperContainer;
        private Container contentContainer;
        private ToolbarBackground background;
        private Box barBackground;
        private Container leftButtonBackgroundPlate;
        private Box leftButtonBackground;
        private Container rightButtonBackgroundPlate;
        private Box rightButtonBackground;
        private Box rulesetFadeBox;
        private OsuScrollContainer rulesetScroll;
        private ToolbarServerInvitation serverInvitation;
        private InverseCornerFillet leftDownwardFillet;
        private InverseCornerFillet rightDownwardFillet;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple, ThemeSurfaceContext.TopBar);

        private IBindable<ThemeMode>? themeModeBindable;
        private IBindable<CustomThemeDefinition>? customThemeBindable;

        internal Container ContentContainer => contentContainer;
        internal Container WrapperContainer => wrapperContainer;
        internal Container LeftPod => leftButtonBackgroundPlate;
        internal Container RightPod => rightButtonBackgroundPlate;
        internal InverseCornerFillet LeftDownwardFillet => leftDownwardFillet;
        internal InverseCornerFillet RightDownwardFillet => rightDownwardFillet;
        public TopBarVariant CurrentVariant => topBarVariantBindable?.Value ?? TopBarVariant.Flat;
        public float CurrentRadius => topBarRadiusBindable?.Value ?? 10f;

        /// <summary>
        /// Determines whether a point in local toolbar coordinates falls within the transparent concave corner cutout
        /// when <see cref="TopBarVariant.DownwardRounded"/> is active.
        /// </summary>
        public bool IsInverseCornerTransparentAt(Vector2 toolbarLocalPos)
        {
            float radius = Math.Clamp(topBarRadiusBindable?.Value ?? 10f, 0f, 24f);
            if ((topBarVariantBindable?.Value ?? TopBarVariant.Flat) != TopBarVariant.DownwardRounded || radius <= 0f)
                return false;

            if (toolbarLocalPos.Y < HEIGHT || toolbarLocalPos.Y > HEIGHT + radius)
                return false;

            if (toolbarLocalPos.X >= 0f && toolbarLocalPos.X <= radius)
            {
                float dx = toolbarLocalPos.X - radius;
                float dy = toolbarLocalPos.Y - (HEIGHT + radius);
                return (dx * dx + dy * dy) <= (radius * radius);
            }

            if (toolbarLocalPos.X >= DrawWidth - radius && toolbarLocalPos.X <= DrawWidth)
            {
                float dx = toolbarLocalPos.X - (DrawWidth - radius);
                float dy = toolbarLocalPos.Y - (HEIGHT + radius);
                return (dx * dx + dy * dy) <= (radius * radius);
            }

            return false;
        }

        private IBindable<TopBarVariant> topBarVariantBindable;
        private IBindable<float> topBarRadiusBindable;

        private const double transition_time = 500;

        protected readonly IBindable<OverlayActivation> OverlayActivationMode = new Bindable<OverlayActivation>(OverlayActivation.All);

        // Toolbar and its components need keyboard input even when hidden.
        public override bool PropagateNonPositionalInputSubTree => OverlayActivationMode.Value != OverlayActivation.Disabled;

        public Toolbar()
        {
            RelativeSizeAxes = Axes.X;
            Size = new Vector2(1, HEIGHT);
            AlwaysPresent = true;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            float targetSurfaceAlpha = SongSelectActive && OverlayTransparency.SongSelectActive ? 0.25f : 1f;
            if (surfaceAlpha != targetSurfaceAlpha)
            {
                surfaceAlpha = targetSurfaceAlpha;
                updateLayout();
            }
            background.Alpha = surfaceAlpha;
            rulesetSelector.TransparentBackground = SongSelectActive && OverlayTransparency.SongSelectActive;
            rulesetScroll.Padding = new MarginPadding { Right = serverInvitation.Alpha > 0 ? Math.Min(serverInvitation.Width + 8, rulesetScroll.DrawWidth) : 0 };
            serverInvitation.Width = Math.Min(340, rulesetScroll.DrawWidth);
            // An opaque scroll-edge gradient does not belong on glass chrome.
            rulesetFadeBox.Alpha = serverInvitation.Alpha > 0 || (SongSelectActive && OverlayTransparency.SongSelectActive) ? 0 : 1;

            // this only needed to be set for the initial LoadComplete/Update, so layout completes and gets buttons in a state they can correctly handle keyboard input for hotkeys.
            AlwaysPresent = false;

            if ((topBarVariantBindable?.Value ?? TopBarVariant.Flat) == TopBarVariant.Attached)
            {
                float radius = Math.Clamp(topBarRadiusBindable?.Value ?? 10f, 0f, 24f);
                if (leftButtonBackgroundPlate.Parent != null)
                    leftButtonBackgroundPlate.Width = leftButtonBackgroundPlate.Parent.DrawWidth + radius;
                if (rightButtonBackgroundPlate.Parent != null)
                    rightButtonBackgroundPlate.Width = rightButtonBackgroundPlate.Parent.DrawWidth + radius;
            }
        }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame osuGame)
        {
            HoverInterceptor interceptor;

            Children = new Drawable[]
            {
                background = new ToolbarBackground(),
                leftDownwardFillet = new InverseCornerFillet(FilletDirection.Left)
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(0, HEIGHT),
                    Alpha = 0,
                },
                rightDownwardFillet = new InverseCornerFillet(FilletDirection.Right)
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(0, HEIGHT),
                    Alpha = 0,
                },
                wrapperContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        contentContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new SongSelectBackdropSurface
                                {
                                    IsActive = () => SongSelectActive,
                                    GetCornerRadius = () => contentContainer.CornerRadius,
                                },
                                barBackground = new Box
                                {
                                    Colour = OsuColour.Gray(0.1f),
                                    RelativeSizeAxes = Axes.Both,
                                },
                            },
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize)
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new Container
                                    {
                                        Name = "Left buttons",
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Depth = float.MinValue,
                                        Children = new Drawable[]
                                        {
                                            leftButtonBackgroundPlate = new Container
                                            {
                                                Alpha = 0,
                                                BypassAutoSizeAxes = Axes.Both,
                                                RelativeSizeAxes = Axes.None,
                                                Size = Vector2.Zero,
                                                Children = new Drawable[]
                                                {
                                                    new SongSelectBackdropSurface
                                                    {
                                                        IsActive = () => SongSelectActive,
                                                        GetCornerRadius = () => leftButtonBackgroundPlate.CornerRadius,
                                                    },
                                                    leftButtonBackground = new Box
                                                    {
                                                        Colour = OsuColour.Gray(0.1f),
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                },
                                            },
                                            new FillFlowContainer
                                            {
                                                Direction = FillDirection.Horizontal,
                                                RelativeSizeAxes = Axes.Y,
                                                AutoSizeAxes = Axes.X,
                                                Children = new Drawable[]
                                                {
                                                    new ToolbarSettingsButton(),
                                                    new ToolbarHomeButton
                                                    {
                                                        Action = () => OnHome?.Invoke()
                                                    },
                                                },
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        Name = "Ruleset selector",
                                        RelativeSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            rulesetScroll = new OsuScrollContainer(Direction.Horizontal)
                                            {
                                                ScrollbarVisible = false,
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = false,
                                                Children = new Drawable[]
                                                {
                                                    rulesetSelector = new ToolbarRulesetSelector()
                                                }
                                            },
                                            serverInvitation = new ToolbarServerInvitation
                                            {
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopRight,
                                            },
                                            rulesetFadeBox = new Box
                                            {
                                                Colour = ColourInfo.GradientHorizontal(OsuColour.Gray(0.1f).Opacity(0), OsuColour.Gray(0.1f)),
                                                Width = 50,
                                                RelativeSizeAxes = Axes.Y,
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopRight,
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        Name = "Right buttons",
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        Children = new Drawable[]
                                        {
                                            rightButtonBackgroundPlate = new Container
                                            {
                                                Alpha = 0,
                                                BypassAutoSizeAxes = Axes.Both,
                                                RelativeSizeAxes = Axes.None,
                                                Size = Vector2.Zero,
                                                Children = new Drawable[]
                                                {
                                                    new SongSelectBackdropSurface
                                                    {
                                                        IsActive = () => SongSelectActive,
                                                        GetCornerRadius = () => rightButtonBackgroundPlate.CornerRadius,
                                                    },
                                                    rightButtonBackground = new Box
                                                    {
                                                        Colour = OsuColour.Gray(0.1f),
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                },
                                            },
                                            new FillFlowContainer
                                            {
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopRight,
                                                Direction = FillDirection.Horizontal,
                                                RelativeSizeAxes = Axes.Y,
                                                AutoSizeAxes = Axes.X,
                                                Children = new Drawable[]
                                                {
                                                    new ToolbarRankingsButton(),
                                                    new ToolbarBeatmapListingButton(),
                                                    new ToolbarSupporterQuestsButton(),
                                                    new ToolbarThemeLibraryButton(),
                                                    new ToolbarDiscoveryButton(),
                                                    new ToolbarChatButton(),
                                                    new ToolbarSocialButton(),
                                                    new ToolbarMusicButton(),
                                                    //new ToolbarButton
                                                    //{
                                                    //    Icon = FontAwesome.Solid.search
                                                    //},
                                                    userButton = new ToolbarUserButton(),
                                                    new ToolbarClock { Alpha = HideClockForCapture ? 0 : 1 },
                                                    new ToolbarNotificationButton(),
                                                    new ToolbarMemoryDisplay(),
                                                }
                                            },
                                        }
                                    },
                                },
                            }
                        },
                    }
                },
                interceptor = new HoverInterceptor
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            ((IBindable<bool>)background.ShowGradient).BindTo(interceptor.ReceivedHover);

            if (osuGame != null)
                OverlayActivationMode.BindTo(osuGame.OverlayActivationMode);

            topBarVariantBindable = OverlayColourProvider.GetTopBarVariant();
            topBarRadiusBindable = OverlayColourProvider.GetTopBarRadius();

            topBarVariantBindable.BindValueChanged(_ => updateLayout());
            topBarRadiusBindable.BindValueChanged(_ => updateLayout());

            themeModeBindable = OverlayColourProvider.CurrentTheme.GetBoundCopy();
            themeModeBindable.BindValueChanged(_ => updateLayout());

            customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
            customThemeBindable.BindValueChanged(_ => updateLayout());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rulesetSelector.Current.BindTo(ruleset);
            updateLayout();
        }

        private void updateLayout()
        {
            var variant = topBarVariantBindable?.Value ?? TopBarVariant.Flat;
            float radius = Math.Clamp(topBarRadiusBindable?.Value ?? 10f, 0f, 24f);

            Colour4 bgColour = OverlayColourProvider.CurrentTheme.Value == ThemeMode.Custom
                ? (colourProvider?.Background5 ?? (Colour4)OsuColour.Gray(0.1f))
                : (Colour4)OsuColour.Gray(0.1f);
            Colour4 deepBgColour = OverlayColourProvider.CurrentTheme.Value == ThemeMode.Custom
                ? (colourProvider?.Background6 ?? (Colour4)OsuColour.Gray(0.06f))
                : (Colour4)OsuColour.Gray(0.06f);
            Colour4 podBgColour = OverlayColourProvider.CurrentTheme.Value == ThemeMode.Custom
                ? (colourProvider?.Background4 ?? (Colour4)OsuColour.Gray(0.12f))
                : (Colour4)OsuColour.Gray(0.12f);

            bgColour = bgColour.Opacity(surfaceAlpha);
            deepBgColour = deepBgColour.Opacity(surfaceAlpha);
            podBgColour = podBgColour.Opacity(surfaceAlpha);

            background?.UpdateGradientPosition(variant, radius);

            if (leftDownwardFillet != null)
                leftDownwardFillet.Alpha = 0;
            if (rightDownwardFillet != null)
                rightDownwardFillet.Alpha = 0;

            switch (variant)
            {
                case TopBarVariant.Flat:
                    Height = HEIGHT;
                    wrapperContainer.Padding = new MarginPadding(0);
                    contentContainer.RelativeSizeAxes = Axes.Both;
                    contentContainer.Size = Vector2.One;
                    contentContainer.Position = Vector2.Zero;
                    contentContainer.Masking = false;
                    contentContainer.CornerRadius = 0f;
                    contentContainer.EdgeEffect = default;

                    barBackground.Colour = bgColour;
                    if (rulesetFadeBox != null)
                        rulesetFadeBox.Colour = ColourInfo.GradientHorizontal(bgColour.Opacity(0), bgColour);
                    resetPods(bgColour);
                    break;

                case TopBarVariant.Rounded:
                    Height = HEIGHT;
                    wrapperContainer.Padding = new MarginPadding(0);
                    contentContainer.RelativeSizeAxes = Axes.X;
                    contentContainer.Width = 1f;
                    contentContainer.Height = HEIGHT + radius;
                    contentContainer.Position = new Vector2(0, -radius);
                    contentContainer.Masking = radius > 0f;
                    contentContainer.CornerRadius = radius;
                    contentContainer.EdgeEffect = radius > 0f ? new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Colour4.Black.Opacity(0.25f),
                        Radius = 8f,
                    } : default;

                    barBackground.Colour = bgColour;
                    if (rulesetFadeBox != null)
                        rulesetFadeBox.Colour = ColourInfo.GradientHorizontal(bgColour.Opacity(0), bgColour);
                    resetPods(bgColour);
                    break;

                case TopBarVariant.DownwardRounded:
                    Height = HEIGHT;
                    wrapperContainer.Padding = new MarginPadding(0);
                    contentContainer.RelativeSizeAxes = Axes.Both;
                    contentContainer.Size = Vector2.One;
                    contentContainer.Position = Vector2.Zero;
                    contentContainer.Masking = false;
                    contentContainer.CornerRadius = 0f;
                    contentContainer.EdgeEffect = default;

                    barBackground.Colour = bgColour;
                    if (rulesetFadeBox != null)
                        rulesetFadeBox.Colour = ColourInfo.GradientHorizontal(bgColour.Opacity(0), bgColour);
                    resetPods(bgColour);

                    if (radius > 0f)
                    {
                        if (leftDownwardFillet != null)
                            leftDownwardFillet.UpdateFillet(radius, bgColour);
                        if (rightDownwardFillet != null)
                            rightDownwardFillet.UpdateFillet(radius, bgColour);
                    }
                    break;

                case TopBarVariant.Floating:
                    Height = FLOATING_HEIGHT;
                    wrapperContainer.Padding = new MarginPadding
                    {
                        Top = FLOATING_TOP_MARGIN,
                        Bottom = FLOATING_HEIGHT - HEIGHT - FLOATING_TOP_MARGIN,
                        Horizontal = FLOATING_HORIZONTAL_MARGIN,
                    };
                    contentContainer.RelativeSizeAxes = Axes.Both;
                    contentContainer.Size = Vector2.One;
                    contentContainer.Position = Vector2.Zero;
                    contentContainer.Masking = radius > 0f;
                    contentContainer.CornerRadius = radius;
                    contentContainer.EdgeEffect = radius > 0f ? new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Colour4.Black.Opacity(0.35f),
                        Radius = 12f,
                    } : default;

                    barBackground.Colour = bgColour;
                    if (rulesetFadeBox != null)
                        rulesetFadeBox.Colour = ColourInfo.GradientHorizontal(bgColour.Opacity(0), bgColour);
                    resetPods(bgColour);
                    break;

                case TopBarVariant.Attached:
                    Height = HEIGHT;
                    wrapperContainer.Padding = new MarginPadding(0);
                    contentContainer.RelativeSizeAxes = Axes.Both;
                    contentContainer.Size = Vector2.One;
                    contentContainer.Position = Vector2.Zero;
                    contentContainer.Masking = false;
                    contentContainer.CornerRadius = 0f;
                    contentContainer.EdgeEffect = default;

                    barBackground.Colour = deepBgColour;
                    if (rulesetFadeBox != null)
                        rulesetFadeBox.Colour = ColourInfo.GradientHorizontal(deepBgColour.Opacity(0), deepBgColour);
                    applyAttachedPods(radius, podBgColour);
                    break;

                default:
                    goto case TopBarVariant.Flat;
            }

            if (State.Value == Visibility.Hidden)
                Y = -Height;
        }

        private void resetPods(Colour4 podColour)
        {
            leftButtonBackgroundPlate.Alpha = 0;
            leftButtonBackgroundPlate.BypassAutoSizeAxes = Axes.Both;
            leftButtonBackgroundPlate.RelativeSizeAxes = Axes.None;
            leftButtonBackgroundPlate.Size = Vector2.Zero;
            leftButtonBackgroundPlate.Position = Vector2.Zero;
            leftButtonBackgroundPlate.Masking = false;
            leftButtonBackgroundPlate.CornerRadius = 0f;
            leftButtonBackgroundPlate.EdgeEffect = default;
            leftButtonBackground.Colour = podColour;

            rightButtonBackgroundPlate.Alpha = 0;
            rightButtonBackgroundPlate.BypassAutoSizeAxes = Axes.Both;
            rightButtonBackgroundPlate.RelativeSizeAxes = Axes.None;
            rightButtonBackgroundPlate.Size = Vector2.Zero;
            rightButtonBackgroundPlate.Position = Vector2.Zero;
            rightButtonBackgroundPlate.Masking = false;
            rightButtonBackgroundPlate.CornerRadius = 0f;
            rightButtonBackgroundPlate.EdgeEffect = default;
            rightButtonBackground.Colour = podColour;
        }

        private void applyAttachedPods(float radius, Colour4 podColour)
        {
            leftButtonBackgroundPlate.Alpha = 1;
            leftButtonBackgroundPlate.BypassAutoSizeAxes = Axes.Both;
            leftButtonBackgroundPlate.RelativeSizeAxes = Axes.None;
            leftButtonBackgroundPlate.Width = (leftButtonBackgroundPlate.Parent?.DrawWidth ?? 100f) + radius;
            leftButtonBackgroundPlate.Height = HEIGHT + radius;
            leftButtonBackgroundPlate.Position = new Vector2(-radius, -radius);
            leftButtonBackgroundPlate.Masking = radius > 0f;
            leftButtonBackgroundPlate.CornerRadius = radius;
            leftButtonBackgroundPlate.EdgeEffect = radius > 0f ? new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Colour4.Black.Opacity(0.3f),
                Radius = 6f,
            } : default;
            leftButtonBackground.Colour = podColour;

            rightButtonBackgroundPlate.Alpha = 1;
            rightButtonBackgroundPlate.BypassAutoSizeAxes = Axes.Both;
            rightButtonBackgroundPlate.RelativeSizeAxes = Axes.None;
            rightButtonBackgroundPlate.Width = (rightButtonBackgroundPlate.Parent?.DrawWidth ?? 400f) + radius;
            rightButtonBackgroundPlate.Height = HEIGHT + radius;
            rightButtonBackgroundPlate.Position = new Vector2(0, -radius);
            rightButtonBackgroundPlate.Masking = radius > 0f;
            rightButtonBackgroundPlate.CornerRadius = radius;
            rightButtonBackgroundPlate.EdgeEffect = radius > 0f ? new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Colour4.Black.Opacity(0.3f),
                Radius = 6f,
            } : default;
            rightButtonBackground.Colour = podColour;
        }

        public partial class ToolbarBackground : Container
        {
            public Bindable<bool> ShowGradient { get; } = new BindableBool();

            private readonly Box gradientBackground;

            public ToolbarBackground()
            {
                RelativeSizeAxes = Axes.Both;
                Children = new Drawable[]
                {
                    gradientBackground = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Y = HEIGHT,
                        Alpha = 0,
                        Height = 80,
                        Colour = ColourInfo.GradientVertical(
                            OsuColour.Gray(0f).Opacity(0.7f), OsuColour.Gray(0).Opacity(0)),
                    },
                };
            }

            public void UpdateGradientPosition(TopBarVariant variant, float radius = 0f)
            {
                float barBottom = variant switch
                {
                    TopBarVariant.Floating => FLOATING_TOP_MARGIN + HEIGHT,
                    TopBarVariant.DownwardRounded => HEIGHT,
                    _ => HEIGHT,
                };

                gradientBackground.Y = barBottom;
                gradientBackground.Margin = new MarginPadding(0);
                Padding = new MarginPadding
                {
                    Horizontal = variant == TopBarVariant.Floating ? FLOATING_HORIZONTAL_MARGIN : 0
                };
            }

            public Box GradientBox => gradientBackground;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ShowGradient.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                if (ShowGradient.Value)
                    gradientBackground.FadeIn(InterfaceAnimation.FadeDuration(2500), Easing.OutQuint);
                else
                    gradientBackground.FadeOut(InterfaceAnimation.FadeDuration(200), Easing.OutQuint);
            }
        }

        /// <summary>
        /// Whenever the mouse cursor is within the bounds of the toolbar, we want the background gradient to show, for toolbar button descriptions to be legible.
        /// Unfortunately we also need to ensure that the toolbar buttons handle hover, to prevent the possibility of multiple descriptions being shown
        /// due to hover events passing through multiple buttons.
        /// This drawable is a workaround, that when placed front-most in the toolbar, allows to see whether hover events have been propagated through it without handling them.
        /// </summary>
        private partial class HoverInterceptor : Drawable
        {
            public IBindable<bool> ReceivedHover => receivedHover;
            private readonly Bindable<bool> receivedHover = new BindableBool();

            protected override bool OnHover(HoverEvent e)
            {
                receivedHover.Value = true;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                receivedHover.Value = false;
                base.OnHoverLost(e);
            }
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state)
        {
            bool blockShow = (hiddenByUser && !AutoHideActive) || OverlayActivationMode.Value == OverlayActivation.Disabled;

            if (state.NewValue == Visibility.Visible && blockShow)
            {
                State.Value = Visibility.Hidden;
                return;
            }

            base.UpdateState(state);
        }

        protected override void PopIn()
        {
            this.MoveToY(0, InterfaceAnimation.MovementDuration(transition_time), Easing.OutQuint);
            this.FadeIn(InterfaceAnimation.FadeDuration(transition_time / 4), Easing.OutQuint);
        }

        protected override void PopOut()
        {
            userButton.StateContainer?.Hide();

            this.MoveToY(-DrawSize.Y, InterfaceAnimation.MovementDuration(transition_time), Easing.OutQuint);
            this.FadeOut(InterfaceAnimation.FadeDuration(transition_time), Easing.InQuint);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                return false;

            switch (e.Action)
            {
                case GlobalAction.ToggleToolbar:
                    hiddenByUser = State.Value == Visibility.Visible; // set before toggling to allow the operation to always succeed.
                    ToggleVisibility();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
