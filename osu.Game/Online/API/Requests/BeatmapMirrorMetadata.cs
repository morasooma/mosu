using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    internal sealed record BeatmapMirrorSource(string Name, string Url, string? MetadataUrl = null, bool RequiresAuthentication = false);

    /// <summary>
    /// A catalogue observation, not proof that the mirror's actual archive is current.
    /// Only the MD5s of the downloaded raw .osu bytes can establish that.
    /// </summary>
    internal sealed record BeatmapMirrorMetadata(string Source, DateTimeOffset Updated, IReadOnlyList<string> Checksums, bool Complete = true)
    {
        internal static BeatmapMirrorMetadata? FromModel(IBeatmapSetInfo set)
        {
            // A locally installed file is not a freshness oracle (notably when updating a map).
            var difficulties = set.Beatmaps.Where(b => b is not APIBeatmap { Convert: true }).ToArray();
            var hashes = difficulties.Select(b => b is BeatmapInfo local ? local.OnlineMD5Hash : b.MD5Hash)
                                     .Where(IsChecksum).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (hashes.Length == 0)
                return null;

            DateTimeOffset updated = set is APIBeatmapSet online
                ? online.LastUpdated ?? DateTimeOffset.MinValue
                : DateTimeOffset.MinValue; // Local LastOnlineUpdate is per-difficulty, not the set's last_updated.
            return new BeatmapMirrorMetadata("catalogue", updated, hashes, set is APIBeatmapSet && difficulties.Length == hashes.Length);
        }

        internal static BeatmapMirrorMetadata? Parse(string source, int setId, JObject? json)
        {
            if (json == null || (int?)json["id"] != setId || json["beatmaps"] is not JArray beatmaps)
                return null;

            var difficulties = beatmaps.OfType<JObject>().Where(b => (bool?)b["convert"] != true
                && string.IsNullOrEmpty((string?)b["deleted_at"])
                && string.IsNullOrEmpty((string?)b["deletedAt"])).ToArray();
            var hashes = difficulties.Select(b => (string?)b["checksum"]).Where(IsChecksum).Select(h => h!)
                                 .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (hashes.Length == 0)
                return null;

            DateTimeOffset.TryParse((string?)json["last_updated"], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var updated);
            return new BeatmapMirrorMetadata(source, updated, hashes, difficulties.Length == hashes.Length);
        }

        internal static async Task<BeatmapMirrorMetadata?> FetchAsync(BeatmapMirrorSource source, int setId, CancellationToken cancellationToken)
        {
            if (source.MetadataUrl == null)
                return null;

            using var request = new JsonWebRequest<JObject>(source.MetadataUrl)
            {
                Timeout = 3000,
                AllowRetryOnTimeout = false,
            };
            request.DownloadProgress += (bytes, _) =>
            {
                if (bytes > 2 * 1024 * 1024)
                    request.Abort();
            };
            await request.PerformAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Parse(source.Name, setId, request.ResponseObject);
        }

        internal bool Matches(IReadOnlyCollection<string> archiveHashes) =>
            (!Complete || Checksums.Count == archiveHashes.Count) && Checksums.All(expected => archiveHashes.Contains(expected, StringComparer.OrdinalIgnoreCase));

        internal static BeatmapMirrorMetadata? SelectReference(IEnumerable<BeatmapMirrorMetadata> observations)
        {
            var candidates = observations.ToArray();
            // Real per-difficulty timestamps must not be mixed with a set-level last_updated;
            // only compare timestamps in the same domain, then prefer agreement at equal dates.
            return candidates.OrderByDescending(m => m.Updated)
                             .ThenByDescending(m => candidates.Where(other => SameRevision(m, other)).Select(other => other.Source).Distinct(StringComparer.Ordinal).Count())
                             .ThenByDescending(m => m.Source == "catalogue")
                             .ThenBy(m => m.Source, StringComparer.Ordinal).FirstOrDefault();
        }

        internal static BeatmapMirrorMetadata? Reconcile(BeatmapMirrorMetadata? previous, IEnumerable<BeatmapMirrorMetadata> observations,
                                                         IEnumerable<BeatmapMirrorMetadata>? archiveObservations = null)
        {
            var fresh = observations.ToArray();
            var archives = archiveObservations?.ToArray() ?? [];
            var candidate = fresh.Where(m => m.Complete)
                                 .OrderByDescending(m => m.Updated)
                                 .ThenByDescending(m => fresh.Concat(archives).Where(other => SameRevision(m, other))
                                                            .Select(other => other.Source).Distinct(StringComparer.Ordinal).Count())
                                 .FirstOrDefault();
            if (candidate == null || previous != null && candidate.Updated < previous.Updated || fresh.Any(m => m.Updated > candidate.Updated))
                return null;
            int metadataAgreement = fresh.Where(m => m.Complete && SameRevision(candidate, m)).Select(m => m.Source).Distinct(StringComparer.Ordinal).Count();
            int archiveAgreement = archives.Where(m => SameRevision(candidate, m)).Select(m => m.Source).Distinct(StringComparer.Ordinal).Count();
            // Either two refreshed catalogues, or one complete fresh catalogue corroborated
            // by two fully validated archives. Archives alone must never invent a revision
            // or downgrade newer metadata when mirror catalogue endpoints time out.
            return metadataAgreement >= 2 || metadataAgreement >= 1 && archiveAgreement >= 2 ? candidate : null;
        }

        internal static bool SameRevision(BeatmapMirrorMetadata a, BeatmapMirrorMetadata b) =>
            a.Checksums.Count == b.Checksums.Count && a.Checksums.All(h => b.Checksums.Contains(h, StringComparer.OrdinalIgnoreCase));

        private static bool IsChecksum(string? value) => value?.Length == 32 && value.All(Uri.IsHexDigit);
    }
}
