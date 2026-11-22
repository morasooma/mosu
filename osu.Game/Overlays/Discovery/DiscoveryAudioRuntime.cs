using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Graphics.Backgrounds.AI;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>Official Microsoft DirectML native assets, pinned independently from CPU ORT. No NuGet execution.</summary>
    internal static class DiscoveryAudioRuntime
    {
        // ORT native 1.23.0 DirectML implements the same version-23 C API as the managed 1.23.2 package.
        // This is process-global: an already loaded CPU runtime is never swapped under live AI sessions.
        internal static async Task<string> InstallDirectML(string root, CancellationToken token)
        {
            var ort = new StillLifePackage.Asset("onnxruntime.dll", 17201208, "f5131591edac6b0a8090d0e329040a49319d7a689cb5b465235fbf7030fa8027");
            var shared = new StillLifePackage.Asset("onnxruntime_providers_shared.dll", 22048, "3b27e1417d12b73a6a34d80414c083e359e092d2f0ce572d7e67be8cdbe9e825");
            var dml = new StillLifePackage.Asset("DirectML.dll", 18527776, "9c9e6d822561c6c41b90e6994b3e8857cf1d66dbfb1e0c4c799c7c89b4e92da1");
            string installed = Path.Combine(root, "directml-1.23.0");
            if (await StillLifePackage.Verify(Path.Combine(installed, ort.Path), ort, token).ConfigureAwait(false)
                && await StillLifePackage.Verify(Path.Combine(installed, shared.Path), shared, token).ConfigureAwait(false)
                && await StillLifePackage.Verify(Path.Combine(installed, dml.Path), dml, token).ConfigureAwait(false)) return Path.Combine(installed, ort.Path);
            Directory.CreateDirectory(root);
            string staging = Path.Combine(root, ".dml-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                await Extract("microsoft.ml.onnxruntime.directml", "1.23.0", 12746067,
                    "a33ec2382b3c440bab74042a135733bb6e5085f293b908d3997688a58fe307e7",
                    new[] { ("runtimes/win-x64/native/onnxruntime.dll", ort), ("runtimes/win-x64/native/onnxruntime_providers_shared.dll", shared) }, "ort").ConfigureAwait(false);
                await Extract("microsoft.ai.directml", "1.15.4", 202292617,
                    "4e7cb7ddce8cf837a7a75dc029209b520ca0101470fcdf275c1f49736a3615b9",
                    new[] { ("bin/x64-win/DirectML.dll", dml) }, "dml").ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(installed);
                foreach (string file in Directory.GetFiles(staging, "*", SearchOption.TopDirectoryOnly))
                    if (!file.EndsWith(".zip", StringComparison.Ordinal)) File.Move(file, Path.Combine(installed, Path.GetFileName(file)), true);
                return Path.Combine(installed, ort.Path);

                async Task Extract(string package, string version, long bytes, string hash, (string Entry, StillLifePackage.Asset Asset)[] assets, string prefix)
                {
                    string archive = Path.Combine(staging, prefix + ".zip");
                    using (var response = await client.GetAsync($"https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg", HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                        using var output = File.Create(archive);
                        await StillLifePackage.CopyBounded(input, output, bytes, token).ConfigureAwait(false);
                    }
                    if (!await StillLifePackage.Verify(archive, new StillLifePackage.Asset(prefix + ".zip", bytes, hash), token).ConfigureAwait(false)) throw new InvalidDataException("GPU runtime archive checksum mismatch.");
                    using var zip = ZipFile.OpenRead(archive);
                    foreach (var asset in assets)
                    {
                        var entry = zip.GetEntry(asset.Entry) ?? throw new InvalidDataException("GPU runtime asset missing.");
                        using (var input = entry.Open())
                        using (var output = File.Create(Path.Combine(staging, asset.Asset.Path)))
                            await StillLifePackage.CopyBounded(input, output, asset.Asset.Bytes, token).ConfigureAwait(false);
                        if (!await StillLifePackage.Verify(Path.Combine(staging, asset.Asset.Path), asset.Asset, token).ConfigureAwait(false)) throw new InvalidDataException("GPU runtime checksum mismatch.");
                    }
                    foreach (string notice in new[] { "LICENSE", "LICENSE.txt", "LICENSE-CODE.txt", "ThirdPartyNotices.txt" })
                    {
                        var entry = zip.GetEntry(notice);
                        if (entry == null) continue;
                        using var input = entry.Open();
                        using var output = File.Create(Path.Combine(staging, prefix + "-" + notice));
                        await StillLifePackage.CopyBounded(input, output, 1_000_000, token).ConfigureAwait(false);
                    }
                }
            }
            finally { Directory.Delete(staging, true); }
        }
    }
}
