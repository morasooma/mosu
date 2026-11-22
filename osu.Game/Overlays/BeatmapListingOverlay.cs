// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Localisation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class BeatmapListingOverlay : OnlineOverlay<BeatmapListingHeader>
    {
        [Resolved]
        private PreviewTrackManager previewTrackManager { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; }

        private IBindable<APIUser> apiUser;

        private Container panelTarget;
        private FillFlowContainer<BeatmapCardEntry> foundContent;
        private Box panelBackground;
        private IBindable<Colour4> themeColour;

        private BeatmapListingFilterControl filterControl => Header.FilterControl;

        public BeatmapListingOverlay()
            : base(OverlayColourScheme.Blue, ThemeSurfaceContext.BeatmapLibrary)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new Container
                    {
                        AutoSizeAxes = Axes.Y,
                        RelativeSizeAxes = Axes.X,
                        Children = new Drawable[]
                        {
                            panelBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            panelTarget = new Container
                            {
                                AutoSizeAxes = Axes.Y,
                                RelativeSizeAxes = Axes.X,
                                Masking = true,
                                Padding = new MarginPadding { Horizontal = 20 },
                            }
                        },
                    },
                }
            };

            filterControl.TypingStarted = onTypingStarted;
            filterControl.SearchStarted = onSearchStarted;
            filterControl.SearchFinished = onSearchFinished;

            themeColour = ColourProvider.GetColourBindable(OverlayColour.Background5);
            themeColour.BindValueChanged(colour => panelBackground.Colour = colour.NewValue, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            filterControl.CardSize.BindValueChanged(_ => onCardSizeChanged());

            filterControl.SearchEnabled = State.Value == Visibility.Visible;
            State.BindValueChanged(state => filterControl.SearchEnabled = state.NewValue == Visibility.Visible);

            apiUser = api.LocalUser.GetBoundCopy();
            apiUser.BindValueChanged(_ => Schedule(() =>
            {
                if (api.IsLoggedIn)
                    replaceResultsAreaContent(Empty());
            }));
        }

        public void ShowWithSearch(string query)
        {
            filterControl.Search(query);
            Show();
            ScrollFlow.ScrollToStart();
        }

        public void ShowForRuleset(RulesetInfo ruleset)
        {
            filterControl.FilterRuleset(ruleset);
            filterControl.Search(string.Empty);
            Show();
            ScrollFlow.ScrollToStart();
        }

        public void ShowWithGenreFilter(SearchGenre genre)
        {
            ShowWithSearch(string.Empty);
            filterControl.FilterGenre(genre);
        }

        public void ShowWithLanguageFilter(SearchLanguage language)
        {
            ShowWithSearch(string.Empty);
            filterControl.FilterLanguage(language);
        }

        protected override BeatmapListingHeader CreateHeader() => new BeatmapListingHeader();

        protected override Color4 BackgroundColour => ColourProvider.Background6;

        private void onTypingStarted()
        {
            // temporary until the textbox/header is updated to always stay on screen.
            ScrollFlow.ScrollToStart();
        }

        protected override void OnFocus(FocusEvent e)
        {
            base.OnFocus(e);

            filterControl.TakeFocus();
        }

        private CancellationTokenSource cancellationToken;

        private Task panelLoadTask;
        private readonly Queue<BeatmapCardEntry> pendingCards = new Queue<BeatmapCardEntry>();
        private FillFlowContainer<BeatmapCardEntry> pendingContent;
        private CardBatch pendingBatch;
        private bool replacePendingContent;
        private bool cardInitializedThisFrame;
        private readonly Dictionary<BeatmapCardSize, BeatmapCardPool> cardPools = new Dictionary<BeatmapCardSize, BeatmapCardPool>();

        // Retain a small reverse-scroll neighbourhood, not two seconds' worth of
        // heavy trees at arbitrary scroll speed. Active previews/favourite requests
        // remain protected by BeatmapCardEntry's ordinary visibility policy.
        private const int hidden_card_capacity = 12;
        private readonly Dictionary<BeatmapCardEntry, LinkedListNode<BeatmapCardEntry>> hiddenCards = new Dictionary<BeatmapCardEntry, LinkedListNode<BeatmapCardEntry>>();
        private readonly LinkedList<BeatmapCardEntry> hiddenCardOrder = new LinkedList<BeatmapCardEntry>();
        private readonly object hiddenCardLock = new object();

        private bool retainHiddenCard(BeatmapCardEntry entry, bool release)
        {
            // CancelTasks may also run on the async disposal thread.
            lock (hiddenCardLock)
            {
                if (release)
                {
                    if (hiddenCards.Remove(entry, out var node))
                        hiddenCardOrder.Remove(node);
                    return true;
                }

                if (State.Value != Visibility.Visible)
                    return false;
                if (hiddenCards.ContainsKey(entry))
                    return true;

                hiddenCards.Add(entry, hiddenCardOrder.AddLast(entry));
                if (hiddenCards.Count > hidden_card_capacity)
                {
                    var oldest = hiddenCardOrder.First.Value;
                    hiddenCardOrder.RemoveFirst();
                    hiddenCards.Remove(oldest);
                    oldest.ReleaseHiddenContent();
                }
                return true;
            }
        }

        private bool admitCardFirstUpdate()
        {
            if (cardInitializedThisFrame)
                return false;
            cardInitializedThisFrame = true;
            return true;
        }

        private void onSearchStarted()
        {
            cancellationToken?.Cancel();
            clearPendingCards();

            previewTrackManager.StopAnyPlaying(this);

            if (panelTarget.Any())
                Loading.Show();
        }

        private void onSearchFinished(BeatmapListingFilterControl.SearchResult searchResult)
        {
            cancellationToken?.Cancel();
            clearPendingCards();

            if (searchResult.Type == BeatmapListingFilterControl.SearchResultType.SupporterOnlyFilters)
            {
                var supporterOnly = new SupporterRequiredDrawable(searchResult.SupporterOnlyFiltersUsed);
                replaceResultsAreaContent(supporterOnly);
                return;
            }

            if (searchResult.Type == BeatmapListingFilterControl.SearchResultType.Error)
            {
                replaceResultsAreaContent(new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 20))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(30),
                    Text = searchResult.ErrorMessage,
                });
                return;
            }

            var newSets = searchResult.Results.ToArray();

            if (filterControl.CurrentPage == 0 || foundContent == null)
            {
                //No matches case
                if (newSets.Length == 0)
                {
                    replaceResultsAreaContent(new NotFoundDrawable());
                    return;
                }

                // Load in the background, but do not initialise the entire result
                // tree on one Update frame when it is attached to the hierarchy.
                var content = createCardContainerFor(Enumerable.Empty<BeatmapCardEntry>());
                loadCardBatch(content, newSets, true);
            }
            else
            {
                // new results may contain beatmaps from a previous page,
                // this is dodgy but matches web behaviour for now.
                // see: https://github.com/ppy/osu-web/issues/9270
                newSets = newSets.ExceptBy(foundContent.Select(c => c.OnlineID), set => set.OnlineID).ToArray();
                if (newSets.Length > 0)
                    loadCardBatch(foundContent, newSets, false);
                else
                    lastFetchDisplayedTime = Time.Current;
            }
        }

        private BeatmapListingResultStore resultStore;

        private void loadCardBatch(FillFlowContainer<BeatmapCardEntry> target, APIBeatmapSet[] sets, bool replace,
                                   (BeatmapListingResultStore.Handle Handle, BeatmapSetFavouriteState Favourite)[] existing = null)
        {
            var size = filterControl.CardSize.Value;
            if (!cardPools.TryGetValue(size, out var pool))
                cardPools[size] = pool = new BeatmapCardPool(size);
            if (replace && existing == null)
            {
                resultStore?.Dispose();
                resultStore = new BeatmapListingResultStore();
            }
            var batch = new CardBatch(sets, size, () => State.Value == Visibility.Visible, admitCardFirstUpdate, retainHiddenCard, pool, resultStore, existing);
            var source = cancellationToken = new CancellationTokenSource();
            var task = LoadComponentAsync(batch, loaded =>
            {
                loaded.Published = true;
                var cards = loaded.ToArray();
                // Keep pending cards owned by a loaded container. Cancellation
                // can then use the framework's async child disposal, not freeze
                // Update while disposing a whole unpublished page synchronously.
                pendingBatch = loaded;
                queueCards(target, cards, replace);
            }, source.Token);
            panelLoadTask = task;
            // The framework suppresses cancelled callbacks but does not dispose
            // their unpublished loadable. Release its entries/file lease after
            // background loading has actually stopped, never during injection.
            source.Token.Register(() => task.ContinueWith(_ =>
            {
                if (!batch.Published)
                    batch.Dispose();
            }, TaskScheduler.Default));
        }

        private partial class CardBatch : Container<BeatmapCardEntry>
        {
            internal volatile bool Published;
            private APIBeatmapSet[] sets;
            private readonly BeatmapListingResultStore store;
            private readonly (BeatmapListingResultStore.Handle Handle, BeatmapSetFavouriteState Favourite)[] existing;
            private readonly BeatmapCardSize size;
            private readonly Func<bool> ownerVisible;
            private readonly Func<bool> admitFirstUpdate;
            private readonly Func<BeatmapCardEntry, bool, bool> retainHiddenContent;
            private readonly BeatmapCardPool pool;

            public CardBatch(APIBeatmapSet[] sets, BeatmapCardSize size, Func<bool> ownerVisible, Func<bool> admitFirstUpdate,
                             Func<BeatmapCardEntry, bool, bool> retainHiddenContent, BeatmapCardPool pool,
                             BeatmapListingResultStore store, (BeatmapListingResultStore.Handle Handle, BeatmapSetFavouriteState Favourite)[] existing)
            {
                this.sets = sets;
                this.size = size;
                this.ownerVisible = ownerVisible;
                this.admitFirstUpdate = admitFirstUpdate;
                this.retainHiddenContent = retainHiddenContent;
                this.pool = pool;
                this.store = store;
                this.existing = existing;
                store.AddOwner();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Only lightweight layout/data entries are retained for the full
                // result set. Each visible entry loads its card on the bounded loader.
                var handles = existing ?? sets.Select(set => (store.Add(set), new BeatmapSetFavouriteState(set.HasFavourited, set.FavouriteCount))).ToArray();
                sets = Array.Empty<APIBeatmapSet>();
                ChildrenEnumerable = handles.Select(set => new BeatmapCardEntry(set.Item1, size, ownerVisible, admitFirstUpdate, retainHiddenContent, pool, set.Item2));
            }

            private int released;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (Interlocked.Exchange(ref released, 1) == 0)
                    store.Dispose();
            }
        }

        private static ReverseChildIDFillFlowContainer<BeatmapCardEntry> createCardContainerFor(IEnumerable<BeatmapCardEntry> newCards)
        {
            // spawn new children with the contained so we only clear old content at the last moment.
            // reverse ID flow is required for correct Z-ordering of the cards' expandable content (last card should be front-most).
            var content = new ReverseChildIDFillFlowContainer<BeatmapCardEntry>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(10),
                Alpha = 0,
                Margin = new MarginPadding
                {
                    Top = 15,
                    // the + 20 adjustment is roughly eyeballed in order to fit all of the expanded content height after it's scaled
                    // as well as provide visual balance to the top margin.
                    Bottom = ExpandedContentScrollContainer.HEIGHT + 20
                },
                ChildrenEnumerable = newCards
            };
            return content;
        }

        private void replaceResultsAreaContent(Drawable content)
        {
            if (content is not FillFlowContainer<BeatmapCardEntry>)
            {
                cancellationToken?.Cancel();
                clearPendingCards();
                resultStore?.Dispose();
                resultStore = null;
            }

            Loading.Hide();
            lastFetchDisplayedTime = Time.Current;

            // A placeholder must not leave the disposed previous result graph
            // rooted through this long-lived overlay.
            foundContent = content as FillFlowContainer<BeatmapCardEntry>;
            panelTarget.Child = content;

            // Do not animate newly published search results.
            content.Alpha = 1;
        }

        private void onCardSizeChanged()
        {
            // Publication attaches the container before its first lifetime update.
            // Parentage, not IsAlive, also covers size changes in that first frame.
            if (foundContent?.Parent != panelTarget || !foundContent.Any())
                return;

            Loading.Show();

            var sets = foundContent.Reverse().Select(card => (card.StoredSet, card.CurrentFavouriteState))
                .Concat(pendingCards.Select(card => (card.StoredSet, card.CurrentFavouriteState))).ToArray();

            cancellationToken?.Cancel();

            clearPendingCards();
            var content = createCardContainerFor(Enumerable.Empty<BeatmapCardEntry>());
            loadCardBatch(content, Array.Empty<APIBeatmapSet>(), true, sets);
        }

        private void queueCards(FillFlowContainer<BeatmapCardEntry> target, IEnumerable<BeatmapCardEntry> cards, bool replace)
        {
            pendingContent = target;
            replacePendingContent = replace;
            foreach (var card in cards)
                pendingCards.Enqueue(card);
        }

        private void clearPendingCards(bool disposing = false)
        {
            pendingCards.Clear();
            if (pendingBatch != null)
            {
                // Dispose may run on the framework's async disposal thread,
                // where mutating a Ready batch's children is forbidden.
                if (!disposing)
                    pendingBatch.Clear();
                pendingBatch.Dispose();
                pendingBatch = null;
            }
            if (replacePendingContent && pendingContent != null)
                pendingContent.Dispose();
            pendingContent = null;
            replacePendingContent = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationToken?.Cancel();
            clearPendingCards(disposing: true);
            base.Dispose(isDisposing);
            foundContent = null;
            resultStore?.Dispose();
            foreach (var pool in cardPools.Values)
                pool.Dispose();
            cardPools.Clear();
            lock (hiddenCardLock)
            {
                hiddenCards.Clear();
                hiddenCardOrder.Clear();
            }
        }

        public partial class NotFoundDrawable : CompositeDrawable
        {
            private OsuSpriteText notFoundText = null!;
            private IBindable<Colour4> themeColour = null!;

            public NotFoundDrawable()
            {
                RelativeSizeAxes = Axes.X;
                Height = 250;
                Alpha = 0;
                Margin = new MarginPadding { Top = 15 };
            }

            [BackgroundDependencyLoader]
            private void load(LargeTextureStore textures, OverlayColourProvider colourProvider)
            {
                AddInternal(new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Fit,
                            Texture = textures.Get(@"Online/not-found")
                        },
                        notFoundText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = BeatmapsStrings.ListingSearchNotFoundQuote,
                        }
                    }
                });

                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(c => notFoundText.Colour = c.NewValue, true);
            }
        }

        // TODO: localisation requires Text/LinkFlowContainer support for localising strings with links inside
        // (https://github.com/ppy/osu-framework/issues/4530)
        public partial class SupporterRequiredDrawable : CompositeDrawable
        {
            private LinkFlowContainer supporterRequiredText;
            private IBindable<Colour4> themeColour = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly List<LocalisableString> filtersUsed;

            public SupporterRequiredDrawable(List<LocalisableString> filtersUsed)
            {
                RelativeSizeAxes = Axes.X;
                Height = 225;
                Alpha = 0;

                this.filtersUsed = filtersUsed;
            }

            [BackgroundDependencyLoader]
            private void load(LargeTextureStore textures)
            {
                AddInternal(new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Fit,
                            Texture = textures.Get(@"Online/supporter-required"),
                        },
                        supporterRequiredText = new LinkFlowContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding { Bottom = 10 },
                        },
                    }
                });

                supporterRequiredText.AddText(
                    BeatmapsStrings.ListingSearchSupporterFilterQuoteDefault(string.Join(" and ", filtersUsed), "").ToString(),
                    t =>
                    {
                        t.Font = OsuFont.GetFont(size: 16);
                    }
                );

                supporterRequiredText.AddLink(BeatmapsStrings.ListingSearchSupporterFilterQuoteLinkText.ToString(), @"/store/products/supporter-tag");

                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => supporterRequiredText.Colour = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Colour4.White, true);
            }
        }

        private const double time_between_fetches = 500;

        private double lastFetchDisplayedTime;

        protected override void Update()
        {
            base.Update();
            cardInitializedThisFrame = false;

            if (State.Value == Visibility.Visible && pendingContent != null)
            {
                // Bound publication of layout entries; heavy card subtrees are
                // loaded independently only when their entries reach the viewport.
                for (int i = 0; i < 4 && pendingCards.TryDequeue(out var card); i++)
                {
                    pendingBatch.Remove(card, false);
                    pendingContent.Add(card);
                }

                if (replacePendingContent)
                {
                    foundContent = pendingContent;
                    replaceResultsAreaContent(foundContent);
                    replacePendingContent = false;
                }

                if (pendingCards.Count == 0)
                {
                    pendingContent = null;
                    pendingBatch.Dispose();
                    pendingBatch = null;
                    lastFetchDisplayedTime = Time.Current;
                    Loading.Hide();
                }
            }

            const int pagination_scroll_distance = 500;

            bool shouldShowMore = State.Value == Visibility.Visible
                                  && panelLoadTask?.IsCompleted != false
                                  && pendingContent == null
                                  && Time.Current - lastFetchDisplayedTime > time_between_fetches
                                  && (ScrollFlow.ScrollableExtent > 0 && ScrollFlow.IsScrolledToEnd(pagination_scroll_distance));

            if (shouldShowMore)
                filterControl.FetchNextPage();
        }
    }
}
