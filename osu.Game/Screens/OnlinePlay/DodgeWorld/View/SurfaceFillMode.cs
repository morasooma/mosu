// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// How a texture covers a surface.
    /// </summary>
    /// <remarks>
    /// The first three values match osu!framework's <see cref="osu.Framework.Graphics.FillMode"/>,
    /// which is what worlds published before tiling existed stored, so their surfaces keep their
    /// appearance.
    /// </remarks>
    internal enum SurfaceFillMode
    {
        /// <summary>Fills the surface, distorting the image.</summary>
        Stretch = 0,

        /// <summary>Covers the surface, keeping the aspect ratio and cropping the overflow.</summary>
        Fill = 1,

        /// <summary>Fits inside the surface, keeping the aspect ratio and leaving the panel visible.</summary>
        Fit = 2,

        /// <summary>
        /// Repeats the image across the surface at one texture pixel per world unit, so one small
        /// image covers a room of any size at a constant scale.
        /// </summary>
        Tile = 3,
    }
}
