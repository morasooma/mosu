using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Owns one loopback-only upstream process; retains the model between passes and backgrounds.</summary>
    internal sealed class StillLifeEngine : IDisposable
    {
        private readonly Process worker;
        private readonly HttpClient client;
        private readonly string work;
        private readonly StringBuilder diagnostic = new StringBuilder();
        private readonly object diagnosticLock = new object();
        internal static readonly string[] Prompts =
        {
            "the same image, the same scene, anime illustration, slightly warped, imperfect edges, faint duplicated details",
            "the same image, the same scene, recreated imperfectly, extra eyes and mouths, duplicated features, garbled text, missing letters",
            "the same scene, recreated badly, many extra eyes and mouths, multiplied objects, scrambled letters, melting, uncanny",
            "the same scene emptied, background stripped bare, distorted figure, empty walls, broken mangled objects, minimal, plain",
            "an empty room, bare walls, an empty cube, featureless, blank, nothing inside",
        };

        public StillLifeEngine(string directory, StillLifeOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            work = Path.Combine(directory, ".worker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            // Never use the system proxy for a private local worker.
            client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            client.BaseAddress = new Uri($"http://127.0.0.1:{port}/");
            var start = new ProcessStartInfo(Path.Combine(directory, DirectBackgroundPackage.Executable))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = work,
            };
            foreach (string argument in new[]
                     {
                         "-m", Path.Combine(directory, "clip.gguf"), "--diffusion-model", Path.Combine(directory, "unet.safetensors"),
                         "--taesd", Path.Combine(directory, "tae.safetensors"), "--backend", options.Backend == StillLifeBackend.Cpu ? "cpu" : "diffusion=vulkan0,te=cpu,vae=vulkan0",
                         "--type", options.Backend == StillLifeBackend.Cpu ? "f32" : "f16", "-t", options.Threads.ToString(CultureInfo.InvariantCulture),
                         "--listen-ip", "127.0.0.1", "--listen-port", port.ToString(CultureInfo.InvariantCulture), "--log-level", "error",
                     }) start.ArgumentList.Add(argument);
            if (OperatingSystem.IsLinux())
                start.Environment["LD_LIBRARY_PATH"] = directory + (start.Environment.TryGetValue("LD_LIBRARY_PATH", out var existing) ? ":" + existing : string.Empty);
            worker = new Process { StartInfo = start };
            try
            {
                var timer = Stopwatch.StartNew();
                if (!worker.Start()) throw new IOException("Background runtime did not start.");
                worker.ErrorDataReceived += Capture; worker.OutputDataReceived += Capture;
                worker.BeginErrorReadLine(); worker.BeginOutputReadLine();
                try { worker.PriorityClass = ProcessPriorityClass.BelowNormal; }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
                startup.CancelAfter(TimeSpan.FromMinutes(3));
                try
                {
                    while (true)
                    {
                        startup.Token.ThrowIfCancellationRequested();
                        if (worker.HasExited) throw WorkerError();
                        try
                        {
                            using var response = client.GetAsync("sdcpp/v1/capabilities", startup.Token).GetAwaiter().GetResult();
                            response.EnsureSuccessStatusCode();
                            if (worker.HasExited) throw WorkerError();
                            break;
                        }
                        catch (HttpRequestException)
                        {
                            if (startup.Token.WaitHandle.WaitOne(100)) startup.Token.ThrowIfCancellationRequested();
                        }
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    throw new TimeoutException("Background runtime startup exceeded three minutes.");
                }
                Logger.Log($"Background distortion runtime ready: backend={options.Backend}, load_ms={timer.ElapsedMilliseconds}");
            }
            catch { Dispose(); throw; }
        }

        internal static string Sigmas(StillLifeOptions options)
        {
            float[] alphas = new float[1000];
            float product = 1;
            for (int i = 0; i < alphas.Length; i++)
            {
                float beta = MathF.Sqrt(.00085f) + i / 999f * (MathF.Sqrt(.012f) - MathF.Sqrt(.00085f));
                product *= 1 - beta * beta; alphas[i] = product;
            }
            int originalSteps = (int)(50 * options.Strength);
            var sigmas = Enumerable.Range(0, options.Steps).Select(i =>
            {
                int timestep = (originalSteps - (int)Math.Floor(i * originalSteps / (double)options.Steps)) * 20 - 1;
                return MathF.Sqrt((1 - alphas[timestep]) / alphas[timestep]).ToString("R", CultureInfo.InvariantCulture);
            });
            return string.Join(",", sigmas.Append("0"));
        }

        public BackgroundImageCache.Artwork Generate(Image<Rgb24> source, StillLifeOptions options, CancellationToken token, Func<bool> shouldPause)
        {
            float ratio = Math.Min(1, (int)options.Resolution / (float)Math.Max(source.Width, source.Height));
            int width = Math.Max(32, (int)(source.Width * ratio) / 32 * 32);
            int height = Math.Max(32, (int)(source.Height * ratio) / 32 * 32);
            int paddedWidth = (width + 63) / 64 * 64;
            int paddedHeight = (height + 63) / 64 * 64;
            using var bytes = new MemoryStream();
            using (var image = source.Clone(c => c.Resize(width, height).Pad(paddedWidth, paddedHeight))) image.SaveAsPng(bytes);
            byte[] input = bytes.ToArray();
            float[] sigmas = Sigmas(options).Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var total = Stopwatch.StartNew();
            for (int pass = 0; pass < options.Passes; pass++)
            {
                token.ThrowIfCancellationRequested();
                while (shouldPause())
                    if (token.WaitHandle.WaitOne(250)) token.ThrowIfCancellationRequested();
                if (worker.HasExited) throw WorkerError();
                // Native API's initial image is essential. Reference images are ignored by SD1 Tiny-SD.
                var request = new
                {
                    prompt = Prompts[Math.Min(pass, Prompts.Length - 1)] + " <sd_cpp_extra_args>" + JsonSerializer.Serialize(new
                    {
                        sample_params = new { custom_sigmas = sigmas },
                    }) + "</sd_cpp_extra_args>",
                    init_images = new[] { Convert.ToBase64String(input) },
                    width = paddedWidth, height = paddedHeight, steps = options.Steps, cfg_scale = 1,
                    // Strength is encoded in training timesteps, not applied again to truncate the schedule.
                    denoising_strength = 1, sampler_name = "lcm", scheduler = "lcm", seed = (long)options.Seed + pass,
                };
                var timer = Stopwatch.StartNew();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMinutes(3));
                using var cancellation = timeout.Token.Register(Kill);
                try
                {
                    using var content = JsonContent.Create(request);
                    using var response = client.PostAsync("sdapi/v1/img2img", content, timeout.Token).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode) throw WorkerError();
                    using var json = JsonDocument.Parse(response.Content.ReadAsStream(timeout.Token));
                    input = Convert.FromBase64String(json.RootElement.GetProperty("images")[0].GetString()!);
                }
                catch (Exception) when (timeout.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("Background generation exceeded three minutes.");
                }
                Logger.Log($"Background distortion pass: backend={options.Backend}, size={width}x{height}, pass={pass + 1}/{options.Passes}, ms={timer.ElapsedMilliseconds}");
            }
            using var result = Image.Load<Rgb24>(input);
            if (result.Width != paddedWidth || result.Height != paddedHeight) throw new InvalidDataException("Unexpected generated image size.");
            result.Mutate(c => c.Crop(new Rectangle((paddedWidth - width) / 2, (paddedHeight - height) / 2, width, height)));
            byte[] rgb = new byte[width * height * 3]; result.CopyPixelDataTo(rgb);
            Logger.Log($"Background distortion generated: backend={options.Backend}, passes={options.Passes}, ms={total.ElapsedMilliseconds}");
            return new BackgroundImageCache.Artwork(rgb, width, height);
        }

        private void Capture(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (diagnosticLock)
            {
                if (diagnostic.Length > 8192) diagnostic.Remove(0, diagnostic.Length - 4096);
                diagnostic.AppendLine(e.Data);
            }
        }

        private IOException WorkerError()
        {
            lock (diagnosticLock) return new IOException("Background generation failed: " + diagnostic);
        }

        private void Kill()
        {
            try { if (!worker.HasExited) worker.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }

        public void Dispose()
        {
            Kill();
            try { worker.WaitForExit(10000); }
            catch (InvalidOperationException) { }
            worker.Dispose();
            client.Dispose();
            if (Directory.Exists(work)) Directory.Delete(work, true);
        }
    }
}
