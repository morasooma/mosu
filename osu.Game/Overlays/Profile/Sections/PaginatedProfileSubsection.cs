// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Sections
{
    public abstract partial class PaginatedProfileSubsection<TModel> : ProfileSubsection
    {
        /// <summary>
        /// The number of items displayed per page.
        /// </summary>
        protected virtual int ItemsPerPage => 50;

        /// <summary>
        /// The number of items displayed initially.
        /// </summary>
        protected virtual int InitialItemsCount => 5;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        protected PaginationParameters? CurrentPage { get; private set; }

        protected ReverseChildIDFillFlowContainer<Drawable> ItemsContainer { get; private set; } = null!;

        private APIRequest<List<TModel>>? retrievalRequest;
        private CancellationTokenSource? loadCancellation;
        private ItemBatch? pendingBatch;
        private readonly Queue<Drawable> pendingItems = new Queue<Drawable>();
        private bool pendingHasMore;

        private ShowMoreButton moreButton = null!;
        private OsuSpriteText missing = null!;
        private readonly LocalisableString? missingText;
        private IBindable<Colour4> themeColour = null!;

        protected PaginatedProfileSubsection(Bindable<UserProfileData?> user, LocalisableString? headerText = null, LocalisableString? missingText = null,
                                            CounterVisibilityState counterVisibilityState = CounterVisibilityState.AlwaysVisible)
            : base(user, headerText, counterVisibilityState)
        {
            this.missingText = missingText;
        }

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                // reverse ID flow is required for correct Z-ordering of the items (last item should be front-most).
                // particularly important in PaginatedBeatmapContainer, as it uses beatmap cards, which have expandable overhanging content.
                ItemsContainer = new ReverseChildIDFillFlowContainer<Drawable>
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Spacing = new Vector2(0, 2),
                    // ensure the container and its contents are in front of the "more" button.
                    Depth = float.MinValue
                },
                moreButton = new ShowMoreButton
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Alpha = 0,
                    Margin = new MarginPadding { Top = 10 },
                    Action = showMore,
                },
                missing = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 15),
                    Text = missingText ?? string.Empty,
                    Alpha = 0,
                }
            }
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
            themeColour.BindValueChanged(colour => missing.Colour = colour.NewValue, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(onUserChanged, true);
        }

        private void onUserChanged(ValueChangedEvent<UserProfileData?> e)
        {
            loadCancellation?.Cancel();
            retrievalRequest?.Cancel();

            clearPendingItems();
            CurrentPage = null;
            ItemsContainer.Clear();

            if (e.NewValue?.User != null)
            {
                showMore();
                SetCount(GetCount(e.NewValue.User));
            }
        }

        private void showMore()
        {
            if (User.Value == null)
                return;

            loadCancellation = new CancellationTokenSource();

            CurrentPage = CurrentPage?.TakeNext(ItemsPerPage) ?? new PaginationParameters(InitialItemsCount);

            retrievalRequest = CreateRequest(User.Value, new PaginationParameters(CurrentPage.Value.Offset, CurrentPage.Value.Limit + 1));
            // Capture this request's token, not the mutable field: a later user/page
            // change must not give an obsolete response the new request's lifetime.
            var cancellation = loadCancellation;
            retrievalRequest.Success += items => UpdateItems(items, cancellation);

            api.Queue(retrievalRequest);
        }

        protected virtual void UpdateItems(List<TModel> items, CancellationTokenSource cancellationTokenSource) => Schedule(() =>
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            if (!items.Any() && CurrentPage?.Offset == 0)
            {
                moreButton.Hide();
                moreButton.IsLoading = false;

                if (missingText.HasValue)
                    missing.Show();

                return;
            }

            bool hasMore = items.Count > CurrentPage?.Limit;

            if (hasMore)
                items.RemoveAt(items.Count - 1);

            OnItemsReceived(items);

            // LoadComponentsAsync enumerates its input on the caller. Passing a LINQ
            // projection there constructed every score/card subtree on Update before
            // any background loading began. Construct them inside the batch's loader.
            LoadComponentAsync(new ItemBatch(items, CreateDrawableItemFactory(items.Count)), batch =>
            {
                clearPendingItems();
                pendingBatch = batch;
                pendingHasMore = hasMore;
                foreach (var drawable in batch)
                    pendingItems.Enqueue(drawable);
            }, cancellationTokenSource.Token);
        });

        protected override void Update()
        {
            base.Update();

            if (pendingBatch == null)
                return;

            // Dependency loading is complete, but each subtree still runs its first
            // LoadComplete/layout on admission. Bound that work as in the listing.
            for (int i = 0; i < 4 && pendingItems.TryDequeue(out var item); i++)
            {
                pendingBatch.Remove(item, false);
                ItemsContainer.Add(item);
            }

            if (pendingItems.Count == 0)
            {
                clearPendingItems();
                missing.Hide();
                moreButton.FadeTo(pendingHasMore ? 1 : 0);
                moreButton.IsLoading = false;
            }
        }

        private void clearPendingItems()
        {
            pendingItems.Clear();
            pendingBatch?.Dispose();
            pendingBatch = null;
        }

        private partial class ItemBatch : Container<Drawable>
        {
            private readonly List<TModel> models;
            private readonly Func<TModel, Drawable?> createItem;

            public ItemBatch(List<TModel> models, Func<TModel, Drawable?> createItem)
            {
                this.models = models;
                this.createItem = createItem;
            }

            [BackgroundDependencyLoader]
            private void load() => ChildrenEnumerable = models.Select(createItem).OfType<Drawable>();
        }

        protected virtual int GetCount(APIUser user) => 0;

        protected virtual void OnItemsReceived(List<TModel> items)
        {
        }

        protected abstract APIRequest<List<TModel>> CreateRequest(UserProfileData user, PaginationParameters pagination);

        /// <summary>
        /// Captures any page-local state before construction starts on a loader thread.
        /// </summary>
        protected virtual Func<TModel, Drawable?> CreateDrawableItemFactory(int itemCount) => CreateDrawableItem;

        protected abstract Drawable? CreateDrawableItem(TModel model);

        protected override void Dispose(bool isDisposing)
        {
            retrievalRequest?.Cancel();
            loadCancellation?.Cancel();
            clearPendingItems();
            base.Dispose(isDisposing);
        }
    }
}
