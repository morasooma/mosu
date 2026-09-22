// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A freely pannable and zoomable two-dimensional view over the regular beatmap carousel.
    /// Filtering, sorting, selection and activation remain owned by <see cref="BeatmapCarousel"/>.
    /// Only cards intersecting the viewport are materialised, and those card drawables are reused.
    /// </summary>
    internal partial class InfiniteGlassBeatmapCanvas : CompositeDrawable
    {
        private const float min_zoom = 0.35f;
        private const float max_zoom = 1.8f;
        private const double camera_animation_duration = 450;

        public required Action<CarouselItem> ActivateItem { private get; init; }

        /// <summary>
        /// Supplies the complete result of the carousel's latest filter operation.
        /// </summary>
        public required Func<IEnumerable<CarouselItem>?> GetCarouselItems { private get; init; }

        private readonly UnboundedWorldContainer world;
        private readonly List<CardEntry> entries = new List<CardEntry>();
        private readonly Dictionary<int, InfiniteGlassBeatmapCard> visibleCards = new Dictionary<int, InfiniteGlassBeatmapCard>();
        private readonly Stack<InfiniteGlassBeatmapCard> reusableCards = new Stack<InfiniteGlassBeatmapCard>();

        private IBindable<WorkingBeatmap> selectedBeatmap = null!;
        private BeatmapManager beatmaps = null!;
        private BeatmapDifficultyCache difficultyCache = null!;
        private OsuColour colours = null!;
        private IBindable<bool> showAdditionalInfo = null!;
        private InfiniteGlassBeatmapLayout layout = InfiniteGlassBeatmapLayout.Empty;
        private bool active;
        private float zoom = 1;
        private int selectedEntryIndex = -1;
        private Vector2 lastViewportSize = new Vector2(float.NaN, float.NaN);

        private bool cameraAnimationActive;
        private int? cameraAnimationFocusIndex;
        private double cameraAnimationElapsed;
        private Vector2 cameraAnimationStartPosition;
        private Vector2 cameraAnimationTargetPosition;
        private float cameraAnimationStartZoom;
        private float cameraAnimationTargetZoom;

        public InfiniteGlassBeatmapCanvas()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            Alpha = 0;

            InternalChild = world = new UnboundedWorldContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            };
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<WorkingBeatmap> selectedBeatmap, BeatmapManager beatmaps, BeatmapDifficultyCache difficultyCache,
                          OsuColour colours, OsuConfigManager config)
        {
            // Keep a local copy rather than the injected game-wide bindable itself. Drawable cleanup
            // automatically calls UnbindAll() on bindable fields; storing the dependency directly
            // would therefore detach MusicController and every other global beatmap consumer when
            // song select exits.
            this.selectedBeatmap = selectedBeatmap.GetBoundCopy();
            this.beatmaps = beatmaps;
            this.difficultyCache = difficultyCache;
            this.colours = colours;
            showAdditionalInfo = config.GetBindable<bool>(OsuSetting.ForkDifficultyAdditionalInfo);
            this.selectedBeatmap.BindValueChanged(_ =>
            {
                int previousSelection = selectedEntryIndex;
                selectedEntryIndex = findSelectedEntryIndex();
                updateSelection();

                if (active)
                {
                    // Clicks and spatial keyboard navigation update the local selection immediately.
                    // Only animate here for selections originating elsewhere.
                    if (selectedEntryIndex >= 0 && selectedEntryIndex != previousSelection)
                        centreOnEntry(selectedEntryIndex, true);

                    updateVisibleCards();
                }
            }, true);
        }

        public void SetActive(bool value)
        {
            if (active == value)
                return;

            active = value;
            Alpha = active ? 1 : 0;

            if (!active)
            {
                cancelCameraAnimation();
                recycleVisibleCards();
                return;
            }

            var carouselItems = GetCarouselItems();

            if (carouselItems != null)
                SetItems(carouselItems);
            else
                updateVisibleCards();
        }

        /// <summary>
        /// Rebuilds the lightweight layout model from the complete filtered carousel result.
        /// No drawables or textures are created for off-screen beatmaps.
        /// </summary>
        public void SetItems(IEnumerable<CarouselItem> carouselItems)
        {
            // The regular styles do not pay the cost of maintaining a second presentation.
            // SetActive() reads the carousel's latest result when this style becomes active.
            if (!active)
                return;

            recycleVisibleCards();
            entries.Clear();

            CarouselItem[] items = carouselItems.ToArray();
            CarouselItem[] difficultyItems = items.Where(item => item.Model is GroupedBeatmap).ToArray();
            IEnumerable<CarouselItem> source = difficultyItems.Length > 0
                ? difficultyItems
                : items.Where(item => item.Model is GroupedBeatmapSet);

            foreach (var item in source)
            {
                BeatmapInfo? beatmap = getBeatmap(item);

                if (beatmap != null)
                    entries.Add(new CardEntry(item, beatmap, getSongKey(beatmap)));
            }

            layout = new InfiniteGlassBeatmapLayout(entries.Select(entry => entry.SongKey).ToArray());
            selectedEntryIndex = findSelectedEntryIndex();
            centreOnEntry(selectedEntryIndex >= 0 ? selectedEntryIndex : 0, false);
            updateVisibleCards();
        }

        public bool HandleNavigation(GlobalAction action)
        {
            if (!active || entries.Count == 0)
                return false;

            Vector2 direction;
            int linearFallbackDelta;

            switch (action)
            {
                case GlobalAction.SelectPrevious:
                    direction = -Vector2.UnitY;
                    linearFallbackDelta = -1;
                    break;

                case GlobalAction.SelectNext:
                    direction = Vector2.UnitY;
                    linearFallbackDelta = 1;
                    break;

                case GlobalAction.ActivatePreviousSet:
                    direction = -Vector2.UnitX;
                    linearFallbackDelta = -1;
                    break;

                case GlobalAction.ActivateNextSet:
                    direction = Vector2.UnitX;
                    linearFallbackDelta = 1;
                    break;

                default:
                    return false;
            }

            int currentIndex = selectedEntryIndex;

            if (currentIndex < 0)
                return true;

            int? targetIndex = layout.FindClosestInDirection(currentIndex, direction);

            // A song cluster may have every difficulty on one horizontal row. In that case the
            // regular previous/next actions have no candidate above or below even though adjacent
            // cards exist. Preserve spatial navigation where possible, then fall back to the next
            // carousel entry so the action never becomes a swallowed no-op.
            if (targetIndex == null)
            {
                int fallbackIndex = currentIndex + linearFallbackDelta;

                if ((uint)fallbackIndex < entries.Count)
                    targetIndex = fallbackIndex;
            }

            if (targetIndex != null)
                activateEntry(targetIndex.Value);

            // Do not fall through to the hidden carousel's linear navigation at field edges.
            return true;
        }

        private void activateItem(CarouselItem item)
        {
            int index = entries.FindIndex(entry => ReferenceEquals(entry.Item, item));

            if (index >= 0)
                activateEntry(index);
            else
                ActivateItem(item);
        }

        private void activateEntry(int index)
        {
            if (index != selectedEntryIndex)
            {
                selectedEntryIndex = index;
                updateSelection();
                centreOnEntry(index, true);
                updateVisibleCards();
            }

            ActivateItem(entries[index].Item);
        }

        protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left || e.Button == MouseButton.Middle;

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left && e.Button != MouseButton.Middle)
                return false;

            cancelCameraAnimation();
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            // Match ScrollContainer's coordinate handling. DragEvent.Delta is in the target's
            // parent space; the world position is in this canvas' local space.
            Vector2 localDelta = ToLocalSpace(e.ScreenSpaceMousePosition) - ToLocalSpace(e.ScreenSpaceLastMousePosition);
            world.Position += localDelta;
            updateVisibleCards();
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return false;

            cancelCameraAnimation();

            float oldZoom = zoom;
            zoom = Math.Clamp(zoom * (e.ScrollDelta.Y > 0 ? 1.12f : 1 / 1.12f), min_zoom, max_zoom);

            if (Math.Abs(zoom - oldZoom) < float.Epsilon)
                return true;

            Vector2 cursorFromCentre = ToLocalSpace(e.ScreenSpaceMousePosition) - DrawSize / 2;
            world.Position = InfiniteGlassBeatmapLayout.GetWorldPositionAfterZoom(world.Position, cursorFromCentre, oldZoom, zoom);
            world.Scale = new Vector2(zoom);
            updateVisibleCards();
            return true;
        }

        protected override void Update()
        {
            base.Update();

            bool cameraChanged = updateCameraAnimation();

            if (active && (cameraChanged || lastViewportSize != DrawSize))
                updateVisibleCards();
        }

        private bool updateCameraAnimation()
        {
            if (!active || !cameraAnimationActive)
                return false;

            cameraAnimationElapsed = Math.Min(camera_animation_duration, cameraAnimationElapsed + Time.Elapsed);
            world.Position = Interpolation.ValueAt(cameraAnimationElapsed, cameraAnimationStartPosition, cameraAnimationTargetPosition,
                0, camera_animation_duration, Easing.OutQuint);
            zoom = Interpolation.ValueAt(cameraAnimationElapsed, cameraAnimationStartZoom, cameraAnimationTargetZoom,
                0, camera_animation_duration, Easing.OutQuint);
            world.Scale = new Vector2(zoom);

            if (cameraAnimationElapsed >= camera_animation_duration)
            {
                cameraAnimationActive = false;
                cameraAnimationFocusIndex = null;
            }

            return true;
        }

        private void updateVisibleCards()
        {
            if (!active)
                return;

            lastViewportSize = DrawSize;

            var desiredSet = layout.GetVisibleIndices(DrawSize, world.Position, zoom).ToHashSet();

            // While moving to a selection, materialise its future viewport immediately. This lets
            // its preview and nearby previews load during camera travel instead of after arrival.
            if (cameraAnimationFocusIndex is int focusIndex)
            {
                Vector2 targetWorldPosition = -layout.GetCardPosition(focusIndex) * zoom;
                desiredSet.UnionWith(layout.GetVisibleIndices(DrawSize, targetWorldPosition, zoom));
                desiredSet.Add(focusIndex);
            }

            // Recycle cards which left the viewport before requesting new ones. This means a
            // normal pan generally performs no allocations at all after the initial screenful.
            foreach (int index in visibleCards.Keys.Where(index => !desiredSet.Contains(index)).ToArray())
                recycleCard(index);

            int[] loadOrder = desiredSet.OrderBy(loadPriorityDistanceSquared).ToArray();

            for (int priority = 0; priority < loadOrder.Length; priority++)
            {
                int index = loadOrder[priority];
                double backgroundLoadDelay = cameraAnimationFocusIndex != null
                    ? index == cameraAnimationFocusIndex ? 0 : 30 + priority * 18
                    : 75 + priority * 24;

                if (visibleCards.TryGetValue(index, out InfiniteGlassBeatmapCard? existingCard))
                {
                    existingCard.PrioritiseBackgroundLoad(backgroundLoadDelay);
                    continue;
                }

                InfiniteGlassBeatmapCard card;

                if (reusableCards.Count > 0)
                    card = reusableCards.Pop();
                else
                {
                    card = new InfiniteGlassBeatmapCard(activateItem, beatmaps, difficultyCache, colours, showAdditionalInfo);
                    world.Add(card);
                }

                Vector2 position = layout.GetCardPosition(index);

                card.Bind(entries[index], position, backgroundLoadDelay);
                visibleCards.Add(index, card);
            }

            updateSelection();

            float loadPriorityDistanceSquared(int index)
            {
                if (cameraAnimationFocusIndex is int focus)
                    return (layout.GetCardPosition(index) - layout.GetCardPosition(focus)).LengthSquared;

                Vector2 position = layout.GetScreenPosition(index, world.Position, zoom);
                return position.LengthSquared;
            }
        }

        private void recycleCard(int index)
        {
            var card = visibleCards[index];
            visibleCards.Remove(index);
            card.Unbind();
            reusableCards.Push(card);
        }

        private void recycleVisibleCards()
        {
            foreach (int index in visibleCards.Keys.ToArray())
                recycleCard(index);
        }

        private static BeatmapInfo? getBeatmap(CarouselItem item)
        {
            return item.Model switch
            {
                GroupedBeatmap groupedBeatmap => groupedBeatmap.Beatmap,
                GroupedBeatmapSet groupedBeatmapSet => groupedBeatmapSet.BeatmapSet.Beatmaps.OrderBy(b => b.StarRating).FirstOrDefault(),
                _ => null,
            };
        }

        private static string getSongKey(BeatmapInfo beatmap)
        {
            IBeatmapMetadataInfo metadata = beatmap.Metadata;
            string title = metadata.Title.Trim().ToUpperInvariant();
            string unicodeTitle = metadata.TitleUnicode.Trim().ToUpperInvariant();
            string setId = beatmap.BeatmapSet?.ID.ToString("N") ?? string.Empty;

            // The set identity prevents unrelated maps with a coincidentally equal title from being linked.
            // Keeping both title forms in the key also enforces the requested same-song/title condition.
            return $"{setId}\u001f{title}\u001f{unicodeTitle}";
        }

        private int findSelectedEntryIndex()
            => entries.FindIndex(entry => entry.Beatmap.Equals(selectedBeatmap.Value.BeatmapInfo));

        private void centreOnEntry(int targetIndex, bool animated)
        {
            if (entries.Count == 0)
            {
                cancelCameraAnimation();
                world.Position = Vector2.Zero;
                return;
            }

            if (!animated)
            {
                cancelCameraAnimation();
                world.Position = -layout.GetCardPosition(targetIndex) * zoom;
                return;
            }

            cameraAnimationStartPosition = world.Position;
            cameraAnimationStartZoom = zoom;
            // Selection moves the camera without overriding the user's zoom level.
            cameraAnimationTargetZoom = zoom;
            cameraAnimationTargetPosition = -layout.GetCardPosition(targetIndex) * cameraAnimationTargetZoom;
            cameraAnimationElapsed = 0;
            cameraAnimationFocusIndex = targetIndex;
            cameraAnimationActive = cameraAnimationStartPosition != cameraAnimationTargetPosition
                                    || Math.Abs(cameraAnimationStartZoom - cameraAnimationTargetZoom) > float.Epsilon;

            if (!cameraAnimationActive)
                cameraAnimationFocusIndex = null;
        }

        private void cancelCameraAnimation()
        {
            cameraAnimationActive = false;
            cameraAnimationFocusIndex = null;
        }

        private void updateSelection()
        {
            foreach ((int index, InfiniteGlassBeatmapCard card) in visibleCards)
                card.Selected = index == selectedEntryIndex;
        }

        private readonly record struct CardEntry(CarouselItem Item, BeatmapInfo Beatmap, string SongKey);

        /// <summary>
        /// Hosts cards whose positions intentionally extend far beyond this container's own quad.
        /// The default composite masking optimisation uses that quad to discard the complete
        /// subtree once it leaves the viewport, even when some of its children are still visible.
        /// </summary>
        private partial class UnboundedWorldContainer : Container
        {
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
        }

        private partial class InfiniteGlassBeatmapCard : CompositeDrawable
        {
            private const int background_preview_resolution = 75;

            private readonly Action<CarouselItem> activate;
            private readonly BeatmapManager beatmaps;
            private readonly BeatmapDifficultyCache difficultyCache;
            private readonly OsuColour colours;
            private readonly IBindable<bool> showAdditionalInfo;
            private readonly Container backgroundContainer;
            private readonly Box accentBar;
            private readonly TruncatingSpriteText titleText;
            private readonly TruncatingSpriteText difficultyText;
            private readonly TruncatingSpriteText additionalStatsText;
            private readonly OsuSpriteText starText;
            private readonly Box hover;
            private readonly Box selection;

            private CardEntry entry;
            private bool bound;
            private bool selected;
            private double timeSinceBind;
            private double backgroundLoadDelay;
            private CancellationTokenSource? backgroundLoadCancellation;
            private CancellationTokenSource? difficultyCancellation;
            private IBindable<StarDifficulty>? starDifficulty;
            private StarDifficulty displayedDifficulty;
            private Drawable? background;

            public BeatmapInfo? Beatmap => bound ? entry.Beatmap : null;

            public bool Selected
            {
                set
                {
                    if (!bound || selected == value)
                        return;

                    selected = value;
                    selection.FadeTo(value ? 1 : 0, 160, Easing.OutQuint);
                }
            }

            public InfiniteGlassBeatmapCard(Action<CarouselItem> activate, BeatmapManager beatmaps, BeatmapDifficultyCache difficultyCache,
                                            OsuColour colours, IBindable<bool> showAdditionalInfo)
            {
                this.activate = activate;
                this.beatmaps = beatmaps;
                this.difficultyCache = difficultyCache;
                this.colours = colours;
                this.showAdditionalInfo = showAdditionalInfo.GetBoundCopy();

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Size = new Vector2(InfiniteGlassBeatmapLayout.CardWidth, InfiniteGlassBeatmapLayout.CardHeight);
                CornerRadius = 14;
                Masking = true;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                    },
                    backgroundContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.30f),
                    },
                    accentBar = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 5,
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(13),
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            titleText = new TruncatingSpriteText
                            {
                                Font = OsuFont.Style.Body.With(size: 17, weight: FontWeight.SemiBold),
                                MaxWidth = InfiniteGlassBeatmapLayout.CardWidth - 26,
                            },
                            difficultyText = new TruncatingSpriteText
                            {
                                Font = OsuFont.Style.Caption1.With(size: 14),
                                Colour = Color4.White.Opacity(0.75f),
                                MaxWidth = InfiniteGlassBeatmapLayout.CardWidth - 26,
                            },
                            additionalStatsText = new TruncatingSpriteText
                            {
                                Font = OsuFont.Style.Caption2.With(size: 11, weight: FontWeight.SemiBold),
                                Colour = Color4.White.Opacity(0.80f),
                                MaxWidth = InfiniteGlassBeatmapLayout.CardWidth - 26,
                                Alpha = 0,
                            },
                            starText = new OsuSpriteText
                            {
                                Font = OsuFont.Style.Heading2.With(size: 19),
                            },
                        }
                    },
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.12f),
                        Alpha = 0,
                    },
                    selection = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                };

                this.showAdditionalInfo.BindValueChanged(value =>
                {
                    if (bound)
                    {
                        if (value.NewValue && starDifficulty == null)
                            bindDifficulty(backgroundLoadDelay);
                        else if (!value.NewValue)
                            resetDifficulty();
                    }

                    updateAdditionalInfo();
                }, true);
            }

            public void Bind(CardEntry entry, Vector2 position, double backgroundLoadDelay)
            {
                resetBackground();
                resetDifficulty();

                this.entry = entry;
                this.backgroundLoadDelay = backgroundLoadDelay;
                bound = true;
                selected = false;
                Position = position;

                IBeatmapMetadataInfo metadata = entry.Beatmap.Metadata;

                // Reuse the same localisable metadata value as the regular song-select panels.
                titleText.Text = new RomanisableString(metadata.TitleUnicode, metadata.Title);
                difficultyText.Text = entry.Beatmap.DifficultyName;
                applyDifficulty(new StarDifficulty(entry.Beatmap.StarRating, 0));

                if (showAdditionalInfo.Value)
                    bindDifficulty(backgroundLoadDelay);

                hover.ClearTransforms();
                hover.Alpha = 0;
                selection.ClearTransforms();
                selection.Alpha = 0;
                Alpha = 1;
            }

            public void PrioritiseBackgroundLoad(double delay)
            {
                if (!bound || background != null || backgroundLoadCancellation != null)
                    return;

                backgroundLoadDelay = Math.Min(backgroundLoadDelay, delay);
            }

            public void Unbind()
            {
                bound = false;
                selected = false;
                Alpha = 0;
                resetBackground();
                resetDifficulty();
            }

            protected override void Update()
            {
                base.Update();

                if (!bound || background != null || backgroundLoadCancellation != null)
                    return;

                timeSinceBind += Time.Elapsed;

                if (timeSinceBind < backgroundLoadDelay)
                    return;

                CarouselItem requestedItem = entry.Item;
                var cancellation = backgroundLoadCancellation = new CancellationTokenSource();
                var requestedBackground = new PanelSetBackground.PanelBeatmapBackground(
                    beatmaps.GetWorkingBeatmap(entry.Beatmap), true, background_preview_resolution)
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fit,
                    Colour = Color4.White,
                };

                bool callbackInvoked = false;

                LoadComponentAsync(requestedBackground, loadedBackground =>
                {
                    callbackInvoked = true;

                    if (!bound || entry.Item != requestedItem || backgroundLoadCancellation != cancellation || cancellation.IsCancellationRequested)
                    {
                        loadedBackground.Dispose();
                        return;
                    }

                    backgroundLoadCancellation = null;
                    background = loadedBackground;
                    backgroundContainer.Add(loadedBackground);
                    loadedBackground.FadeInFromZero(160, Easing.OutQuint);
                }, cancellation.Token).ContinueWith(_ =>
                {
                    Schedule(() =>
                    {
                        if (!callbackInvoked)
                            requestedBackground.Dispose();
                    });
                }, TaskScheduler.Default);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!bound)
                    return false;

                activate(entry.Item);
                return true;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!bound)
                    return false;

                hover.FadeIn(100);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(250, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            // Let the canvas own drags begun on a card, while retaining normal click behaviour.
            protected override bool OnDragStart(DragStartEvent e) => false;

            protected override void Dispose(bool isDisposing)
            {
                backgroundLoadCancellation?.Cancel();
                difficultyCancellation?.Cancel();
                showAdditionalInfo.UnbindAll();
                base.Dispose(isDisposing);
            }

            private void resetBackground()
            {
                backgroundLoadCancellation?.Cancel();
                backgroundLoadCancellation = null;
                backgroundContainer.Clear();
                background = null;
                timeSinceBind = 0;
            }

            private void bindDifficulty(double delay)
            {
                CarouselItem requestedItem = entry.Item;
                var cancellation = difficultyCancellation = new CancellationTokenSource();
                starDifficulty = difficultyCache.GetBindableDifficulty(entry.Beatmap, cancellation.Token,
                    (int)Math.Max(SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE, delay), calculatePerformance: false,
                    usePersistedAdditionalInfo: true);
                starDifficulty.BindValueChanged(value =>
                {
                    if (!bound || entry.Item != requestedItem || difficultyCancellation != cancellation || cancellation.IsCancellationRequested)
                        return;

                    applyDifficulty(value.NewValue);
                }, true);
            }

            private void applyDifficulty(StarDifficulty difficulty)
            {
                displayedDifficulty = difficulty;

                Color4 accent = colours.ForStarDifficulty(difficulty.Stars);
                accentBar.Colour = accent;
                starText.Text = $"\u2605 {difficulty.Stars:0.00}";
                starText.Colour = difficulty.Stars >= OsuColour.STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF
                    ? colours.ForStarDifficultyText(difficulty.Stars)
                    : accent;
                selection.Colour = accent.Opacity(0.28f);
                updateAdditionalInfo();
            }

            private void updateAdditionalInfo()
            {
                if (!bound || !showAdditionalInfo.Value)
                {
                    additionalStatsText.Alpha = 0;
                    return;
                }

                additionalStatsText.Text = BeatmapAdditionalInfoFormatter.Format(displayedDifficulty);
                additionalStatsText.Alpha = 1;
            }

            private void resetDifficulty()
            {
                difficultyCancellation?.Cancel();
                difficultyCancellation = null;
                starDifficulty = null;
            }
        }
    }
}
