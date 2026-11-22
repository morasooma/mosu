// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace osu.Game.Online.API.Requests
{
    public class DownloadBeatmapSetRequest : ArchiveDownloadRequest<IBeatmapSetInfo>
    {
        private const int mirror_timeout = 15000;
        private const int parallel_downloads = 4;
        private const int hedge_delay = 2000;
        private const int maximum_parallel_downloads = 6;
        private const int metadata_budget = 1500;
        private static readonly BeatmapMirrorHistory history = new BeatmapMirrorHistory();
        private static readonly string user_agent = $"Morasooma/{typeof(DownloadBeatmapSetRequest).Assembly.GetName().Version} (+https://github.com/GooGuTeam/osu)";
        private readonly bool noVideo;
        private readonly int setId;
        private readonly bool serverExclusive;
        private readonly BeatmapMirrorMetadata? catalogue;

        public DownloadMirror Mirror = DownloadMirror.Auto;

        public DownloadBeatmapSetRequest(IBeatmapSetInfo set, bool noVideo)
            : base(set)
        {
            this.noVideo = noVideo;
            // Model may be backed by Realm. Snapshot catalogue state on the caller's thread,
            // never enumerate its managed difficulties from the download worker.
            setId = set.OnlineID;
            serverExclusive = setId >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD || set.IsServerExclusive()
                              || set.Beatmaps.Any(b => b.OnlineID >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD || b.Metadata.IsServerExclusive());
            catalogue = BeatmapMirrorMetadata.FromModel(set);
        }

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Timeout = mirror_timeout;
            return request;
        }

        protected override string FileExtension => ".osz";
        protected override string Target => $@"beatmapsets/{setId}/download{(noVideo ? "?noVideo=1" : "")}";

        public override void Perform()
        {
            // Never leak server-only IDs/archives to public mirrors, even for direct request callers.
            if (Mirror == DownloadMirror.Server || serverExclusive)
            {
                base.Perform();
                return;
            }

            if (API == null)
            {
                Fail(new NotSupportedException($"A {nameof(APIAccess)} is required to perform requests."));
                return;
            }

            try
            {
                performMirroredDownload().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // Virtual seams exercise the actual scheduler, networking, cancellation and validation in local tests.
        internal virtual IReadOnlyList<BeatmapMirrorSource> GetMirrors()
        {
            int id = setId;
            var sources = new Dictionary<DownloadMirror, BeatmapMirrorSource>
            {
                [DownloadMirror.Mino] = new BeatmapMirrorSource("Mino", $"https://catboy.best/d/{id}{(noVideo ? "n" : "")}"),
                [DownloadMirror.Mirria] = new BeatmapMirrorSource("Mirria", $"https://mirror.lisek.cc/d/{id}?video={(noVideo ? "false" : "true")}", $"https://mirror.lisek.cc/api/v1/beatmapsets/{id}"),
                [DownloadMirror.OsuDirect] = new BeatmapMirrorSource("OsuDirect", $"https://osu.direct/api/d/{id}{(noVideo ? "?noVideo=1" : "")}", $"https://osu.direct/api/v2/s/{id}"),
                [DownloadMirror.Nerinyan] = new BeatmapMirrorSource("Nerinyan", $"https://api.nerinyan.moe/d/{id}?noVideo={(noVideo ? "true" : "false")}"),
                [DownloadMirror.Sayobot] = new BeatmapMirrorSource("Sayobot", $"https://dl.sayobot.cn/beatmaps/download/{(noVideo ? "novideo" : "full")}/{id}"),
                [DownloadMirror.Nekoha] = new BeatmapMirrorSource("Nekoha", $"https://mirror.nekoha.moe/api/download/{id}?noVideo={(noVideo ? "1" : "0")}", $"https://mirror.nekoha.moe/api/beatmapset/{id}"),
                [DownloadMirror.Hinamizawa] = new BeatmapMirrorSource("Hinamizawa", $"https://mirror.hinamizawa.ai/api/v1/hinai/d/{id}{(noVideo ? "?noVideo=1" : "")}", $"https://mirror.hinamizawa.ai/v3/osu/beatmaps/s/{id}"),
                [DownloadMirror.BeatConnect] = new BeatmapMirrorSource("BeatConnect", $"https://beatconnect.io/b/{id}"),
                // Retained for existing explicit settings, but the defunct service is not in Auto.
                [DownloadMirror.Chimu] = new BeatmapMirrorSource("Chimu", $"https://api.chimu.moe/v1/download/{id}"),
            };

            if (Mirror != DownloadMirror.Auto && sources.TryGetValue(Mirror, out var selected))
                return [selected];

            return
            [
                sources[DownloadMirror.Mino], sources[DownloadMirror.Mirria], sources[DownloadMirror.OsuDirect], sources[DownloadMirror.Nerinyan],
                sources[DownloadMirror.Sayobot], sources[DownloadMirror.Nekoha], sources[DownloadMirror.Hinamizawa], sources[DownloadMirror.BeatConnect],
                new BeatmapMirrorSource("Server", Uri, RequiresAuthentication: true),
            ];
        }

        internal virtual FileWebRequest CreateMirrorRequest(string filename, BeatmapMirrorSource source) =>
            new MirrorFileWebRequest(filename, source.Url);

        internal virtual Task<BeatmapMirrorMetadata?> FetchMirrorMetadataAsync(BeatmapMirrorSource source, CancellationToken cancellationToken) =>
            BeatmapMirrorMetadata.FetchAsync(source, setId, cancellationToken);

        private async Task performMirroredDownload()
        {
            var sources = GetMirrors().ToArray();
            var remaining = sources.ToList();
            var active = new List<MirrorDownload>();
            var allDownloads = new List<MirrorDownload>();
            var observations = new List<BeatmapMirrorMetadata>();
            var cachedMetadata = BeatmapCatalogueMetadataCache.Get(setId);
            if (cachedMetadata != null)
                observations.Add(cachedMetadata);
            if (catalogue != null && cachedMetadata == null)
                observations.Add(catalogue);
            BeatmapMirrorMetadata? reference = BeatmapMirrorMetadata.SelectReference(observations);
            using var cancellation = new CancellationTokenSource();
            using var metadataCancellation = new CancellationTokenSource(metadata_budget);
            var metadataTasks = remaining.Where(s => s.MetadataUrl != null && cachedMetadata == null).Select(s => fetchMetadataCore(s, metadataCancellation.Token)).ToArray();
            var raceClock = Stopwatch.StartNew();
            string? winnerFile = null;
            Exception? lastException = null;
            Exception? revisionException = null;
            bool reconciledMetadata = false;
            BeatmapMirrorMetadata[] refreshedMetadata = [];
            var archiveObservations = new List<BeatmapMirrorMetadata>();
            int maxParallel = Mirror == DownloadMirror.Auto ? parallel_downloads : 1;
            long nextHedge = hedge_delay;

            try
            {
                // A bounded catalogue round sorts the initial wave by revision, not just host response time.
                // No credentials are sent and timeout/absence does not prevent an archive download.
                await Task.WhenAll(metadataTasks).ConfigureAwait(false);
                if (CompletionState != APIRequestCompletionState.Waiting)
                    return;
                foreach (var task in metadataTasks)
                {
                    if (await task.ConfigureAwait(false) is { } metadata)
                        observations.Add(metadata);
                }
                reference = BeatmapMirrorMetadata.SelectReference(observations);
                Logger.Log($"Beatmap mirror reference set={setId} source={reference?.Source ?? "unknown"} updated={reference?.Updated:O} "
                           + $"hashes={string.Join(',', reference?.Checksums ?? [])}", LoggingTarget.Network);
                remaining = history.Order(remaining, observations, reference, DateTimeOffset.UtcNow).ToList();
                Logger.Log($"Beatmap mirror race set={setId} parallel={maxParallel} order={string.Join(',', remaining.Select(s => s.Name))}", LoggingTarget.Network);
                // Start four bodies together, not four HEAD probes or a 2-second staircase.
                nextHedge = raceClock.ElapsedMilliseconds + hedge_delay;
                while (active.Count < maxParallel && remaining.Count > 0)
                    launchNext();

                while (active.Count > 0 || remaining.Count > 0)
                {
                    if (CompletionState != APIRequestCompletionState.Waiting)
                        return;

                    foreach (var completed in active.Where(d => d.Task.IsCompleted).OrderBy(d => d.Task.GetResultSafely().Seconds).ToArray())
                    {
                        active.Remove(completed);
                        var result = await completed.Task.ConfigureAwait(false);
                        if (result.Success)
                        {
                            archiveObservations.Add(new BeatmapMirrorMetadata(completed.Source.Name, DateTimeOffset.MinValue, result.Checksums));
                            bool matches = reference == null || reference.Matches(result.Checksums);
                            if (!matches && !reconciledMetadata)
                            {
                                reconciledMetadata = true;
                                BeatmapCatalogueMetadataCache.Invalidate(setId);
                                using var refreshBudget = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                                var refreshed = await Task.WhenAll(sources.Where(s => s.MetadataUrl != null)
                                    .Select(s => fetchMetadataCore(s, refreshBudget.Token))).ConfigureAwait(false);
                                if (CompletionState != APIRequestCompletionState.Waiting)
                                    return;
                                refreshedMetadata = refreshed.OfType<BeatmapMirrorMetadata>().ToArray();
                            }
                            if (!matches && BeatmapMirrorMetadata.Reconcile(reference, refreshedMetadata, archiveObservations) is { } corrected)
                            {
                                reference = corrected;
                                BeatmapCatalogueMetadataCache.Store(setId, corrected);
                                Logger.Log($"Beatmap mirror reference reconciled set={setId} source={corrected.Source} updated={corrected.Updated:O} hashes={string.Join(',', corrected.Checksums)}", LoggingTarget.Network);
                                // Recheck bytes already on disk; do not redownload an archive.
                                matches = reference.Matches(result.Checksums);
                            }
                            Logger.Log($"Beatmap mirror compare set={setId} source={completed.Source.Name} "
                                       + $"revision={(reference == null ? "unverified" : matches ? "match" : "mismatch")} hashes={string.Join(',', result.Checksums)}", LoggingTarget.Network);
                            if (matches)
                            {
                                winnerFile = completed.Filename;
                                cancellation.Cancel();
                                foreach (var loser in allDownloads.Where(d => d != completed))
                                    loser.Cancel();
                                // Cancellation can race with APIRequest.Cancel(), which aborts WebRequest.
                                // No longer expose a disposed loser while finishing the race.
                                WebRequest = null;
                                await Task.WhenAll(allDownloads.Select(d => d.Task)).ConfigureAwait(false);
                                if (CompletionState != APIRequestCompletionState.Waiting)
                                {
                                    winnerFile = null;
                                    return;
                                }
                                history.Record(completed.Source.Name, result.Bytes, result.Seconds, result.FirstByteSeconds);
                                WebRequest = completed.Request;
                                API!.Schedule(() => SetProgress(1));
                                TriggerSuccess(winnerFile);
                                Logger.Log($"Beatmap set {setId} downloaded from {completed.Source.Name}; "
                                           + $"race_ms={raceClock.ElapsedMilliseconds} cancelled={allDownloads.Count(d => d != completed && d.Task.GetResultSafely().Cancelled)} "
                                           + $"verified={(reference != null)}", LoggingTarget.Network);
                                return;
                            }
                            BeatmapCatalogueMetadataCache.Invalidate(setId);
                            history.BackOff(completed.Source.Name, DateTimeOffset.UtcNow.AddMinutes(2));
                            revisionException = new InvalidDataException("Downloaded beatmap archives do not match the catalogue revision, even after metadata revalidation. See network.log for expected and downloaded hashes.");
                        }
                        else if (!result.Cancelled)
                        {
                            lastException = result.Exception;
                            // A 404 is per-set absence, not evidence that the whole mirror is unhealthy.
                            if (completed.Request.ResponseStatusCode != HttpStatusCode.NotFound)
                                history.BackOff(completed.Source.Name, result.RetryAfter ?? DateTimeOffset.UtcNow.AddSeconds(30));
                        }
                    }

                    while (active.Count < maxParallel && remaining.Count > 0)
                        launchNext();

                    // Give reserves a chance without killing a valid slow-starting origin/CDN request.
                    // Initial wave = four, at most six concurrent bodies; never fan out without a bound.
                    if (Mirror == DownloadMirror.Auto && remaining.Count > 0 && raceClock.ElapsedMilliseconds >= nextHedge
                        && active.Count < maximum_parallel_downloads)
                    {
                        launchNext();
                        nextHedge = raceClock.ElapsedMilliseconds + hedge_delay;
                    }

                    await Task.Delay(50).ConfigureAwait(false);
                }

                WebRequest = null; // Do not let APIRequest.Fail read a disposed/binary loser's response.
                Fail(revisionException ?? lastException ?? new InvalidOperationException("All beatmap mirrors failed."));
            }
            finally
            {
                cancellation.Cancel();
                metadataCancellation.Cancel();
                foreach (var download in allDownloads)
                    download.Cancel();
                await Task.WhenAll(allDownloads.Select(d => d.Task)).ConfigureAwait(false);
                await Task.WhenAll(metadataTasks).ConfigureAwait(false);
                if (CompletionState != APIRequestCompletionState.Completed)
                    winnerFile = null;
                foreach (var download in allDownloads)
                {
                    download.Request.Dispose();
                    if (download.Filename != winnerFile)
                    {
                        try
                        {
                            File.Delete(download.Filename);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Could not delete mirror download {download.Filename}: {e.Message}", LoggingTarget.Network);
                        }
                    }
                }
            }

            void launchNext()
            {
                var source = remaining[0];
                remaining.RemoveAt(0);
                string filename = Path.Combine(Path.GetTempPath(), $"mosu-beatmap-{Guid.NewGuid():N}.osz");
                var request = CreateMirrorRequest(filename, source);
                request.AllowRetryOnTimeout = false;
                request.Timeout = mirror_timeout;
                if (source.RequiresAuthentication)
                {
                    request.AddHeader("Accept-Language", API!.Language.ToCultureCode());
                    request.AddHeader("x-api-version", API.APIVersion.ToString(CultureInfo.InvariantCulture));
                    if (!string.IsNullOrEmpty(API.AccessToken))
                        request.AddHeader("Authorization", $"Bearer {API.AccessToken}");
                }
                var download = new MirrorDownload(source, request, filename);
                request.DownloadProgress += (bytes, total) =>
                {
                    Interlocked.Exchange(ref download.Bytes, bytes);
                    if (bytes > 0)
                        Interlocked.CompareExchange(ref download.FirstByteTicks, download.Clock.ElapsedTicks, 0);
                    if (bytes > BeatmapMirrorArchive.MaximumArchiveBytes || total > BeatmapMirrorArchive.MaximumArchiveBytes)
                    {
                        download.TooLarge = true;
                        request.Abort();
                    }
                    if (total > 0)
                    {
                        float progress = Math.Clamp((float)bytes / total, 0, 0.99f);
                        API!.Schedule(() =>
                        {
                            if (CompletionState == APIRequestCompletionState.Waiting && progress > Progress)
                                SetProgress(progress);
                        });
                    }
                };
                Logger.Log($"Downloading beatmap set {setId} from {source.Name} ({source.Url})", LoggingTarget.Network);
                download.Task = downloadAndValidate(download, cancellation.Token);
                active.Add(download);
                allDownloads.Add(download);
                WebRequest = request;
            }
        }

        private async Task<BeatmapMirrorMetadata?> fetchMetadataCore(BeatmapMirrorSource source, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await FetchMirrorMetadataAsync(source, cancellationToken).ConfigureAwait(false);
                Logger.Log($"Beatmap mirror metadata set={setId} source={source.Name} updated={result?.Updated:O} hashes={string.Join(',', result?.Checksums ?? [])}", LoggingTarget.Network);
                return result;
            }
            catch (Exception e)
            {
                Logger.Log($"Beatmap mirror metadata failed set={setId} source={source.Name}: {e.Message}", LoggingTarget.Network);
                return null;
            }
        }

        private async Task<MirrorDownloadResult> downloadAndValidate(MirrorDownload download, CancellationToken cancellationToken)
        {
            string[] hashes = [];
            Exception? error = null;
            bool cancelled = false;
            double transferSeconds = 0;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                await download.Request.PerformAsync(deadline.Token).ConfigureAwait(false);
                transferSeconds = download.Clock.Elapsed.TotalSeconds;
                cancellationToken.ThrowIfCancellationRequested();
                if (download.Cancelled)
                    throw new OperationCanceledException();
                // FileWebRequest can return normally after an abort and delete the partial file.
                // A per-mirror deadline is a transfer failure, not cancellation of the entire race.
                if (deadline.IsCancellationRequested)
                    throw new TimeoutException("Beatmap mirror exceeded the download deadline.");
                if (download.Request.Aborted)
                    throw new IOException("Beatmap mirror transfer was aborted before the archive was ready.");

                hashes = await Task.Run(() => BeatmapMirrorArchive.Inspect(download.Filename, cancellationToken, setId), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                cancelled = !download.TooLarge && (cancellationToken.IsCancellationRequested || download.Cancelled);
                error = download.TooLarge ? new InvalidDataException("Beatmap archive exceeds the download size limit.") : e;
            }

            double totalSeconds = download.Clock.Elapsed.TotalSeconds;
            double seconds = transferSeconds > 0 ? transferSeconds : totalSeconds;
            double firstByte = download.FirstByteTicks / (double)Stopwatch.Frequency;
            var retry = download.Request.ResponseHeaders?.RetryAfter;
            DateTimeOffset? retryAfter = retry?.Date ?? (retry?.Delta is { } delta ? DateTimeOffset.UtcNow + delta : null);
            Logger.Log($"Beatmap mirror transfer set={setId} source={download.Source.Name} status={(int?)download.Request.ResponseStatusCode} "
                       + $"outcome={(cancelled ? "cancelled" : error == null ? "complete" : "failed")} bytes={download.Bytes} "
                       + $"first_byte_ms={(download.FirstByteTicks == 0 ? -1 : firstByte * 1000):F0} transfer_ms={seconds * 1000:F0} validation_ms={(totalSeconds - seconds) * 1000:F0} "
                       + $"bytes_per_second={download.Bytes / Math.Max(0.001, seconds - firstByte):F0} retry_after={retryAfter:O} error={error?.Message}", LoggingTarget.Network);
            return new MirrorDownloadResult(error == null, cancelled, hashes, download.Bytes, seconds, firstByte, retryAfter, error);
        }

        internal sealed class MirrorFileWebRequest(string filename, string url) : FileWebRequest(filename, url)
        {
            protected override string UserAgent => user_agent;

            // FileWebRequest closes its write-only response before WebRequest tries to log
            // an HTTP error body. Supply the error explicitly instead of masking 404/429.
            protected override Task Complete(Exception? e = null) =>
                base.Complete(e ?? (ResponseStatusCode is { } status && (int)status >= 400 ? new IOException($"HTTP {(int)status} ({status})") : null));
        }

        private sealed class MirrorDownload(BeatmapMirrorSource source, FileWebRequest request, string filename)
        {
            internal readonly BeatmapMirrorSource Source = source;
            internal readonly FileWebRequest Request = request;
            internal readonly string Filename = filename;
            internal readonly Stopwatch Clock = Stopwatch.StartNew();
            internal long Bytes;
            internal long FirstByteTicks;
            internal bool TooLarge;
            internal volatile bool Cancelled;
            internal Task<MirrorDownloadResult> Task = null!;

            internal void Cancel()
            {
                if (Task.IsCompleted)
                    return;
                Cancelled = true;
                Request.Abort();
            }
        }

        private sealed record MirrorDownloadResult(bool Success, bool Cancelled, string[] Checksums, long Bytes, double Seconds,
                                                   double FirstByteSeconds, DateTimeOffset? RetryAfter, Exception? Exception);
    }
}
