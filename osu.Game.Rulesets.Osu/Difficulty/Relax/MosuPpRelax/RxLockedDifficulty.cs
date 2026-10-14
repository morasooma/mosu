// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp rule "AR/OD Lock (RX)" (only for <c>ForkRelaxPpSystem.MosuPp</c>): rate mods with "Lock AR/OD"
    /// (DT / NC / HT / DC, SB, TS) are calculated with the AR and OD the player really plays.
    /// </summary>
    /// <remarks>
    /// The RX core speeds up the approach time by the clock rate and uses the map's OD as the "rate-adjusted" baseline,
    /// so before this rule a locked play got exactly the PP of an unlocked one (Attack DT vs DT lock: 7871 / 7871),
    /// although in game the lock keeps the real approach time and hit windows of the map.
    /// A locked play is now calculated like an unlocked play of the same map with AR / OD lowered (raised for HT) so that
    /// on screen they match the locked values — the same as DA: DT 1.5 lock on AR 10 / OD 10 = DT with DA AR 8.5 / OD 8.33.
    /// Lower real AR at high BPM is harder to read (reading PP), wider hit windows are easier (accuracy / OD PP).
    /// Unlocked plays are unchanged.
    /// When changing it, bump <c>ForkDataStore.RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION</c>.
    /// </remarks>
    internal static class RxLockedDifficulty
    {
        public static bool IsLocked(IEnumerable<Mod> mods)
            => mods.Any(m => m is ModRateAdjust rateAdjust && rateAdjust.LockDifficultyAdjust.Value)
               || mods.Any(m => m is OsuModMosuStaticBpm staticBpm && staticBpm.LockDifficultyAdjust.Value)
               || mods.Any(m => m is OsuModMosuTargetDifficulty targetDifficulty && targetDifficulty.LockDifficultyAdjust.Value);

        /// <summary>AR whose approach time divided by <paramref name="clockRate"/> equals the approach time of <paramref name="ar"/>.</summary>
        public static float Ar(float ar, double clockRate)
            => (float)preemptToAr(arToPreempt(ar) * clockRate);

        /// <summary>OD whose 300 hit window divided by <paramref name="clockRate"/> equals the 300 hit window of <paramref name="od"/>.</summary>
        public static float Od(float od, double clockRate)
            => (float)((80 - (80 - 6 * od) * clockRate) / 6);

        private static double arToPreempt(double ar) => ar < 5 ? 1800 - 120 * ar : 1950 - 150 * ar;

        private static double preemptToAr(double preempt) => preempt > 1200 ? (1800 - preempt) / 120 : (1950 - preempt) / 150;
    }
}
