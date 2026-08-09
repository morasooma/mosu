// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Notifications.WebSocket;

namespace osu.Game.Online.API
{
    public interface IBeatmapApiProvider : IAPIProvider
    {
        OfficialOsuBeatmapApi? OfficialApi { get; }
    }

    /// <summary>
    /// Routes only official beatmap catalogue/download traffic to osu.ppy.sh.
    /// Every other request, including all score and realtime traffic, stays on the primary Mosu API.
    /// </summary>
    public sealed class BeatmapApiProvider : IBeatmapApiProvider
    {
        public const int SERVER_EXCLUSIVE_ID_THRESHOLD = 2_000_000_000;
        private static readonly TimeSpan observation_cooldown = TimeSpan.FromMinutes(10);

        private readonly IAPIProvider primaryApi;
        private readonly object observationLock = new object();
        private readonly Dictionary<int, DateTimeOffset> lastObservations = new Dictionary<int, DateTimeOffset>();
        public OfficialOsuBeatmapApi? OfficialApi { get; }

        public BeatmapApiProvider(IAPIProvider primaryApi, OfficialOsuBeatmapApi? officialApi)
        {
            this.primaryApi = primaryApi;
            OfficialApi = officialApi;
        }

        internal IAPIProvider SelectProvider(APIRequest request)
        {
            if (OfficialApi?.IsAvailable != true)
                return primaryApi;

            bool usePrimary = request switch
            {
                SearchBeatmapSetsRequest search => search.ServerExclusiveOnly,
                DownloadBeatmapSetRequest download => isServerExclusive(download.Model),
                GetBeatmapSetRequest set => set.ID >= SERVER_EXCLUSIVE_ID_THRESHOLD,
                GetBeatmapRequest beatmap => beatmap.ServerExclusive || beatmap.OnlineID >= SERVER_EXCLUSIVE_ID_THRESHOLD,
                GetBeatmapsRequest => false,
                _ => true,
            };

            return usePrimary ? primaryApi : OfficialApi;
        }

        private static bool isServerExclusive(IBeatmapSetInfo set) =>
            set.OnlineID >= SERVER_EXCLUSIVE_ID_THRESHOLD
            || set.IsServerExclusive()
            || set.Beatmaps.Any(b => b.OnlineID >= SERVER_EXCLUSIVE_ID_THRESHOLD || b.Metadata.IsServerExclusive());

        public void Queue(APIRequest request)
        {
            var provider = SelectProvider(request);

            if (provider == OfficialApi)
                attachObservation(request);

            provider.Queue(request);
        }

        public void Perform(APIRequest request)
        {
            var provider = SelectProvider(request);
            provider.Perform(request);

            if (provider == OfficialApi && request.CompletionState == APIRequestCompletionState.Completed)
                observeResponse(request);
        }

        public Task PerformAsync(APIRequest request)
        {
            var provider = SelectProvider(request);

            if (provider == OfficialApi)
                attachObservation(request);

            return provider.PerformAsync(request);
        }

        private void attachObservation(APIRequest request)
        {
            if (request is GetBeatmapRequest beatmapRequest)
                beatmapRequest.Success += _ => observeResponse(beatmapRequest);
        }

        private void observeResponse(APIRequest request)
        {
            if (request is not GetBeatmapRequest beatmapRequest ||
                beatmapRequest.Response is not { } response ||
                response.OnlineBeatmapSetID <= 0 ||
                response.OnlineBeatmapSetID >= SERVER_EXCLUSIVE_ID_THRESHOLD ||
                !officialMetadataChanged(beatmapRequest, response) ||
                !tryBeginObservation(response.OnlineBeatmapSetID))
                return;

            primaryApi.Queue(new ObserveOfficialBeatmapRequest(response));
        }

        private bool tryBeginObservation(int beatmapsetId)
        {
            var now = DateTimeOffset.UtcNow;

            lock (observationLock)
            {
                if (lastObservations.TryGetValue(beatmapsetId, out var last) && now - last < observation_cooldown)
                    return false;

                lastObservations[beatmapsetId] = now;

                if (lastObservations.Count > 4096)
                {
                    foreach (int expired in lastObservations.Where(entry => now - entry.Value >= observation_cooldown).Select(entry => entry.Key).ToArray())
                        lastObservations.Remove(expired);
                }
            }

            return true;
        }

        internal static bool officialMetadataChanged(GetBeatmapRequest request, APIBeatmap response)
        {
            if (!string.IsNullOrEmpty(request.MD5Hash) &&
                !string.IsNullOrEmpty(response.Checksum) &&
                !string.Equals(request.MD5Hash, response.Checksum, StringComparison.OrdinalIgnoreCase))
                return true;

            if (request.CurrentStatus is { } currentStatus &&
                currentStatus != BeatmapOnlineStatus.LocallyModified &&
                currentStatus != response.Status)
                return true;

            if (request.CurrentBeatmapsetStatus is { } currentSetStatus &&
                currentSetStatus != BeatmapOnlineStatus.LocallyModified &&
                response.BeatmapSet != null &&
                currentSetStatus != response.BeatmapSet.Status)
                return true;

            return request.CurrentLastOnlineUpdate is { } lastUpdate && response.LastUpdated > lastUpdate;
        }

        void IAPIProvider.Schedule(Action action) => ((IAPIProvider)primaryApi).Schedule(action);

        public IBindable<APIUser> LocalUser => primaryApi.LocalUser;
        public ILocalUserState LocalUserState => primaryApi.LocalUserState;
        public string ScoreProcessingNoticeUrl => primaryApi.ScoreProcessingNoticeUrl;
        public Language Language => primaryApi.Language;
        public string AccessToken => primaryApi.AccessToken;
        public Guid SessionIdentifier => primaryApi.SessionIdentifier;
        public bool IsLoggedIn => primaryApi.IsLoggedIn;
        public string ProvidedUsername => primaryApi.ProvidedUsername;
        public EndpointConfiguration Endpoints => primaryApi.Endpoints;
        public int APIVersion => primaryApi.APIVersion;
        public Exception? LastLoginError => primaryApi.LastLoginError;
        public IBindable<APIState> State => primaryApi.State;
        public IBindable<string?> UserFacingOutageMessage => primaryApi.UserFacingOutageMessage;
        public SessionVerificationMethod? SessionVerificationMethod => primaryApi.SessionVerificationMethod;
        public INotificationsClient NotificationsClient => primaryApi.NotificationsClient;

        public void Login(string username, string password) => primaryApi.Login(username, password);
        public void AuthenticateSecondFactor(string code) => primaryApi.AuthenticateSecondFactor(code);
        public void Logout() => primaryApi.Logout();
        public IHubClientConnector? GetHubConnector(string clientName, string endpoint) => primaryApi.GetHubConnector(clientName, endpoint);
        public IChatClient GetChatClient() => primaryApi.GetChatClient();
        public RegistrationRequest.RegistrationRequestErrors? CreateAccount(string email, string username, string password) => primaryApi.CreateAccount(email, username, password);
    }
}
