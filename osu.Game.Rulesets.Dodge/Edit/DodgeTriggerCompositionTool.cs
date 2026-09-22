// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public class DodgeTriggerCompositionTool : CompositionTool<DodgeAction>
    {
        public DodgeTriggerCompositionTool()
            : base(DodgeEditorStrings.Trigger)
        {
            Action = DodgeAction.EditorTriggerTool;
            TooltipText = DodgeEditorStrings.TriggerToolTip;
        }

        public override Drawable CreateIcon() => new Box
        {
            Origin = Anchor.Centre,
            Size = new Vector2(10),
            Rotation = 45,
            Colour = Color4.HotPink,
        };

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new DodgeTriggerPlacementBlueprint();
    }
}
