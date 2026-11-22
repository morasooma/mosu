using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Discovery
{
    internal sealed class DiscoveryHistory
    {
        public List<DiscoveryFeedbackEvent> PendingFeedback { get; set; } = new List<DiscoveryFeedbackEvent>();
        public DiscoverySeedSource SeedSource { get; set; } = DiscoverySeedSource.Mixed;
        public string ManualReferences { get; set; } = string.Empty;
        public List<int> Skipped { get; set; } = new List<int>();
        public List<int> Seen { get; set; } = new List<int>();
        public List<string> SeenSongs { get; set; } = new List<string>();
        public List<APIBeatmapSet> Recent { get; set; } = new List<APIBeatmapSet>();

        internal static string SongKey(APIBeatmapSet set) => SongKey(set.Artist, set.Title);

        internal static string SongKey(string artistName, string titleName)
        {
            static string normalise(string value) => new string(value.Normalize(NormalizationForm.FormKC)
                .Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            string artist = normalise(artistName ?? string.Empty);
            string title = normalise(titleName ?? string.Empty);
            return artist.Length == 0 || title.Length == 0 ? string.Empty : artist + ":" + title;
        }

        internal bool HasSeen(APIBeatmapSet set)
        {
            string song = SongKey(set);
            return Seen.Contains(set.OnlineID) || song.Length > 0 && SeenSongs.Contains(song);
        }

        internal void RememberShown(APIBeatmapSet set)
        {
            if (!Seen.Contains(set.OnlineID)) Seen.Add(set.OnlineID);
            string song = SongKey(set);
            if (song.Length > 0 && !SeenSongs.Contains(song)) SeenSongs.Add(song);
            Seen = Seen.TakeLast(10000).ToList();
            SeenSongs = SeenSongs.TakeLast(10000).ToList();
            Recent.RemoveAll(s => s.OnlineID == set.OnlineID);
            Recent.Add(new APIBeatmapSet { OnlineID = set.OnlineID, Artist = set.Artist, Title = set.Title, AuthorString = set.AuthorString });
            Recent = Recent.TakeLast(12).ToList();
        }
        public List<APIBeatmapSet> Saved { get; set; } = new List<APIBeatmapSet>();

        internal static string Filename(string endpoint, long userId, int mode) =>
            $"discovery/{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)))[..16]}-{userId}-{mode}.json";

        internal static DiscoveryHistory Load(Storage storage, string filename)
        {
            try
            {
                if (!storage.Exists(filename)) return new DiscoveryHistory();
                using var stream = storage.GetStream(filename);
                if (stream.Length > 2 * 1024 * 1024) return new DiscoveryHistory();
                using var reader = new StreamReader(stream);
                var result = JsonConvert.DeserializeObject<DiscoveryHistory>(reader.ReadToEnd()) ?? new DiscoveryHistory();
                result.PendingFeedback = result.PendingFeedback?.Where(e => e != null && e.EventId != Guid.Empty).TakeLast(256).ToList() ?? new List<DiscoveryFeedbackEvent>();
                if (!Enum.IsDefined(result.SeedSource)) result.SeedSource = DiscoverySeedSource.Mixed;
                if (result.ManualReferences == null || result.ManualReferences.Length > 2048) result.ManualReferences = string.Empty;
                result.Skipped = result.Skipped?.TakeLast(500).ToList() ?? new List<int>();
                result.Seen = result.Seen?.Where(id => id > 0).Distinct().TakeLast(10000).ToList() ?? new List<int>();
                result.SeenSongs = result.SeenSongs?.Where(s => !string.IsNullOrEmpty(s)).Distinct().TakeLast(10000).ToList() ?? new List<string>();
                result.Recent = result.Recent?.Where(s => s != null && s.OnlineID > 0).TakeLast(12).ToList() ?? new List<APIBeatmapSet>();
                result.Saved = result.Saved?.Where(s => s != null && s.OnlineID > 0 && s.Beatmaps != null).TakeLast(100).ToList() ?? new List<APIBeatmapSet>();
                foreach (var set in result.Saved)
                {
                    set.Beatmaps = set.Beatmaps.Where(m => m != null && m.OnlineID > 0).ToArray();
                    foreach (var map in set.Beatmaps) map.BeatmapSet = set;
                }
                return result;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                Logger.Log($"Discovery history could not be read: {e.Message}", LoggingTarget.Runtime);
                return new DiscoveryHistory();
            }
        }

        internal void Like(APIBeatmapSet set, int? selectedMapId = null)
        {
            Skipped.RemoveAll(id => id == set.OnlineID);
            if (Saved.Any(s => s.OnlineID == set.OnlineID)) return;
            if (selectedMapId.HasValue)
            {
                // Keep the selected difficulty first without changing the shared catalogue object.
                set = new APIBeatmapSet
                {
                    OnlineID = set.OnlineID, Artist = set.Artist, Title = set.Title, AuthorString = set.AuthorString, AuthorID = set.AuthorID,
                    Genre = set.Genre, Tags = set.Tags,
                    Beatmaps = set.Beatmaps.OrderByDescending(m => m.OnlineID == selectedMapId.Value).ToArray(),
                };
            }
            Saved.Add(set);
            Saved = Saved.TakeLast(100).ToList();
        }

        internal bool Save(Storage storage, string filename)
        {
            Skipped = Skipped.TakeLast(500).ToList();
            Saved = Saved.TakeLast(100).ToList();
            try
            {
                // Retain only metadata used by discovery, not users/tags/nominations for entire catalogue sets.
                var payload = new
                {
                    PendingFeedback = PendingFeedback.TakeLast(256),
                    SeedSource, ManualReferences,
                    Skipped,
                    Seen = Seen.TakeLast(10000),
                    SeenSongs = SeenSongs.TakeLast(10000),
                    Recent = Recent.TakeLast(12).Select(s => new { id = s.OnlineID, artist = s.Artist, title = s.Title, creator = s.AuthorString }),
                    Saved = Saved.Select(s => new
                    {
                        id = s.OnlineID, title = s.Title, artist = s.Artist, creator = s.AuthorString, user_id = s.AuthorID,
                        genre = s.Genre, tags = s.Tags,
                        beatmaps = s.Beatmaps.Select(m => new
                        {
                            id = m.OnlineID, beatmapset_id = s.OnlineID, checksum = m.Checksum, mode_int = m.RulesetID,
                            difficulty_rating = m.StarRating, version = m.DifficultyName, total_length = m.Length / 1000, bpm = m.BPM,
                        }),
                    }),
                };
                string json = JsonConvert.SerializeObject(payload);
                if (Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) return false;
                using var stream = storage.GetStream(filename, FileAccess.Write, FileMode.Create);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
                return true;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                Logger.Log($"Discovery history could not be saved: {e.Message}", LoggingTarget.Runtime);
                return false;
            }
        }
    }
}
