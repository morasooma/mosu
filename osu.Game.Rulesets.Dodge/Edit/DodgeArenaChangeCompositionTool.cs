// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public class DodgeArenaChangeCompositionTool : CompositionTool<DodgeAction>
    {
        public DodgeArenaChangeCompositionTool()
            : base(DodgeEditorStrings.Arena)
        {
            Action = DodgeAction.EditorArenaChangeTool;
            TooltipText = DodgeEditorStrings.ArenaToolTip;
        }

        public override Drawable CreateIcon() => new Container
        {
            Size = new Vector2(18, 14),
            Masking = true,
            BorderThickness = 2,
            BorderColour = Color4.White,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
        };

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new DodgeArenaChangePlacementBlueprint();
    }
}
