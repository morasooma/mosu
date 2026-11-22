// Adapted from taziksfear’s MosuPP 2.0.0 (PR #7, 3d3a196ed6540a3099d246dd5c6f662366c28db8).

using System;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax
{
    /// <summary>
    /// TazikPp player balance applies one multiplier per map and mod combination:
    /// (length / 90 s)^<see cref="TazikPpBalance.LengthExponent"/> · e^(<see cref="TazikPpBalance.SliderShareExponent"/> · (slider share − 0.2))
    /// · rate^(<see cref="TazikPpBalance.RateExponent"/> + <see cref="TazikPpBalance.RateLevelExponent"/> · clamp(ln(level / 1500), −2.5, 1.5)).
    /// </summary>
    /// <remarks>
    /// Longer maps are harder to the players than their PP says, slider-heavy ones easier, a faster rate adds less than the PP gives
    /// (more so on easy maps). Length is the played length (first object to the end of the last, divided by the rate); the slider
    /// share is sliders / (circles + sliders). The level is the SS PP of the map + mods before this rule; only rates above 1 count
    /// and the rate part never gives PP (HT and DT buffs stay with the other rules). All exponents 0 (the default) = off.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxPlayerBalance
    {
        public const double LENGTH_REFERENCE_SECONDS = 90;
        public const double SLIDER_SHARE_REFERENCE = 0.2;
        public const double LEVEL_REFERENCE_PP = 1500;

        /// <summary>Played length at rate 1 (ms) and slider share of a playable beatmap.</summary>
        public readonly record struct Shape(double LengthMs, double SliderShare);

        public static Shape Analyze(IBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return new Shape(0, 0);

            int circles = beatmap.HitObjects.Count(h => h is HitCircle), sliders = beatmap.HitObjects.Count(h => h is Slider);
            return new Shape(beatmap.HitObjects[^1].GetEndTime() - beatmap.HitObjects[0].StartTime, (double)sliders / Math.Max(1, circles + sliders));
        }

        public static double Multiplier(Shape shape, double clockRate, double levelPp, TazikPpBalance balance)
        {
            double seconds = Math.Max(5, shape.LengthMs / 1000 / clockRate);
            double multiplier = Math.Pow(seconds / LENGTH_REFERENCE_SECONDS, balance.LengthExponent)
                                * Math.Exp(balance.SliderShareExponent * (shape.SliderShare - SLIDER_SHARE_REFERENCE));

            double rate = Math.Max(0, Math.Log(clockRate));
            if (rate > 0 && levelPp > 0)
            {
                double level = Math.Clamp(Math.Log(levelPp / LEVEL_REFERENCE_PP), -2.5, 1.5);
                multiplier *= Math.Exp(rate * Math.Min(0, balance.RateExponent + balance.RateLevelExponent * level));
            }

            return multiplier;
        }
    }
}
