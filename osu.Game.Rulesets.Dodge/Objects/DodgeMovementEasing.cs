// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Rulesets.Dodge.Localisation;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Controls projectile speed during its authored flight to the control end point.
    /// Continued movement beyond that point remains linear so projectiles can still leave the playfield.
    /// </summary>
    public enum DodgeMovementEasing
    {
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementEasingLinear))]
        Linear,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementEasingIn))]
        EaseIn,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementEasingOut))]
        EaseOut,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementEasingInOut))]
        EaseInOut,
    }

    internal static class DodgeMovementEasingExtensions
    {
        public static float Apply(this DodgeMovementEasing easing, float progress)
        {
            if (progress <= 0 || progress >= 1)
                return progress;

            return (float)Interpolation.ApplyEasing(easing switch
            {
                DodgeMovementEasing.EaseIn => Easing.InQuad,
                DodgeMovementEasing.EaseOut => Easing.OutQuad,
                DodgeMovementEasing.EaseInOut => Easing.InOutQuad,
                _ => Easing.None,
            }, progress);
        }
    }
}
