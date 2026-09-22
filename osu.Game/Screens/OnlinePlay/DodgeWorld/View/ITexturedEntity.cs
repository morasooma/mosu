// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Textures;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// An entity the author can dress in an image from the texture library.
    /// </summary>
    /// <remarks>
    /// The editor and the texture library work through this rather than through <c>RoomSurface</c>,
    /// so that floors, NPCs and mobs are all textured by the same buttons and the same loading path.
    /// </remarks>
    internal interface ITexturedEntity
    {
        /// <summary>
        /// The image's stored reference, or null when the entity draws its built-in appearance.
        /// </summary>
        string? TexturePath { get; }

        bool TextureSmoothing { get; }

        float TextureOpacity { get; }

        /// <summary>
        /// Whether the image has to be loaded with a repeating wrap mode, which only a tiling
        /// surface needs. Repeating textures cannot share a texture atlas, so this decides which
        /// store resolves the path.
        /// </summary>
        bool TextureRepeats { get; }

        /// <summary>
        /// A label for the editor describing how the image covers the entity, or null when the
        /// entity offers no choice.
        /// </summary>
        string? TextureFillModeName { get; }

        /// <summary>
        /// How the image covers the entity, as the stored index. Zero where there is no choice.
        /// </summary>
        int TextureFillModeIndex { get; }

        /// <summary>
        /// Steps to the next way of covering the entity. Does nothing where there is no choice.
        /// </summary>
        void CycleTextureFill();

        void SetTexturePath(string? path);

        /// <summary>
        /// Applies the stored configuration, before the image itself has been resolved.
        /// </summary>
        void ReadTexture(string? path, float? opacity, bool? smoothing);

        /// <summary>
        /// Hands over the resolved image, or null to fall back to the built-in appearance.
        /// </summary>
        void SetTexture(Texture? texture);

        void SetTextureOpacity(float value);

        void ToggleTextureSmoothing();
    }
}
