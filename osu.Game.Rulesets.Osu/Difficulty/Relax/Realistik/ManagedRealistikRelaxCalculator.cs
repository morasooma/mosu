// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MosuRxPureCs;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Realistik
{
    /// <summary>
    /// Managed RX calculator integration. Vanilla/non-RX does not enter this class.
    /// </summary>
    internal static class ManagedRealistikRelaxCalculator
    {
        // Difficulty attributes own their converted map only while they are in use. The fallback
        // cache is bounded: song select can prepare thousands of RX maps in one session.
        private const int MAX_CACHED_BEATMAPS = 32;
        private static readonly object beatmapCacheLock = new object();
        private static readonly Dictionary<int, RxBeatmap> beatmaps = new Dictionary<int, RxBeatmap>();
        private static readonly LinkedList<int> beatmapRecency = new LinkedList<int>();
        private static readonly Dictionary<int, LinkedListNode<int>> beatmapNodes = new Dictionary<int, LinkedListNode<int>>();
        private static readonly ConditionalWeakTable<OsuDifficultyAttributes, RxBeatmap> beatmapsByAttributes = new ConditionalWeakTable<OsuDifficultyAttributes, RxBeatmap>();

        /// <summary>
        /// Live performance is recalculated on every qualifying judgement during gameplay. The
        /// full <see cref="MosuRxCalculator.Calculate"/> pass is O(passedObjects) and allocates
        /// several per-object lists, so on long Relax maps the frame cost grows with the object
        /// count. Cache the most recent result keyed by score state; revert and the next
        /// qualifying judgement after a score-state change reuse it instead of recomputing.
        /// </summary>
        private static readonly ConcurrentDictionary<int, CachedRelaxResult> liveResultCache = new ConcurrentDictionary<int, CachedRelaxResult>();

        internal sealed record CachedRelaxResult(int PassedObjects, int Count300, int Count100, int Count50, int Misses, int MaxCombo,
            double RelaxPatternPenaltyRatio, OsuPerformanceAttributes Attributes);

        public static bool IsRelax(IEnumerable<Mod> mods) => RealistikRelaxBalance.IsRelax(mods);

        public static void Prepare(IBeatmap beatmap, Mod[] mods, OsuDifficultyAttributes attributes)
        {
            if (!IsRelax(mods))
                return;

            try
            {
                RxBeatmap converted = RealistikBeatmapConverter.Convert(beatmap);
                int cacheKey = createCacheKey(beatmap.BeatmapInfo, mods);

                lock (beatmapCacheLock)
                    addBeatmapToCache(cacheKey, converted);

                // Only callers which explicitly requested performance preparation reach here.
                // Star-rating-only carousel panels skip Prepare entirely.
                beatmapsByAttributes.AddOrUpdate(attributes, converted);

                // MosuPp keeps its own converted beatmap (separate calculator, see Relax/MosuPpRelax).
                MosuPpRelax.MosuPpRelaxCalculator.Prepare(beatmap, mods);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Managed RX beatmap preparation failed for '{beatmap.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        internal static RxBeatmap? GetPreparedBeatmap(OsuDifficultyAttributes attributes)
            => beatmapsByAttributes.TryGetValue(attributes, out RxBeatmap? beatmap) ? beatmap : null;

        public static void PrepareTimed(IBeatmap beatmap, Mod[] mods, IEnumerable<OsuDifficultyAttributes> attributes)
        {
            if (!IsRelax(mods))
                return;

            try
            {
                RxBeatmap converted = RealistikBeatmapConverter.Convert(beatmap);
                int cacheKey = createCacheKey(beatmap.BeatmapInfo, mods);

                lock (beatmapCacheLock)
                    addBeatmapToCache(cacheKey, converted);

                foreach (OsuDifficultyAttributes attribute in attributes)
                    beatmapsByAttributes.AddOrUpdate(attribute, converted);

                // MosuPp keeps its own converted beatmap (separate calculator, see Relax/MosuPpRelax).
                MosuPpRelax.MosuPpRelaxCalculator.Prepare(beatmap, mods);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Managed timed RX beatmap preparation failed for '{beatmap.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        public static OsuPerformanceAttributes CalculatePerformance(ScoreInfo score, OsuDifficultyAttributes currentAttributes)
        {
            if (score.BeatmapInfo is null)
                throw new InvalidOperationException("Managed RX calculation requires BeatmapInfo.");
            if (!IsRelax(score.Mods))
                throw new InvalidOperationException("Managed RX calculator was called for a non-RX score.");

            int key = createCacheKey(score.BeatmapInfo, score.Mods);
            if (!beatmapsByAttributes.TryGetValue(currentAttributes, out RxBeatmap? beatmap))
            {
                lock (beatmapCacheLock)
                {
                    if (beatmaps.TryGetValue(key, out beatmap))
                    {
                        beatmapRecency.Remove(beatmapNodes[key]);
                        beatmapNodes[key] = beatmapRecency.AddLast(key);
                    }

                    if (beatmap != null)
                        beatmapsByAttributes.AddOrUpdate(currentAttributes, beatmap);
                }

                if (beatmap == null)
                {
                    throw new InvalidOperationException(
                        $"Managed RX beatmap was not prepared for '{score.BeatmapInfo.Hash}'. "
                        + "Run the matching difficulty calculation before performance calculation.");
                }
            }

            // Object-less beatmaps legitimately produce empty difficulty attributes. They are
            // still prepared so the difficulty/performance pipeline remains consistent, but the
            // native RX calculator requires at least one hit object and would throw here. Such a
            // score cannot award performance, so match the regular calculator's zero result.
            if (beatmap.HitObjects.Count == 0)
                return new OsuPerformanceAttributes();

            // Live (in-gameplay) callers recompute PP on every qualifying judgement. The full
            // O(passedObjects) difficulty pass is identical for an unchanged score state, so a
            // short-lived cache keyed by that state collapses repeated throttled calls and revert
            // replays to a single computation. The cache is entry-per-beatmap/mods and replaced
            // (not grown) whenever the score state changes, so it never accumulates over a play.
            RxScoreState scoreState = createScoreState(score);
            int passedObjects = (int)(scoreState.PassedObjects ?? 0);
            int misses = (int)scoreState.Misses;
            int maxCombo = (int)(scoreState.MaxCombo ?? 0);

            if (liveResultCache.TryGetValue(key, out CachedRelaxResult? cached)
                && cached.PassedObjects == passedObjects
                && cached.Count300 == scoreState.Count300
                && cached.Count100 == scoreState.Count100
                && cached.Count50 == scoreState.Count50
                && cached.Misses == misses
                && cached.MaxCombo == maxCombo
                // Live callers pass time-sliced attributes, whose anti-abuse ratio advances
                // independently of the score state and scales the final result.
                && cached.RelaxPatternPenaltyRatio.Equals(currentAttributes.RelaxPatternPenaltyRatio))
            {
                return cached.Attributes;
            }

            try
            {
                RxMods performanceMods = getPerformanceMods(score.Mods);
                double clockRate = ModUtils.CalculateRateWithMods(score.Mods);
                RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
                {
                    Beatmap = beatmap,
                    Mods = performanceMods,
                    Score = scoreState,
                    EffectiveAr = beatmap.ApproachRate,
                    EffectiveOd = beatmap.OverallDifficulty,
                    EffectiveCs = beatmap.CircleSize,
                    EffectiveHp = beatmap.HpDrainRate,
                    ClockRate = clockRate,
                });

                double nativeMultiplier = calculateNativeMultiplier(result, scoreState);
                RealistikRelaxBalance.ApplyComponentGuards(
                    result,
                    score.Mods,
                    scoreState,
                    beatmap,
                    (float)result.Difficulty.Cs,
                    clockRate);

                double patternMultiplier = RealistikRelaxBalance.PatternMultiplier(score.Mods, result);
                double lengthAimMultiplier = RealistikRelaxBalance.LengthAimMultiplier(score.Mods, result);
                result.PpAim *= lengthAimMultiplier;
                result.Pp = RealistikRelaxBalance.RebuildTotal(result, scoreState, nativeMultiplier);

                if (RealistikRelaxBalance.IsVerticalBalanceEligible(score.Mods))
                {
                    double verticalPressure = RealistikRelaxBalance.VerticalPressure(
                        beatmap,
                        (float)result.Difficulty.Cs,
                        scoreState.PassedObjects,
                        clockRate);
                    if (verticalPressure > 0)
                    {
                        result.PpAim *= 1 - 0.06 * verticalPressure;
                        result.Pp = RealistikRelaxBalance.RebuildTotal(result, scoreState, nativeMultiplier);
                    }
                }

                double odMultiplier = RealistikRelaxBalance.OdMultiplier(result.Difficulty.Od);
                double currentAntiAbuseMultiplier = calculateCurrentAntiAbuseMultiplier(currentAttributes);

                var attributes = new OsuPerformanceAttributes
                {
                    Aim = result.PpAim * odMultiplier * patternMultiplier * currentAntiAbuseMultiplier,
                    Speed = result.PpSpeed * odMultiplier,
                    Accuracy = result.PpAccuracy * odMultiplier,
                    Reading = result.PpReading * odMultiplier,
                    Flashlight = 0,
                    EffectiveMissCount = result.EffectiveMissCount,
                    Total = result.Pp * odMultiplier * patternMultiplier * currentAntiAbuseMultiplier,
                };

                liveResultCache[key] = new CachedRelaxResult(passedObjects, (int)scoreState.Count300, (int)scoreState.Count100,
                    (int)scoreState.Count50, misses, maxCombo, currentAttributes.RelaxPatternPenaltyRatio, attributes);

                return attributes;
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Managed RX PP calculation failed for '{score.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        private static void addBeatmapToCache(int key, RxBeatmap beatmap)
        {
            if (beatmapNodes.Remove(key, out LinkedListNode<int>? existing))
                beatmapRecency.Remove(existing);

            beatmaps[key] = beatmap;
            beatmapNodes[key] = beatmapRecency.AddLast(key);
            liveResultCache.TryRemove(key, out _);

            while (beatmaps.Count > MAX_CACHED_BEATMAPS)
            {
                int oldest = beatmapRecency.First!.Value;
                beatmapRecency.RemoveFirst();
                beatmapNodes.Remove(oldest);
                beatmaps.Remove(oldest);
                liveResultCache.TryRemove(oldest, out _);
            }
        }

        private static RxScoreState createScoreState(ScoreInfo score)
        {
            uint count300 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Great));
            uint count100 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Ok));
            uint count50 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Meh));
            uint misses = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Miss));
            uint passedObjects = count300 + count100 + count50 + misses;

            return new RxScoreState
            {
                Count300 = count300,
                Count100 = count100,
                Count50 = count50,
                Misses = misses,
                MaxCombo = score.MaxCombo > 0 ? (uint)score.MaxCombo : null,
                PassedObjects = passedObjects > 0 ? passedObjects : null,
            };
        }

        private static RxMods getPerformanceMods(IEnumerable<Mod> mods)
        {
            RxMods value = RxMods.Relax;

            foreach (Mod mod in mods)
            {
                value |= mod.Acronym switch
                {
                    "NF" => RxMods.NoFail,
                    "TD" => RxMods.TouchDevice,
                    "HD" => RxMods.Hidden,
                    "FL" => RxMods.Flashlight,
                    "SO" => RxMods.SpunOut,
                    _ => RxMods.None,
                };
            }

            return value;
        }

        private static double calculateNativeMultiplier(RxNativePerformanceResult result, RxScoreState score)
        {
            double componentNorm = RealistikRelaxBalance.RebuildTotal(result, score, 1);
            if (componentNorm <= 0)
                return 1.09;

            // Preserves the exact native global multiplier, including SpunOut.
            return result.Pp / componentNorm;
        }

        private static double calculateCurrentAntiAbuseMultiplier(OsuDifficultyAttributes attributes)
        {
            double evidence = Math.Clamp(attributes.RelaxPatternPenaltyRatio / 0.80, 0.10, 1);
            return Math.Pow(evidence, 1.30);
        }

        private static int createCacheKey(IBeatmapInfo beatmapInfo, IEnumerable<Mod> mods)
        {
            var hash = new HashCode();
            hash.Add(beatmapInfo.Hash);

            foreach (Mod mod in mods.OrderBy(m => m.Acronym))
                hash.Add(mod);

            return hash.ToHashCode();
        }
    }
}
