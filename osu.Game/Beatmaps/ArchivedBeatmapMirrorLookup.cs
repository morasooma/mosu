using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Online;
using osu.Game.Online.API;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Checks for an exact archived .osu revision after the server's metadata lookup fails.
    /// Archive content is never trusted for ranking status or server-exclusive maps.
    /// </summary>
    internal static class ArchivedBeatmapMirrorLookup
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        internal static OnlineBeatmapMetadata? TryLookup(BeatmapInfo beatmap)
        {
            int id = beatmap.OnlineID;
            string checksum = beatmap.MD5Hash;

            if (MosuServerEnvironment.IsThirdPartyServer || id <= 0 || id >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD
                || beatmap.BeatmapSet?.IsServerExclusive() == true || beatmap.Metadata.IsServerExclusive()
                || checksum.Length != 32 || !isHex(checksum))
                return null;

            try
            {
                // Same archived .osu endpoint used by the performance server. Hash the raw bytes:
                // decoding/re-encoding changes the checksum for UTF-16 beatmaps.
                using var response = client.GetAsync($"https://mirror.hinamizawa.ai/api/osu/{id}").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return null;

                byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024
                    || !Convert.ToHexString(MD5.HashData(bytes)).Equals(checksum, StringComparison.OrdinalIgnoreCase))
                    return null;

                Logger.Log($"Exact archived revision found for beatmap {id} (not an official server status).", LoggingTarget.Network);
                return new OnlineBeatmapMetadata
                {
                    BeatmapID = id,
                    BeatmapSetID = beatmap.BeatmapSet?.OnlineID ?? -1,
                    MD5Hash = checksum,
                    BeatmapStatus = BeatmapOnlineStatus.ArchivedMirror,
                    BeatmapSetStatus = BeatmapOnlineStatus.ArchivedMirror,
                };
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                Logger.Log($"Archived beatmap lookup failed for {id}: {e.Message}", LoggingTarget.Network);
                return null;
            }
        }

        private static bool isHex(string value)
        {
            foreach (char c in value)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }

            return true;
        }
    }
}
