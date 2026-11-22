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
    /// <summary>
    /// Small editor-only marker for a time trigger. The marker position is not used by gameplay.
    /// </summary>
    public partial class DodgeTriggerPiece : CompositeDrawable
    {
        private const float marker_size = 24;

        private readonly Box marker;
        private readonly osu.Framework.Graphics.Lines.Path playerPath;
        private readonly Container targetOutline;

        public RectangleF GamefieldBounds { get; private set; }

        public DodgeTriggerPiece()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                playerPath = new osu.Framework.Graphics.Lines.Path
                {
                    AutoSizeAxes = Axes.None,
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = 2,
                    Colour = Color4.Cyan,
                    Alpha = 0,
                },
                targetOutline = new Container
                {
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.X, Height = 2 },
                        new Box { RelativeSizeAxes = Axes.X, Height = 2, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft },
                        new Box { RelativeSizeAxes = Axes.Y, Width = 2 },
                        new Box { RelativeSizeAxes = Axes.Y, Width = 2, Anchor = Anchor.TopRight, Origin = Anchor.TopRight },
                    },
                    Colour = Color4.Cyan,
                },
                marker = new Box
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(marker_size),
                    Rotation = 45,
                    Colour = Color4.HotPink,
                    Alpha = 0.8f,
                },
            };
        }

        public void UpdateFrom(DodgeTrigger trigger)
        {
            playerPath.Alpha = trigger.Action == DodgeTriggerAction.MovePlayer && !trigger.MoveFromCurrentPosition ? 0.65f : 0;
            if (playerPath.Alpha > 0)
            {
                if (playerPath.Vertices.Count != 2 || playerPath.Vertices[0] != trigger.SourcePosition || playerPath.Vertices[1] != trigger.TargetPosition)
                {
                    playerPath.ClearVertices();
                    playerPath.AddVertex(trigger.SourcePosition);
                    playerPath.AddVertex(trigger.TargetPosition);
                    playerPath.Position = -playerPath.PositionInBoundingBox(Vector2.Zero);
                }
            }
            targetOutline.Alpha = trigger.Action == DodgeTriggerAction.ResizeArena ? 0.65f : 0;
            targetOutline.Position = trigger.TargetPosition;
            targetOutline.Size = trigger.TargetSize;
            Vector2 markerPosition = trigger.Action is DodgeTriggerAction.MovePlayer or DodgeTriggerAction.ResizeArena
                ? trigger.TargetPosition : trigger.Position;
            marker.Position = markerPosition;
            marker.Colour = trigger.Colour;
            marker.Alpha = 0.55f + trigger.Strength * 0.45f;
            float extent = marker_size * MathF.Sqrt(2);
            GamefieldBounds = new RectangleF(
                markerPosition.X - extent / 2,
                markerPosition.Y - extent / 2,
                extent,
                extent);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => marker.ReceivePositionalInputAt(screenSpacePos);
    }
}
