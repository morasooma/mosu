// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// How the mobs of one spawn zone should look.
    /// </summary>
    /// <remarks>
    /// A mob's appearance belongs to the zone that spawns it, but the simulation deals only in rules
    /// and knows nothing about textures. So the view asks for this by zone id when a mob appears,
    /// instead of the look being carried through the simulation.
    /// </remarks>
    internal readonly record struct MobAppearance(Texture? Texture, float Opacity)
    {
        public static MobAppearance None => new MobAppearance(null, 1);

        /// <summary>
        /// The size <paramref name="texture"/> should be drawn at to fill as much of <paramref name="box"/>
        /// as it can without being stretched.
        /// </summary>
        /// <remarks>
        /// What <see cref="osu.Framework.Graphics.FillMode.Fit"/> does, except that the result is the
        /// drawable's own size — so an image that does not fill the box is placed by its anchor rather than
        /// being centred inside it, and a mob keeps standing on the ground whatever shape its image is.
        /// </remarks>
        public static Vector2 FitInto(Texture texture, Vector2 box)
        {
            float width = Math.Max(1, texture.DisplayWidth);
            float height = Math.Max(1, texture.DisplayHeight);
            float scale = Math.Min(box.X / width, box.Y / height);

            return new Vector2(width * scale, height * scale);
        }
    }
}
