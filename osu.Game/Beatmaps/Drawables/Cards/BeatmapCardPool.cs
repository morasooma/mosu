using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    /// <summary>
    /// Listing-local bounded reuse. Only detached, fully initialised, inactive
    /// cards can enter; pending first-load work remains owned by its wrapper.
    /// </summary>
    internal sealed class BeatmapCardPool : IDisposable
    {
        private const int capacity = 64;
        private readonly Stack<BeatmapCard> cards = new Stack<BeatmapCard>();
        private readonly object sync = new object();
        private readonly BeatmapCardSize size;
        private bool disposed;

        internal BeatmapCardPool(BeatmapCardSize size) => this.size = size;

        internal BeatmapCard Get(APIBeatmapSet set, Bindable<BeatmapSetFavouriteState> favourites)
        {
            BeatmapCard? card;
            lock (sync)
                cards.TryPop(out card);
            if (card != null)
            {
                card.ChangeBeatmapSet(set, favourites);
                return card;
            }
            card = BeatmapCard.Create(set, size, keepCoversLoaded: true);
            card.BindFavouriteState(favourites);
            return card;
        }

        internal bool Return(BeatmapCard card)
        {
            if (!card.IsLoaded || card.IsPreviewPlaying || card.HasPendingFavouriteRequest)
                return false;
            lock (sync)
            {
                if (disposed || cards.Count >= capacity)
                    return false;
            }
            // Drop the old catalogue graph even while the reusable shell is idle.
            card.ReleaseForPool();
            lock (sync)
            {
                if (disposed) return false;
                cards.Push(card);
                return true;
            }
        }

        public void Dispose()
        {
            BeatmapCard[] remaining;
            lock (sync)
            {
                disposed = true;
                remaining = cards.ToArray();
                cards.Clear();
            }
            foreach (var card in remaining)
                card.Dispose();
        }
    }
}
