using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Optional native ORT runtime. No native AI binaries are shipped in the client.</summary>
    internal static class BackgroundAiRuntime
    {
        internal const string VERSION = "1.23.2";
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private static IntPtr nativeLibrary;

        internal sealed record Release(string Archive, long Bytes, string Sha256, string LibraryEntry, long LibraryBytes, string LibrarySha256);
        internal static Release CurrentRelease
        {
            get
            {
                if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                    throw new PlatformNotSupportedException("Background AI runtime currently requires x64.");
                if (OperatingSystem.IsLinux())
                    return new Release("onnxruntime-linux-x64-1.23.2.tgz", 8309231,
                        "1fa4dcaef22f6f7d5cd81b28c2800414350c10116f5fdd46a2160082551c5f9b",
                        "onnxruntime-linux-x64-1.23.2/lib/libonnxruntime.so.1.23.2", 22326072,
                        "13ab8084954fa4a47c777880180b90810d6020f021441395712b48a75b74c68b");
                if (OperatingSystem.IsWindows())
                    return new Release("onnxruntime-win-x64-1.23.2.zip", 78127794,
                        "0b38df9af21834e41e73d602d90db5cb06dbd1ca618948b8f1d66d607ac9f3cd",
                        "onnxruntime-win-x64-1.23.2/lib/onnxruntime.dll", 14186016,
                        "dec964ab1ee36cc9b0ae247d13b376627992fc57dec0454354017ab8fd84f1ea");
                throw new PlatformNotSupportedException("Background AI runtime currently supports Linux and Windows.");
            }
        }

        // Called only when a cache miss actually needs inference, never at startup or while disabled.
        public static async Task Ensure(string directory, CancellationToken token, bool preferDirectML = false)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (nativeLibrary != IntPtr.Zero)
                    return;
                var release = CurrentRelease;
                string installed;
                if (preferDirectML && OperatingSystem.IsWindows())
                {
                    try { installed = await Overlays.Discovery.DiscoveryAudioRuntime.InstallDirectML(directory, token).ConfigureAwait(false); }
                    catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException)
                    {
                        osu.Framework.Logging.Logger.Log($"Audio GPU runtime unavailable ({e.GetType().Name}); loading CPU runtime.");
                        installed = await Install(directory, release, null, null, token).ConfigureAwait(false);
                    }
                }
                else installed = await Install(directory, release, null, null, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (preferDirectML && OperatingSystem.IsWindows() && File.Exists(Path.Combine(Path.GetDirectoryName(installed)!, "DirectML.dll")))
                {
                    // Load dependencies by verified absolute path, never by the user's PATH/current directory.
                    // Retain process-wide, just like ORT: live GPU sessions may still reference them.
                    NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(installed)!, "DirectML.dll"));
                    NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(installed)!, "onnxruntime_providers_shared.dll"));
                }
                IntPtr handle = NativeLibrary.Load(installed);
                try
                {
                    NativeLibrary.SetDllImportResolver(typeof(InferenceSession).Assembly,
                        (name, _, _) => name == "onnxruntime" ? handle : IntPtr.Zero);
                    // ORT has process-global delegates; unloading while any managed ORT object exists is unsafe.
                    nativeLibrary = handle;
                }
                catch
                {
                    NativeLibrary.Free(handle);
                    throw;
                }
            }
            finally { gate.Release(); }
        }

        internal static void RequireLoaded()
        {
            if (nativeLibrary == IntPtr.Zero)
                throw new InvalidOperationException("Download the optional AI runtime before creating an inference session.");
        }

        internal static async Task<string> Install(string directory, Release release, HttpMessageHandler? handler, Uri? source, CancellationToken token)
        {
            string destination = Path.Combine(directory, VERSION, Path.GetFileName(release.LibraryEntry));
            var library = new StillLifePackage.Asset(Path.GetFileName(destination), release.LibraryBytes, release.LibrarySha256);
            if (await StillLifePackage.Verify(destination, library, token).ConfigureAwait(false))
                return destination;
            Directory.CreateDirectory(directory);
            string staging = Path.Combine(directory, ".runtime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                string archive = Path.Combine(staging, release.Archive);
                using var client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
                client.Timeout = TimeSpan.FromMinutes(10);
                var uri = source ?? new Uri($"https://github.com/microsoft/onnxruntime/releases/download/v{VERSION}/{release.Archive}");
                if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                    throw new InvalidDataException("AI runtime requires HTTPS without credentials.");
                using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                    using var output = File.Create(archive);
                    await StillLifePackage.CopyBounded(input, output, release.Bytes, token).ConfigureAwait(false);
                }
                if (!await StillLifePackage.Verify(archive, new StillLifePackage.Asset(release.Archive, release.Bytes, release.Sha256), token).ConfigureAwait(false))
                    throw new InvalidDataException("AI runtime archive checksum mismatch.");
                string extracted = Path.Combine(staging, "installed");
                Directory.CreateDirectory(extracted);
                await Extract(archive, extracted, release, token).ConfigureAwait(false);
                string ready = Path.Combine(extracted, library.Path);
                if (!await StillLifePackage.Verify(ready, library, token).ConfigureAwait(false))
                    throw new InvalidDataException("AI runtime library checksum mismatch.");
                token.ThrowIfCancellationRequested();
                string installed = Path.GetDirectoryName(destination)!;
                Directory.CreateDirectory(installed);
                // Only replace verified files; never extract arbitrary archive paths or ship headers/tools/PDBs.
                foreach (string file in Directory.GetFiles(extracted))
                    File.Move(file, Path.Combine(installed, Path.GetFileName(file)), true);
                return destination;
            }
            finally { Directory.Delete(staging, true); }
        }

        private static async Task Extract(string archive, string destination, Release release, CancellationToken token)
        {
            string prefix = release.LibraryEntry[..release.LibraryEntry.IndexOf('/', StringComparison.Ordinal)];
            if (release.Archive.EndsWith(".zip", StringComparison.Ordinal))
            {
                using var zip = ZipFile.OpenRead(archive);
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName != release.LibraryEntry && entry.FullName != prefix + "/LICENSE" && entry.FullName != prefix + "/ThirdPartyNotices.txt")
                        continue;
                    using var input = entry.Open();
                    await Save(input, entry.FullName).ConfigureAwait(false);
                }
            }
            else
            {
                using var input = File.OpenRead(archive);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var tar = new TarReader(gzip);
                TarEntry? entry;
                while ((entry = await tar.GetNextEntryAsync(copyData: false, cancellationToken: token).ConfigureAwait(false)) != null)
                {
                    if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream == null)
                        continue;
                    if (entry.Name == release.LibraryEntry || entry.Name == prefix + "/LICENSE" || entry.Name == prefix + "/ThirdPartyNotices.txt")
                        await Save(entry.DataStream, entry.Name).ConfigureAwait(false);
                }
            }
            if (!File.Exists(Path.Combine(destination, "LICENSE")) || !File.Exists(Path.Combine(destination, "ThirdPartyNotices.txt")))
                throw new InvalidDataException("AI runtime license notices are missing.");

            async Task Save(Stream input, string name)
            {
                using var output = File.Create(Path.Combine(destination, Path.GetFileName(name)));
                await StillLifePackage.CopyBounded(input, output, name == release.LibraryEntry ? release.LibraryBytes : 1_000_000, token).ConfigureAwait(false);
            }
        }
    }
}
