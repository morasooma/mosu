// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The image an entity is configured to wear: what was stored, and the texture it resolved to.
    /// </summary>
    /// <remarks>
    /// Shared by every entity that can be given a texture, because they all store the same three
    /// fields of the record and resolve them the same way. Only the drawing differs.
    /// </remarks>
    internal sealed class EntityTexture
    {
        /// <summary>
        /// The stored reference: either a file in the world's texture directory or a URL on a
        /// trusted domain. Null when the entity draws its built-in appearance.
        /// </summary>
        public string? Path { get; private set; }

        /// <summary>
        /// Whether the image is filtered when scaled. Off is what pixel art wants.
        /// </summary>
        public bool Smoothing { get; private set; } = true;

        public float Opacity { get; private set; } = 1;

        /// <summary>
        /// The loaded image, once the library has resolved <see cref="Path"/>.
        /// </summary>
        public Texture? Resolved { get; private set; }

        public void SetPath(string? path)
        {
            Path = string.IsNullOrWhiteSpace(path) ? null : path;

            // The old image must go with the old path, or clearing a texture would leave the
            // previous one on screen until something else triggered a reload.
            if (Path == null)
                Resolved = null;
        }

        public void SetResolved(Texture? texture) => Resolved = texture;

        public void SetOpacity(float value) => Opacity = Math.Clamp(value, 0, 1);

        public void ToggleSmoothing() => Smoothing = !Smoothing;

        /// <summary>
        /// Applies stored values, falling back to the defaults for anything the document omits.
        /// </summary>
        public void Read(string? path, float? opacity, bool? smoothing)
        {
            SetPath(path);
            SetOpacity(opacity ?? 1);
            Smoothing = smoothing ?? true;
        }

        public void Clear()
        {
            Path = null;
            Resolved = null;
            Opacity = 1;
            Smoothing = true;
        }
    }
}
