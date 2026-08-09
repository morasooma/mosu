// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Configuration;

namespace osu.Game.Performance
{
    /// <summary>
    /// Applies the user-facing Mosu recommended values. Options which regressed or only traded average FPS
    /// for latency in the isolated benchmark are explicitly disabled; the verified skin package is enabled.
    /// </summary>
    public static class MosuRecommendedPerformancePreset
    {
        private static readonly (OsuSetting Setting, bool Value)[] recommended_boolean_settings =
        {
            (OsuSetting.ForkWindowsUltraPerformanceMode, false),
            (OsuSetting.ForkSkinPerformanceMode, true),
            (OsuSetting.ForkSkinPerformanceFreezeAnimations, true),
            (OsuSetting.ForkSkinPerformanceSimplifyEffects, true),
            (OsuSetting.ForkSkinPerformanceOptimiseTextures, true),
            (OsuSetting.ForkSkinPerformanceSimplifyHud, true),
            (OsuSetting.ForkSkinPerformanceSimplifyCounters, true),
            (OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, true),
            (OsuSetting.ForkSkinPerformanceBlackBackground, true),
        };

        public static IReadOnlyList<(OsuSetting Setting, bool Value)> Settings => recommended_boolean_settings;

        public static bool GetValue(OsuSetting setting, bool fallback = false)
        {
            foreach ((OsuSetting candidate, bool value) in recommended_boolean_settings)
            {
                if (candidate == setting)
                    return value;
            }

            return fallback;
        }

        public static void Apply(OsuConfigManager config)
        {
            foreach ((OsuSetting setting, bool value) in recommended_boolean_settings)
                config.SetValue(setting, value);
        }
    }
}
