// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Dodge.Localisation;

namespace osu.Game.Rulesets.Dodge.Objects
{
    public enum DodgeEmitterBeatDivisor
    {
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatOne))]
        Whole = 1,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatHalf))]
        Half = 2,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatThird))]
        Third = 3,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatQuarter))]
        Quarter = 4,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatSixth))]
        Sixth = 6,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatEighth))]
        Eighth = 8,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatTwelfth))]
        Twelfth = 12,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.BeatSixteenth))]
        Sixteenth = 16,
    }
}
