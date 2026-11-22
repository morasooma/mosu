using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Downloads a versioned, size-bounded worker bundle. Source is explicitly configured; never installs Python or compilers.</summary>
    internal sealed class StillLifePackage
    {
        public const long MAXIMUM_BYTES = 500_000_000;
        public const string VERSION = "tiny-lcm-fp8-v1";
        private readonly string directory;
        private readonly HttpMessageHandler? downloadHandler;
        public static string Platform => OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        public static string WorkerFile => OperatingSystem.IsWindows() ? "mosu-still-life.exe" : "mosu-still-life";
        public sealed record Asset(string Path, long Bytes, string Sha256);
        public sealed record Manifest(string Version, string Platform, Asset[] Files);
        public StillLifePackage(string directory, HttpMessageHandler? downloadHandler = null)
        {
            this.directory = directory;
            this.downloadHandler = downloadHandler;
        }

        public async Task<string> Ensure(string source, CancellationToken token)
        {
            if ((!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows()) || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Still Life currently requires Linux x64 or Windows x64.");
            string installed = Path.Combine(directory, VERSION, Platform);
            string manifestPath = Path.Combine(installed, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    if (new FileInfo(manifestPath).Length > 65536)
                        throw new InvalidDataException("Oversized installed manifest.");
                    var local = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(manifestPath, token).ConfigureAwait(false));
                    Validate(local);
                    bool valid = true;
                    foreach (var asset in local!.Files)
                        valid &= await Verify(Path.Combine(installed, asset.Path), asset, token).ConfigureAwait(false);
                    if (valid)
                        return installed;
                }
                catch (Exception e) when (e is JsonException or InvalidDataException)
                {
                    // A damaged install can be replaced from the configured trusted source.
                }
            }
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("No Still Life package source is configured. Install a verified bundle or configure its HTTPS manifest URL.");
            var uri = new Uri(source.Replace("PLATFORM", Platform, StringComparison.Ordinal).Replace("{platform}", Platform, StringComparison.Ordinal));
            if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException("Still Life packages require an HTTPS source without credentials.");
            using var client = downloadHandler == null ? new HttpClient() : new HttpClient(downloadHandler, disposeHandler: false);
            client.Timeout = TimeSpan.FromMinutes(20);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var manifestStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var manifestBytes = new MemoryStream();
            await CopyBounded(manifestStream, manifestBytes, 65536, token).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes.ToArray());
            Validate(manifest);
            Directory.CreateDirectory(directory);
            string staging = Path.Combine(directory, ".install-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                foreach (var asset in manifest!.Files)
                {
                    string target = Path.Combine(staging, asset.Path);
                    using var download = await client.GetAsync(new Uri(uri, asset.Path), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    download.EnsureSuccessStatusCode();
                    using (var input = await download.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    using (var output = File.Create(target))
                        await CopyBounded(input, output, asset.Bytes, token).ConfigureAwait(false);
                    if (!await Verify(target, asset, token).ConfigureAwait(false))
                        throw new InvalidDataException("Still Life asset checksum mismatch: " + asset.Path);
                }
                await File.WriteAllBytesAsync(Path.Combine(staging, "manifest.json"), manifestBytes.ToArray(), token).ConfigureAwait(false);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(Path.Combine(staging, WorkerFile), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
                if (Directory.Exists(installed))
                    Directory.Delete(installed, true);
                Directory.Move(staging, installed);
                return installed;
            }
            finally
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
            }
        }

        internal static void Validate(Manifest? manifest)
        {
            string[] required = { WorkerFile, "unet.safetensors", "encoder.onnx", "decoder.onnx", "embeddings.bin" };
            if (manifest == null || manifest.Version != VERSION || manifest.Platform != Platform || manifest.Files == null || manifest.Files.Length is < 5 or > 24)
                throw new InvalidDataException("Unsupported Still Life package manifest.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 65536;
            foreach (var asset in manifest.Files)
            {
                if (string.IsNullOrEmpty(asset.Path) || asset.Path.Length > 100 || asset.Path.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_')
                    || asset.Path is "." or ".." || asset.Path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || !names.Add(asset.Path)
                    || asset.Bytes <= 0 || asset.Bytes > MAXIMUM_BYTES || asset.Sha256 == null || asset.Sha256.Length != 64 || !asset.Sha256.All(Uri.IsHexDigit))
                    throw new InvalidDataException("Invalid Still Life package asset.");
                total += asset.Bytes;
            }
            if (total > MAXIMUM_BYTES || required.Any(name => !names.Contains(name)))
                throw new InvalidDataException("Still Life package exceeds 500 MB or is incomplete.");
        }

        internal static async Task<bool> Verify(string path, Asset asset, CancellationToken token)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != asset.Bytes)
                return false;
            using var stream = File.OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);
            return Convert.ToHexString(hash).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        internal static async Task CopyBounded(Stream input, Stream output, long limit, CancellationToken token)
        {
            byte[] buffer = new byte[65536];
            long total = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                total += count;
                if (total > limit)
                    throw new InvalidDataException("Download exceeds its declared size.");
                await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            }
        }
    }
}
