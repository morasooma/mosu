// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#if MOSUPP_LAB
using System;
using System.Text.Json;
#endif

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// Approved MosuPp 0.5.0 balance (the owner's site scenario of 2026-10-15 + CS reduction). Normal client builds use immutable defaults;
    /// the polygon's process-local override is only compiled with MOSUPP_LAB.
    /// </summary>
    public sealed class MosuPpBalance
    {
        public static MosuPpBalance Current
        {
            get;
#if MOSUPP_LAB
            private set;
#endif
        } = new MosuPpBalance();

        public double GlobalScale { get; init; } = 1;
        public double ArlesBonus { get; init; } = 0.2;
        public double ControlScale { get; init; } = 0.9;
        public double RpStrength { get; init; } = 1;
        public double StreamNerf { get; init; } = RxStreamMapNerf.MAX_NERF;
        public double StreamScale { get; init; } = 0.97; // 1.0.0 (cache 38, owner's scenario): was 0.85
        public double AimSlopScale { get; init; } = 0.895; // 0.9.0: was 0.87; aim slop ≈ −11% against production 0.4.0
        public double TechScale { get; init; } = 1.2; // 0.9.0 (owner's final): 0.95 → 1.0 in 0.6.0 → 1.2
        public double AimControlScale { get; init; } = 0.87; // 1.0.0: was 0.75 (0.5.0 patch for small-spacing control, fixed at its root)

        /// <summary>"CS Reduction (RX)": exponent k of (native radius / played radius)^k when DA or EZ make the circles bigger.</summary>
        public double CsReductionStrength { get; init; } = RxCsReduction.DEFAULT_STRENGTH;

        /// <summary>"Player Balance (RX)" (<see cref="RxPlayerBalance"/>): exponents of the length, slider share and rate parts; 0 = off.</summary>
        public double LengthExponent { get; init; }

        public double SliderShareExponent { get; init; }
        public double RateExponent { get; init; }
        public double RateLevelExponent { get; init; }

        /// <summary>PP for high AR: the core's reading for an approach under 500 ms on screen and RP's reaction bonus (+10% RP from
        /// AR 10 to 11 on screen). The owner (2026-10-28): there must be none.</summary>
        public bool HighArReading { get; init; } = true;

        /// <summary>MosuPp 1.1 draft: plays without a rate change or with a rate up to 1.15x (NM, HR, DA, HD; <see cref="RxOwnerRules.IsNomodRate"/>) × this.</summary>
        public double NomodScale { get; init; } = 1;

        /// <summary>"RP Balance (RX)": how far RP may move the total (±).</summary>
        public double RpLimit { get; init; } = RxRespectPoints.MULTIPLIER_MAX - 1;

        /// <summary>MosuPp 1.1 draft: the owner's rules of 2026-10-28 (<see cref="RxOwnerRules"/>) — no HD reading in the core, no
        /// anti-farm part and no CS PP Buff (the owner's HR / DA CS buffs instead). Off = MosuPp 1.0.</summary>
        public bool OwnerRules { get; init; }

        internal double TypeScale(RxRespectPoints.MapType type) => type switch
        {
            RxRespectPoints.MapType.Stream => StreamScale,
            RxRespectPoints.MapType.AimSlop => AimSlopScale,
            RxRespectPoints.MapType.Tech => TechScale,
            RxRespectPoints.MapType.AimControl => AimControlScale,
            _ => 1,
        };

#if MOSUPP_LAB
        public static void ConfigureForLab(string json)
        {
            var value = JsonSerializer.Deserialize<MosuPpBalance>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? throw new ArgumentException("Empty lab scenario");
            validate(value.GlobalScale, 0.5, 1.5);
            validate(value.ArlesBonus, 0, 0.6);
            validate(value.ControlScale, 0, 2);
            validate(value.RpStrength, 0, 1);
            validate(value.StreamNerf, 0, 0.8);
            validate(value.CsReductionStrength, 0, 4);
            validate(value.LengthExponent, -0.5, 0.5);
            validate(value.SliderShareExponent, -2, 2);
            validate(value.RateExponent, -2, 0.5);
            validate(value.RateLevelExponent, -1, 1);
            validate(value.RpLimit, 0, 0.2);
            validate(value.NomodScale, 0.8, 1.5);
            foreach (double scale in new[] { value.StreamScale, value.AimSlopScale, value.TechScale, value.AimControlScale })
                validate(scale, 0.5, 1.5);
            Current = value;
        }

        private static void validate(double value, double min, double max)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), "Lab parameter outside permitted range");
        }
#endif
    }
}
