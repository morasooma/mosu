// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeEmitterSelectionBlueprint : HitObjectSelectionBlueprint<DodgeEmitter>
    {
        private const float selection_padding = 8;

        private readonly DodgeEmitterPathPiece pathPiece;

        protected override bool AlwaysShowWhenSelected => true;

        public DodgeEmitterSelectionBlueprint(DodgeEmitter hitObject)
            : base(hitObject)
        {
            InternalChild = pathPiece = new DodgeEmitterPathPiece();
        }

        protected override void Update()
        {
            base.Update();
            pathPiece.UpdateFrom(HitObject);
        }

        public override Vector2 ScreenSpaceSelectionPoint => ToScreenSpace(HitObject.Position);

        protected override Vector2[] ScreenSpaceAdditionalNodes => HitObject.MoveSource && HitObject.EffectiveBurstCount > 1
            ? new[] { ToScreenSpace(HitObject.AimPosition), ToScreenSpace(HitObject.MovementEndPosition) }
            : new[] { ToScreenSpace(HitObject.AimPosition) };

        public override Quad SelectionQuad
        {
            get
            {
                RectangleF bounds = pathPiece.GamefieldBounds;
                Vector2 topLeft = ToScreenSpace(new Vector2(bounds.Left, bounds.Top));
                Vector2 topRight = ToScreenSpace(new Vector2(bounds.Right, bounds.Top));
                Vector2 bottomLeft = ToScreenSpace(new Vector2(bounds.Left, bounds.Bottom));
                Vector2 bottomRight = ToScreenSpace(new Vector2(bounds.Right, bounds.Bottom));

                float left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X)) - selection_padding;
                float top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y)) - selection_padding;
                float right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X)) + selection_padding;
                float bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y)) + selection_padding;

                return new Quad(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => pathPiece.ReceivePositionalInputAt(screenSpacePos);
    }
}
