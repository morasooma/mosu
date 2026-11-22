// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Footer
{
    public partial class ScreenFooterButton : OsuClickableContainer, IKeyBindingHandler<GlobalAction>
    {
        public const int CORNER_RADIUS = 10;

        public const int HEIGHT = 75;
        protected const int BUTTON_WIDTH = 116;

        /// <summary>
        /// If this footer button controls the visibility of an overlay, it will be bound to this bindable.
        /// </summary>
        public readonly Bindable<Visibility> OverlayState = new Bindable<Visibility>();

        /// <summary>
        /// Used to tell whether this button is currently on screen.
        /// <see cref="ScreenFooter"/> controls this to convey when buttons are hidden temporarily via <see cref="ScreenFooter.RegisterActiveOverlayContainer"/>.
        /// </summary>
        public readonly Bindable<Visibility> State = new Bindable<Visibility>(Visibility.Visible);

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private ScreenFooter? footer { get; set; }

        private bool glassActive => footer?.SongSelectActive == true && footer.DefaultChromeVisible && !footer.HasActiveOverlay;

        protected override void Update()
        {
            base.Update();
            backgroundBox.Alpha = glassActive ? OverlayTransparency.SongSelectSurfaceAlpha : 1;
        }

        private Colour4 buttonAccentColour;

        public Colour4 AccentColour
        {
            get => buttonAccentColour;
            set
            {
                buttonAccentColour = value;
                bar.Colour = buttonAccentColour;
                icon.Colour = buttonAccentColour;
            }
        }

        public IconUsage Icon
        {
            set => icon.Icon = value;
        }

        public LocalisableString Text
        {
            get => text.Text;
            set => text.Text = value;
        }

        public new Action? Action
        {
            set
            {
                if (value == null)
                    base.Action = null;
                else
                {
                    base.Action = () =>
                    {
                        if (State.Value != Visibility.Hidden)
                            value.Invoke();
                    };
                }
            }
        }

        private readonly Container shearedContent;

        protected IBindable<float>? ButtonRadiusBindable;
        private readonly Container textIconContainer;
        private readonly Container barContainer;

        private readonly SpriteText text;
        private readonly SpriteIcon icon;

        protected Container TextContainer;
        private readonly Box bar;
        private readonly Box backgroundBox;
        /// <summary>
        /// Specific footer button context for independent theme overrides (e.g. Mods, Random, Options).
        /// </summary>
        public virtual osu.Game.Overlays.CustomTheme.ThemeSurfaceContext? ButtonContext => null;

        private IBindable<int>? paletteVersionBindable;
        private IBindable<bool>? buttonFooterGradientsBindable;
        private readonly Box glowBox;
        private readonly Box flashLayer;
        private IBindable<Colour4> themeColour = null!;
        private readonly IBindable<bool> disableShear = OsuGame.DisableShear.GetBoundCopy();

        public readonly OverlayContainer? Overlay;

        public ScreenFooterButton(OverlayContainer? overlay = null)
        {
            Overlay = overlay;

            Size = new Vector2(BUTTON_WIDTH, HEIGHT);

            Children = new Drawable[]
            {
                shearedContent = new Container
                {
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 4,
                        // Figma says 50% opacity, but it does not match up visually if taken at face value, and looks bad.
                        Colour = Colour4.Black.Opacity(0.25f),
                        Offset = new Vector2(0, 2),
                    },
                    Shear = OsuGame.SHEAR,
                    Masking = true,
                    CornerRadius = CORNER_RADIUS,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new SongSelectBackdropSurface
                        {
                            IsActive = () => glassActive,
                            GetCornerRadius = () => shearedContent.CornerRadius,
                        },
                        backgroundBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        glowBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        // For elements that should not be sheared.
                        textIconContainer = new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Shear = -OsuGame.SHEAR,
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                TextContainer = new Container
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Y = 35,
                                    AutoSizeAxes = Axes.Both,
                                    Child = text = new OsuSpriteText
                                    {
                                        Font = OsuFont.TorusAlternate.With(size: 16),
                                        AlwaysPresent = true
                                    }
                                },
                                icon = new SpriteIcon
                                {
                                    Y = 10,
                                    Size = new Vector2(16),
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre
                                },
                            }
                        },
                        barContainer = new Container
                        {
                            Shear = -OsuGame.SHEAR,
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Y = -CORNER_RADIUS,
                            Size = new Vector2(100, 5),
                            Masking = true,
                            CornerRadius = 3,
                            Child = bar = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            }
                        },
                        flashLayer = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White.Opacity(0.9f),
                            Blending = BlendingParameters.Additive,
                            Alpha = 0,
                        },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Overlay != null)
                OverlayState.BindTo(Overlay.State);

            OverlayState.BindValueChanged(_ => UpdateDisplay());
            Enabled.BindValueChanged(_ => UpdateDisplay(), true);

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => UpdateDisplay(), true);

            paletteVersionBindable = colourProvider.PaletteVersion.GetBoundCopy();
            paletteVersionBindable.BindValueChanged(_ => UpdateDisplay());

            buttonFooterGradientsBindable = OverlayColourProvider.ButtonFooterGradients.GetBoundCopy();
            buttonFooterGradientsBindable.BindValueChanged(_ => UpdateDisplay());

            ButtonRadiusBindable = colourProvider.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Button, CORNER_RADIUS);
            ButtonRadiusBindable.BindValueChanged(_ => UpdateRadius(), true);

            disableShear.BindValueChanged(_ =>
            {
                UpdateShear();
                UpdateRadius();
            }, true);

            FinishTransforms(true);
        }

        protected virtual void UpdateRadius()
        {
            float target = ButtonRadiusBindable?.Value ?? CORNER_RADIUS;
            shearedContent.CornerRadius = !disableShear.Value ? Math.Min(target, 10f) : target;
        }

        protected virtual void UpdateShear()
        {
            shearedContent.Shear = OsuGame.SHEAR;
            if (textIconContainer != null)
                textIconContainer.Shear = -OsuGame.SHEAR;
            if (barContainer != null)
                barContainer.Shear = -OsuGame.SHEAR;
        }

        // account for shear and buttons temporarily hidden with DisappearToBottom.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => shearedContent.ReceivePositionalInputAt(screenSpacePos);

        public GlobalAction? Hotkey;

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
                Flash();

            return base.OnClick(e);
        }

        protected virtual void Flash() => flashLayer.FadeOutFromOne(800, Easing.OutQuint);

        protected override bool OnHover(HoverEvent e)
        {
            UpdateDisplay();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => UpdateDisplay();

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != Hotkey || e.Repeat) return false;

            TriggerClick();
            return true;
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<GlobalAction> e) { }

        public virtual void UpdateDisplay()
        {
            Colour4 effectiveAccent = buttonAccentColour;
            if (ButtonContext.HasValue && OverlayColourProvider.TryGetCustomContextColour(ButtonContext.Value, osu.Game.Overlays.CustomTheme.SurfaceColourRole.Accent, out var customAccent))
                effectiveAccent = customAccent;

            Color4 activeForegroundColour = OsuColour.ForegroundTextColourFor(effectiveAccent);

            Color4 backgroundColour;
            if (OverlayState.Value == Visibility.Visible)
            {
                if (ButtonContext.HasValue && OverlayColourProvider.TryGetCustomContextColour(ButtonContext.Value, osu.Game.Overlays.CustomTheme.SurfaceColourRole.ControlSelected, out var customSelected))
                    backgroundColour = customSelected;
                else
                    backgroundColour = effectiveAccent;
            }
            else
            {
                if (ButtonContext.HasValue && OverlayColourProvider.TryGetCustomContextColour(ButtonContext.Value, osu.Game.Overlays.CustomTheme.SurfaceColourRole.ControlIdle, out var customIdle))
                    backgroundColour = customIdle;
                else
                    backgroundColour = colourProvider.Background3;
            }

            Color4 textColour;
            if (OverlayState.Value == Visibility.Visible)
            {
                textColour = activeForegroundColour;
            }
            else
            {
                if (ButtonContext.HasValue && OverlayColourProvider.TryGetCustomContextColour(ButtonContext.Value, osu.Game.Overlays.CustomTheme.SurfaceColourRole.TextPrimary, out var customText))
                    textColour = customText;
                else
                    textColour = colourProvider.Content1;
            }

            Color4 accentColour = OverlayState.Value == Visibility.Visible ? activeForegroundColour : effectiveAccent;

            if (!Enabled.Value)
                backgroundColour = backgroundColour.Darken(1f);
            else if (IsHovered)
            {
                if (ButtonContext.HasValue && OverlayColourProvider.TryGetCustomContextColour(ButtonContext.Value, osu.Game.Overlays.CustomTheme.SurfaceColourRole.ControlHover, out var customHover))
                    backgroundColour = customHover;
                else
                    backgroundColour = backgroundColour.Lighten(0.2f);
            }

            double duration = OverlayColourProvider.ThemeTransitionDuration(150);
            backgroundBox.FadeColour(backgroundColour, duration, Easing.OutQuint);

            if (!Enabled.Value)
                textColour = textColour.Opacity(0.6f);

            text.FadeColour(textColour, duration, Easing.OutQuint);
            icon.FadeColour(accentColour, duration, Easing.OutQuint);
            bar.FadeColour(accentColour, duration, Easing.OutQuint);

            bool enableGradients = OverlayColourProvider.ButtonFooterGradients.Value;
            if (enableGradients)
                glowBox.FadeColour(ColourInfo.GradientVertical(effectiveAccent.Opacity(0f), effectiveAccent.Opacity(0.2f)), duration, Easing.OutQuint);
            else
                glowBox.FadeColour(Colour4.Transparent, duration, Easing.OutQuint);
        }

        public void AppearFromLeft(double delay)
        {
            Content.FinishTransforms();
            Content.MoveToX(-300f)
                   .FadeOut()
                   .Delay(delay)
                   .MoveToX(0f, 240, Easing.OutCubic)
                   .FadeIn(240, Easing.OutCubic);
        }

        public void AppearFromBottom(double delay)
        {
            Content.FinishTransforms();
            Content.MoveToY(100f)
                   .FadeOut()
                   .Delay(delay)
                   .MoveToY(0f, 240, Easing.OutCubic)
                   .FadeIn(240, Easing.OutCubic);
        }

        public void DisappearToRight(double delay, bool expire)
        {
            Content.FinishTransforms();
            Content.Delay(delay)
                   .FadeOut(240, Easing.InOutCubic)
                   .MoveToX(300f, 360, Easing.InOutCubic);

            if (expire)
                this.Delay(Content.LatestTransformEndTime - Time.Current).Expire();
        }

        public void DisappearToBottom(double delay, bool expire)
        {
            Content.FinishTransforms();
            Content.Delay(delay)
                   .FadeOut(240, Easing.InOutCubic)
                   .MoveToY(100f, 240, Easing.InOutCubic);

            if (expire)
                this.Delay(Content.LatestTransformEndTime - Time.Current).Expire();
        }
    }
}
