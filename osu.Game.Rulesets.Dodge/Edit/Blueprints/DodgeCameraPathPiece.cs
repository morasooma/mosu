// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    /// <summary>
    /// Editor visual for a <see cref="DodgeCameraChange"/>: a line with an arrow-like
    /// direction marker between the start offset point and the end offset point.
    /// </summary>
    public partial class DodgeCameraPathPiece : CompositeDrawable
    {
        private const float node_size = 14;

        private readonly Path path;
        private readonly Circle start;
        private readonly Circle end;
        private Vector2 cachedStart;
        private Vector2 cachedEnd;
        private bool stateValid;

        public RectangleF GamefieldBounds { get; private set; }

        public DodgeCameraPathPiece()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                path = new Path
                {
                    AutoSizeAxes = Axes.None,
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = 1f,
                    Alpha = 0.5f,
                    Colour = Color4.Yellow,
                },
                start = new Circle
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(node_size),
                    Colour = Color4.Yellow,
                    Alpha = 0.5f,
                },
                end = new Circle
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(node_size),
                    Colour = Color4.White,
                    Alpha = 0.8f,
                },
            };
        }

        public void UpdateFrom(DodgeCameraChange change)
        {
            if (stateValid && cachedStart == change.Position && cachedEnd == change.EndPosition)
                return;

            cachedStart = change.Position;
            cachedEnd = change.EndPosition;
            stateValid = true;

            start.Position = change.Position;
            end.Position = change.EndPosition;

            path.ClearVertices();
            path.AddVertex(change.Position);
            path.AddVertex(change.EndPosition);
            // Path normalises its vertices into their bounding box internally.
            // Offset that normalisation so playfield coordinates remain aligned with the nodes.
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);

            float left = Math.Min(change.Position.X, change.EndPosition.X);
            float top = Math.Min(change.Position.Y, change.EndPosition.Y);
            float right = Math.Max(change.Position.X, change.EndPosition.X);
            float bottom = Math.Max(change.Position.Y, change.EndPosition.Y);
            GamefieldBounds = new RectangleF(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => start.ReceivePositionalInputAt(screenSpacePos)
               || end.ReceivePositionalInputAt(screenSpacePos)
               || path.ReceivePositionalInputAt(screenSpacePos);
    }
}
