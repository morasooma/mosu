using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Scoring.Render;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Captures the real song-selection screen in a silent, hidden, disposable client process.
    /// Theme globals are process-local; the interactive client's configuration and palette are never touched.
    /// </summary>
    public static class ThemePreviewRenderer
    {
        public const int WIDTH = 640;
        public const int HEIGHT = 360;
        public const string ARGUMENT = "--render-theme-preview";
        public const int MAXIMUM_BYTES = 256 * 1024;

        public static async Task<byte[]> RenderAsync(CustomThemeDefinition theme, CancellationToken cancellationToken = default)
        {
            string directory = Path.Combine(Path.GetTempPath(), "mosu-theme-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "theme.json"), theme.ToJson(), cancellationToken).ConfigureAwait(false);
                var start = CreateStartInfo(directory);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start theme capture.");
                // Drain both streams, but never include child logs or environment in publication errors.
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                    if (process.ExitCode != 0) throw new InvalidOperationException("Theme capture failed.");
                    byte[] png = await File.ReadAllBytesAsync(Path.Combine(directory, "preview.png"), timeout.Token).ConfigureAwait(false);
                    Validate(png);
                    return png;
                }
                finally
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                }
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        internal static ProcessStartInfo CreateStartInfo(string directory)
        {
            var start = ReplayRenderProcessStartInfoBuilder.Create(new[] { ARGUMENT, directory });
            // Do not inherit the author's renderer override, external resources or active custom profile.
            foreach (string key in start.Environment.Keys.Where(k => k.StartsWith("MOSU_", StringComparison.Ordinal) || k.StartsWith("OSU_", StringComparison.Ordinal)).ToArray())
                start.Environment.Remove(key);
            start.RedirectStandardOutput = true;
            return start;
        }

        internal static void Validate(byte[] png)
        {
            if (png.Length > MAXIMUM_BYTES) throw new InvalidOperationException("Theme preview exceeds the upload limit.");
            using var image = Image.Load<Rgba32>(png);
            if (image.Width != WIDTH || image.Height != HEIGHT) throw new InvalidOperationException("Theme preview has invalid dimensions.");
        }
    }
}
