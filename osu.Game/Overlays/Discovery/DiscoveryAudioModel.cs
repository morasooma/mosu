using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using osu.Game.Graphics.Backgrounds.AI;

namespace osu.Game.Overlays.Discovery
{
    internal enum DiscoveryAudioDevice
    {
        [osu.Framework.Localisation.LocalisableDescription(typeof(osu.Game.Localisation.DiscoveryStrings), nameof(osu.Game.Localisation.DiscoveryStrings.AudioAuto))]
        Auto,
        [osu.Framework.Localisation.LocalisableDescription(typeof(osu.Game.Localisation.AiFunctionsStrings), nameof(osu.Game.Localisation.AiFunctionsStrings.Cpu))]
        Cpu,
    }

    /// <summary>LAION larger_clap_music audio tower, Apache-2.0 weights and ONNX conversion. No MERT assets or transforms.</summary>
    internal sealed class DiscoveryAudioModel : IDisposable
    {
        // New model AND preprocessor identity: MERT caches are never consumed or migrated.
        internal const string Version = "clap-music-2ad6afda-48k-slaney-v1";
        internal const int SampleRate = 48000;
        internal const int Window = SampleRate * 10;
        internal const int Features = 512;
        internal const string WeightHash = "2ad6afdaddf2f38a72cc9ef737203baa23d60a6919b14f70b57e9bd7582be2d2";
        internal const long WeightBytes = 277439052;
        internal const string FileName = "clap-music-audio.onnx";
        private readonly InferenceSession session;
        internal string Provider { get; }

        internal static async Task<string> Install(string root, CancellationToken token)
        {
            Directory.CreateDirectory(root);
            foreach (string notice in new[] { "CLAP-NOTICE.txt", "CLAP-Apache-2.0.txt" })
            {
                using var source = typeof(DiscoveryAudioModel).Assembly.GetManifestResourceStream("osu.Game.Resources.AI." + notice)
                    ?? throw new InvalidDataException("Missing CLAP licence notice.");
                using var output = File.Create(Path.Combine(root, notice));
                await source.CopyToAsync(output, token).ConfigureAwait(false);
            }
            var weight = new StillLifePackage.Asset(FileName, WeightBytes, WeightHash);
            string target = Path.Combine(root, weight.Path);
            if (await StillLifePackage.Verify(target, weight, token).ConfigureAwait(false)) return target;
            if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!).AvailableFreeSpace < WeightBytes + 128_000_000)
                throw new IOException("Audio model needs at least 410 MB free.");
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
                using var response = await client.GetAsync("https://huggingface.co/akashiclibrary-dev/larger_clap_music/resolve/81b7a6ed45111632f34135478cfb71617aec6a25/onnx/audio_model.onnx",
                    HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                using (var output = File.Create(temporary))
                    await StillLifePackage.CopyBounded(input, output, WeightBytes, token).ConfigureAwait(false);
                if (!await StillLifePackage.Verify(temporary, weight, token).ConfigureAwait(false)) throw new InvalidDataException("CLAP weight checksum mismatch.");
                token.ThrowIfCancellationRequested();
                File.Move(temporary, target, true);
                return target;
            }
            finally { File.Delete(temporary); }
        }

        internal DiscoveryAudioModel(string path, DiscoveryAudioDevice device)
        {
            using var options = new SessionOptions
            {
                // No additional ORT worker threads inheriting normal priority during play.
                IntraOpNumThreads = 1, InterOpNumThreads = 1, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                EnableMemoryPattern = false,
            };
            options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            options.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
            Provider = "CPU";
            if (device == DiscoveryAudioDevice.Auto)
            {
                string[] available = OrtEnv.Instance().GetAvailableProviders();
                if (OperatingSystem.IsWindows() && available.Contains("DmlExecutionProvider"))
                {
                    options.AddFreeDimensionOverrideByName("batch_size", 1);
                    options.AppendExecutionProvider_DML(0);
                    Provider = "DirectML";
                }
                else if (available.Contains("CUDAExecutionProvider"))
                {
                    options.AppendExecutionProvider_CUDA(0);
                    Provider = "CUDA";
                }
            }
            session = new InferenceSession(path, options);
            var shape = session.InputMetadata.Single().Value.Dimensions;
            if (session.InputMetadata.Count != 1 || session.InputNames[0] != "input_features"
                || shape.Length != 4 || shape[1] != 1 || shape[2] != 1001 || shape[3] != 64
                || !session.OutputMetadata.ContainsKey("audio_embeds"))
            {
                session.Dispose();
                throw new InvalidDataException("Unexpected CLAP graph contract.");
            }
        }

        internal float[] Infer(float[] samples, CancellationToken token) => InferFeatures(DiscoveryAudioFeatures.Extract(samples, token), token);

        // A single session owner; each ten-second window is a pause/cancellation boundary.
        internal float[] InferFeatures(float[] features, CancellationToken token)
        {
            if (features.Length != 1001 * 64 || features.Any(x => !float.IsFinite(x))) throw new InvalidDataException("Invalid CLAP spectrogram.");
            token.ThrowIfCancellationRequested();
            using var run = new RunOptions();
            using var registration = token.Register(() => run.Terminate = true);
            var input = new DenseTensor<float>(features, new[] { 1, 1, 1001, 64 });
            try
            {
                using var output = session.Run(new[] { NamedOnnxValue.CreateFromTensor("input_features", input) }, new[] { "audio_embeds" }, run);
                token.ThrowIfCancellationRequested();
                return Transform(output.Single().AsTensor<float>().ToArray());
            }
            catch (OnnxRuntimeException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        }

        internal static float[] Transform(float[] embedding)
        {
            if (embedding.Length != Features || embedding.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid CLAP embedding.");
            double norm = Math.Sqrt(embedding.Sum(v => (double)v * v));
            if (norm < 1e-12 || !double.IsFinite(norm)) throw new InvalidDataException("Empty CLAP embedding.");
            return embedding.Select(v => (float)(v / norm)).ToArray();
        }

        public void Dispose() => session.Dispose();
    }
}
