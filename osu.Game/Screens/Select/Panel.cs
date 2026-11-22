// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public abstract partial class Panel : PoolableDrawable, ICarouselPanel, IHasContextMenu
    {
        public const float CORNER_RADIUS = 10;

        private const float active_x_offset = 25f;

        protected const float DURATION = 400;

        protected float PanelXOffset { get; init; }

        private Container backgroundContainer = null!;
        private Container iconContainer = null!;

        private Drawable activationFlash = null!;
        private Drawable hoverLayer = null!;

        internal Color4 HoverLayerColour => hoverLayer.Colour;

        private Drawable keyboardSelectionLayer = null!;

        private PulsatingBox selectionLayer = null!;
        private IBindable<Colour4> themeColour = null!;
        private IBindable<Colour4> themeHighlightColour = null!;
        private OverlayColourProvider colourProvider = null!;

        public Container TopLevelContent { get; private set; } = null!;

        internal bool StableXOffsetInitialised { get; set; }

        private Container contentPaddingContainer = null!;
        protected Container Content { get; private set; } = null!;

        private float lastIconDrawWidth = float.NaN;

        private readonly BindableBool useSkinnedLegacyCarousel = new BindableBool();
        private IBindable<float>? panelRadius;

        protected readonly BindableBool CarouselPerformanceMode = new BindableBool();
        protected readonly BindableBool CarouselPreviews = new BindableBool(true);

        public Drawable Background
        {
            set => backgroundContainer.Child = value;
        }

        public Drawable Icon
        {
            set => iconContainer.Child = value;
        }

        private Color4? accentColour;

        public Color4? AccentColour
        {
            get => accentColour;
            set
            {
                if (value == accentColour)
                    return;

                accentColour = value;
                updateAccentColour();
            }
        }

        public sealed override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (item == null)
                return TopLevelContent.ReceivePositionalInputAt(screenSpacePos);

            var inputRectangle = TopLevelContent.DrawRectangle;

            // Cover the gaps introduced by the spacing between panels so that user mis-aims don't result in no-ops.
            inputRectangle = inputRectangle.Inflate(new MarginPadding
            {
                Top = item.CarouselInputLenienceAbove,
                Bottom = item.CarouselInputLenienceBelow,
            });

            return inputRectangle.Contains(TopLevelContent.ToLocalSpace(screenSpacePos));
        }

        [Resolved]
        private BeatmapCarousel? carousel { get; set; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours, OsuConfigManager config)
        {
            this.colourProvider = colourProvider;
            ForkSongSelectStyleBinding.BindSkinnedLegacyCarousel(config, useSkinnedLegacyCarousel, () => carousel?.SupportsStableStyle ?? true);
            config.BindWith(OsuSetting.ForkSongSelectCarouselPerformanceMode, CarouselPerformanceMode);
            config.BindWith(OsuSetting.ForkSongSelectCarouselPreviews, CarouselPreviews);

            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;

            RelativeSizeAxes = Axes.X;
            Height = CarouselItem.DEFAULT_HEIGHT;

            InternalChild = TopLevelContent = new Container
            {
                Masking = true,
                CornerRadius = CORNER_RADIUS,
                RelativeSizeAxes = Axes.Both,
                X = CORNER_RADIUS,
                Children = new[]
                {
                    backgroundContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    iconContainer = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                    },
                    contentPaddingContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = Content = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = CORNER_RADIUS,
                            Masking = true,
                        },
                    },
                    hoverLayer = new Box
                    {
                        Alpha = 0,
                        Colour = colourProvider.Highlight1.Opacity(0.1f),
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    },
                    selectionLayer = new PulsatingBox
                    {
                        Alpha = 0,
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.8f,
                        Blending = BlendingParameters.Additive,
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                    },
                    keyboardSelectionLayer = new Box
                    {
                        Alpha = 0,
                        Colour = ColourInfo.GradientHorizontal(colourProvider.Highlight1.Opacity(0.1f), colourProvider.Highlight1.Opacity(0.4f)),
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    },
                    activationFlash = new Box
                    {
                        Colour = Color4.White.Opacity(0.4f),
                        Blending = BlendingParameters.Additive,
                        Alpha = 0f,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new HoverSounds(),
                }
            };

            panelRadius = colourProvider.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Panel, CORNER_RADIUS);
            panelRadius.BindValueChanged(_ => updateCornerRadius());

            useSkinnedLegacyCarousel.BindValueChanged(legacy =>
            {
                updateCornerRadius();

                if (legacy.NewValue)
                {
                    hoverLayer.Hide();
                    keyboardSelectionLayer.Hide();
                    activationFlash.Hide();
                }

                updateSelectedState(animated: false);
            }, true);

            CarouselPerformanceMode.BindValueChanged(enabled =>
            {
                TopLevelContent.Masking = !enabled.NewValue && !useSkinnedLegacyCarousel.Value;
                Content.Masking = !enabled.NewValue && !useSkinnedLegacyCarousel.Value;

                if (enabled.NewValue)
                {
                    TopLevelContent.EdgeEffect = new EdgeEffectParameters();
                    selectionLayer.Hide();
                    activationFlash.Hide();
                }
                else
                    updateSelectedState(animated: false);

                updateXOffset(animated: false);
            }, true);

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours());
            themeHighlightColour = colourProvider.GetColourBindable(OverlayColour.Highlight1);
            themeHighlightColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            hoverLayer.Colour = colourProvider.Highlight1.Opacity(0.1f);
            keyboardSelectionLayer.Colour = ColourInfo.GradientHorizontal(colourProvider.Highlight1.Opacity(0.1f), colourProvider.Highlight1.Opacity(0.4f));
            updateAccentColour();
        }

        private void updateCornerRadius()
        {
            float radius = useSkinnedLegacyCarousel.Value ? 0 : (panelRadius?.Value ?? CORNER_RADIUS);
            TopLevelContent.CornerRadius = radius;
            Content.CornerRadius = radius;
            // Stable's natural-width button extends beyond the row's layout width.
            // Clipping it to that width makes its right edge retreat on hover.
            TopLevelContent.Masking = !useSkinnedLegacyCarousel.Value && !CarouselPerformanceMode.Value;
            Content.Masking = !useSkinnedLegacyCarousel.Value && !CarouselPerformanceMode.Value;
            updateXOffset(animated: false);
        }

        public partial class PulsatingBox : BeatSyncedContainer
        {
            public int FlashOffset;

            private readonly Box box;

            public PulsatingBox()
            {
                EarlyActivationMilliseconds = 40;

                InternalChildren = new Drawable[]
                {
                    box = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            }

            protected override void Update()
            {
                base.Update();
                if (!InterfaceAnimation.AllowDecorativeMovement)
                {
                    box.ClearTransforms();
                    box.Alpha = 0.4f;
                }
            }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                if (!InterfaceAnimation.AllowDecorativeMovement || beatIndex % Math.Pow(2, FlashOffset) != 0)
                    return;

                double length = timingPoint.BeatLength;

                while (length < 250)
                    length *= 2;

                box
                    .FadeTo(0.8f, InterfaceAnimation.FadeDuration(40), Easing.Out)
                    .Then()
                    .FadeTo(0.4f, length, Easing.Out);
            }
        }

        private IBindable<InterfaceAnimationMode>? animationMode;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            animationMode = InterfaceAnimation.Observe(this, true, () => useSkinnedLegacyCarousel.Value);
            animationMode.BindValueChanged(_ =>
            {
                if (useSkinnedLegacyCarousel.Value)
                    return;
                updateSelectedState(animated: false);
                updateXOffset(animated: false);
            });

            Expanded.BindValueChanged(_ =>
            {
                updateSelectedState();
                updateXOffset();
            });

            Selected.BindValueChanged(_ =>
            {
                updateSelectedState();
                updateXOffset();
            }, true);

            KeyboardSelected.BindValueChanged(selected =>
            {
                if (useSkinnedLegacyCarousel.Value)
                    keyboardSelectionLayer.Hide();
                else if (CarouselPerformanceMode.Value || !InterfaceAnimation.AllowDecorativeMovement)
                    keyboardSelectionLayer.Alpha = selected.NewValue ? 0.5f : 0;
                else if (selected.NewValue)
                {
                    keyboardSelectionLayer.FadeIn(InterfaceAnimation.FadeDuration(80), Easing.Out)
                                          .Then()
                                          .FadeTo(0.5f, InterfaceAnimation.FadeDuration(2000), Easing.OutQuint);
                }
                else
                    keyboardSelectionLayer.FadeOut(InterfaceAnimation.FadeDuration(1000), Easing.OutQuint);

                updateXOffset();
            }, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            hoverLayer.Colour = colourProvider.Highlight1.Opacity(0.1f);
            keyboardSelectionLayer.Colour = ColourInfo.GradientHorizontal(colourProvider.Highlight1.Opacity(0.1f), colourProvider.Highlight1.Opacity(0.4f));

            if (!IsHovered)
                hoverLayer.Alpha = 0;

            // Slightly offset the flash animation based on the panel depth.
            // This assumes a minimum depth of -2 (groups).
            selectionLayer.FlashOffset = -(Item?.DepthLayer ?? 0);

            updateAccentColour();

            updateXOffset(animated: false);
            updateSelectedState(animated: false);

            this.FadeIn(CarouselPerformanceMode.Value ? 0 : useSkinnedLegacyCarousel.Value ? DURATION : InterfaceAnimation.FadeDuration(DURATION), Easing.OutQuint);
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            hoverLayer.ClearTransforms();
            hoverLayer.Alpha = 0;
            carousel?.SetStableHoveredPanel(this, false);
            StableXOffsetInitialised = false;

            Hide();

            // Important to set this to null to handle reuse scenarios correctly, see `Item` implementation.
            item = null;
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Item may be set to null before actual `FreeAfterUse`.
            // This is because Carousel knows to do this ahead of time and let the drawable fade/animate away.
            // See https://github.com/ppy/osu/blob/033e13cb3b79e6195ddcd9f659b04095aa52fd2f/osu.Game/Graphics/Carousel/Carousel.cs#L1132-L1135.
            if (item != null)
                carousel?.Activate(item);

            return true;
        }

        private void updateAccentColour()
        {
            var backgroundColour = accentColour ?? Color4.White;

            selectionLayer.Colour = ColourInfo.GradientHorizontal(backgroundColour.Opacity(0), backgroundColour.Opacity(0.5f));

            updateSelectedState(animated: false);
        }

        private void updateSelectedState(bool animated = true)
        {
            bool selectedOrExpanded = Expanded.Value || Selected.Value;

            if (CarouselPerformanceMode.Value)
            {
                TopLevelContent.EdgeEffect = new EdgeEffectParameters();
                selectionLayer.Hide();
                return;
            }

            if (useSkinnedLegacyCarousel.Value)
            {
                TopLevelContent.EdgeEffect = new EdgeEffectParameters();
                selectionLayer.Hide();
                return;
            }

            var edgeEffectColour = accentColour ?? Color4Extensions.FromHex(@"4EBFFF");

            if (selectedOrExpanded)
            {
                TopLevelContent.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 2f,
                    Hollow = true,
                };
            }
            else
            {
                TopLevelContent.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 4f,
                    Hollow = true,
                    Offset = new Vector2(0f, 1f),
                };
            }

            TopLevelContent.FadeEdgeEffectTo(selectedOrExpanded ? edgeEffectColour.Opacity(0.8f) : Color4.Black.Opacity(0.2f), animated ? InterfaceAnimation.FadeDuration(DURATION) : 0, Easing.OutQuint);

            if (selectedOrExpanded)
                selectionLayer.FadeIn(InterfaceAnimation.FadeDuration(100), Easing.OutQuint);
            else
                selectionLayer.FadeOut(InterfaceAnimation.FadeDuration(200), Easing.OutQuint);
        }

        private void updateXOffset(bool animated = true)
        {
            float radius = useSkinnedLegacyCarousel.Value ? 0 : (panelRadius?.Value ?? CORNER_RADIUS);
            float x = PanelXOffset + radius;

            if (useSkinnedLegacyCarousel.Value)
            {
                // Stable row offsets (including hover) are applied by the carousel, not
                // an additional lazer indentation inside each card.
                TopLevelContent.MoveToX(0);
                return;
            }

            if (!Expanded.Value && !Selected.Value)
            {
                if (this is PanelBeatmap || this is PanelBeatmapStandalone)
                    x += active_x_offset * 2;
                else
                    x += active_x_offset * 4;
            }

            if (!KeyboardSelected.Value)
                x += active_x_offset;

            TopLevelContent.MoveToX(x, animated && !CarouselPerformanceMode.Value ? InterfaceAnimation.MovementDuration(DURATION) : 0, Easing.OutQuint);
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (useSkinnedLegacyCarousel.Value)
                carousel?.SetStableHoveredPanel(this, true);

            if (!useSkinnedLegacyCarousel.Value)
            {
                if (CarouselPerformanceMode.Value)
                    hoverLayer.Show();
                else
                    hoverLayer.FadeIn(InterfaceAnimation.FadeDuration(100), Easing.OutQuint);
            }

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            carousel?.SetStableHoveredPanel(this, false);
            if (CarouselPerformanceMode.Value)
                hoverLayer.Hide();
            else
                hoverLayer.FadeOut(InterfaceAnimation.FadeDuration(1000), Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();

            if (iconContainer.DrawWidth != lastIconDrawWidth)
            {
                lastIconDrawWidth = iconContainer.DrawWidth;
                contentPaddingContainer.Padding = contentPaddingContainer.Padding with { Left = lastIconDrawWidth };
            }
        }

        public abstract MenuItem[]? ContextMenuItems { get; }

        #region ICarouselPanel

        private CarouselItem? item;

        public CarouselItem? Item
        {
            get => item;
            set
            {
                if (ReferenceEquals(item, value))
                    return;

                // If a new item is set and we already have an item, this is a special case of reuse.
                // See https://github.com/ppy/osu/blob/033e13cb3b79e6195ddcd9f659b04095aa52fd2f/osu.Game/Graphics/Carousel/Carousel.cs#L1071
                // To keep things simple, assume that we need to do a full refresh.
                //
                // In the future, this could be more contextual and check whether the associated model has actually changed.
                if (item != null && value != null)
                {
                    item = value;
                    PrepareForUse();
                }
                else
                    item = value;
            }
        }

        public BindableBool Selected { get; } = new BindableBool();
        public BindableBool Expanded { get; } = new BindableBool();
        public BindableBool KeyboardSelected { get; } = new BindableBool();

        public double DrawYPosition { get; set; }

        public virtual void Activated()
        {
            if (!useSkinnedLegacyCarousel.Value && !CarouselPerformanceMode.Value)
                activationFlash.FadeOutFromOne(InterfaceAnimation.FadeDuration(1000), Easing.OutQuint);
        }

        #endregion
    }
}
