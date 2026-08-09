// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeArenaChangePiece : CompositeDrawable
    {
        private readonly Container rectangle;
        private readonly Box background;

        public DodgeArenaChangePiece()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = rectangle = new Container
            {
                Origin = Anchor.Centre,
                Masking = true,
                BorderThickness = 2,
                BorderColour = Color4.White,
                Child = background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0.08f,
                },
            };
        }

        public void UpdateFrom(DodgeArenaChange change)
        {
            rectangle.Position = change.TargetPosition + change.TargetSize / 2;
            rectangle.Size = change.TargetSize;
            rectangle.Rotation = change.TargetRotation;
            rectangle.BorderColour = change.OutlineColour.Opacity(change.BorderOpacity);
            background.Colour = change.Colour;
            background.Alpha = change.Opacity * 0.2f;
        }
    }
}
