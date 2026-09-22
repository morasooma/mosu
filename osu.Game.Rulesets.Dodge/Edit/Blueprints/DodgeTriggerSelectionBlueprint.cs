// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeTriggerSelectionBlueprint : HitObjectSelectionBlueprint<DodgeTrigger>
    {
        private const float selection_padding = 6;
        private readonly DodgeTriggerPiece piece;

        protected override bool AlwaysShowWhenSelected => true;

        public DodgeTriggerSelectionBlueprint(DodgeTrigger hitObject)
            : base(hitObject)
        {
            InternalChild = piece = new DodgeTriggerPiece();
        }

        protected override void Update()
        {
            base.Update();
            piece.UpdateFrom(HitObject);
        }

        public override Vector2 ScreenSpaceSelectionPoint => ToScreenSpace(HitObject.Position);

        public override Quad SelectionQuad
        {
            get
            {
                RectangleF bounds = piece.GamefieldBounds;
                Vector2 topLeft = ToScreenSpace(new Vector2(bounds.Left, bounds.Top));
                Vector2 bottomRight = ToScreenSpace(new Vector2(bounds.Right, bounds.Bottom));
                return new Quad(
                    topLeft.X - selection_padding,
                    topLeft.Y - selection_padding,
                    bottomRight.X - topLeft.X + selection_padding * 2,
                    bottomRight.Y - topLeft.Y + selection_padding * 2);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => piece.ReceivePositionalInputAt(screenSpacePos);
    }
}
