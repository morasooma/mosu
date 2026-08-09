// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeArenaChangeSelectionBlueprint : HitObjectSelectionBlueprint<DodgeArenaChange>
    {
        private readonly DodgeArenaChangePiece piece;

        protected override bool AlwaysShowWhenSelected => true;

        public DodgeArenaChangeSelectionBlueprint(DodgeArenaChange hitObject)
            : base(hitObject)
        {
            InternalChild = piece = new DodgeArenaChangePiece();
        }

        protected override void Update()
        {
            base.Update();
            piece.UpdateFrom(HitObject);
        }

        public override Vector2 ScreenSpaceSelectionPoint => ToScreenSpace(HitObject.TargetPosition + HitObject.TargetSize / 2);

        protected override Vector2[] ScreenSpaceAdditionalNodes
            => new[]
            {
                ToScreenSpace(HitObject.TargetPosition),
                ToScreenSpace(HitObject.TargetPosition + HitObject.TargetSize),
            };

        public override Quad SelectionQuad
        {
            get
            {
                Vector2 center = HitObject.TargetPosition + HitObject.TargetSize / 2;
                float halfW = HitObject.TargetSize.X / 2;
                float halfH = HitObject.TargetSize.Y / 2;

                if (Math.Abs(HitObject.TargetRotation) < 0.001f)
                {
                    Vector2 topLeft = ToScreenSpace(HitObject.TargetPosition);
                    Vector2 bottomRight = ToScreenSpace(HitObject.TargetPosition + HitObject.TargetSize);
                    return new Quad(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
                }

                float rad = MathHelper.DegreesToRadians(HitObject.TargetRotation);
                float cos = MathF.Cos(rad);
                float sin = MathF.Sin(rad);

                Vector2 vx = new Vector2(halfW * cos, halfW * sin);
                Vector2 vy = new Vector2(-halfH * sin, halfH * cos);

                Vector2 topLeftRotated = ToScreenSpace(center - vx - vy);
                Vector2 topRightRotated = ToScreenSpace(center + vx - vy);
                Vector2 bottomLeftRotated = ToScreenSpace(center - vx + vy);
                Vector2 bottomRightRotated = ToScreenSpace(center + vx + vy);

                return new Quad(topLeftRotated, topRightRotated, bottomLeftRotated, bottomRightRotated);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => SelectionQuad.Contains(screenSpacePos);
    }
}
