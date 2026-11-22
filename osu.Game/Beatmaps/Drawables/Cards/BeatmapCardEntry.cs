using System;
using System.Threading.Tasks;
using System.Threading;
using osu.Game.Overlays.BeatmapListing;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Statistics;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    /// <summary>
    /// Keeps catalogue data and layout without retaining the complete card tree
    /// for every page the user has visited. The framework owns asynchronous
    /// construction, cancellation and disposal of the visible card.
    /// </summary>
    public partial class BeatmapCardEntry : DelayedLoadUnloadWrapper
    {
        private readonly APIBeatmapSet? directSet;
        private readonly BeatmapListingResultStore.Handle? storedSet;
        private Task? restoreTask;
        public APIBeatmapSet BeatmapSet => (Content as BeatmapCard)?.BeatmapSet ?? directSet ?? storedSet!.Get();
        internal int OnlineID => directSet?.OnlineID ?? storedSet!.OnlineID;
        internal BeatmapListingResultStore.Handle? StoredSet => storedSet;
        private readonly Bindable<BeatmapSetFavouriteState> favouriteState;
        internal BeatmapSetFavouriteState CurrentFavouriteState => favouriteState.Value;
        public BeatmapCardSize CardSize { get; }

        private readonly Func<bool>? ownerVisible;
        private readonly Func<bool>? admitFirstUpdate;
        private readonly Func<BeatmapCardEntry, bool, bool>? retainHiddenContent;
        private volatile bool releaseHiddenContent;
        private readonly BeatmapCardPool? pool;

        internal void ReleaseHiddenContent() => releaseHiddenContent = true;

        public BeatmapCardEntry(APIBeatmapSet set, BeatmapCardSize size, Func<bool>? ownerVisible = null, Func<bool>? admitFirstUpdate = null,
                                Func<BeatmapCardEntry, bool, bool>? retainHiddenContent = null)
            : this(set, null, size, ownerVisible, admitFirstUpdate, retainHiddenContent, null, new Bindable<BeatmapSetFavouriteState>(new BeatmapSetFavouriteState(set.HasFavourited, set.FavouriteCount)))
        {
        }

        internal BeatmapCardEntry(BeatmapListingResultStore.Handle set, BeatmapCardSize size, Func<bool> ownerVisible, Func<bool> admitFirstUpdate,
                                  Func<BeatmapCardEntry, bool, bool> retainHiddenContent, BeatmapCardPool pool, BeatmapSetFavouriteState? favourites = null)
            : this(null, set, size, ownerVisible, admitFirstUpdate, retainHiddenContent, pool,
                new Bindable<BeatmapSetFavouriteState>(favourites ?? set.Favourites)) { }

        private BeatmapCardEntry(APIBeatmapSet? set, BeatmapListingResultStore.Handle? stored, BeatmapCardSize size, Func<bool>? ownerVisible, Func<bool>? admitFirstUpdate,
                                 Func<BeatmapCardEntry, bool, bool>? retainHiddenContent, BeatmapCardPool? pool, Bindable<BeatmapSetFavouriteState> favouriteState)
            : base(() =>
            {
                var model = set ?? stored!.Get();
                using var operation = StutterTraceEventSource.Log.Measure("BeatmapListing.CardCreate", model.OnlineID);
                // This wrapper already delays loading and bounds the card's
                // lifetime. Do not add another 500ms delay to its cover loaders.
                if (pool != null)
                    return pool.Get(model, favouriteState);
                var card = BeatmapCard.Create(model, size, keepCoversLoaded: true);
                card.BindFavouriteState(favouriteState);
                return card;
            }, 40, 2000)
        {
            this.favouriteState = favouriteState;
            directSet = set;
            storedSet = stored;
            storedSet?.AddOwner();
            CardSize = size;
            this.ownerVisible = ownerVisible;
            this.admitFirstUpdate = admitFirstUpdate;
            this.retainHiddenContent = retainHiddenContent;
            this.pool = pool;
            Anchor = Origin = Anchor.TopCentre;
            // Fixed geometry must be available before the child exists, otherwise
            // FillFlowContainer cannot scroll to cards which have not loaded yet.
            Size = size switch
            {
                BeatmapCardSize.Normal => new Vector2(BeatmapCard.WIDTH, BeatmapCardNormal.HEIGHT),
                BeatmapCardSize.Extra => new Vector2(BeatmapCard.WIDTH, BeatmapCardExtra.HEIGHT),
                BeatmapCardSize.Nano => new Vector2(BeatmapCardNano.WIDTH, BeatmapCardNano.HEIGHT),
                _ => throw new ArgumentOutOfRangeException(nameof(size))
            };
        }

        public override bool UpdateSubTree()
        {
            if (Content == null || Content.IsLoaded)
                return base.UpdateSubTree();

            // Background loading is parallel, but LoadComplete/layout still run
            // synchronously on Update. Admit one ready card at a time per listing.
            if (Content.LoadState == LoadState.Ready)
            {
                if (ownerVisible?.Invoke() == false || !IsIntersecting || admitFirstUpdate?.Invoke() == false)
                    return false;
            }

            using var operation = StutterTraceEventSource.Log.Measure("BeatmapListing.CardFirstUpdate", OnlineID);
            return base.UpdateSubTree();
        }

        protected override bool ShouldLoadContent
        {
            get
            {
                if (ownerVisible?.Invoke() == false || !base.ShouldLoadContent)
                    return false;
                if (storedSet == null) return true;
                restoreTask ??= storedSet.GetAsync();
                if (restoreTask.IsFaulted)
                    restoreTask.GetAwaiter().GetResult();
                return restoreTask.IsCompletedSuccessfully;
            }
        }

        protected override bool IsContentVisible
        {
            get
            {
                bool visible = (Content as BeatmapCard)?.HasPendingFavouriteRequest == true
                               || (ownerVisible?.Invoke() != false
                                   && (base.IsContentVisible || Content is BeatmapCard { IsHovered: true } || (Content as BeatmapCard)?.IsPreviewPlaying == true));
                releaseHiddenContent = !visible && (releaseHiddenContent || retainHiddenContent?.Invoke(this, false) == false);
                if (visible)
                    retainHiddenContent?.Invoke(this, true);
                return visible;
            }
        }

        protected override bool ShouldUnloadContent => base.ShouldUnloadContent || releaseHiddenContent;

        protected override bool RetainUnloadedContent(Drawable content) => pool?.Return((BeatmapCard)content) == true;

        private int releasedStore;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (Interlocked.Exchange(ref releasedStore, 1) == 0)
                storedSet?.ReleaseOwner();
        }

        protected override void CancelTasks()
        {
            retainHiddenContent?.Invoke(this, true);
            releaseHiddenContent = false;
            restoreTask = null;
            base.CancelTasks();
        }
    }
}
