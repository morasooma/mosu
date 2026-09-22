// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The line a beam device holds, as the player sees it.
    /// </summary>
    /// <remarks>
    /// Three states, and the middle one is the point: a thin bright thread means "this is where it will
    /// be", the full width means "it is lethal now". Without the warning a beam is a death to memorise
    /// rather than an obstacle to read.
    /// </remarks>
    internal partial class WorldBeam : CompositeDrawable
    {
        private readonly Box line;
        private readonly Box core;

        private float length;
        private float width;

        public WorldBeam(OsuColour colours)
        {
            // Anchored at the middle of the room like every other world drawable. Without this the beam
            // was laid out from the room's top left corner, so it was drawn half a room away from the
            // device holding it — which is why it looked like beams were not drawn at all.
            Anchor = Anchor.Centre;
            Origin = Anchor.CentreLeft;
            AutoSizeAxes = Axes.None;

            InternalChildren = new Drawable[]
            {
                line = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Purple2.Opacity(0.5f),
                },
                core = new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    Colour = colours.PurpleLight,
                },
            };
        }

        /// <summary>
        /// Points the beam and sets how dangerous it currently looks.
        /// </summary>
        public void SyncFrom(Vector2 origin, float angleDegrees, float beamLength, float beamWidth, bool active, bool warning)
        {
            Position = origin;
            Rotation = angleDegrees;

            if (length != beamLength || width != beamWidth)
            {
                length = beamLength;
                width = beamWidth;
                Size = new Vector2(beamLength, beamWidth);
            }

            // Off is drawn as nothing rather than as a faint line: a course whose beams are always visible
            // reads as one long wall.
            float target = active ? 1 : warning ? 0.85f : 0;

            if (Alpha != target)
                this.FadeTo(target, active ? 60 : 140, Easing.OutQuint);

            core.Height = active ? beamWidth : 3;
            line.Alpha = active ? 1 : 0;
        }
    }
}
