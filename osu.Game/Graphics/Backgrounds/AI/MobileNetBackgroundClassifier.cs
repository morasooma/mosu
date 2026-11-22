using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Graphics.Backgrounds.AI
{
    internal sealed class MobileNetBackgroundClassifier : IDisposable
    {
        public const string ModelVersion = "mobilenetv2-558f9d90-rgb-nearest-v1";
        internal const string ModelHash = "558f9d9071736d6121dc8aa393187a2169e4119e2b1893882ebf5ce219dc2e5f";
        private const string model_url = "https://raw.githubusercontent.com/Kazuhito00/nsfw_model_onnx_sample/2e93d8ad6987fc2aa315f4bcf577f4be9e9b8ac5/model/nsfw_mobilenet2_224x224.onnx";
        private InferenceSession? session;

        // Called only on the dedicated worker, never at client startup or by a drawable loader.
        public static async Task<string> Download(string directory, CancellationToken token)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "mobilenetv2-558f9d90.onnx");
            if (File.Exists(path) && verify(path))
                return path;

            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                using var response = await client.GetAsync(model_url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using (var output = File.Create(temporary))
                await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                {
                    byte[] buffer = new byte[65536];
                    int total = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                    {
                        total += count;
                        if (total > 12 * 1024 * 1024)
                            throw new InvalidDataException("AI model exceeds the download limit.");
                        await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    }
                }
                if (!verify(temporary))
                    throw new InvalidDataException("AI model SHA-256 mismatch.");
                File.Move(temporary, path, true);
                return path;
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private static bool verify(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(ModelHash, StringComparison.OrdinalIgnoreCase);
        }

        public MobileNetBackgroundClassifier(string path)
        {
            BackgroundAiRuntime.RequireLoaded();
            using var options = new SessionOptions
            {
                IntraOpNumThreads = 1, // No hidden ORT pool competing with update/draw/audio.
                InterOpNumThreads = 1,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };
            options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            options.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
            session = new InferenceSession(path, options);
            var shape = session.InputMetadata.Single().Value.Dimensions;
            if (shape.Length != 4 || shape[1] != 224 || shape[2] != 224 || shape[3] != 3)
                throw new InvalidDataException("Unexpected MobileNetV2 input layout.");
        }

        public BackgroundClassification Classify(Image<Rgb24> image)
        {
            // Match upstream keras load_img: RGB, nearest-neighbour resize, NHWC, [0,1].
            using var resized = image.Clone(c => c.Resize(224, 224, KnownResamplers.NearestNeighbor));
            var tensor = new DenseTensor<float>(new[] { 1, 224, 224, 3 });
            resized.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < 224; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < 224; x++)
                    {
                        tensor[0, y, x, 0] = row[x].R / 255f;
                        tensor[0, y, x, 1] = row[x].G / 255f;
                        tensor[0, y, x, 2] = row[x].B / 255f;
                    }
                }
            });
            using var output = session!.Run(new[] { NamedOnnxValue.CreateFromTensor(session.InputNames[0], tensor) });
            float[] scores = output.First().AsEnumerable<float>().ToArray();
            if (scores.Length != 5)
                throw new InvalidDataException("Unexpected MobileNetV2 output.");
            var result = new BackgroundClassification(scores[0], scores[1], scores[2], scores[3], scores[4]);
            if (!result.IsValid)
                throw new InvalidDataException("Invalid MobileNetV2 probabilities.");
            return result;
        }

        public void Dispose()
        {
            session?.Dispose();
            session = null;
        }
    }
}
