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
    public class DodgeBeamCompositionTool : CompositionTool
    {
        public DodgeBeamCompositionTool()
            : base(DodgeEditorStrings.Beam)
        {
            TooltipText = DodgeEditorStrings.BeamToolTip;
        }

        public override Drawable CreateIcon() => new Container
        {
            Size = new Vector2(18, 14),
            Children = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(4, 14),
                    Colour = Color4.White,
                },
            },
        };

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new DodgeBeamPlacementBlueprint();
    }
}
