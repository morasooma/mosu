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
    public class DodgeEmitterCompositionTool : CompositionTool<DodgeAction>
    {
        public DodgeEmitterCompositionTool()
            : base(DodgeEditorStrings.Emitter)
        {
            Action = DodgeAction.EditorEmitterTool;
            TooltipText = DodgeEditorStrings.EmitterToolTip;
        }

        public override Drawable CreateIcon() => new Container
        {
            Size = new Vector2(18),
            Children = new Drawable[]
            {
                new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(7),
                    Colour = Color4.White,
                },
                createRay(-35),
                createRay(0),
                createRay(35),
            },
        };

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new DodgeEmitterPlacementBlueprint();

        private static Drawable createRay(float rotation) => new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.CentreLeft,
            Position = new Vector2(3, 0),
            Size = new Vector2(7, 1.5f),
            Rotation = rotation,
            Colour = Color4.White,
        };
    }
}
