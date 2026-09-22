// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Draws a projectile. Movement, range and damage belong to
    /// <see cref="ProjectileState"/> — this only follows it.
    /// </summary>
    internal partial class WorldProjectile : CircularContainer
    {
        public WorldProjectile(OsuColour colours)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(12);
            Masking = true;
            BorderThickness = 2;
            BorderColour = colours.Red0;
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Red2 };
        }

        public void SyncFrom(ProjectileState projectile) => Position = projectile.Position;

        /// <summary>
        /// Takes the projectile out of play, fading it rather than deleting it mid-flight.
        /// </summary>
        /// <remarks>
        /// A shot that vanishes on the frame its range runs out reads as a rendering glitch; one that
        /// fades reads as a shot that ran out. The simulation has already stopped counting it either
        /// way — nothing here can hurt anybody.
        /// </remarks>
        public void Retire() =>
            this.FadeOut(140, Easing.OutQuint).ScaleTo(0.55f, 140, Easing.OutQuint).Expire();
    }
}
