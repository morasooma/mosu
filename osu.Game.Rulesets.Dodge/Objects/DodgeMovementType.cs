// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Dodge.Localisation;

namespace osu.Game.Rulesets.Dodge.Objects
{
    public enum DodgeMovementType
    {
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementLinear))]
        Linear,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementSine))]
        Sine,
    }
}
