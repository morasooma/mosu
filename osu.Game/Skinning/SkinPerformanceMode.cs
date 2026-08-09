// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using System;

namespace osu.Game.Skinning
{
    public static class SkinPerformanceMode
    {
        public static event Action? SettingsChanged;
        public const double FastJudgementVisibleDuration = 180;
        public const double FastJudgementFadeOutDuration = 90;
        public const double HitErrorMeterVisibleDuration = 1800;
        public const double HitErrorMeterFadeOutDuration = 350;
        public const float LegacyTextureResolutionScale = 0.5f;

        /// <summary>
        /// Dim level at or above which <see cref="ShouldBlackOutBackground"/> stops drawing the beatmap
        /// background entirely. At 75%+ dim the image is barely visible, while drawing it still costs a full
        /// 1080p textured pass per frame — the largest single GPU expense on bandwidth-bound iGPUs (see R11).
        /// </summary>
        public const float BLACK_BACKGROUND_DIM_THRESHOLD = 0.75f;

        private static bool enabled = true;
        private static bool freezeAnimations = true;
        private static bool simplifyEffects = true;
        private static bool optimiseTextures = true;
        private static bool simplifyHud = true;
        private static bool simplifyCounters = true;
        private static bool disableKiaiFlashing = true;
        private static bool blackBackground = true;
        private static bool argonFollowRing = true;

        private static SkinPerformanceModeOverride? benchmarkOverride;

        public static bool Enabled
        {
            get => benchmarkOverride?.Enabled ?? enabled;
            set => enabled = value;
        }

        public static bool FreezeAnimations
        {
            get => benchmarkOverride?.FreezeAnimations ?? freezeAnimations;
            set => freezeAnimations = value;
        }

        public static bool SimplifyEffects
        {
            get => benchmarkOverride?.SimplifyEffects ?? simplifyEffects;
            set => simplifyEffects = value;
        }

        public static bool OptimiseTextures
        {
            get => benchmarkOverride?.OptimiseTextures ?? optimiseTextures;
            set => optimiseTextures = value;
        }

        public static bool SimplifyHud
        {
            get => benchmarkOverride?.SimplifyHud ?? simplifyHud;
            set => simplifyHud = value;
        }

        public static bool SimplifyCounters
        {
            get => benchmarkOverride?.SimplifyCounters ?? simplifyCounters;
            set => simplifyCounters = value;
        }

        public static bool DisableKiaiFlashing
        {
            get => benchmarkOverride?.DisableKiaiFlashing ?? disableKiaiFlashing;
            set => disableKiaiFlashing = value;
        }

        public static bool BlackBackground
        {
            get => benchmarkOverride?.BlackBackground ?? blackBackground;
            set => blackBackground = value;
        }

        /// <summary>
        /// Draw the Argon slider follow circle as a ring only, dropping its additive interior fill
        /// (~96k px/frame saved while tracking, audit R13.G). Composed with the
        /// <c>MOSU_ARGON_FOLLOW_RING</c> environment kill-switch in
        /// <see cref="ShouldUseArgonFollowRing"/>.
        /// </summary>
        public static bool ArgonFollowRing
        {
            get => benchmarkOverride?.ArgonFollowRing ?? argonFollowRing;
            set => argonFollowRing = value;
        }

        /// <summary>
        /// Whether a process-level benchmark override is active.
        /// </summary>
        public static bool HasBenchmarkOverride => benchmarkOverride != null;

        /// <summary>
        /// Applies benchmark values before the game and its skin caches are constructed.
        /// The persisted user configuration continues to update the backing values and is exposed again
        /// when <see cref="ClearBenchmarkOverride"/> is called.
        /// </summary>
        public static void ApplyBenchmarkOverride(SkinPerformanceModeOverride settings)
        {
            benchmarkOverride = settings;
            NotifySettingsChanged();
        }

        /// <summary>
        /// Stops applying the process-level benchmark values without changing the user's configuration.
        /// </summary>
        public static void ClearBenchmarkOverride()
        {
            if (benchmarkOverride == null)
                return;

            benchmarkOverride = null;
            NotifySettingsChanged();
        }

        public static string DescribeEffectiveState() =>
            $"enabled={Enabled}"
            + $";freeze_animations={FreezeAnimations}"
            + $";simplify_effects={SimplifyEffects}"
            + $";optimise_textures={OptimiseTextures}"
            + $";simplify_hud={SimplifyHud}"
            + $";simplify_counters={SimplifyCounters}"
            + $";disable_kiai={DisableKiaiFlashing}"
            + $";black_background={BlackBackground}"
            + $";argon_follow_ring={ArgonFollowRing}"
            + $";benchmark_override={HasBenchmarkOverride}";

        // NOTE (R13.G, 2026-07-26): a "downscale background framebuffer at high dim" checkbox
        // (FrameBufferScale 0.5 at dim >= 0.6) was implemented and A/B'd here, and measured NO effect
        // (+0.4%, within variance, 1568.1 vs 1562.2 same exe). The fullscreen background's texture-fetch
        // side is already amortised by the texture cache; the paid side is the backbuffer write, which
        // only the blackout option above removes. Removed per worklog acceptance rule §8 — do not
        // re-attempt without different hardware and an A/B.

        public static bool ShouldFreezeAnimations => Enabled && FreezeAnimations;
        public static bool ShouldSimplifyEffects => Enabled && SimplifyEffects;
        public static bool ShouldSimplifyHud => Enabled && SimplifyHud;
        public static bool ShouldSimplifyCounters => Enabled && SimplifyCounters;
        public static bool ShouldDisableKiaiFlashing => Enabled && DisableKiaiFlashing;

        /// <summary>
        /// Whether the beatmap background should be replaced by plain black (i.e. not drawn at all) at the
        /// given effective dim level.
        /// </summary>
        public static bool ShouldBlackOutBackground(float dimLevel) =>
            Enabled
            && BlackBackground
            && Performance.MosuOptimisationToggles.PerfBlackBackground
            && dimLevel >= BLACK_BACKGROUND_DIM_THRESHOLD;

        /// <summary>
        /// Whether the Argon slider follow circle should be drawn as a ring only. The config checkbox
        /// (<see cref="ArgonFollowRing"/>) is the opt-in; the environment variable is a kill-switch —
        /// explicitly setting <c>MOSU_ARGON_FOLLOW_RING=0</c> force-disables regardless of config.
        /// </summary>
        public static bool ShouldUseArgonFollowRing =>
            Enabled
            && ArgonFollowRing
            && Performance.MosuOptimisationToggles.ArgonFollowRing;

        public static bool ShouldDownscaleLegacyTexture(string componentName) => Enabled && OptimiseTextures && LegacyTextureResolutionScale < 1;

        public static float LegacyTextureScaleAdjust(string componentName) => ShouldDownscaleLegacyTexture(componentName) ? LegacyTextureResolutionScale : 1;

        public static bool PreferLowResolutionLegacyTexture(string componentName)
        {
            if (!Enabled || !OptimiseTextures)
                return false;

            string name = componentName.Replace('\\', '/').ToLowerInvariant();

            if (name.EndsWith(".png", System.StringComparison.Ordinal))
                name = name[..^4];
            else if (name.EndsWith(".jpg", System.StringComparison.Ordinal) || name.EndsWith(".jpeg", System.StringComparison.Ordinal))
                return true;

            name = name.Replace("@2x", string.Empty);

            if (isGameplayCritical(name))
                return false;

            return startsWith(name, "scorebar-")
                   || startsWith(name, "score-")
                   || name.Contains("/score-", System.StringComparison.Ordinal)
                   || startsWith(name, "inputoverlay-")
                   || startsWith(name, "pause-")
                   || startsWith(name, "ranking-")
                   || startsWith(name, "ranking_")
                   || startsWith(name, "ranking")
                   || startsWith(name, "selection-")
                   || startsWith(name, "menu-")
                   || startsWith(name, "button-")
                   || startsWith(name, "count")
                   || name is "ready" or "go"
                   || startsWith(name, "play-")
                   || startsWith(name, "section-")
                   || startsWith(name, "comboburst")
                   || startsWith(name, "fonts/");
        }

        public static void NotifySettingsChanged() => SettingsChanged?.Invoke();

        public static void ApplyFastJudgementFade(Drawable drawable)
        {
            drawable.Alpha = 1;
            drawable.Delay(FastJudgementVisibleDuration)
                    .FadeOut(FastJudgementFadeOutDuration)
                    .Expire();
        }

        private static bool isGameplayCritical(string name)
        {
            return startsWith(name, "hitcircle")
                   || startsWith(name, "approachcircle")
                   || startsWith(name, "sliderb")
                   || startsWith(name, "sliderfollowcircle")
                   || startsWith(name, "reversearrow")
                   || startsWith(name, "cursor")
                   || startsWith(name, "followpoint")
                   || startsWith(name, "sliderscorepoint")
                   || startsWith(name, "sliderpoint")
                   || startsWith(name, "hit0")
                   || startsWith(name, "hit50")
                   || startsWith(name, "hit100")
                   || startsWith(name, "hit300");
        }

        private static bool startsWith(string value, string prefix) => value.StartsWith(prefix, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Nullable process-level overrides used to isolate replay benchmark profiles from persisted settings.
    /// </summary>
    public sealed record SkinPerformanceModeOverride(
        bool? Enabled,
        bool? FreezeAnimations,
        bool? SimplifyEffects,
        bool? OptimiseTextures,
        bool? SimplifyHud,
        bool? SimplifyCounters,
        bool? DisableKiaiFlashing,
        bool? BlackBackground,
        bool? ArgonFollowRing);
}
