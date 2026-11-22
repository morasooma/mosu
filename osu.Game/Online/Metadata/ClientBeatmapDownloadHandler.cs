using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.Metadata
{
    internal sealed class ClientBeatmapDownloadHandler
    {
        private readonly IAPIProvider api;
        private readonly Action<Action> schedule;
        private readonly Func<APIBeatmapSet, bool> download;
        private readonly TimeSpan timeout;
        private readonly string apiUrl;
        private readonly string metadataUrl;

        public ClientBeatmapDownloadHandler(IAPIProvider api, Action<Action> schedule, Func<APIBeatmapSet, bool> download,
                                           TimeSpan? timeout = null)
        {
            this.api = api;
            this.schedule = schedule;
            this.download = download;
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            apiUrl = api.Endpoints.APIUrl;
            metadataUrl = api.Endpoints.MetadataUrl;
        }

        public async Task<bool> TryDownloadAsync(int beatmapsetId, Func<bool> isTrustedConnection)
        {
            if (beatmapsetId <= 0)
                return false;

            int userId = 0;
            Guid sessionId = default;

            bool permitted() => isTrustedConnection() && api.IsLoggedIn &&
                                api.Endpoints.APIUrl == apiUrl && api.Endpoints.MetadataUrl == metadataUrl &&
                                ClientBeatmapDownloadPolicy.IsTrusted(api.Endpoints,
                                    MosuServerEnvironment.IsThirdPartyServer, MosuServerEnvironment.UsesStableProtocol);
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sync = new object();
            var request = new GetBeatmapSetRequest(beatmapsetId);
            using var cancellation = new CancellationTokenSource(timeout);
            using var registration = cancellation.Token.Register(() =>
            {
                lock (sync)
                    result.TrySetResult(false);
                request.Cancel();
            });
            request.Failure += _ => result.TrySetResult(false);
            request.Success += set => schedule(() =>
            {
                // A late lookup must not start a download after the website has fallen back,
                // nor deliver a previous account's command after switching accounts.
                lock (sync)
                {
                    if (result.Task.IsCompleted)
                        return;
                    if (!permitted() || api.LocalUser.Value.Id != userId || api.SessionIdentifier != sessionId ||
                        set.OnlineID != beatmapsetId)
                    {
                        result.TrySetResult(false);
                        return;
                    }

                    try
                    {
                        result.TrySetResult(download(set));
                    }
                    catch (Exception exception)
                    {
                        osu.Framework.Logging.Logger.Error(exception, "Could not accept website beatmap download");
                        result.TrySetResult(false);
                    }
                }
            });
            schedule(() =>
            {
                lock (sync)
                {
                    if (result.Task.IsCompleted)
                        return;
                    if (!permitted() || api.LocalUser.Value.Id <= 0)
                    {
                        result.TrySetResult(false);
                        return;
                    }

                    userId = api.LocalUser.Value.Id;
                    sessionId = api.SessionIdentifier;
                    api.Queue(request);
                }
            });
            return await result.Task.ConfigureAwait(false);
        }
    }
}
