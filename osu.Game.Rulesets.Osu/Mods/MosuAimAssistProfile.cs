// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;

namespace osu.Game.Rulesets.Osu.Mods
{
    internal readonly struct MosuAimAssistProfile
    {
        private static readonly MosuAimAssistProfile low_profile = new MosuAimAssistProfile(
            strength: 0.24,
            fovRadius: 92f,
            intentThreshold: 0.18,
            dynamicFriction: 0.16,
            antiJitterMs: 18.0,
            centerBias: 0.38,
            overshootAllowance: 6f);

        private static readonly MosuAimAssistProfile balanced_profile = new MosuAimAssistProfile(
            strength: 0.68,
            fovRadius: 140f,
            intentThreshold: -0.12,
            dynamicFriction: 0.48,
            antiJitterMs: 35.0,
            centerBias: 0.58,
            overshootAllowance: 16f);

        private static readonly MosuAimAssistProfile sticky_profile = new MosuAimAssistProfile(
            strength: 0.92,
            fovRadius: 185f,
            intentThreshold: -0.35,
            dynamicFriction: 0.72,
            antiJitterMs: 28.0,
            centerBias: 0.52,
            overshootAllowance: 22f);

        public readonly double Strength;
        public readonly float FovRadius;
        public readonly double IntentThreshold;
        public readonly double DynamicFriction;
        public readonly double AntiJitterMs;
        public readonly double CenterBias;
        public readonly float OvershootAllowance;

        private MosuAimAssistProfile(double strength, float fovRadius, double intentThreshold, double dynamicFriction, double antiJitterMs, double centerBias, float overshootAllowance)
        {
            Strength = strength;
            FovRadius = fovRadius;
            IntentThreshold = intentThreshold;
            DynamicFriction = dynamicFriction;
            AntiJitterMs = antiJitterMs;
            CenterBias = centerBias;
            OvershootAllowance = overshootAllowance;
        }

        public static MosuAimAssistProfile FromStrength(float strength)
        {
            float clampedStrength = Math.Clamp(strength, 0f, 0.7f);

            if (clampedStrength <= 0.5f)
                return interpolate(low_profile, balanced_profile, clampedStrength / 0.5f);

            return interpolate(balanced_profile, sticky_profile, (clampedStrength - 0.5f) / 0.2f);
        }

        private static MosuAimAssistProfile interpolate(MosuAimAssistProfile from, MosuAimAssistProfile to, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);

            return new MosuAimAssistProfile(
                strength: Interpolation.Lerp(from.Strength, to.Strength, amount),
                fovRadius: (float)Interpolation.Lerp(from.FovRadius, to.FovRadius, amount),
                intentThreshold: Interpolation.Lerp(from.IntentThreshold, to.IntentThreshold, amount),
                dynamicFriction: Interpolation.Lerp(from.DynamicFriction, to.DynamicFriction, amount),
                antiJitterMs: Interpolation.Lerp(from.AntiJitterMs, to.AntiJitterMs, amount),
                centerBias: Interpolation.Lerp(from.CenterBias, to.CenterBias, amount),
                overshootAllowance: (float)Interpolation.Lerp(from.OvershootAllowance, to.OvershootAllowance, amount));
        }
    }
}
