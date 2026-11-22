// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Primitives;
using osu.Game.Screens.Edit;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit.Blueprints
{
    public partial class DodgeTriggerSelectionBlueprint : HitObjectSelectionBlueprint<DodgeTrigger>
    {
        private const float selection_padding = 6;
        private readonly DodgeTriggerPiece piece;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        protected override bool ShouldBeAlive
            => IsSelected || editorClock != null
               && editorClock.CurrentTime >= HitObject.StartTime - HitObject.TimePreempt
               && editorClock.CurrentTime <= Math.Max(HitObject.EndTime, HitObject.StartTime + 500);

        protected override void OnDeselected()
        {
            // The runtime drawable is invisible. Keep an editor marker even
            // without selection, rather than hiding the only visible representation.
        }

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

        public override Vector2 ScreenSpaceSelectionPoint => ToScreenSpace(DodgeSelectionTransformUtils.GetPoints(HitObject).Start);

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

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => SelectionQuad.Contains(screenSpacePos);
    }
}
