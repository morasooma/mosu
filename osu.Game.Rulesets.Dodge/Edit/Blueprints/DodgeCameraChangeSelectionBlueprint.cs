// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeCameraChangeSelectionBlueprint : HitObjectSelectionBlueprint<DodgeCameraChange>
    {
        private const float selection_padding = 8;

        private readonly DodgeCameraPathPiece pathPiece;

        protected override bool AlwaysShowWhenSelected => true;

        public DodgeCameraChangeSelectionBlueprint(DodgeCameraChange hitObject)
            : base(hitObject)
        {
            InternalChild = pathPiece = new DodgeCameraPathPiece();
        }

        protected override void Update()
        {
            base.Update();
            pathPiece.UpdateFrom(HitObject);
        }

        public override Vector2 ScreenSpaceSelectionPoint => ToScreenSpace(HitObject.Position);

        protected override Vector2[] ScreenSpaceAdditionalNodes => new[] { ToScreenSpace(HitObject.EndPosition) };

        public override Quad SelectionQuad
        {
            get
            {
                RectangleF bounds = pathPiece.GamefieldBounds;
                Vector2 topLeft = ToScreenSpace(new Vector2(bounds.Left, bounds.Top));
                Vector2 bottomRight = ToScreenSpace(new Vector2(bounds.Right, bounds.Bottom));
                float left = Math.Min(topLeft.X, bottomRight.X) - selection_padding;
                float top = Math.Min(topLeft.Y, bottomRight.Y) - selection_padding;
                float right = Math.Max(topLeft.X, bottomRight.X) + selection_padding;
                float bottom = Math.Max(topLeft.Y, bottomRight.Y) + selection_padding;

                return new Quad(left, top, right - left, bottom - top);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => pathPiece.ReceivePositionalInputAt(screenSpacePos);
    }
}
