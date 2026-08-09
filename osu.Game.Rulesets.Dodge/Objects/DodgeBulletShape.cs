// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Dodge.Localisation;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Visual shape of a projectile. Shapes intentionally share the same collision box.
    /// </summary>
    public enum DodgeBulletShape
    {
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.ShapeCircle))]
        Circle,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.ShapeSquare))]
        Square,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.ShapeDiamond))]
        Diamond,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.ShapeTriangle))]
        Triangle,
    }
}
