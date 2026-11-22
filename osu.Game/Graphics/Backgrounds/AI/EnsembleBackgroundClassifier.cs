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
    /// <summary>Verified, lazy ONNX classifiers. Construct and use only on the safety worker.</summary>
    internal sealed class EnsembleBackgroundClassifier : IDisposable
    {
        internal sealed record Model(string FileName, string Version, string Url, string Sha256, int Bytes, int Size);

        public static readonly Model General = new Model("swiftformer-xs-8c28c49d.onnx", "swiftformer-xs-8c28c49d-rgb-bilinear-v1",
            "https://huggingface.co/OwenElliott/image-safety-classifier-xs/resolve/54f4560bd9c5ee92d45dc30418a8f8680e80de6d/onnx/image-safety-classifier-xs.onnx",
            "8c28c49d9075f3ad15ebdc2961f02d5b3f99be944815b848b49c9f0e6f3fb689", 13137569, 224);

        public static readonly Model Anime = new Model("anime-rating-76e5c447.onnx", "anime-rating-76e5c447-rgb-bilinear-v1",
            "https://huggingface.co/deepghs/anime_rating/resolve/46be80bfe01a415efa5aa7c025528cac82a8b88a/mobilenetv3_v1_pruned_ls0.1/model.onnx",
            "76e5c44704421e2b6431a2dbb395f8943e205e59f415a5614b16f0601b55544d", 16827558, 384);

        public static async Task<string> Download(Model model, string directory, CancellationToken token)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, model.FileName);
            if (File.Exists(path) && verify(path, model))
                return path;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                using var response = await client.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                await using (var output = File.Create(temporary))
                {
                    byte[] buffer = new byte[65536];
                    int total = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                    {
                        total += count;
                        if (total > model.Bytes)
                            throw new InvalidDataException("AI model exceeds the expected size.");
                        await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    }
                }
                if (!verify(temporary, model))
                    throw new InvalidDataException("AI model SHA-256 mismatch.");
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path, true);
                return path;
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private static bool verify(string path, Model model)
        {
            using var stream = File.OpenRead(path);
            return stream.Length == model.Bytes && Convert.ToHexString(SHA256.HashData(stream)).Equals(model.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        private readonly InferenceSession session;
        private readonly Model model;

        public EnsembleBackgroundClassifier(Model model, string path)
        {
            BackgroundAiRuntime.RequireLoaded();
            this.model = model;
            using var options = new SessionOptions
            {
                IntraOpNumThreads = 1,
                InterOpNumThreads = 1,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };
            options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
            options.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
            session = new InferenceSession(path, options);
            var shape = session.InputMetadata.Single().Value.Dimensions;
            if (shape.Length != 4 || shape[1] != 3 || shape[2] > 0 && shape[2] != model.Size || shape[3] > 0 && shape[3] != model.Size)
            {
                session.Dispose();
                throw new InvalidDataException("Unexpected safety model input layout.");
            }
        }

        public BackgroundClassification Classify(Image<Rgb24> image)
        {
            using var resized = image.Clone(c => c.Resize(model.Size, model.Size, KnownResamplers.Triangle));
            var tensor = new DenseTensor<float>(new[] { 1, 3, model.Size, model.Size });
            resized.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < model.Size; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < model.Size; x++)
                    {
                        // SwiftFormer export includes normalization AND softmax: raw RGB [0,255].
                        // Anime rating expects RGB [-1,1]; its softmax is also part of the graph.
                        tensor[0, 0, y, x] = model == General ? row[x].R : row[x].R / 127.5f - 1;
                        tensor[0, 1, y, x] = model == General ? row[x].G : row[x].G / 127.5f - 1;
                        tensor[0, 2, y, x] = model == General ? row[x].B : row[x].B / 127.5f - 1;
                    }
                }
            });
            using var output = session.Run(new[] { NamedOnnxValue.CreateFromTensor(session.InputNames[0], tensor) });
            var scores = output.First().AsEnumerable<float>().ToArray();
            if (scores.Length != 3)
                throw new InvalidDataException("Unexpected safety model output.");
            // Reuse the existing five-score Realm envelope under separate model-version keys.
            // General: [NSFL, NSFW, SFW, 0, 0]; Anime: [safe, r15, r18, 0, 0].
            var result = new BackgroundClassification(scores[0], scores[1], scores[2], 0, 0);
            if (!result.IsValid)
                throw new InvalidDataException("Invalid safety model probabilities.");
            return result;
        }

        public void Dispose() => session.Dispose();
    }
}
