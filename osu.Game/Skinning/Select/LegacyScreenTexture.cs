// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// Draws the stable song-select top texture in native skin coordinates.
    /// The visible texture width follows the actual logical viewport (1024 at 4:3,
    /// 1366 at 16:9) and a clamped texture extends its rightmost pixels when required.
    /// </summary>
    internal partial class LegacyScreenTexture : Sprite
    {
        protected override void Update()
        {
            base.Update();

            if (Texture == null)
            {
                Size = Vector2.Zero;
                return;
            }

            Size = GetDrawSize(Texture.DisplayHeight, Parent?.ChildSize.X ?? 0);
            TextureRelativeSizeAxes = Axes.None;
            // TextureRectangle specifies where the entire texture is placed in the sprite,
            // not the region to sample. Using the viewport width here stretches the artwork.
            TextureRectangle = GetTextureRectangle(Texture.DisplayWidth, Texture.DisplayHeight);
        }

        internal static Vector2 GetDrawSize(float textureHeight, float viewportWidth)
            => textureHeight <= 0 || viewportWidth <= 0 ? Vector2.Zero : new Vector2(viewportWidth, textureHeight);

        internal static RectangleF GetTextureRectangle(float textureWidth, float textureHeight)
            => new RectangleF(0, 0, Math.Max(0, textureWidth), Math.Max(0, textureHeight));
    }
}
