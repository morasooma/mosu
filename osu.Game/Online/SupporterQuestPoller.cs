using System;
using System.Collections.Generic;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online
{
    /// <summary>
    /// Coalesces background quest refreshes and keeps them out of loading and gameplay.
    /// All members are used on the update thread, including API completion callbacks.
    /// </summary>
    internal sealed class SupporterQuestPoller : IDisposable
    {
        internal const double POLL_INTERVAL = 15000;

        private readonly IAPIProvider api;
        private readonly Action<List<APISupporterQuest>> onSuccess;
        private GetSupporterQuestsRequest? request;
        private double lastRequestTime = double.NegativeInfinity;
        private bool suspended;
        private bool disposed;

        public SupporterQuestPoller(IAPIProvider api, Action<List<APISupporterQuest>> onSuccess)
        {
            this.api = api;
            this.onSuccess = onSuccess;
        }

        public bool Suspended
        {
            get => suspended;
            set
            {
                suspended = value;
                if (value && request != null)
                {
                    cancelRequest();
                    // A cancelled refresh must be retried on return to the menu, even after a short play.
                    lastRequestTime = double.NegativeInfinity;
                }
            }
        }

        public void Poll(double currentTime)
        {
            if (disposed || suspended || api.State.Value != APIState.Online || request != null
                || currentTime - lastRequestTime < POLL_INTERVAL)
                return;

            var next = new GetSupporterQuestsRequest();
            request = next;
            lastRequestTime = currentTime;

            next.Success += quests =>
            {
                if (!ReferenceEquals(request, next))
                    return;

                request = null;
                if (!disposed && !suspended && api.State.Value == APIState.Online)
                    onSuccess(quests);
            };
            next.Failure += _ =>
            {
                // Cancellation callbacks may arrive after a replacement request has been queued.
                if (ReferenceEquals(request, next))
                    request = null;
            };
            api.Queue(next);
        }

        private void cancelRequest()
        {
            var previous = request;
            request = null;
            previous?.Cancel();
        }

        public void Dispose()
        {
            disposed = true;
            cancelRequest();
        }
    }
}
