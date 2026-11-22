// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using osu.Framework.Graphics.Performance;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics.Textures
{
    public partial class TextureAtlas
    {
        // We are adding an extra padding on top of the padding required by
        // mipmap blending in order to support smooth edges without antialiasing which requires
        // inflating texture rectangles.
        internal const int PADDING = (1 << IRenderer.MAX_MIPMAP_LEVELS) * Sprite.MAX_EDGE_SMOOTHNESS;
        internal const int WHITE_PIXEL_SIZE = 1;

        private readonly List<RectangleI> subTextureBounds = new List<RectangleI>();
        private Texture? atlasTexture;

        private readonly IRenderer renderer;
        private readonly int atlasWidth;
        private readonly int atlasHeight;

        private int maxFittableWidth => atlasWidth - PADDING * 2;
        private int maxFittableHeight => atlasHeight - PADDING * 2;

        private Vector2I currentPosition;

        /// <summary>
        /// When <see cref="GraphicsPerformanceSettings.AtlasRegionReuse"/> is enabled, this allocator
        /// replaces the append-only shelf cursor, allowing freed regions to be reused.
        /// </summary>
        private AtlasRegionAllocator? regionAllocator;

        /// <summary>
        /// Whether the region allocator is active for this atlas.
        /// </summary>
        private bool useRegionAllocator => GraphicsPerformanceSettings.AtlasRegionReuse;

        internal TextureWhitePixel WhitePixel
        {
            get
            {
                if (atlasTexture == null)
                    Reset();

                Debug.Assert(atlasTexture != null, "Atlas texture should not be null after Reset().");

                return new TextureWhitePixel(atlasTexture);
            }
        }

        private readonly bool manualMipmaps;
        private readonly TextureFilteringMode filteringMode;
        private readonly object textureRetrievalLock = new object();

        public TextureAtlas(IRenderer renderer, int width, int height, bool manualMipmaps = false, TextureFilteringMode filteringMode = TextureFilteringMode.Linear)
        {
            this.renderer = renderer;
            atlasWidth = width;
            atlasHeight = height;
            this.manualMipmaps = manualMipmaps;
            this.filteringMode = filteringMode;
        }

        private int exceedCount;

        /// <summary>
        /// Creates a new empty texture.
        /// </summary>
        /// <remarks>
        /// Existing textures created via <see cref="Add"/> are not cleared and remain accessible by usages.
        /// </remarks>
        public void Reset()
        {
            lock (textureRetrievalLock)
            {
                subTextureBounds.Clear();
                currentPosition = Vector2I.Zero;

                // We pass PADDING/2 as opposed to PADDING such that the padded region of each individual texture
                // occupies half of the padded space.
                atlasTexture = new BackingAtlasTexture(renderer, atlasWidth, atlasHeight, manualMipmaps, filteringMode, PADDING / 2);

                RectangleI bounds = new RectangleI(0, 0, WHITE_PIXEL_SIZE, WHITE_PIXEL_SIZE);
                subTextureBounds.Add(bounds);

                using (var whiteTex = new TextureRegion(atlasTexture, bounds, WrapMode.Repeat, WrapMode.Repeat))
                    // Generate white padding as if the white texture was wrapped, even though it isn't
                    whiteTex.SetData(new TextureUpload(new Image<Rgba32>(SixLabors.ImageSharp.Configuration.Default, whiteTex.Width, whiteTex.Height, new Rgba32(Vector4.One))));

                currentPosition = new Vector2I(PADDING + WHITE_PIXEL_SIZE, PADDING);

                if (useRegionAllocator)
                {
                    // The usable area starts after the white pixel and its padding.
                    var usableBounds = new RectangleI(PADDING + WHITE_PIXEL_SIZE, PADDING, atlasWidth - (PADDING + WHITE_PIXEL_SIZE), atlasHeight - PADDING);

                    // Always create a fresh allocator. Old TextureRegions hold a closure over the
                    // previous allocator instance, so their OnRelease callbacks will free into the
                    // orphaned allocator rather than corrupting the new one.
                    if (regionAllocator == null)
                        Logger.Log($"TextureAtlas initialised with region allocator ({atlasWidth}x{atlasHeight})", LoggingTarget.Performance);

                    regionAllocator = new AtlasRegionAllocator(usableBounds);
                }
            }
        }

        /// <summary>
        /// Add (allocate) a new texture in the atlas.
        /// </summary>
        /// <param name="width">The width of the requested texture.</param>
        /// <param name="height">The height of the requested texture.</param>
        /// <param name="wrapModeS">The horizontal wrap mode of the texture.</param>
        /// <param name="wrapModeT">The vertical wrap mode of the texture.</param>
        /// <returns>A texture, or null if the requested size exceeds the atlas' bounds.</returns>
        public Texture? Add(int width, int height, WrapMode wrapModeS = WrapMode.None, WrapMode wrapModeT = WrapMode.None)
        {
            if (!canFitEmptyTextureAtlas(width, height))
                return null;

            lock (textureRetrievalLock)
            {
                Vector2I? position = findPosition(width, height);

                if (position == null)
                    return null;

                Debug.Assert(atlasTexture != null, "Atlas texture should not be null after findPosition().");

                RectangleI bounds = new RectangleI(position.Value.X, position.Value.Y, width, height);

                if (!useRegionAllocator)
                    subTextureBounds.Add(bounds);

                var region = new TextureRegion(atlasTexture, bounds, wrapModeS, wrapModeT);

                if (useRegionAllocator && regionAllocator != null)
                {
                    var allocator = regionAllocator;
                    var capturedBounds = bounds;
                    region.OnRelease = b => allocator.Free(capturedBounds, PADDING);
                }

                return region;
            }
        }

        /// <summary>
        /// Whether or not a texture of the given width and height could be placed into a completely empty texture atlas
        /// </summary>
        /// <param name="width">The width of the texture.</param>
        /// <param name="height">The height of the texture.</param>
        /// <returns>True if the texture could fit an empty texture atlas, false if it could not</returns>
        private bool canFitEmptyTextureAtlas(int width, int height)
        {
            // exceeds bounds in one direction
            if (width > maxFittableWidth || height > maxFittableHeight)
                return false;

            // exceeds bounds in both directions (in this one, we have to account for the white pixel)
            if (width + WHITE_PIXEL_SIZE > maxFittableWidth && height + WHITE_PIXEL_SIZE > maxFittableHeight)
                return false;

            return true;
        }

        /// <summary>
        /// Locates a position in the current texture atlas for a new texture of the given size, or
        /// creates a new texture atlas if there is not enough space in the current one.
        /// </summary>
        /// <param name="width">The width of the requested texture.</param>
        /// <param name="height">The height of the requested texture.</param>
        /// <returns>The position within the texture atlas to place the new texture.</returns>
        private Vector2I? findPosition(int width, int height)
        {
            if (atlasTexture == null)
            {
                Logger.Log($"TextureAtlas initialised ({atlasWidth}x{atlasHeight})", LoggingTarget.Performance);
                Reset();
            }

            if (useRegionAllocator && regionAllocator != null)
            {
                RectangleI? allocated = regionAllocator.Allocate(width, height, PADDING);

                if (allocated == null)
                {
                    Logger.Log($"TextureAtlas size exceeded {++exceedCount} time(s); generating new texture ({atlasWidth}x{atlasHeight})", LoggingTarget.Performance);
                    Reset();
                    allocated = regionAllocator.Allocate(width, height, PADDING);
                }

                if (allocated == null)
                    return null;

                return new Vector2I(allocated.Value.X, allocated.Value.Y);
            }

            if (currentPosition.Y + height + PADDING > atlasHeight)
            {
                Logger.Log($"TextureAtlas size exceeded {++exceedCount} time(s); generating new texture ({atlasWidth}x{atlasHeight})", LoggingTarget.Performance);
                Reset();
            }

            if (currentPosition.X + width + PADDING > atlasWidth)
            {
                int maxY = 0;

                foreach (RectangleI bounds in subTextureBounds)
                    maxY = Math.Max(maxY, bounds.Bottom + PADDING);

                subTextureBounds.Clear();
                currentPosition = new Vector2I(PADDING, maxY);

                return findPosition(width, height);
            }

            var result = currentPosition;
            currentPosition.X += width + PADDING;

            return result;
        }
    }
}
