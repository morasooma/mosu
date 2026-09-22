// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Textures;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Textures
{
    public static class DodgeWorldTexture
    {
        /// <summary>
        /// The sword texture shipped with the client, used whenever a configured skin cannot be resolved.
        /// </summary>
        public const string FALLBACK_WEAPON = "DodgeWorld/sword";

        /// <summary>
        /// Identifier under which <see cref="FALLBACK_WEAPON"/> is offered in the texture library.
        /// </summary>
        public const string BUNDLED_SWORD_ASSET_ID = "builtin:sword";

        /// <summary>
        /// Whether a texture can actually be drawn.
        /// </summary>
        /// <remarks>
        /// A texture store hands back placeholder or disposed textures rather than null, so presence
        /// is not enough — a 1x1 or unavailable texture has to be treated as a failed lookup.
        /// </remarks>
        public static bool IsUsable(Texture? texture) => texture is { Available: true, Width: > 1, Height: > 1 };
    }
}
