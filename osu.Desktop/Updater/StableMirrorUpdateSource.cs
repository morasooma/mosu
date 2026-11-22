using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace osu.Desktop.Updater
{
    // Only package bytes can come from an edge. Velopack still resolves the version,
    // delta chain and expected hash from the canonical release feed.
    internal sealed class StableMirrorUpdateSource : SimpleWebSource
    {
        private const string primary_feed = "https://morasooma.net/client/updates";
        private const int sample_bytes = 1024 * 1024;
        private const int minimum_package_size = 16 * sample_bytes;
        private const int probe_timeout_ms = 2000;
        private static readonly string[] mirror_origins = { "https://proxy2.morasooma.net", "https://proxy.morasooma.net" };
        private static readonly Regex stable_package = new Regex(@"\Amosu-[0-9]+\.[0-9]+\.[0-9]+-stable(?:-linux)?-(?:full|delta)\.nupkg\z", RegexOptions.CultureInvariant);
        private static readonly HttpClient transport = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        });
        private readonly HttpClient http;

        internal StableMirrorUpdateSource(HttpClient http, IFileDownloader? downloader = null)
            : base(primary_feed, downloader)
        {
            this.http = http;
        }

        public static SimpleWebSource Create(string feed) => string.Equals(feed.TrimEnd('/'), primary_feed, StringComparison.Ordinal)
            ? new StableMirrorUpdateSource(transport)
            : new SimpleWebSource(feed);

        public override async Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken)
        {
            cancelToken.ThrowIfCancellationRequested();
            string name = releaseEntry.FileName;
            string? hash = releaseEntry.SHA256;
            // Probing costs more than it saves on a small delta. Do not guess an edge
            // path for feeds/packages that lack a canonical SHA-256 or a stable name.
            if (releaseEntry.Size >= minimum_package_size && stable_package.IsMatch(name)
                && hash is { Length: 64 } && hash.All(Uri.IsHexDigit))
            {
                var primary = new Uri($"{primary_feed}/{name}");
                Uri[] mirrors = mirror_origins.Select(origin => new Uri($"{origin}/client-update-packages/{hash.ToLowerInvariant()}/{name}")).ToArray();
                var probes = await Task.WhenAll(new[] { primary }.Concat(mirrors).Select(uri => probe(uri, releaseEntry.Size, cancelToken))).ConfigureAwait(false);
                cancelToken.ThrowIfCancellationRequested();

                // A deadline is a lower bound, not a measured transfer. Only choose an
                // edge if it beat even that bound. If the main package request failed
                // outright, the canonical feed hash still identifies a valid mirror.
                var winner = mirrors.Select((uri, i) => (Uri: uri, Probe: probes[i + 1]))
                                    .Where(x => x.Probe is { Completed: true, Milliseconds: double edge }
                                        && (probes[0].Milliseconds is not double main || (edge < main * 0.85 && main - edge >= 40)))
                                    .OrderBy(x => x.Probe.Milliseconds)
                                    .FirstOrDefault().Uri;
                if (winner != null)
                {
                    try
                    {
                        logger.Info($"Downloading '{name}' from mirror '{winner.Host}'.");
                        await downloadVerified(winner, releaseEntry.Size, hash, localFile, progress, cancelToken).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception e) when (!cancelToken.IsCancellationRequested && e is (HttpRequestException or IOException or OperationCanceledException))
                    {
                        logger.Warn($"Mirror '{winner.Host}' failed; retrying '{name}' from the main feed.");
                        progress(0);
                    }
                }
            }

            cancelToken.ThrowIfCancellationRequested();
            await base.DownloadReleaseEntry(logger, releaseEntry, localFile, progress, cancelToken).ConfigureAwait(false);
        }

        private async Task<(bool Completed, double? Milliseconds)> probe(Uri uri, long size, CancellationToken cancellationToken)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(probe_timeout_ms);
            long start = Stopwatch.GetTimestamp();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(0, sample_bytes - 1);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                var range = response.Content.Headers.ContentRange;
                if (response.StatusCode != HttpStatusCode.PartialContent || response.Content.Headers.ContentLength != sample_bytes
                    || response.Content.Headers.ContentEncoding.Count != 0
                    || range is not { Unit: "bytes", From: 0, To: sample_bytes - 1 } || range.Length != size)
                    return default;

                var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                await using var streamLifetime = stream.ConfigureAwait(false);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    int remaining = sample_bytes;
                    while (remaining > 0)
                    {
                        int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), deadline.Token).ConfigureAwait(false);
                        if (read == 0)
                            return default;
                        remaining -= read;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                deadline.Token.ThrowIfCancellationRequested();
                return (true, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return (false, probe_timeout_ms);
            }
            catch (Exception e) when (e is HttpRequestException or IOException)
            {
                return default;
            }
        }

        private async Task downloadVerified(Uri uri, long size, string sha256, string localFile, Action<int> progress, CancellationToken cancellationToken)
        {
            // Bound periods of no data, not the duration of a large download.
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var idleTimeout = TimeSpan.FromSeconds(8);
            idle.CancelAfter(idleTimeout);
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, idle.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength != size || response.Content.Headers.ContentEncoding.Count != 0)
                throw new IOException("Unexpected update package response from mirror.");

            var input = await response.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
            await using var inputLifetime = input.ConfigureAwait(false);
            var output = new FileStream(localFile, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            await using var outputLifetime = output.ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                long total = 0;
                int lastProgress = 0;
                while (true)
                {
                    idle.CancelAfter(idleTimeout);
                    int read = await input.ReadAsync(buffer.AsMemory(), idle.Token).ConfigureAwait(false);
                    idle.CancelAfter(System.Threading.Timeout.InfiniteTimeSpan);
                    if (read == 0)
                        break;
                    total += read;
                    if (total > size)
                        throw new IOException("Oversized update package from mirror.");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    int percent = (int)(total * 100 / size);
                    if (percent < 100 && percent - lastProgress >= 3)
                        progress(lastProgress = percent);
                }

                if (total != size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Update package from mirror failed SHA-256 verification.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            progress(100);
        }
    }
}
