// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Skinning;

namespace osu.Desktop.Performance
{
    /// <summary>
    /// Applies skin benchmark arguments before <see cref="OsuGameDesktop"/> and its skin caches are constructed.
    /// </summary>
    internal static class MosuBenchmarkSkinOverride
    {
        public static bool ApplyFromArguments(IReadOnlyList<string> args)
        {
            bool benchmarkLaunch = false;
            bool? enabled = null;
            bool? freezeAnimations = null;
            bool? simplifyEffects = null;
            bool? optimiseTextures = null;
            bool? simplifyHud = null;
            bool? simplifyCounters = null;
            bool? disableKiaiFlashing = null;
            bool? blackBackground = null;
            bool? argonFollowRing = null;

            foreach (string arg in args)
            {
                string[] split = arg.Split('=', 2);
                string key = split[0];
                string value = split.Length > 1 ? split[1] : string.Empty;

                if (key.StartsWith("--mosu-replay-benchmark", StringComparison.Ordinal))
                    benchmarkLaunch = true;

                switch (key)
                {
                    case "--mosu-replay-benchmark-no-config":
                        enabled = null;
                        freezeAnimations = null;
                        simplifyEffects = null;
                        optimiseTextures = null;
                        simplifyHud = null;
                        simplifyCounters = null;
                        disableKiaiFlashing = null;
                        blackBackground = null;
                        argonFollowRing = null;
                        break;

                    case "--mosu-replay-benchmark-skin-performance":
                        if (tryParseBool(value, out bool parsedEnabled))
                            enabled = parsedEnabled;

                        break;

                    case "--mosu-replay-benchmark-skin-freeze-animations":
                        if (tryParseBool(value, out bool parsedFreezeAnimations))
                            freezeAnimations = parsedFreezeAnimations;

                        break;

                    case "--mosu-replay-benchmark-skin-simplify-effects":
                        if (tryParseBool(value, out bool parsedSimplifyEffects))
                            simplifyEffects = parsedSimplifyEffects;

                        break;

                    case "--mosu-replay-benchmark-skin-optimise-textures":
                        if (tryParseBool(value, out bool parsedOptimiseTextures))
                            optimiseTextures = parsedOptimiseTextures;

                        break;

                    case "--mosu-replay-benchmark-skin-simplify-hud":
                        if (tryParseBool(value, out bool parsedSimplifyHud))
                            simplifyHud = parsedSimplifyHud;

                        break;

                    case "--mosu-replay-benchmark-skin-simplify-counters":
                        if (tryParseBool(value, out bool parsedSimplifyCounters))
                            simplifyCounters = parsedSimplifyCounters;

                        break;

                    case "--mosu-replay-benchmark-skin-disable-kiai":
                        if (tryParseBool(value, out bool parsedDisableKiai))
                            disableKiaiFlashing = parsedDisableKiai;

                        break;

                    case "--mosu-replay-benchmark-skin-black-background":
                        if (tryParseBool(value, out bool parsedBlackBackground))
                            blackBackground = parsedBlackBackground;

                        break;

                    case "--mosu-replay-benchmark-argon-follow-ring":
                        if (tryParseBool(value, out bool parsedArgonFollowRing))
                            argonFollowRing = parsedArgonFollowRing;

                        break;
                }
            }

            if (!benchmarkLaunch
                || (enabled == null
                    && freezeAnimations == null
                    && simplifyEffects == null
                    && optimiseTextures == null
                    && simplifyHud == null
                    && simplifyCounters == null
                    && disableKiaiFlashing == null
                    && blackBackground == null
                    && argonFollowRing == null))
                return false;

            SkinPerformanceMode.ApplyBenchmarkOverride(new SkinPerformanceModeOverride(
                enabled,
                freezeAnimations,
                simplifyEffects,
                optimiseTextures,
                simplifyHud,
                simplifyCounters,
                disableKiaiFlashing,
                blackBackground,
                argonFollowRing));

            return true;
        }

        private static bool tryParseBool(string value, out bool parsed)
        {
            if (bool.TryParse(value, out parsed))
                return true;

            if (value == "1")
            {
                parsed = true;
                return true;
            }

            if (value == "0")
            {
                parsed = false;
                return true;
            }

            return false;
        }
    }
}
