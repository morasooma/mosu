using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Installs Tiny-SD directly from its original publishers and prepares weights locally.</summary>
    internal sealed class DirectBackgroundPackage
    {
        internal const string VERSION = "tiny-lcm-direct-v2";
        internal const long REQUIRED_PREPARATION_BYTES = 2_500_000_000;

        internal sealed class InsufficientSpaceException(string path, long availableBytes)
            : IOException($"Background model preparation needs 2.5 GB free on the destination filesystem at '{path}'; available: {availableBytes} bytes.") { }
        internal sealed class DownloadException(string host, HttpRequestException inner)
            : IOException($"Background download failed from {host}.", inner)
        {
            public string Host { get; } = host;
        }

        internal sealed record Source(string Name, string Url, long Bytes, string Sha256);
        private const string tiny = "https://huggingface.co/segmind/tiny-sd/resolve/cad0bd7495fa6c4bcca01b19a723dc91627fe84f/";
        internal static readonly Source[] Models =
        {
            new Source("unet.bin", tiny + "unet/diffusion_pytorch_model.bin", 646923637, "c9b63e57cb1479c946c73bf5484bc1a823dbe565158ea0ee40015a18844d9f29"),
            new Source("clip.bin", tiny + "text_encoder/pytorch_model.bin", 246187869, "f99d8915149c782551f7d14aae5ca0b6e76a6e94d30e5dc86a349b228fbf92ad"),
            new Source("adapter.safetensors", "https://huggingface.co/akameswa/lcm-lora-tiny-sd/resolve/b89fe5f76fa8ce74f6f55cc80e3fc3684d6a25a2/adapter_model.safetensors", 117370368, "e1999434a3c8101762bccf4c63cf8facd6ab9672a11710824c9fb624634dd596"),
            new Source("tae-original.safetensors", "https://huggingface.co/madebyollin/taesd/resolve/614f76814bbe30edbe2e627ace1c2234c81a2c0e/diffusion_pytorch_model.safetensors", 9793292, "db169d69145ec4ff064e49d99c95fa05d3eb04ee453de35824a6d0f325513549"),
        };
        internal static Source Runtime
        {
            get
            {
                string name = OperatingSystem.IsWindows() ? "sd-master-0518b34-bin-win-vulkan-x64.zip" : "sd-master-0518b34-bin-Linux-Ubuntu-24.04-x86_64-vulkan.zip";
                return new Source("runtime.zip", "https://github.com/leejet/stable-diffusion.cpp/releases/download/master-982-0518b34/" + name,
                    OperatingSystem.IsWindows() ? 29463840 : 36790829,
                    OperatingSystem.IsWindows() ? "c2ab91b33206beba434f739a9131916539a3d6229da153e6e0311013e6308567" : "8bc413b3e6307fbefdf985cc562f7db1cc4c68e2dc3dca9d69b66ce98b9cbb5a");
            }
        }
        internal static string Executable => OperatingSystem.IsWindows() ? "sd-server.exe" : "sd-server";
        private readonly string root;
        private readonly HttpMessageHandler? handler;
        public DirectBackgroundPackage(string root, HttpMessageHandler? handler = null) { this.root = root; this.handler = handler; }

        public async Task<string> Ensure(CancellationToken token)
        {
            if ((!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Background distortion requires Linux x64 or Windows x64.");
            string installed = Path.Combine(root, VERSION, StillLifePackage.Platform);
            if (await VerifyInstall(installed, token).ConfigureAwait(false)) return installed;
            // Older installations have the same prepared weights but only the one-shot CLI.
            // Reuse verified weights when upgrading the runtime, rather than download 1 GB again.
            bool reuseWeights = await VerifyInstall(installed, token, requireExecutable: false).ConfigureAwait(false);
            Directory.CreateDirectory(root);
            // Original checkpoints plus conversion intermediates are temporary, not part of the final <=500 MB installation.
            CheckPreparationSpace(root);
            string staging = Path.Combine(root, ".prepare-" + Guid.NewGuid().ToString("N"));
            string sources = Path.Combine(staging, "sources"), ready = Path.Combine(staging, "ready");
            Directory.CreateDirectory(sources); Directory.CreateDirectory(ready);
            try
            {
                using var client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
                client.Timeout = TimeSpan.FromMinutes(30);
                foreach (var source in reuseWeights ? new[] { Runtime } : Models.Append(Runtime))
                    await Download(client, source, Path.Combine(sources, source.Name), token).ConfigureAwait(false);
                using (var archive = ZipFile.OpenRead(Path.Combine(sources, "runtime.zip")))
                    await ExtractRuntime(archive, ready, token).ConfigureAwait(false);
                if (reuseWeights)
                {
                    foreach (string name in new[] { "unet.safetensors", "clip.gguf", "tae.safetensors" })
                        File.Copy(Path.Combine(installed, name), Path.Combine(ready, name));
                }
                else
                    await Task.Run(() => Prepare(sources, ready, token), token).ConfigureAwait(false);
                var assets = new List<StillLifePackage.Asset>();
                foreach (string file in Directory.GetFiles(ready))
                {
                    using var input = File.OpenRead(file);
                    assets.Add(new StillLifePackage.Asset(Path.GetFileName(file), input.Length,
                        Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false))));
                }
                await File.WriteAllTextAsync(Path.Combine(ready, "direct-manifest.json"), JsonSerializer.Serialize(assets), token).ConfigureAwait(false);
                if (new DirectoryInfo(ready).GetFiles().Sum(f => f.Length) > StillLifePackage.MAXIMUM_BYTES)
                    throw new InvalidDataException("Prepared background distortion installation exceeds 500 MB.");
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
                string previous = installed + ".previous";
                if (Directory.Exists(previous)) Directory.Delete(previous, true);
                if (Directory.Exists(installed)) Directory.Move(installed, previous);
                try { Directory.Move(ready, installed); }
                catch { if (Directory.Exists(previous)) Directory.Move(previous, installed); throw; }
                if (Directory.Exists(previous)) Directory.Delete(previous, true);
                return installed;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        internal static void CheckPreparationSpace(string directory)
        {
            // On Unix, DriveInfo queries the filesystem containing this path, including mounted volumes.
            // Reducing it to Path.GetPathRoot() would incorrectly query '/' instead.
            string path = Path.GetFullPath(directory);
            long available = new DriveInfo(path).AvailableFreeSpace;
            if (available < REQUIRED_PREPARATION_BYTES)
                throw new InsufficientSpaceException(path, available);
        }

        internal static async Task Download(HttpClient client, Source source, string target, CancellationToken token)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    using (var output = File.Create(target))
                        await StillLifePackage.CopyBounded(input, output, source.Bytes, token).ConfigureAwait(false);
                    if (!await StillLifePackage.Verify(target, new StillLifePackage.Asset(source.Name, source.Bytes, source.Sha256), token).ConfigureAwait(false))
                        throw new InvalidDataException("Background source checksum mismatch: " + source.Name);
                    return;
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), token).ConfigureAwait(false);
                }
                catch (HttpRequestException e)
                {
                    throw new DownloadException(new Uri(source.Url).Host, e);
                }
            }
        }

        internal static async Task ExtractRuntime(ZipArchive archive, string target, CancellationToken token)
        {
            long total = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName;
                bool wanted = name == Executable || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || name.Contains(".so", StringComparison.Ordinal) || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
                if (!wanted) continue;
                if (Path.GetFileName(name) != name || name.Contains('\\') || !names.Add(name) || entry.Length <= 0 || entry.Length > 60_000_000)
                    throw new InvalidDataException("Invalid background runtime archive entry.");
                total += entry.Length;
                if (total > 105_000_000) throw new InvalidDataException("Oversized background runtime.");
                using var input = entry.Open(); using var output = File.Create(Path.Combine(target, name));
                await StillLifePackage.CopyBounded(input, output, entry.Length, token).ConfigureAwait(false);
            }
            if (!File.Exists(Path.Combine(target, Executable))) throw new InvalidDataException("Runtime executable missing.");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(target, Executable), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        internal static void Prepare(string source, string ready, CancellationToken token)
        {
            using var unet = new BackgroundModelWeights.TorchFile(Path.Combine(source, "unet.bin"));
            using var lora = new BackgroundModelWeights.SafeFile(Path.Combine(source, "adapter.safetensors"));
            int merged = 0;
            BackgroundModelWeights.WriteSafe(Path.Combine(ready, "unet.safetensors"), unet.Names, name =>
            {
                var weight = unet.Read(name);
                if (name.EndsWith(".weight", StringComparison.Ordinal))
                {
                    string prefix = "base_model.model." + name[..^7];
                    if (lora.Contains(prefix + ".lora_A.weight"))
                    {
                        merged++;
                        weight = BackgroundModelWeights.Merge(weight, lora.Read(prefix + ".lora_A.weight"), lora.Read(prefix + ".lora_B.weight"), token);
                    }
                }
                return weight;
            }, BackgroundModelWeights.UnetName, true, token);
            if (merged != 147) throw new InvalidDataException("Incomplete Tiny-SD LCM adapter merge.");
            using var clip = new BackgroundModelWeights.TorchFile(Path.Combine(source, "clip.bin"));
            BackgroundModelWeights.WriteClip(Path.Combine(ready, "clip.gguf"), clip, token);
            using var tae = new BackgroundModelWeights.SafeFile(Path.Combine(source, "tae-original.safetensors"));
            BackgroundModelWeights.WriteSafe(Path.Combine(ready, "tae.safetensors"), tae.Names, tae.Read, n => n, false, token);
        }

        internal static async Task<bool> VerifyInstall(string path, CancellationToken token, bool requireExecutable = true)
        {
            string manifest = Path.Combine(path, "direct-manifest.json");
            if (!File.Exists(manifest) || new FileInfo(manifest).Length > 65536) return false;
            try
            {
                var assets = JsonSerializer.Deserialize<StillLifePackage.Asset[]>(await File.ReadAllTextAsync(manifest, token).ConfigureAwait(false));
                string[] required = requireExecutable ? new[] { Executable, "unet.safetensors", "clip.gguf", "tae.safetensors" }
                    : new[] { "unet.safetensors", "clip.gguf", "tae.safetensors" };
                if (assets == null || assets.Length is < 4 or > 60 || assets.Sum(a => a.Bytes) > StillLifePackage.MAXIMUM_BYTES
                    || required.Any(n => !assets.Any(a => a.Path == n))) return false;
                foreach (var asset in assets)
                    if (asset.Path != Path.GetFileName(asset.Path) || asset.Path.Contains('\\') || !await StillLifePackage.Verify(Path.Combine(path, asset.Path), asset, token).ConfigureAwait(false)) return false;
                return true;
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException) { return false; }
        }
    }
}
