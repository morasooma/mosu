// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Beatmaps
{
    public class BeatmapLegacyPreviewBackgroundTextureLoaderStore : IResourceStore<TextureUpload>
    {
        private readonly IResourceStore<TextureUpload>? textureStore;

        public BeatmapLegacyPreviewBackgroundTextureLoaderStore(IResourceStore<TextureUpload>? textureStore)
        {
            this.textureStore = textureStore;
        }

        public void Dispose()
        {
            textureStore?.Dispose();
        }

        public TextureUpload Get(string name)
        {
            CarouselPreviewDecodeLimiter.Semaphore.Wait();

            try
            {
                var (resourceName, resolutionPercent, classic) = CarouselPreviewTextureRequest.ParseLegacy(name);

                using (var stream = textureStore?.GetStream(resourceName))
                {
                    if (stream != null)
                        return processImage(decodePreview(stream, resolutionPercent, classic), resolutionPercent, alreadyScaled: true, classic);
                }

                var textureUpload = textureStore?.Get(resourceName);

                if (textureUpload == null)
                    return null!;

                return processTextureUpload(textureUpload, resolutionPercent, classic);
            }
            catch (ImageFormatException)
            {
                return null!;
            }
            finally
            {
                CarouselPreviewDecodeLimiter.Semaphore.Release();
            }
        }

        public async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            if (textureStore == null)
                return null!;

            await CarouselPreviewDecodeLimiter.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var (resourceName, resolutionPercent, classic) = CarouselPreviewTextureRequest.ParseLegacy(name);

                using (var stream = textureStore.GetStream(resourceName))
                {
                    if (stream != null)
                    {
                        var image = await decodePreviewAsync(stream, resolutionPercent, classic, cancellationToken).ConfigureAwait(false);
                        return processImage(image, resolutionPercent, alreadyScaled: true, classic);
                    }
                }

                var textureUpload = await textureStore.GetAsync(resourceName, cancellationToken).ConfigureAwait(false);

                if (textureUpload == null)
                    return null!;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return processTextureUpload(textureUpload, resolutionPercent, classic);
                }
                catch
                {
                    textureUpload.Dispose();
                    throw;
                }
            }
            catch (ImageFormatException)
            {
                return null!;
            }
            finally
            {
                CarouselPreviewDecodeLimiter.Semaphore.Release();
            }
        }

        private static Image<Rgba32> decodePreview(Stream stream, int resolutionPercent, bool classic)
        {
            Size targetSize = getDecodeTargetSize(Image.Identify(stream).Size, resolutionPercent, classic);
            stream.Position = 0;
            return Image.Load<Rgba32>(createDecoderOptions(targetSize), stream);
        }

        private static async Task<Image<Rgba32>> decodePreviewAsync(Stream stream, int resolutionPercent, bool classic, CancellationToken cancellationToken)
        {
            var info = await Image.IdentifyAsync(stream, cancellationToken).ConfigureAwait(false);
            Size targetSize = getDecodeTargetSize(info.Size, resolutionPercent, classic);
            stream.Position = 0;
            return await Image.LoadAsync<Rgba32>(createDecoderOptions(targetSize), stream, cancellationToken).ConfigureAwait(false);
        }

        private static Size getDecodeTargetSize(Size sourceSize, int resolutionPercent, bool classic)
        {
            double scale = Math.Min(1, Math.Max(512 / (double)sourceSize.Width, (classic ? 384 : 288) / (double)sourceSize.Height)) * resolutionPercent / 100;
            return new Size(
                Math.Max(1, (int)Math.Round(sourceSize.Width * scale)),
                Math.Max(1, (int)Math.Round(sourceSize.Height * scale)));
        }

        private static DecoderOptions createDecoderOptions(Size targetSize) => new DecoderOptions
        {
            SkipMetadata = true,
            TargetSize = targetSize,
        };

        private static TextureUpload processTextureUpload(TextureUpload textureUpload, int resolutionPercent, bool classic)
        {
            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // The original texture upload will no longer be returned or used.
            textureUpload.Dispose();

            return processImage(image, resolutionPercent, alreadyScaled: false, classic);
        }

        private static TextureUpload processImage(Image<Rgba32> image, int resolutionPercent, bool alreadyScaled, bool classic)
        {
            Size size = image.Size;

            int targetWidth = size.Width;
            int targetHeight = size.Height;

            double aspect = classic ? 4.0 / 3.0 : 16.0 / 9.0;
            if ((double)size.Width / size.Height > aspect)
                targetWidth = Math.Max(1, Math.Min(size.Width, (int)Math.Round(size.Height * aspect)));
            else
                targetHeight = Math.Max(1, Math.Min(size.Height, (int)Math.Round(size.Width / aspect)));

            Rectangle cropRectangle = new Rectangle(
                (size.Width - targetWidth) / 2,
                (size.Height - targetHeight) / 2,
                targetWidth,
                targetHeight
            );

            // Crop classic thumbnails directly from the source, never via a 16:9 intermediate.
            double scale = Math.Min(1, 512.0 / targetWidth) * resolutionPercent / 100;
            int outputWidth = alreadyScaled ? targetWidth : classic
                ? Math.Max(1, (int)Math.Round(targetWidth * scale))
                : Math.Max(1, Math.Min(targetWidth, 512) * resolutionPercent / 100);
            int outputHeight = alreadyScaled ? targetHeight : classic
                ? Math.Max(1, (int)Math.Round(targetHeight * scale))
                : Math.Max(1, Math.Min(targetHeight, 288) * resolutionPercent / 100);

            image.Mutate(i =>
            {
                i.Crop(cropRectangle);
                if (targetWidth != outputWidth || targetHeight != outputHeight)
                    i.Resize(new Size(outputWidth, outputHeight));
            });

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => textureStore?.GetStream(CarouselPreviewTextureRequest.ParseLegacy(name).ResourceName);

        public IEnumerable<string> GetAvailableResources() => textureStore?.GetAvailableResources() ?? Array.Empty<string>();
    }
}
