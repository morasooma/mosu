// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Where a passage in another room puts the player down, drawn in the editor only.
    /// </summary>
    /// <remarks>
    /// An arrival point is stored on the passage that leads here, which lives in a room the author is not
    /// looking at — so until this existed, the one thing the author needed to see about it was the one
    /// thing they could not: where it actually is. It names the room the player is coming from, because a
    /// room with two ways in has two of these and they are otherwise identical.
    /// </remarks>
    internal partial class ArrivalMarker : CompositeDrawable
    {
        /// <param name="colours">The game palette.</param>
        /// <param name="caption">Which passage of which room lands here.</param>
        /// <param name="authored">
        /// Whether the author placed this point by hand. A computed one is drawn in another colour, because
        /// the difference between «this is where I said» and «this is what the rule worked out» is the
        /// difference between a decision and a guess.
        /// </param>
        public ArrivalMarker(OsuColour colours, string caption, bool authored = true)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(46);
            Colour = authored ? colours.Yellow : colours.Blue1;
            Alpha = authored ? 1 : 0.75f;

            InternalChildren = new Drawable[]
            {
                new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 3,
                    BorderColour = colours.Yellow,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Yellow.Opacity(0.14f),
                    },
                },
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(3, 22),
                    Colour = colours.Yellow,
                },
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(22, 3),
                    Colour = colours.Yellow,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Y = 6,
                    Text = caption.ToUpperInvariant(),
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                    Colour = colours.Yellow,
                },
            };
        }
    }
}
