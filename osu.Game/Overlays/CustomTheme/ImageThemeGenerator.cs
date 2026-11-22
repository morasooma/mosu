using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaterialColorUtilities.ColorAppearance;
using MaterialColorUtilities.Quantize;
using MaterialColorUtilities.Score;
using MaterialTheming;
using osu.Framework.Graphics;
using osu.Framework.IO.Network;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Framework.Utils;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThemeMode = osu.Game.Overlays.ThemeMode;

namespace osu.Game.Overlays.CustomTheme
{
    public enum ImageThemeSource { Avatar, Banner, Background, CurrentBackground }
    public enum ImageThemeStyle { KeepCurrent, Rounded, Sharp }

    /// <summary>Image analysis is CPU-only. No GPU readback or global theme mutation is needed for a preview.</summary>
    public static class ImageThemeGenerator
    {
        public const int MAXIMUM_BYTES = 8 * 1024 * 1024;
        private const long maximum_pixels = 24_000_000;

        public static string? GetSourceUrl(APIUser user, ImageThemeSource source) => source switch
        {
            ImageThemeSource.Avatar => user.AvatarUrl,
            ImageThemeSource.Banner => user.CoverUrl,
            ImageThemeSource.Background => user.ProfileBackgroundUrl,
            _ => null,
        };

        public static async Task<ImageThemeAnalysis> DownloadAsync(string url, OsuConfigManager config, CancellationToken cancellationToken)
        {
            // Use the same host policy and migrated /file/ URLs as profile textures. Never send API credentials to media URLs.
            string resolved = string.IsNullOrWhiteSpace(config.Get<string>(OsuSetting.CustomApiUrl))
                ? TrustedDomainOnlineStore.ResolveLookupUrl(url) : url;
            if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0
                || uri.Scheme is not ("http" or "https"))
                throw new InvalidDataException("Unavailable profile image.");

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            using var request = new WebRequest(resolved) { AllowRetryOnTimeout = false };
            request.DownloadProgress += (received, total) =>
            {
                if (received > MAXIMUM_BYTES || total > MAXIMUM_BYTES)
                    throw new InvalidDataException("Profile image exceeds the size limit.");
            };
            await request.PerformAsync(deadline.Token).ConfigureAwait(false);
            byte[] data = request.GetResponseData() ?? throw new InvalidDataException("Empty profile image.");
            return await Task.Run(() => Analyse(data, deadline.Token), deadline.Token).ConfigureAwait(false);
        }

        public static ImageThemeAnalysis Analyse(Stream stream, CancellationToken cancellationToken = default)
        {
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[32768];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (bytes.Length + read > MAXIMUM_BYTES) throw new InvalidDataException("Image exceeds the size limit.");
                bytes.Write(buffer, 0, read);
            }
            return Analyse(bytes.ToArray(), cancellationToken);
        }

        public static ImageThemeAnalysis Analyse(byte[] data, CancellationToken cancellationToken = default)
        {
            if (data.Length == 0 || data.Length > MAXIMUM_BYTES)
                throw new InvalidDataException("Invalid image size.");
            cancellationToken.ThrowIfCancellationRequested();
            var options = new DecoderOptions { MaxFrames = 1, SkipMetadata = true };
            var info = Image.Identify(options, data);
            if ((long)info.Width * info.Height > maximum_pixels)
                throw new InvalidDataException("Image dimensions exceed the limit.");
            using var image = Image.Load<Rgba32>(options, data);
            image.Mutate(c => c.AutoOrient().Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(128, 128) }));
            var pixels = new List<uint>(image.Width * image.Height);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var pixel in accessor.GetRowSpan(y))
                        if (pixel.A >= 128)
                            pixels.Add(0xff000000u | (uint)pixel.R << 16 | (uint)pixel.G << 8 | pixel.B);
                }
            });
            if (pixels.Count == 0)
                throw new InvalidDataException("The image is transparent.");
            var quantized = QuantizerCelebi.Quantize(pixels.ToArray(), 128);
            // The older C# scorer returns Google Blue if every candidate is grey/dark. A fallback must come from this image instead.
            uint[] candidates = Scorer.Score(quantized).Where(quantized.ContainsKey).Take(4).ToArray();
            if (candidates.Length == 0)
                candidates = new[] { quantized.MaxBy(p => p.Value).Key };
            using var thumbnail = new MemoryStream();
            image.SaveAsPng(thumbnail);
            return new ImageThemeAnalysis(candidates, thumbnail.ToArray());
        }

        public static CustomThemeDefinition Generate(uint seed, CustomThemeDefinition current, ThemeInterfaceSettings appearance,
                                                     bool light, ImageThemeStyle style = ImageThemeStyle.KeepCurrent)
        {
            if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style));
            var hct = Hct.FromInt(seed);
            var palette = ThemeBuilder.CreateFromSourceColor(RgbColor.FromArgb(unchecked((int)seed)))
                                      .WithMode(light ? MaterialTheming.ThemeMode.Light : MaterialTheming.ThemeMode.Dark)
                                      .WithVariant(hct.Chroma < 5 ? Variant.Monochrome : Variant.Content)
                                      .WithContrastLevel(ContrastLevel.Medium)
                                      .Build();
            var result = current.DeepClone();
            result.BaseTheme = light ? ThemeMode.Light : ThemeMode.Dark;
            result.Interface = appearance.DeepClone();

            foreach (var token in Enum.GetValues<OverlayColour>())
                result.SetOverride(token, colour(token switch
                {
                    OverlayColour.Colour0 or OverlayColour.Colour1 or OverlayColour.Highlight1 => palette.Primary,
                    OverlayColour.Colour2 or OverlayColour.Colour3 => palette.Primary,
                    OverlayColour.Colour4 => palette.PrimaryContainer,
                    OverlayColour.Content1 => palette.OnSurface,
                    OverlayColour.Content2 => palette.OnSurfaceVariant,
                    OverlayColour.Light1 or OverlayColour.Light2 => palette.Secondary,
                    OverlayColour.Light3 => palette.Tertiary,
                    OverlayColour.Light4 => palette.SurfaceContainerHighest,
                    OverlayColour.Foreground1 => palette.Outline,
                    OverlayColour.Background1 or OverlayColour.Dark1 => palette.SurfaceContainerHighest,
                    OverlayColour.Background2 or OverlayColour.Dark2 => palette.SurfaceContainerHigh,
                    OverlayColour.Background3 or OverlayColour.Dark3 => palette.SurfaceContainer,
                    OverlayColour.Background4 or OverlayColour.Dark4 => palette.SurfaceContainerLow,
                    OverlayColour.Background5 or OverlayColour.Dark5 => palette.Surface,
                    _ => palette.SurfaceContainerLowest,
                }));

            // Replace scoped colours too, otherwise an existing theme's local overrides hide the generated palette.
            // Geometry, mod-type colours, interface preferences and extension fields are retained independently.
            foreach (var context in Enum.GetValues<ThemeSurfaceContext>())
                foreach (var role in Enum.GetValues<SurfaceColourRole>())
                    result.SetContextOverride(context, role, colour(role switch
                    {
                        SurfaceColourRole.SurfaceBackground => palette.Surface,
                        SurfaceColourRole.SurfaceElevated => palette.SurfaceContainer,
                        SurfaceColourRole.Accent => palette.Primary,
                        SurfaceColourRole.TextPrimary => palette.OnSurface,
                        SurfaceColourRole.TextSecondary => palette.OnSurfaceVariant,
                        SurfaceColourRole.ControlIdle => palette.SurfaceContainerHigh,
                        SurfaceColourRole.ControlHover or SurfaceColourRole.ControlSelected => palette.SurfaceContainerHighest,
                        SurfaceColourRole.HeaderBackground => palette.SurfaceContainerLow,
                        _ => palette.SurfaceContainerLowest,
                    }));

            // Retain a little of each footer action's established identity instead of making all three identical.
            var originalColours = new OsuColour();
            foreach (var (context, originalAccent) in new[]
            {
                (ThemeSurfaceContext.FooterMods, originalColours.Lime1),
                (ThemeSurfaceContext.FooterRandom, originalColours.Blue1),
                (ThemeSurfaceContext.FooterOptions, originalColours.Purple1),
            })
            {
                var accent = tintAtSurfaceTone(palette.Primary, originalAccent, 0.25f);
                result.SetContextOverride(context, SurfaceColourRole.Accent, accent);
                result.SetContextOverride(context, SurfaceColourRole.ControlIdle,
                    tintAtSurfaceTone(palette.SurfaceContainerHigh, accent, 0.12f));
                result.SetContextOverride(context, SurfaceColourRole.ControlHover,
                    tintAtSurfaceTone(palette.SurfaceContainerHighest, accent, 0.18f));
                result.SetContextOverride(context, SurfaceColourRole.ControlSelected, accent);
            }

            if (style != ImageThemeStyle.KeepCurrent)
            {
                bool rounded = style == ImageThemeStyle.Rounded;
                result.Geometry.ButtonRadius = rounded ? 12 : 0;
                result.Geometry.InputRadius = rounded ? 10 : 0;
                result.Geometry.CardRadius = rounded ? 18 : 0;
                result.Geometry.PanelRadius = rounded ? 18 : 0;
                result.Geometry.PopupRadius = rounded ? 20 : 0;
                result.Geometry.TopBarRadius = rounded ? 18 : 0;
                result.Geometry.TopBarVariant = rounded ? TopBarVariant.Floating : TopBarVariant.Flat;
                result.Geometry.EnableGradients = rounded;
                result.Interface.DisableShear = true;
            }
            return result;
        }

        private static Colour4 tintAtSurfaceTone(RgbColor surface, Colour4 tint, float amount)
        {
            // Linear-light blending with a bright accent lifts dark fills dramatically (T21 -> T40).
            // Keep the mixed hue/chroma, but restore the surface's perceptual lightness.
            Colour4 mixed = Interpolation.ValueAt<Colour4>(amount, colour(surface), tint, 0, 1);
            uint argb = 0xff000000u | (uint)Math.Round(mixed.R * 255) << 16
                | (uint)Math.Round(mixed.G * 255) << 8 | (uint)Math.Round(mixed.B * 255);
            var hct = HctColor.FromRgbColor(RgbColor.FromArgb(unchecked((int)argb)));
            return colour(HctColor.From(hct.Hue, hct.Chroma, HctColor.FromRgbColor(surface).Tone).ToRgbColor());
        }

        private static Colour4 colour(RgbColor rgb) => new Colour4(rgb.Red, rgb.Green, rgb.Blue, (byte)255);
    }

    public sealed class ImageThemeAnalysis
    {
        public IReadOnlyList<uint> Colours { get; }
        public byte[] Thumbnail { get; }
        public ImageThemeAnalysis(uint[] colours, byte[] thumbnail) { Colours = colours; Thumbnail = thumbnail; }
    }
}
