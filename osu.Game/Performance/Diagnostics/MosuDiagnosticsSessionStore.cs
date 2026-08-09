// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using osu.Framework.Platform;
using osu.Game.IO;

namespace osu.Game.Performance.Diagnostics
{
    public static class MosuDiagnosticsSessionStore
    {
        private const string relative_path = "performance/diagnostics/session.json";

        private static readonly JsonSerializerSettings serializer_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
        };

        public static string GetSessionPath(Storage storage) => storage.GetFullPath(relative_path);

        public static MosuDiagnosticsSession? TryLoad(Storage storage)
        {
            string path = GetSessionPath(storage);

            if (!File.Exists(path))
                return null;

            try
            {
                var session = JsonConvert.DeserializeObject<MosuDiagnosticsSession>(File.ReadAllText(path), serializer_settings);

                if (session?.SchemaVersion != MosuDiagnosticsDefaults.SchemaVersion)
                {
                    Delete(storage);
                    return null;
                }

                return session;
            }
            catch
            {
                Delete(storage);
                return null;
            }
        }

        public static void Save(Storage storage, MosuDiagnosticsSession session)
        {
            string path = GetSessionPath(storage);
            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonConvert.SerializeObject(session, serializer_settings));
        }

        public static void Delete(Storage storage)
        {
            string path = GetSessionPath(storage);

            if (File.Exists(path))
                File.Delete(path);
        }

        public static bool HasActiveSession(Storage storage)
        {
            var session = TryLoad(storage);

            return session != null
                   && session.Phase is MosuDiagnosticsPhase.PendingRestart
                       or MosuDiagnosticsPhase.RunningBenchmark
                       or MosuDiagnosticsPhase.PendingResultsRestart
                       or MosuDiagnosticsPhase.Paused;
        }

        public static bool IsResumable(MosuDiagnosticsSession session)
            => session.SchemaVersion == MosuDiagnosticsDefaults.SchemaVersion
               && session.Phase is (MosuDiagnosticsPhase.PendingRestart
                   or MosuDiagnosticsPhase.RunningBenchmark
                   or MosuDiagnosticsPhase.PendingResultsRestart
                   or MosuDiagnosticsPhase.ShowResults);
    }
}
