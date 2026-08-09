// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.PolygonExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class PanelSetBackground : Container
    {
        private const float legacy_preview_aspect_ratio = 16f / 9f;
        private const float classic_preview_aspect_ratio = 4f / 3f;

        [Resolved]
        private BeatmapCarousel? beatmapCarousel { get; set; }

        private readonly BindableBool useLegacyPreviewLayout = new BindableBool();
        private readonly BindableBool useSkinnedLegacyCarousel = new BindableBool();

        private Box fallbackBackground = null!;
        private Box legacyBlackBackground = null!;
        private LegacyMenuButtonBackground legacyMenuButtonBackground = null!;
        private Drawable modernEffects = null!;
        private Drawable legacyEffects = null!;
        private IBindable<Colour4> themeColour = null!;
        private OverlayColourProvider colourProvider = null!;

        private Drawable? background;

        private WorkingBeatmap? working;

        private CancellationTokenSource? loadCancellation;

        private double timeSinceUnpool;

        internal bool HasLoadedBackground => background != null;

        internal bool IsShowingLegacyPreview => background is LegacyPreviewBackground;

        internal bool IsShowingSkinnedLegacyBackground => useSkinnedLegacyCarousel.Value && legacyMenuButtonBackground.HasTexture;

        internal bool SkinnedLegacyModeEnabled => useSkinnedLegacyCarousel.Value;

        internal Color4 LegacyActiveTextColour => legacyMenuButtonBackground.ActiveTextColour;

        internal Color4 LegacyInactiveTextColour => legacyMenuButtonBackground.InactiveTextColour;

        internal Color4 LegacyMenuGlowColour => legacyMenuButtonBackground.MenuGlowColour;

        internal float LegacySelectedOverlayAlpha => legacyMenuButtonBackground.SelectedOverlayAlpha;

        internal float FallbackBackgroundAlpha => fallbackBackground.Alpha;

        internal float LegacyPreviewDrawWidthRatio => background is LegacyPreviewBackground legacyPreview ? legacyPreview.PreviewDrawWidthRatio : 0;

        internal int BackgroundLoadCount { get; private set; }

        internal static float GetLegacyPreviewWidth(float width, float height, bool classicLayout = false)
            => width <= 0 ? 0 : Math.Min(width, height * (classicLayout ? classic_preview_aspect_ratio : legacy_preview_aspect_ratio));

        public WorkingBeatmap? Beatmap
        {
            get => working;
            set
            {
                if (working == null && value == null)
                    return;

                // This guard papers over excessive refreshes of the background asset which occur if
                // `working == value` type guards are used.
                string? currentBackgroundHash = getBackgroundIdentity(working);
                string? newBackgroundHash = getBackgroundIdentity(value);

                if (working != null && value != null && currentBackgroundHash != null && currentBackgroundHash == newBackgroundHash)
                    return;

                working = value;
                resetLoadedBackground();
            }
        }

        private static string? getBackgroundIdentity(WorkingBeatmap? working)
        {
            if (working == null)
                return null;

            if (working is StableWorkingBeatmap stableWorking)
                return stableWorking.BackgroundFilePath;

            string backgroundFile = working.Metadata.BackgroundFile;

            if (string.IsNullOrEmpty(backgroundFile))
                return null;

            return working.BeatmapSetInfo.GetFile(backgroundFile)?.File.Hash;
        }

        public PanelSetBackground()
        {
            RelativeSizeAxes = Axes.Both;
            CornerRadius = Panel.CORNER_RADIUS;
            Masking = true;

            // Add some level of smoothness around the rounded edges to give more visual polish.
            MaskingSmoothness = 2f;
        }

        protected override void Update()
        {
            base.Update();

            loadContentIfRequired();
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            this.colourProvider = colourProvider;
            config.BindWith(OsuSetting.ForkSongSelectOldCarouselPreviews, useLegacyPreviewLayout);
            config.BindWith(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, useSkinnedLegacyCarousel);

            InternalChildren = new Drawable[]
            {
                fallbackBackground = new Box
                {
                    Depth = 1,
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4),
                },
                legacyMenuButtonBackground = new LegacyMenuButtonBackground
                {
                    Depth = 0.5f,
                },
                legacyBlackBackground = new Box
                {
                    Depth = 0.75f,
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                modernEffects = new FillFlowContainer
                {
                    Depth = -1,
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    // This makes the gradient not be perfectly horizontal, but diagonal at a ~40-degree angle.
                    Shear = new Vector2(0.8f, 0),
                    Children = new[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.5f),
                            Width = 0.4f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.5f), Color4.Black.Opacity(0.3f)),
                            Width = 0.2f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.3f), Color4.Black.Opacity(0.2f)),
                            Width = 0.45f,
                        },
                    }
                },
                legacyEffects = new Container
                {
                    Depth = -1,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.08f),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.02f), Color4.Black.Opacity(0.32f)),
                        },
                    }
                },
            };

            updateLayerVisibility();
            useLegacyPreviewLayout.BindValueChanged(_ => previewLayoutChanged());
            useSkinnedLegacyCarousel.BindValueChanged(_ => previewLayoutChanged());

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => fallbackBackground.Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4), true);
        }


        private void loadContentIfRequired()
        {
            // A load is already in progress if the cancellation token is non-null.
            if (background != null || loadCancellation != null || working == null)
                return;

            if (beatmapCarousel != null)
            {
                float panelY = this.ToSpaceOfOtherDrawable(new Vector2(0, DrawHeight / 2), beatmapCarousel).Y;

                // We want to preload backgrounds while panels are off-screen, prioritising panels closest
                // to the visual centre so the currently browsed area fills in first.
                float timeUpdatingBeforeLoad = Math.Abs(panelY - beatmapCarousel.DrawHeight / 2) / beatmapCarousel.DrawHeight * 100;

                timeSinceUnpool += Time.Elapsed;

                if (timeSinceUnpool <= timeUpdatingBeforeLoad)
                    return;
            }

            var cancellation = loadCancellation = new CancellationTokenSource();
            bool legacyLayout = useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value;
            bool classicLayout = useSkinnedLegacyCarousel.Value;

            LoadComponentAsync(createBackgroundDrawable(working, legacyLayout, classicLayout), loadedBackground =>
            {
                if (loadCancellation != cancellation || cancellation.IsCancellationRequested
                    || legacyLayout != (useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value)
                    || classicLayout != useSkinnedLegacyCarousel.Value)
                {
                    loadedBackground.Dispose();
                    return;
                }

                AddInternal(background = loadedBackground);
                BackgroundLoadCount++;

                bool spriteOnScreen = beatmapCarousel?.ScreenSpaceDrawQuad.Intersects(loadedBackground.ScreenSpaceDrawQuad) != false;
                loadedBackground.FadeInFromZero(spriteOnScreen ? 400 : 0, Easing.OutQuint);
            }, cancellation.Token);
        }

        private static Drawable createBackgroundDrawable(WorkingBeatmap beatmap, bool legacyLayout, bool classicLayout)
        {
            if (legacyLayout)
                return new LegacyPreviewBackground(beatmap, classicLayout);

            return new PanelBeatmapBackground(beatmap)
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            };
        }

        private void previewLayoutChanged()
        {
            updateLayerVisibility();
            resetLoadedBackground();
        }

        private void updateLayerVisibility()
        {
            bool legacyLayout = useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value;

            fallbackBackground.Alpha = legacyLayout ? 0.62f : 1;
            legacyBlackBackground.Alpha = useSkinnedLegacyCarousel.Value ? 1 : 0;
            legacyMenuButtonBackground.Alpha = useSkinnedLegacyCarousel.Value ? 1 : 0;
            modernEffects.Alpha = legacyLayout ? 0 : 1;
            legacyEffects.Alpha = legacyLayout ? 1 : 0;
            CornerRadius = useSkinnedLegacyCarousel.Value ? 0 : Panel.CORNER_RADIUS;
        }

        private void resetLoadedBackground()
        {
            loadCancellation?.Cancel();
            loadCancellation = null;

            background?.RemoveAndDisposeImmediately();
            background = null;

            timeSinceUnpool = 0;
        }

        private partial class LegacyPreviewBackground : CompositeDrawable
        {
            private readonly WorkingBeatmap working;
            private readonly bool classicLayout;
            private Container previewFrame = null!;

            public LegacyPreviewBackground(WorkingBeatmap working, bool classicLayout)
            {
                this.working = working;
                this.classicLayout = classicLayout;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = previewFrame = new Container
                {
                    Depth = 0,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Masking = true,
                    CornerRadius = classicLayout ? 0 : Panel.CORNER_RADIUS,
                    MaskingSmoothness = 2f,
                    Child = new PanelBeatmapBackground(working, true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                    },
                };
            }

            public float PreviewDrawWidthRatio => DrawWidth > 0 ? previewFrame.DrawWidth / DrawWidth : 0;

            protected override void Update()
            {
                base.Update();

                if (DrawWidth <= 0)
                    return;



                previewFrame.Width = GetLegacyPreviewWidth(DrawWidth, DrawHeight, classicLayout) / DrawWidth;
            }
        }

        public partial class PanelBeatmapBackground : Sprite
        {
            private readonly IWorkingBeatmap working;
            private readonly bool useLegacyTexture;

            public PanelBeatmapBackground(IWorkingBeatmap working)
                : this(working, false)
            {
            }

            public PanelBeatmapBackground(IWorkingBeatmap working, bool useLegacyTexture)
            {
                ArgumentNullException.ThrowIfNull(working);

                this.working = working;
                this.useLegacyTexture = useLegacyTexture;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Texture = useLegacyTexture
                    ? working.GetLegacyPreviewBackground() ?? working.GetPanelBackground()
                    : working.GetPanelBackground();
            }
        }
    }
}
