using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Immutable sort/filter input. Estimates never become calculator attributes or database
    /// values; a result arriving during sorting cannot change its comparer halfway through.
    /// </summary>
    internal sealed class SongSelectRatingSnapshot
    {
        internal readonly record struct Value(string Checksum, double Stars, double? Pp);
        private readonly IReadOnlyDictionary<Guid, Value> exact;
        private readonly ConcurrentDictionary<(Guid, string), Value> estimates = new ConcurrentDictionary<(Guid, string), Value>();
        private readonly bool relax;
        private readonly string? ruleset;
        private readonly double starFactor;
        private readonly double ppFactor;

        public SongSelectRatingSnapshot(RulesetInfo? ruleset, IReadOnlyList<Mod> mods, IReadOnlyDictionary<Guid, Value> exact)
        {
            this.exact = new Dictionary<Guid, Value>(exact);
            this.ruleset = ruleset?.ShortName;
            relax = mods.Any(m => m is ModRelax || m.Acronym == "MRX");
            // Deliberately coarse metadata-only first pass. Do not pretend this is a
            // modded difficulty formula: FL/pattern/AR effects require the real map.
            double rate = ModUtils.CalculateRateWithMods(mods);
            starFactor = double.IsFinite(rate) && rate > 0 ? Math.Pow(rate, .6) : 1;
            if (mods.Any(m => m is ModHardRock)) starFactor *= 1.08;
            if (mods.Any(m => m is ModEasy)) starFactor *= .8;
            if (mods.Any(m => m is ModTouchDevice)) starFactor *= .9;
            ppFactor = Math.Pow(starFactor, 3);
            if (mods.Any(m => m is ModHidden)) ppFactor *= 1.04;
            if (mods.Any(m => m is ModFlashlight)) ppFactor *= 1.06;
        }

        public double Stars(BeatmapInfo map) => get(map).Stars;
        public double Pp(BeatmapInfo map) => get(map).Pp ?? -1;

        private Value get(BeatmapInfo map) => estimates.GetOrAdd((map.ID, map.MD5Hash),
            static (_, state) => state.Snapshot.estimate(state.Map), (Snapshot: this, Map: map));

        private Value estimate(BeatmapInfo map)
        {
            // Freeze each map's metadata/cache projection on first access. Concurrent
            // library maintenance cannot change comparison keys within this operation.
            double stars = relax ? ForkDataStore.Instance?.GetRelaxStarRating(map.ID) ?? map.StarRating : map.StarRating;
            if (!double.IsFinite(stars) || stars <= 0) stars = Math.Max(0, map.StarRating);
            double pp = relax ? ForkDataStore.Instance?.GetRelaxPP(map.ID) ?? -1 : ForkDataStore.Instance?.GetPP(map.ID) ?? -1;
            bool standard = map.Ruleset.ShortName == "osu" && (ruleset == null || ruleset == "osu");
            if (standard)
            {
                stars *= starFactor;
                pp = pp > 0 ? pp * ppFactor : 2.5 * Math.Pow(stars, 3);
            }
            if (exact.TryGetValue(map.ID, out var value) && value.Checksum == map.MD5Hash)
                return value with { Pp = value.Pp ?? pp };
            return new Value(map.MD5Hash, stars, pp);
        }
    }
}
