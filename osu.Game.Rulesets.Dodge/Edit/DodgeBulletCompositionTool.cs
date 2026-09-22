// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public class DodgeBulletCompositionTool : CompositionTool<DodgeAction>
    {
        public DodgeBulletCompositionTool()
            : base(DodgeEditorStrings.Bullet)
        {
            Action = DodgeAction.EditorBulletTool;
        }

        public override Drawable CreateIcon() => new Circle
        {
            Size = new Vector2(16),
            Colour = Color4.White,
        };

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new DodgeBulletPlacementBlueprint();
    }
}
