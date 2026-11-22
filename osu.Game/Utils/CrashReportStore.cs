using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace osu.Game.Utils
{
    /// <summary>Persists only fatal exceptions and preserves the logs of their session before the next startup.</summary>
    public sealed class CrashReportStore
    {
        public static CrashReportStore? Current { get; set; }

        private readonly string root;
        private readonly string pendingPath;
        private int captured;
        public CrashReport? Previous { get; }
        public long? DiagnosticUserId { get; set; }

        public CrashReportStore(string storagePath)
        {
            root = storagePath;
            pendingPath = Path.Combine(root, "crashes", "pending.json");
            try
            {
                if (File.Exists(pendingPath))
                    Previous = JsonConvert.DeserializeObject<CrashReport>(File.ReadAllText(pendingPath));
            }
            catch
            {
                // A damaged report must never prevent the client from starting.
            }
        }

        public void Capture(Exception exception, string version)
        {
            if (Interlocked.Exchange(ref captured, 1) != 0)
                return;

            try
            {
                string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
                string directory = Path.Combine(root, "crashes", id);
                Directory.CreateDirectory(directory);
                var report = new CrashReport
                {
                    Id = id,
                    Timestamp = DateTimeOffset.UtcNow,
                    Version = version,
                    DiagnosticUserId = DiagnosticUserId,
                    Exception = formatException(exception),
                };
                File.WriteAllText(Path.Combine(directory, "crash.txt"), report.ToText(), Encoding.UTF8);

                // Publish the exception first: copying logs is best effort during a fatal failure.
                string temporary = pendingPath + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(report), Encoding.UTF8);
                File.Move(temporary, pendingPath, true);

                Logger.Flush();
                string sessionPrefix = Logger.GetLogger().Filename.Split('.')[0] + ".";
                string logs = Path.Combine(root, "logs");
                if (Directory.Exists(logs))
                {
                    foreach (string file in Directory.GetFiles(logs).Where(f => Path.GetFileName(f).StartsWith(sessionPrefix, StringComparison.Ordinal)))
                    {
                        try
                        {
                            using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var destination = File.Create(Path.Combine(directory, Path.GetFileName(file)));
                            source.CopyTo(destination);
                        }
                        catch
                        {
                            // Keep the exception even if an individual log is locked or unavailable.
                        }
                    }
                }
            }
            catch
            {
                // Never throw from a crash handler (including disk full/access denied).
            }
        }

        private static string formatException(Exception exception)
        {
            var text = new StringBuilder(exception.ToString());
            appendDiagnostics(exception, text);
            return text.ToString();
        }

        private static void appendDiagnostics(Exception exception, StringBuilder text)
        {
            // Framework diagnostic allowlist: never export arbitrary Exception.Data (it may contain private data).
            if (exception.Data["Mosu.D3D11.DeviceRemovedReason"] is string diagnostic)
                text.AppendLine().AppendLine().Append(diagnostic);

            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    appendDiagnostics(inner, text);
            }
            else if (exception.InnerException != null)
                appendDiagnostics(exception.InnerException, text);
        }

        public string Export(CrashReport report)
        {
            string directory = GetReportDirectory(report);
            string exports = Path.Combine(root, "exports");
            Directory.CreateDirectory(exports);
            string path = Path.Combine(exports, $"mosu-crash-{report.Timestamp:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
            try
            {
                using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
                var entry = archive.CreateEntry("crash.txt");
                using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                    writer.Write(report.ToText());
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory).Where(f => Path.GetFileName(f) != "crash.txt"))
                        archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }
            }
            catch
            {
                if (File.Exists(path)) File.Delete(path);
                throw;
            }
            return path;
        }

        public void Acknowledge(CrashReport report)
        {
            try
            {
                // Do not acknowledge a newer crash that happened while this dialog was open.
                if (File.Exists(pendingPath) && JsonConvert.DeserializeObject<CrashReport>(File.ReadAllText(pendingPath))?.Id == report.Id)
                    File.Move(pendingPath, Path.Combine(GetReportDirectory(report), "report.json"), true);
            }
            catch
            {
                // Retaining the report is safer than deleting it on a failed acknowledgement.
            }
        }

        private string GetReportDirectory(CrashReport report)
        {
            if (string.IsNullOrEmpty(report.Id) || report.Id != Path.GetFileName(report.Id) || report.Id.Contains(".."))
                throw new InvalidDataException("Invalid crash report ID.");
            return Path.Combine(root, "crashes", report.Id);
        }
    }

    public sealed class CrashReport
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string Version { get; set; } = string.Empty;
        public long? DiagnosticUserId { get; set; }
        public string Exception { get; set; } = string.Empty;
        public string ToText() => $"Mosu crash — {Timestamp:O}\nClient: {Version}\n\n{Exception}";
    }
}
