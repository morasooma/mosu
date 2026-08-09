// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    internal partial class DodgeDirectionIndicator : CompositeDrawable
    {
        public override bool RemoveWhenNotAlive => false;

        public DodgeDirectionIndicator()
        {
            Origin = Anchor.Centre;
            Size = new Vector2(14);
            Alpha = 0;
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(10, 2),
                    Rotation = 45,
                    Colour = Color4.White,
                },
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(10, 2),
                    Rotation = -45,
                    Colour = Color4.White,
                },
            };
        }
    }
}
