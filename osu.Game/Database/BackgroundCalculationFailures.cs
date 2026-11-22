using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Database
{
    /// <summary>
    /// Persistent failures of library calculations, never cached zeroes or gameplay restrictions.
    /// The same map content is skipped until the map changes, including on explicit library requests.
    /// </summary>
    internal sealed class BackgroundCalculationFailures
    {
        private const string filename = "background-pp-timeouts.json";
        private readonly object sync = new object();
        private readonly Storage storage;
        private Dictionary<string, DateTimeOffset> failures = new Dictionary<string, DateTimeOffset>();

        public BackgroundCalculationFailures(Storage storage)
        {
            this.storage = storage;
            try
            {
                string path = storage.GetFullPath(filename);
                if (File.Exists(path))
                    failures = JsonConvert.DeserializeObject<Dictionary<string, DateTimeOffset>>(File.ReadAllText(path)) ?? failures;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Logger.Error(e, "Could not read background calculation failures");
            }
        }

        private static string key(Guid id, string checksum) => $"{id:N}:{checksum}";

        public bool ShouldSkip(Guid id, string checksum)
        {
            lock (sync)
                return failures.ContainsKey(key(id, checksum));
        }

        public void Record(Guid id, string checksum, Exception error, bool cancelled)
        {
            // A stopped queue, shutdown or disposed dependency is not a broken map.
            // An uncancelled OperationCanceledException is the calculator's own timeout.
            if (cancelled || error is ObjectDisposedException)
                return;

            lock (sync)
            {
                failures[key(id, checksum)] = DateTimeOffset.UtcNow;
                save();
            }
        }

        private void save()
        {
            string path = storage.GetFullPath(filename, true);
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(failures));
                File.Move(temporary, path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Logger.Error(e, "Could not save background calculation failures");
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Logger.Error(e, "Could not remove background calculation failures temporary file");
                }
            }
        }
    }
}
