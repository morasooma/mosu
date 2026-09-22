// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Utils;

namespace osu.Game.Online.API.Requests
{
    public class DownloadBeatmapSetRequest : ArchiveDownloadRequest<IBeatmapSetInfo>
    {
        private const int mirror_timeout = 15000;
        private const int hedge_delay = 2000;
        private const int cancellation_poll_interval = 100;
        private static readonly string hinamizawa_user_agent = $"Morasooma/{typeof(DownloadBeatmapSetRequest).Assembly.GetName().Version} (+https://github.com/GooGuTeam/osu)";

        private static readonly object preferred_mirror_lock = new object();
        private static string? preferredAutoMirror;

        private readonly bool noVideo;

        public DownloadMirror Mirror = DownloadMirror.Auto;

        public DownloadBeatmapSetRequest(IBeatmapSetInfo set, bool noVideo)
            : base(set)
        {
            this.noVideo = noVideo;
        }

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Timeout = mirror_timeout;
            return request;
        }

        protected override string FileExtension => ".osz";

        protected override string Target => $@"beatmapsets/{Model.OnlineID}/download{(noVideo ? "?noVideo=1" : "")}";

        public override void Perform()
        {
            if (Mirror == DownloadMirror.Server)
            {
                base.Perform();
                return;
            }

            if (API == null)
            {
                Fail(new NotSupportedException($"A {nameof(APIAccess)} is required to perform requests."));
                return;
            }

            var mirrors = getMirrors();

            if (Mirror == DownloadMirror.Auto)
                prioritisePreviouslySuccessfulMirror(mirrors);

            performMirroredDownload(mirrors);
        }

        private List<DownloadSource> getMirrors()
        {
            string sayobotUrl = $"https://dl.sayobot.cn/beatmaps/download/{(noVideo ? "novideo" : "full")}/{Model.OnlineID}";
            string nerinyanUrl = $"https://api.nerinyan.moe/d/{Model.OnlineID}?noVideo={(noVideo ? "true" : "false")}";
            string minoUrl = $"https://catboy.best/d/{Model.OnlineID}{(noVideo ? "n" : "")}";
            string hinamizawaUrl = $"https://mirror.hinamizawa.ai/api/v1/hinai/d/{Model.OnlineID}{(noVideo ? "?noVideo=1" : "")}";

            return Mirror switch
            {
                DownloadMirror.Sayobot => [new DownloadSource("Sayobot", sayobotUrl)],
                DownloadMirror.Nerinyan => [new DownloadSource("Nerinyan", nerinyanUrl)],
                DownloadMirror.Mino => [new DownloadSource("Mino", minoUrl)],
                DownloadMirror.BeatConnect => [new DownloadSource("BeatConnect", $"https://beatconnect.io/b/{Model.OnlineID}")],
                DownloadMirror.Chimu => [new DownloadSource("Chimu", $"https://api.chimu.moe/v1/download/{Model.OnlineID}")],
                DownloadMirror.OsuDirect => [new DownloadSource("OsuDirect", $"https://api.osu.direct/d/{Model.OnlineID}?noVideo={(noVideo ? "true" : "false")}")],
                DownloadMirror.Hinamizawa => [new DownloadSource("Hinamizawa", hinamizawaUrl, UserAgent: hinamizawa_user_agent)],
                _ =>
                [
                    new DownloadSource("Sayobot", sayobotUrl),
                    new DownloadSource("Nerinyan", nerinyanUrl),
                    new DownloadSource("Mino", minoUrl),
                    new DownloadSource("BeatConnect", $"https://beatconnect.io/b/{Model.OnlineID}"),
                    new DownloadSource("Chimu", $"https://api.chimu.moe/v1/download/{Model.OnlineID}"),
                    new DownloadSource("OsuDirect", $"https://api.osu.direct/d/{Model.OnlineID}?noVideo={(noVideo ? "true" : "false")}"),
                    new DownloadSource("Hinamizawa", hinamizawaUrl, UserAgent: hinamizawa_user_agent),
                    new DownloadSource("Server", Uri, true)
                ]
            };
        }

        private static void prioritisePreviouslySuccessfulMirror(List<DownloadSource> mirrors)
        {
            string? preferred;

            lock (preferred_mirror_lock)
                preferred = preferredAutoMirror;

            int preferredIndex = mirrors.FindIndex(m => m.Name == preferred);

            if (preferredIndex <= 0)
                return;

            DownloadSource preferredSource = mirrors[preferredIndex];
            mirrors.RemoveAt(preferredIndex);
            mirrors.Insert(0, preferredSource);
        }

        private void performMirroredDownload(IReadOnlyList<DownloadSource> mirrors)
        {
            var activeDownloads = new List<MirrorDownload>();
            Exception? lastException = null;
            int nextMirror = 0;
            DateTime nextHedgeAt = DateTime.MinValue;

            launchNextMirror();

            while (activeDownloads.Count > 0 || nextMirror < mirrors.Count)
            {
                if (CompletionState != APIRequestCompletionState.Waiting)
                {
                    abortAndWait(activeDownloads);
                    return;
                }

                bool mirrorFailed = false;

                foreach (MirrorDownload completed in activeDownloads.Where(d => d.Task.IsCompleted).ToArray())
                {
                    activeDownloads.Remove(completed);
                    MirrorDownloadResult result = completed.Task.GetAwaiter().GetResult();

                    if (result.Exception == null)
                    {
                        if (CompletionState != APIRequestCompletionState.Waiting)
                        {
                            tryDeleteTemporaryFile(completed.Filename);
                            abortAndWait(activeDownloads);
                            return;
                        }

                        completeFrom(completed, activeDownloads);
                        return;
                    }

                    mirrorFailed = true;
                    lastException = result.Exception;
                    Logger.Log($"Beatmap mirror {completed.Source.Name} failed: {result.Exception.Message}", LoggingTarget.Network);
                }

                if (nextMirror < mirrors.Count && (activeDownloads.Count == 0 || mirrorFailed || DateTime.UtcNow >= nextHedgeAt))
                {
                    launchNextMirror();
                    continue;
                }

                if (activeDownloads.Count == 0)
                    break;

                int waitTime = nextMirror < mirrors.Count
                    ? Math.Clamp((int)(nextHedgeAt - DateTime.UtcNow).TotalMilliseconds, 1, cancellation_poll_interval)
                    : cancellation_poll_interval;

                Task.WhenAny(activeDownloads.Select(d => d.Task).Append(Task.Delay(waitTime))).GetAwaiter().GetResult();
            }

            Fail(lastException ?? new InvalidOperationException("All beatmap mirrors failed."));

            void launchNextMirror()
            {
                DownloadSource source = mirrors[nextMirror++];
                MirrorDownload download = createDownload(source);
                activeDownloads.Add(download);
                WebRequest = download.Request;
                nextHedgeAt = DateTime.UtcNow.AddMilliseconds(hedge_delay);

                Logger.Log($"Downloading beatmap set {Model.OnlineID} from {source.Name} ({source.Url})", LoggingTarget.Network);
            }
        }

        private MirrorDownload createDownload(DownloadSource source)
        {
            string temporaryFile = Path.GetTempFileName();
            string filename = Path.ChangeExtension(temporaryFile, FileExtension);
            File.Move(temporaryFile, filename);

            FileWebRequest request = source.UserAgent == null
                ? new FileWebRequest(filename, source.Url)
                : new UserAgentFileWebRequest(filename, source.Url, source.UserAgent);

            request.AllowRetryOnTimeout = false;
            request.Timeout = mirror_timeout;

            if (source.RequiresAuthentication)
            {
                request.AddHeader(@"Accept-Language", API!.Language.ToCultureCode());
                request.AddHeader(@"x-api-version", API.APIVersion.ToString(CultureInfo.InvariantCulture));

                if (!string.IsNullOrEmpty(API.AccessToken))
                    request.AddHeader(@"Authorization", $@"Bearer {API.AccessToken}");
            }

            request.DownloadProgress += (current, total) =>
            {
                if (total > 0)
                {
                    float progress = Math.Clamp((float)current / total, 0, 1);
                    API!.Schedule(() =>
                    {
                        if (progress > Progress)
                            SetProgress(progress);
                    });
                }
            };

            return new MirrorDownload(source, request, filename, downloadAndValidate(request, filename));
        }

        private static async Task<MirrorDownloadResult> downloadAndValidate(FileWebRequest request, string filename)
        {
            try
            {
                await request.PerformAsync().ConfigureAwait(false);

                if (!ZipUtils.IsBeatmapArchive(filename))
                    throw new InvalidDataException("The mirror returned an invalid beatmap archive.");

                return new MirrorDownloadResult(null);
            }
            catch (Exception e)
            {
                if (tryDeleteTemporaryFile(filename) is Exception cleanupException)
                    e = new AggregateException(e, cleanupException);

                return new MirrorDownloadResult(e);
            }
        }

        private void completeFrom(MirrorDownload winner, List<MirrorDownload> remainingDownloads)
        {
            abortAndWait(remainingDownloads);

            if (CompletionState != APIRequestCompletionState.Waiting)
            {
                tryDeleteTemporaryFile(winner.Filename);
                return;
            }

            if (Mirror == DownloadMirror.Auto)
            {
                lock (preferred_mirror_lock)
                    preferredAutoMirror = winner.Source.Name;
            }

            WebRequest = winner.Request;
            API!.Schedule(() => SetProgress(1));
            TriggerSuccess(winner.Filename);

            Logger.Log($"Beatmap set {Model.OnlineID} downloaded from {winner.Source.Name}; cancelled {remainingDownloads.Count} slower mirror(s).", LoggingTarget.Network);
        }

        private static void abortAndWait(IEnumerable<MirrorDownload> downloads)
        {
            MirrorDownload[] remaining = downloads.ToArray();

            foreach (MirrorDownload download in remaining)
                download.Request.Abort();

            Task.WhenAll(remaining.Select(d => d.Task)).GetAwaiter().GetResult();

            foreach (MirrorDownload download in remaining)
            {
                if (tryDeleteTemporaryFile(download.Filename) is Exception cleanupException)
                    Logger.Log($"Could not delete cancelled mirror download {download.Filename}: {cleanupException.Message}", LoggingTarget.Network);
            }
        }

        private static Exception? tryDeleteTemporaryFile(string filename)
        {
            try
            {
                File.Delete(filename);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        private sealed record DownloadSource(string Name, string Url, bool RequiresAuthentication = false, string? UserAgent = null);

        private sealed class UserAgentFileWebRequest : FileWebRequest
        {
            private readonly string userAgent;

            protected override string UserAgent => userAgent;

            public UserAgentFileWebRequest(string filename, string url, string userAgent)
                : base(filename, url)
            {
                this.userAgent = userAgent;
            }
        }

        private sealed record MirrorDownload(DownloadSource Source, FileWebRequest Request, string Filename, Task<MirrorDownloadResult> Task);

        private sealed record MirrorDownloadResult(Exception? Exception);
    }
}
