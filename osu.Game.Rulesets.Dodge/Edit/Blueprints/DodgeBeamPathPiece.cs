// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeBeamPathPiece : CompositeDrawable
    {
        private readonly Container body;
        private readonly Box bodyBackground;
        private readonly Circle startMarker;
        private readonly Circle endMarker;

        private BeamPathState cachedState;
        private bool stateValid;

        public RectangleF GamefieldBounds { get; private set; }

        public DodgeBeamPathPiece()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                body = new Container
                {
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = Color4.White,
                    Child = bodyBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.15f,
                    }
                },
                startMarker = new Circle
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(10),
                    Colour = Color4.White,
                },
                endMarker = new Circle
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(10),
                    Colour = Color4.White,
                },
            };
        }

        public void UpdateFrom(DodgeBeam beam)
        {
            var state = new BeamPathState(
                beam.Position,
                beam.EndPosition,
                beam.BeamWidth,
                beam.Colour,
                beam.OutlineColour,
                beam.OutlineThickness,
                Math.Clamp(beam.Opacity, 0, 1));

            if (stateValid && state == cachedState)
                return;

            cachedState = state;
            stateValid = true;

            body.Position = beam.BeamCenter;
            body.Size = new Vector2(beam.BeamLength, beam.BeamWidth);
            body.Rotation = beam.BeamRotation;

            bodyBackground.Colour = beam.Colour;
            bodyBackground.Alpha = 0.15f * Math.Clamp(beam.Opacity, 0, 1);
            body.BorderColour = beam.OutlineColour;
            body.BorderThickness = beam.OutlineThickness;

            startMarker.Position = beam.Position;
            endMarker.Position = beam.EndPosition;

            float left = Math.Min(beam.Position.X, beam.EndPosition.X) - 16f;
            float right = Math.Max(beam.Position.X, beam.EndPosition.X) + 16f;
            float top = Math.Min(beam.Position.Y, beam.EndPosition.Y) - 16f;
            float bottom = Math.Max(beam.Position.Y, beam.EndPosition.Y) + 16f;
            GamefieldBounds = new RectangleF(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => body.ReceivePositionalInputAt(screenSpacePos)
               || startMarker.ReceivePositionalInputAt(screenSpacePos)
               || endMarker.ReceivePositionalInputAt(screenSpacePos);

        private readonly record struct BeamPathState(
            Vector2 Position,
            Vector2 EndPosition,
            float BeamWidth,
            Colour4 Colour,
            Colour4 OutlineColour,
            float OutlineThickness,
            float Opacity);
    }
}
