// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Rulesets.Dodge.Localisation;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Timing curve used by bounded camera and arena transitions.
    /// Continuous camera changes represent velocity and therefore do not use an easing curve.
    /// </summary>
    public enum DodgeCameraEasing
    {
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingLinear))]
        Linear,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingIn))]
        EaseIn,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingOut))]
        EaseOut,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingInOut))]
        EaseInOut,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingSmooth))]
        Smooth,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingOvershoot))]
        Overshoot,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingBounce))]
        Bounce,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.CameraEasingElastic))]
        Elastic,
    }

    internal static class DodgeCameraEasingExtensions
    {
        public static float Apply(this DodgeCameraEasing easing, float progress)
            => (float)Interpolation.ApplyEasing(easing switch
            {
                DodgeCameraEasing.EaseIn => Easing.InQuad,
                DodgeCameraEasing.EaseOut => Easing.OutQuad,
                DodgeCameraEasing.EaseInOut => Easing.InOutQuad,
                DodgeCameraEasing.Smooth => Easing.InOutSine,
                DodgeCameraEasing.Overshoot => Easing.OutBack,
                DodgeCameraEasing.Bounce => Easing.OutBounce,
                DodgeCameraEasing.Elastic => Easing.OutElasticQuarter,
                _ => Easing.None,
            }, progress);
    }
}
