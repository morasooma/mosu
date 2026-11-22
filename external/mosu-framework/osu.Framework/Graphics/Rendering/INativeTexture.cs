// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Visualisation;

namespace osu.Framework.Graphics.Rendering
{
    public interface INativeTexture : IDisposable
    {
        /// <summary>
        /// The renderer that created this texture.
        /// </summary>
        IRenderer Renderer { get; }

        /// <summary>
        /// An identifier for this texture, to show up in the <see cref="TextureVisualiser"/>.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Maximum texture size in any direction.
        /// </summary>
        int MaxSize { get; }

        /// <summary>
        /// The width of the texture.
        /// </summary>
        int Width { get; set; }

        /// <summary>
        /// The height of the texture.
        /// </summary>
        int Height { get; set; }

        /// <summary>
        /// The level of the texture to display, or null to display all levels automatically.
        /// This is used by the texture visualiser tool for viewing textures on a specific mipmap level.
        /// </summary>
        int? MipLevel { get; set; }

        /// <summary>
        /// Whether the texture is in a usable state.
        /// </summary>
        bool Available { get; }

        /// <summary>
        /// The total number of times this texture has been bound, ever.
        /// </summary>
        ulong TotalBindCount { get; internal set; }

        /// <summary>
        /// By default, texture uploads are queued for upload at the beginning of each frame, allowing loading them ahead of time.
        /// When this is true, this will be bypassed and textures will only be uploaded on use. Should be set for every-frame texture uploads
        /// to avoid overloading the global queue.
        /// </summary>
        bool BypassTextureUploadQueueing { get; set; }

        /// <summary>
        /// Whether the latest data has been uploaded.
        /// </summary>
        bool UploadComplete { get; }

        /// <summary>
        /// Flush any unprocessed uploads without actually uploading.
        /// </summary>
        void FlushUploads();

        /// <summary>
        /// Sets the pixel data of the texture.
        /// </summary>
        /// <param name="upload">The <see cref="ITextureUpload"/> containing the data.</param>
        void SetData(ITextureUpload upload);

        /// <summary>
        /// Uploads this texture.
        /// </summary>
        /// <returns>Whether any uploads occurred.</returns>
        bool Upload();

        /// <summary>
        /// Performs queued upload work within a pixel budget where supported. Implementations which
        /// cannot split uploads retain the synchronous <see cref="Upload()"/> behaviour.
        /// Drawing a texture still completes all remaining uploads via <see cref="Upload()"/>.
        /// </summary>
        /// <param name="maxPixels">The desired maximum number of pixels to upload, at least one.</param>
        /// <returns>The number of pixels uploaded. An indivisible upload or row may exceed the budget.</returns>
        int Upload(int maxPixels) => Upload() ? (int)Math.Min(int.MaxValue, (long)Width * Height) : 0;

        /// <summary>
        /// The size of this texture in bytes.
        /// </summary>
        int GetByteSize();
    }
}
