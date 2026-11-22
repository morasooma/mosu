// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
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
        private const float classic_preview_height_ratio = 0.95f;
        private const float classic_preview_left_ratio = 5.2f * StableCarouselLayout.Scale / PanelBeatmapStandalone.CLASSIC_HEIGHT;
        private const double normal_distance_time_before_load = 100;
        private const double torii_minimum_time_before_load = 200;
        private const double torii_distance_time_before_load = 200;

        [Resolved]
        private BeatmapCarousel? beatmapCarousel { get; set; }

        private readonly BindableBool useLegacyPreviewLayout = new BindableBool();
        private readonly BindableBool useSkinnedLegacyCarousel = new BindableBool();
        private readonly BindableBool carouselPerformanceMode = new BindableBool();
        private readonly BindableBool carouselPreviews = new BindableBool(true);
        private readonly BindableBool carouselLazyLoading = new BindableBool();
        private readonly BindableInt carouselPreviewResolution = new BindableInt(100);
        private IBindable<float>? panelRadius;

        private Box fallbackBackground = null!;
        private Box legacyBlackBackground = null!;
        private LegacyMenuButtonBackground legacyMenuButtonBackground = null!;
        private Drawable modernEffects = null!;
        private Drawable legacyEffects = null!;
        private IBindable<Colour4> themeColour = null!;
        private OverlayColourProvider colourProvider = null!;

        private Drawable? background;

        private WorkingBeatmap? working;
        private WeakReference<WorkingBeatmap>? loadedWorking;

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

        internal Color4 StableStateColour { get; private set; } = Color4.White;
        private Color4 stablePreviewColour = Color4.White;

        internal void SetStableStateColour(Color4 colour, bool selected, double duration = StableCarouselLayout.ColourDuration)
        {
            Color4 previewColour = selected ? Color4.White : new Color4(50, 50, 50, 255);
            if (duration != 0 && StableStateColour == colour && stablePreviewColour == previewColour)
                return;

            StableStateColour = colour;
            stablePreviewColour = previewColour;
            legacyMenuButtonBackground.FadeColour(colour, useSkinnedLegacyCarousel.Value ? duration : osu.Game.Graphics.InterfaceAnimation.FadeDuration(duration));
            background?.FadeColour(previewColour, useSkinnedLegacyCarousel.Value ? duration : osu.Game.Graphics.InterfaceAnimation.FadeDuration(duration));
        }

        internal float FallbackBackgroundAlpha => fallbackBackground.Alpha;

        internal float LegacyPreviewDrawWidthRatio => background is LegacyPreviewBackground legacyPreview ? legacyPreview.PreviewDrawWidthRatio : 0;

        internal int BackgroundLoadCount { get; private set; }

        internal bool RetainsWorkingBeatmap => working != null;

        internal static float GetLegacyPreviewWidth(float width, float height, bool classicLayout = false)
            => width <= 0 ? 0 : Math.Min(width, height * (classicLayout
                ? classic_preview_height_ratio * classic_preview_aspect_ratio + classic_preview_left_ratio
                : legacy_preview_aspect_ratio));

        internal static Vector2 GetClassicPreviewSize(float rowHeight)
            => new Vector2(rowHeight * classic_preview_height_ratio * classic_preview_aspect_ratio, rowHeight * classic_preview_height_ratio);

        public WorkingBeatmap? Beatmap
        {
            get
            {
                if (working != null)
                    return working;

                return loadedWorking?.TryGetTarget(out var loaded) == true ? loaded : null;
            }
            set
            {
                WorkingBeatmap? current = Beatmap;

                if (ReferenceEquals(current, value))
                    return;

                // This guard papers over excessive refreshes of the background asset which occur if
                // `working == value` type guards are used.
                string? currentBackgroundHash = getBackgroundIdentity(current);
                string? newBackgroundHash = getBackgroundIdentity(value);

                if (current != null && value != null && currentBackgroundHash != null && currentBackgroundHash == newBackgroundHash)
                {
                    // Keep the already-loaded texture, but never retain the first WorkingBeatmap for
                    // the lifetime of a pooled panel. It may have decoded its complete beatmap while
                    // selected, which can otherwise keep thousands of hit objects alive after scroll.
                    if (background != null)
                    {
                        loadedWorking = new WeakReference<WorkingBeatmap>(value);
                        working = null;
                    }
                    else
                    {
                        working = value;
                        loadedWorking = null;
                    }

                    return;
                }

                working = value;
                loadedWorking = null;
                resetLoadedBackground();
            }
        }

        private static string? getBackgroundIdentity(WorkingBeatmap? working)
        {
            if (working == null)
                return null;

            if (working is StableWorkingBeatmap stableWorking)
                return stableWorking.CachedBackgroundFilePath;

            if (working is WorkingBeatmapCache.CarouselBackgroundBeatmap carouselWorking)
                return carouselWorking.BackgroundHash;

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
            if (!useSkinnedLegacyCarousel.Value)
                fallbackBackground.Alpha = osu.Game.Overlays.OverlayTransparency.SongSelectActive
                    ? 0.25f
                    : carouselPerformanceMode.Value ? 1 : useLegacyPreviewLayout.Value ? 0.62f : 1;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            this.colourProvider = colourProvider;
            panelRadius = colourProvider.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Panel, Panel.CORNER_RADIUS);
            panelRadius.BindValueChanged(_ => updateLayerVisibility());
            config.BindWith(OsuSetting.ForkSongSelectOldCarouselPreviews, useLegacyPreviewLayout);
            ForkSongSelectStyleBinding.BindSkinnedLegacyCarousel(config, useSkinnedLegacyCarousel, () => beatmapCarousel?.SupportsStableStyle ?? true);
            config.BindWith(OsuSetting.ForkSongSelectCarouselPerformanceMode, carouselPerformanceMode);
            config.BindWith(OsuSetting.ForkSongSelectCarouselPreviews, carouselPreviews);
            config.BindWith(OsuSetting.ForkSongSelectCarouselLazyLoading, carouselLazyLoading);
            config.BindWith(OsuSetting.ForkSongSelectCarouselPreviewResolution, carouselPreviewResolution);

            InternalChildren = new Drawable[]
            {
                new osu.Game.Graphics.Containers.SongSelectBackdropSurface
                {
                    Depth = 2,
                    IsActive = () => !useSkinnedLegacyCarousel.Value,
                    GetCornerRadius = () => panelRadius?.Value ?? Panel.CORNER_RADIUS,
                },
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
            carouselPerformanceMode.BindValueChanged(_ => previewLayoutChanged());
            carouselPreviews.BindValueChanged(_ => previewLayoutChanged());
            carouselPreviewResolution.BindValueChanged(_ => previewLayoutChanged());

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => fallbackBackground.Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4), true);
        }


        private void loadContentIfRequired()
        {
            // InfiniteGlass keeps the regular carousel present to drive its filtering pipeline,
            // but rendering previews for that fully transparent carousel duplicates every texture
            // load. Release existing previews as well so they cannot accumulate across maps.
            if (beatmapCarousel?.SuppressPanelPreviews == true)
            {
                if (background != null || loadCancellation != null)
                    resetLoadedBackground();

                timeSinceUnpool = 0;
                return;
            }

            // A load is already in progress if the cancellation token is non-null.
            if (!carouselPreviews.Value || background != null || loadCancellation != null || working == null)
                return;

            if (beatmapCarousel != null)
            {
                float panelY = this.ToSpaceOfOtherDrawable(new Vector2(0, DrawHeight / 2), beatmapCarousel).Y;

                // We want to preload backgrounds while panels are off-screen, prioritising panels closest
                // to the visual centre so the currently browsed area fills in first.
                double relativeDistanceFromCentre = Math.Abs(panelY - beatmapCarousel.DrawHeight / 2) / beatmapCarousel.DrawHeight;
                double timeUpdatingBeforeLoad = GetBackgroundLoadDelay(relativeDistanceFromCentre, carouselLazyLoading.Value);

                timeSinceUnpool += Time.Elapsed;

                if (timeSinceUnpool <= timeUpdatingBeforeLoad)
                    return;
            }

            // Keep the per-frame admission check free of capturing lambdas. Otherwise
            // Roslyn allocates the callback closure before any of the early returns,
            // even for every already-loaded or disabled panel on every Update.
            startBackgroundLoad();
        }

        private void startBackgroundLoad()
        {
            var cancellation = loadCancellation = new CancellationTokenSource();
            bool legacyLayout = useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value;
            bool classicLayout = useSkinnedLegacyCarousel.Value;
            int previewResolution = carouselPreviewResolution.Value;
            Drawable backgroundToLoad = createBackgroundDrawable(working!, legacyLayout, classicLayout, previewResolution);
            bool callbackInvoked = false;

            LoadComponentAsync(backgroundToLoad, loadedBackground =>
            {
                callbackInvoked = true;

                if (loadCancellation != cancellation || cancellation.IsCancellationRequested
                    || !carouselPreviews.Value
                    || beatmapCarousel?.SuppressPanelPreviews == true
                    || legacyLayout != (useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value)
                    || classicLayout != useSkinnedLegacyCarousel.Value
                    || previewResolution != carouselPreviewResolution.Value)
                {
                    loadedBackground.Dispose();
                    return;
                }

                AddInternal(background = loadedBackground);
                if (useSkinnedLegacyCarousel.Value)
                    background.Colour = stablePreviewColour;
                loadedWorking = new WeakReference<WorkingBeatmap>(working!);
                working = null;
                BackgroundLoadCount++;

                bool spriteOnScreen = beatmapCarousel?.ScreenSpaceDrawQuad.Intersects(loadedBackground.ScreenSpaceDrawQuad) != false;
                loadedBackground.FadeInFromZero(spriteOnScreen ? (useSkinnedLegacyCarousel.Value ? 400 : osu.Game.Graphics.InterfaceAnimation.FadeDuration(400)) : 0, Easing.OutQuint);
            }, cancellation.Token).ContinueWith(_ =>
            {
                // CompositeDrawable omits the callback when cancellation wins during loading. Queue
                // cleanup after its already-queued callback, releasing any texture held by a loaded but
                // unattached drawable rather than leaving it to finalization during rapid scrolling.
                Schedule(() =>
                {
                    if (!callbackInvoked)
                        backgroundToLoad.Dispose();
                });
            }, TaskScheduler.Default);
        }

        internal static double GetBackgroundLoadDelay(double relativeDistanceFromCentre, bool useToriiStyleLazyLoading)
        {
            double distanceDelay = relativeDistanceFromCentre * (useToriiStyleLazyLoading ? torii_distance_time_before_load : normal_distance_time_before_load);
            return (useToriiStyleLazyLoading ? torii_minimum_time_before_load : 0) + distanceDelay;
        }

        private static Drawable createBackgroundDrawable(WorkingBeatmap beatmap, bool legacyLayout, bool classicLayout, int previewResolution)
        {
            if (legacyLayout)
                return new LegacyPreviewBackground(beatmap, classicLayout, previewResolution);

            return new PanelBeatmapBackground(beatmap, false, previewResolution)
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

            // Keep the weakly-held source alive only while a replacement drawable is loading.
            // The currently attached background still owns it at this point.
            working ??= Beatmap;
            loadedWorking = null;
            resetLoadedBackground();
        }

        private void updateLayerVisibility()
        {
            if (carouselPerformanceMode.Value)
            {
                Masking = false;
                CornerRadius = 0;
                fallbackBackground.Alpha = 1;
                legacyBlackBackground.Alpha = 0;
                legacyMenuButtonBackground.Alpha = 0;
                modernEffects.Alpha = 0;
                legacyEffects.Alpha = 0;
                return;
            }

            bool legacyLayout = useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value;

            Masking = !useSkinnedLegacyCarousel.Value;
            CornerRadius = useSkinnedLegacyCarousel.Value ? 0 : (panelRadius?.Value ?? Panel.CORNER_RADIUS);

            fallbackBackground.Alpha = useSkinnedLegacyCarousel.Value ? 0 : legacyLayout ? 0.62f : 1;
            if (!useSkinnedLegacyCarousel.Value)
            {
                legacyMenuButtonBackground.Colour = Color4.White;
                background?.FadeColour(Color4.White);
                StableStateColour = Color4.White;
                stablePreviewColour = Color4.White;
            }
            legacyBlackBackground.Alpha = 0;
            legacyMenuButtonBackground.Alpha = useSkinnedLegacyCarousel.Value ? 1 : 0;
            modernEffects.Alpha = legacyLayout ? 0 : 1;
            legacyEffects.Alpha = legacyLayout && !useSkinnedLegacyCarousel.Value ? 1 : 0;
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
            private WorkingBeatmap? working;
            private readonly bool classicLayout;
            private readonly int previewResolution;
            private Container previewFrame = null!;
            private IBindable<float>? panelRadius;

            public LegacyPreviewBackground(WorkingBeatmap working, bool classicLayout, int previewResolution)
            {
                this.working = working;
                this.classicLayout = classicLayout;
                this.previewResolution = previewResolution;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader(permitNulls: true)]
            private void load(OverlayColourProvider? colourProvider)
            {
                var workingBeatmap = working!;
                working = null;

                panelRadius = colourProvider?.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Panel, Panel.CORNER_RADIUS);

                InternalChild = previewFrame = new Container
                {
                    Depth = 0,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Masking = true,
                    CornerRadius = classicLayout ? 0 : (panelRadius?.Value ?? Panel.CORNER_RADIUS),
                    MaskingSmoothness = 2f,
                    Child = new PanelBeatmapBackground(workingBeatmap, true, previewResolution, classicLayout)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                    },
                };

                panelRadius?.BindValueChanged(r =>
                {
                    previewFrame.CornerRadius = classicLayout ? 0 : r.NewValue;
                });
            }

            public float PreviewDrawWidthRatio => DrawWidth > 0 ? previewFrame.DrawWidth / DrawWidth : 0;

            protected override void Update()
            {
                base.Update();

                if (DrawWidth <= 0 || DrawHeight <= 0)
                    return;

                if (classicLayout)
                {
                    // Stable's 80x60 thumbnail is drawn at 1.425 scale, with an
                    // independent (5.2, 0.25) offset in its 480-high coordinate space.
                    // It is not stretched to fill the button's complete row height.
                    Vector2 size = GetClassicPreviewSize(DrawHeight);
                    previewFrame.Size = new Vector2(size.X / DrawWidth, size.Y / DrawHeight);
                    previewFrame.X = DrawHeight * classic_preview_left_ratio;
                    previewFrame.Y = DrawHeight * (0.25f * StableCarouselLayout.Scale / PanelBeatmapStandalone.CLASSIC_HEIGHT);
                }
                else
                    previewFrame.Width = GetLegacyPreviewWidth(DrawWidth, DrawHeight) / DrawWidth;
            }
        }

        public partial class PanelBeatmapBackground : Sprite
        {
            private IWorkingBeatmap? working;
            private readonly bool useLegacyTexture;
            private readonly bool classicLayout;
            private readonly int previewResolution;
            private osu.Game.Graphics.Backgrounds.AI.BackgroundSafetyPresentation? safety;

            public PanelBeatmapBackground(IWorkingBeatmap working)
                : this(working, false, 100)
            {
            }

            public PanelBeatmapBackground(IWorkingBeatmap working, bool useLegacyTexture)
                : this(working, useLegacyTexture, 100)
            {
            }

            public PanelBeatmapBackground(IWorkingBeatmap working, bool useLegacyTexture, int previewResolution, bool classicLayout = false)
            {
                ArgumentNullException.ThrowIfNull(working);

                this.working = working;
                this.useLegacyTexture = useLegacyTexture;
                this.classicLayout = classicLayout;
                this.previewResolution = previewResolution;
            }

            [BackgroundDependencyLoader]
            private void load(osu.Game.Graphics.Backgrounds.AI.BackgroundSafetyService? safetyService = null, OsuConfigManager? config = null,
                              osu.Framework.Graphics.Rendering.IRenderer? renderer = null, osu.Game.Graphics.Backgrounds.AI.StillLifeService? stillLife = null)
            {
                var workingBeatmap = working!;
                working = null;

                var texture = classicLayout
                    ? workingBeatmap.GetClassicPreviewBackground(previewResolution)
                    : useLegacyTexture
                        ? workingBeatmap.GetLegacyPreviewBackground(previewResolution) ?? workingBeatmap.GetPanelBackground(previewResolution)
                        : workingBeatmap.GetPanelBackground(previewResolution);
                if (workingBeatmap is WorkingBeatmap beatmap && safetyService != null && config != null && renderer != null)
                    safety = new osu.Game.Graphics.Backgrounds.AI.BackgroundSafetyPresentation(this, texture, beatmap, safetyService, config, renderer, stillLife);
                else
                    Texture = texture;
            }

            protected override void Update()
            {
                base.Update();
                safety?.Update();
            }

            protected override void Dispose(bool isDisposing)
            {
                safety?.Dispose();
                base.Dispose(isDisposing);
            }
        }
    }
}
