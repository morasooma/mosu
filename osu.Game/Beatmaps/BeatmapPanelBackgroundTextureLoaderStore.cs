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
    // Implementation of this class is based off of `MaxDimensionLimitedTextureLoaderStore`.
    // If issues are found it's worth checking to make sure similar issues exist there.
    public class BeatmapPanelBackgroundTextureLoaderStore : IResourceStore<TextureUpload>
    {
        // The aspect ratio of SetPanelBackground at its maximum size (very tall window).
        private const float minimum_display_ratio = 512 / 80f;

        // TextureStore uses ScaleAdjust = 2, so this provides a full-resolution 512px-wide card
        // without uploading the mostly invisible full-width source background to the GPU.
        private const int maximum_output_width = 1024;

        private readonly IResourceStore<TextureUpload>? textureStore;

        public BeatmapPanelBackgroundTextureLoaderStore(IResourceStore<TextureUpload>? textureStore)
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
                (string resourceName, int resolutionPercent) = CarouselPreviewTextureRequest.Parse(name);

                using (var stream = textureStore?.GetStream(resourceName))
                {
                    if (stream != null)
                        return processImage(decodePreview(stream, resolutionPercent), resolutionPercent, alreadyScaled: true);
                }

                var textureUpload = textureStore?.Get(resourceName);

                // NRT not enabled on framework side classes (IResourceStore / TextureLoaderStore), welp.
                if (textureUpload == null)
                    return null!;

                return limitTextureUploadSize(textureUpload, resolutionPercent);
            }
            finally
            {
                CarouselPreviewDecodeLimiter.Semaphore.Release();
            }
        }

        public async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            // NRT not enabled on framework side classes (IResourceStore / TextureLoaderStore), welp.
            if (textureStore == null)
                return null!;

            await CarouselPreviewDecodeLimiter.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                (string resourceName, int resolutionPercent) = CarouselPreviewTextureRequest.Parse(name);

                using (var stream = textureStore.GetStream(resourceName))
                {
                    if (stream != null)
                    {
                        var image = await decodePreviewAsync(stream, resolutionPercent, cancellationToken).ConfigureAwait(false);
                        return processImage(image, resolutionPercent, alreadyScaled: true);
                    }
                }

                var textureUpload = await textureStore.GetAsync(resourceName, cancellationToken).ConfigureAwait(false);

                if (textureUpload == null)
                    return null!;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return limitTextureUploadSize(textureUpload, resolutionPercent);
                }
                catch
                {
                    textureUpload.Dispose();
                    throw;
                }
            }
            finally
            {
                CarouselPreviewDecodeLimiter.Semaphore.Release();
            }
        }

        private static Image<Rgba32> decodePreview(Stream stream, int resolutionPercent)
        {
            Size targetSize = getDecodeTargetSize(Image.Identify(stream).Size, resolutionPercent);
            stream.Position = 0;
            return Image.Load<Rgba32>(createDecoderOptions(targetSize), stream);
        }

        private static async Task<Image<Rgba32>> decodePreviewAsync(Stream stream, int resolutionPercent, CancellationToken cancellationToken)
        {
            var info = await Image.IdentifyAsync(stream, cancellationToken).ConfigureAwait(false);
            Size targetSize = getDecodeTargetSize(info.Size, resolutionPercent);
            stream.Position = 0;
            return await Image.LoadAsync<Rgba32>(createDecoderOptions(targetSize), stream, cancellationToken).ConfigureAwait(false);
        }

        private static Size getDecodeTargetSize(Size sourceSize, int resolutionPercent)
        {
            int outputWidth = Math.Max(1, Math.Min(sourceSize.Width, maximum_output_width) * resolutionPercent / 100);
            double scale = outputWidth / (double)sourceSize.Width;
            return new Size(outputWidth, Math.Max(1, (int)Math.Round(sourceSize.Height * scale)));
        }

        private static DecoderOptions createDecoderOptions(Size targetSize) => new DecoderOptions
        {
            SkipMetadata = true,
            TargetSize = targetSize,
        };

        private TextureUpload limitTextureUploadSize(TextureUpload textureUpload, int resolutionPercent)
        {
            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // The original texture upload will no longer be returned or used.
            textureUpload.Dispose();

            return processImage(image, resolutionPercent, alreadyScaled: false);
        }

        private static TextureUpload processImage(Image<Rgba32> image, int resolutionPercent, bool alreadyScaled)
        {
            Size size = image.Size;

            // Assume that panel backgrounds are always displayed using `FillMode.Fill`.
            // Also assume that all backgrounds are wider than they are tall, so the
            // fill is always going to be based on width.
            //
            // We need to include enough height to make this work for all ratio panels are displayed at.
            int usableHeight = (int)Math.Ceiling(size.Width * 1 / minimum_display_ratio);

            usableHeight = Math.Min(size.Height, usableHeight);

            // Crop the centre region of the background for now.
            Rectangle cropRectangle = new Rectangle(
                0,
                (size.Height - usableHeight) / 2,
                size.Width,
                usableHeight
            );

            double outputScale = alreadyScaled ? 1 : Math.Min(1, maximum_output_width / (double)size.Width) * resolutionPercent / 100;
            int outputWidth = Math.Max(1, (int)Math.Round(size.Width * outputScale));
            int outputHeight = Math.Max(1, (int)Math.Round(usableHeight * outputScale));

            image.Mutate(i =>
            {
                i.Crop(cropRectangle);

                if (size.Width != outputWidth || usableHeight != outputHeight)
                    i.Resize(new Size(outputWidth, outputHeight));
            });

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => textureStore?.GetStream(CarouselPreviewTextureRequest.Parse(name).ResourceName);

        public IEnumerable<string> GetAvailableResources() => textureStore?.GetAvailableResources() ?? Array.Empty<string>();
    }
}
