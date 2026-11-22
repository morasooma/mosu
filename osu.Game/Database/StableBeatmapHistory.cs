using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;

namespace osu.Game.Database
{
    /// <summary>
    /// Client-owned play history for directly accessed stable maps. Never writes to osu!.db.
    /// A stored null overrides stable's timestamp when a map is explicitly marked unplayed.
    /// </summary>
    public sealed class StableBeatmapHistory
    {
        private const string filename = "stable-beatmap-history.json";
        private readonly Storage storage;
        private readonly object sync = new object();
        private readonly Dictionary<Guid, DateTimeOffset?> lastPlayed;

        public event Action<Guid>? Changed;

        public StableBeatmapHistory(Storage storage)
        {
            this.storage = storage;
            try
            {
                if (storage.Exists(filename))
                {
                    using var stream = storage.GetStream(filename);
                    using var reader = new StreamReader(stream);
                    lastPlayed = JsonConvert.DeserializeObject<Dictionary<Guid, DateTimeOffset?>>(reader.ReadToEnd()) ?? new();
                    return;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Logger.Error(e, "Stable beatmap history could not be read");
            }

            lastPlayed = new Dictionary<Guid, DateTimeOffset?>();
        }

        public void Apply(BeatmapInfo beatmap)
        {
            lock (sync)
            {
                if (lastPlayed.TryGetValue(beatmap.ID, out var date))
                    beatmap.LastPlayed = date;
            }
        }

        public void SetLastPlayed(Guid id, DateTimeOffset? date)
        {
            try
            {
                lock (sync)
                {
                    lastPlayed[id] = date;
                    string json = JsonConvert.SerializeObject(lastPlayed);
                    using var stream = storage.CreateFileSafely(filename);
                    using var writer = new StreamWriter(stream);
                    writer.Write(json);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Logger.Error(e, "Stable beatmap history could not be saved");
            }

            Changed?.Invoke(id);
        }
    }
}
