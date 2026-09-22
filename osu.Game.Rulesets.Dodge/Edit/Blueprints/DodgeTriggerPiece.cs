// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    /// <summary>
    /// Small editor-only marker for a time trigger. The marker position is not used by gameplay.
    /// </summary>
    public partial class DodgeTriggerPiece : CompositeDrawable
    {
        private const float marker_size = 18;

        private readonly Box marker;

        public RectangleF GamefieldBounds { get; private set; }

        public DodgeTriggerPiece()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = marker = new Box
            {
                Origin = Anchor.Centre,
                Size = new Vector2(marker_size),
                Rotation = 45,
                Colour = Color4.HotPink,
                Alpha = 0.8f,
            };
        }

        public void UpdateFrom(DodgeTrigger trigger)
        {
            marker.Position = trigger.Position;
            marker.Colour = trigger.Colour;
            marker.Alpha = 0.55f + trigger.Strength * 0.45f;
            GamefieldBounds = new RectangleF(
                trigger.Position.X - marker_size / 2,
                trigger.Position.Y - marker_size / 2,
                marker_size,
                marker_size);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => marker.ReceivePositionalInputAt(screenSpacePos);
    }
}
