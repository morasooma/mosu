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
            var textureUpload = textureStore?.Get(name);

            if (textureUpload == null)
                return null!;

            return processTextureUpload(textureUpload);
        }

        public async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            if (textureStore == null)
                return null!;

            var textureUpload = await textureStore.GetAsync(name, cancellationToken).ConfigureAwait(false);

            if (textureUpload == null)
                return null!;

            return await Task.Run(() => processTextureUpload(textureUpload), cancellationToken).ConfigureAwait(false);
        }

        private TextureUpload processTextureUpload(TextureUpload textureUpload)
        {
            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // The original texture upload will no longer be returned or used.
            textureUpload.Dispose();

            Size size = image.Size;

            int targetWidth = size.Width;
            int targetHeight = size.Height;

            if ((double)size.Width / size.Height > 16.0 / 9.0)
            {
                // Too wide, crop width
                targetWidth = (int)Math.Round(size.Height * 16.0 / 9.0);
            }
            else
            {
                // Too tall, crop height
                targetHeight = (int)Math.Round(size.Width * 9.0 / 16.0);
            }

            Rectangle cropRectangle = new Rectangle(
                (size.Width - targetWidth) / 2,
                (size.Height - targetHeight) / 2,
                targetWidth,
                targetHeight
            );

            // Target dimensions for high quality downscaling.
            // 512x288 is a standard 16:9 thumbnail size.
            const int max_width = 512;
            const int max_height = 288;

            image.Mutate(i =>
            {
                i.Crop(cropRectangle);
                if (targetWidth > max_width)
                {
                    i.Resize(new Size(max_width, max_height));
                }
            });

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => textureStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => textureStore?.GetAvailableResources() ?? Array.Empty<string>();
    }
}
