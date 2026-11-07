// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// The owner's rules of 2026-10-28 (MosuPp 1.1 draft, <see cref="MosuPpBalance.OwnerRules"/>): one multiplier each per map + mods,
    /// applied after RP Balance. Percentages inside one rule add up; the rules multiply.
    /// </summary>
    /// <remarks>
    /// "Division" = 0.1 of AR / CS / OD. AR is the one on screen (rate and AR/OD lock included); CS and OD changes are the mod settings
    /// (HR, DA) against the map's own values, not the rate. Lengths of the map are before the rate, lengths of the loaded parts
    /// ("kiai", <see cref="RxLoadSections"/>) are played seconds; BPM is the played BPM (× rate).
    /// </remarks>
    internal static class RxOwnerRules
    {
        public readonly record struct Play(double ClockRate, float BaseCs, float PlayedCs, float BaseAr, float PlayedAr, float BaseOd, float PlayedOd,
                                           bool HardRock, bool DifficultyAdjust, bool Hidden, bool Locked)
        {
            /// <summary>AR as the player sees it.</summary>
            public float ScreenAr => Locked ? PlayedAr : RxLockedDifficulty.OnScreenAr(PlayedAr, ClockRate);

            /// <summary>CS of the AimSlop rules: without HR (it has its own buff).</summary>
            public float AimSlopCs => HardRock ? BaseCs : PlayedCs;
        }

        // ---------------------------------------------------------------- mods

        /// <summary>"HD Buff": +1% at AR 10, +0.1% per division below (down to AR −8: +19%), −0.05% per division above (0 from AR 12).
        /// Replaces the HD reading of the core.</summary>
        public static double Hidden(Play play)
        {
            if (!play.Hidden)
                return 1;

            double ar = Math.Max(play.ScreenAr, -8);
            double percent = ar <= 10 ? 1 + (10 - ar) : Math.Max(0, 1 - 0.5 * (ar - 10));
            return 1 + percent / 100;
        }

        /// <summary>Highest OD the core and RP see; OD above it pays only through <see cref="Od"/>.</summary>
        public const float OD_BASE = 10;

        /// <summary>
        /// "OD": OD set above 10 (DA) +0.7% per division, at most +7% (OD 11 = exactly +7% over OD 10: the core is calculated at OD 10);
        /// OD set below the map's own (DA, EZ) −0.7% per division, without a limit. A map's own low OD costs nothing (the 1.0 OD
        /// multiplier is gone). The rate does not count.
        /// </summary>
        public static double Od(Play play)
        {
            double up = 0.007 * Math.Clamp(divisions(play.PlayedOd - OD_BASE), 0, 10);
            double down = 0.007 * Math.Max(0, divisions(play.BaseOd - play.PlayedOd));
            return Math.Max(0.05, 1 + up - down);
        }

        /// <summary>
        /// "CS raise" (HR, and DA the same way — the same CS pays the same): +0.04% per division of the map's own CS from 3.1 to 4.3
        /// (at most +0.48%), +0.3% per division of the played CS above 4.4 (CS 7 after HR: +8.28%). Replaces CS PP Buff.
        /// </summary>
        public static double CsRaise(Play play)
        {
            if (!play.HardRock && !(play.DifficultyAdjust && play.PlayedCs > play.BaseCs))
                return 1;

            double low = 0.04 * Math.Clamp(divisions(Math.Min(play.BaseCs, 4.3) - 3.1), 0, 12);
            double high = 0.3 * Math.Max(0, divisions(play.PlayedCs - 4.4));
            return 1 + (low + high) / 100;
        }

        /// <summary>Highest rate that still counts as "no rate" for <see cref="MosuPpBalance.NomodScale"/> (the owner, 2026-10-29: up to 1.15x inclusive).</summary>
        public const double NOMOD_RATE_LIMIT = 1.15;

        public static bool IsNomodRate(double clockRate) => clockRate >= 1 && clockRate <= NOMOD_RATE_LIMIT + 1e-6;

        // ---------------------------------------------------------------- map

        /// <summary>"Sliders": +0.4% per 1% of sliders in the map above 15%.</summary>
        public static double Sliders(RxOwnerShape.Shape shape) => 1 + 0.004 * Math.Max(0, 100 * shape.SliderShare - 15);

        /// <summary>"Control share": +0.2% per 1% of aim control in the map's composition (on top of the Aim Control PP).</summary>
        public static double Control(IReadOnlyDictionary<string, double>? composition)
            => 1 + 0.2 * Math.Clamp(composition?.GetValueOrDefault("control") ?? 0, 0, 1);

        /// <summary>
        /// "Streams", on the share of the PP earned in streams (any map): spacing BPM · 0.09 · distance spacing (/10 %, at most +10%)
        /// + loaded parts BPM · 0.15 · count · (longest in minutes · 0.3) (/10 %, at most +10%) + 0.8% per CS of change.
        /// </summary>
        public static double Streams(RxOwnerShape.Shape shape, RxLoadSections.Result load, Play play, double streamShare)
        {
            if (streamShare <= 0)
                return 1;

            double bpm = (shape.StreamBpm > 0 ? shape.StreamBpm : shape.MainBpm) * play.ClockRate;
            double spacing = Math.Min(10, bpm * 0.09 * shape.StreamSpacing / 10);

            double count = load.Seconds.Sum(s => RxMath.SmoothStep(s, 4, 6));
            double longest = load.Seconds.Length > 0 ? load.Seconds.Max() / 60 : 0;
            double loaded = Math.Min(10, bpm * 0.15 * count * longest * 0.3 / 10);

            double cs = 0.8 * (play.PlayedCs - play.BaseCs);
            return 1 + streamShare * (spacing + loaded + cs) / 100;
        }

        /// <summary>"Overlapping streams" (proposal): streams of overlapping notes are only a moving cursor in Relax — the share of PP
        /// earned in streams counts by <see cref="RxOwnerShape.Shape.StreamSpaced"/>.</summary>
        public static double OverlappingStreams(RxOwnerShape.Shape shape, double streamShare)
            => Math.Max(0.1, 1 - Math.Clamp(streamShare, 0, 1) * (1 - shape.StreamSpaced));

        public const double STREAM_MAP_FROM = 0.8, STREAM_MAP_FULL = 0.95; // RxOwnerShape.Shape.TightStreamShare
        public const double OVERLAP_FULL = 0.45, OVERLAP_END = 0.65; // StreamSpaced
        public const double STREAM_MAP_OVERLAP_PENALTY = 0.65;

        /// <summary>"Overlapping stream map" (the owner, 2026-10-29: stronger only where streams are nearly the whole map): −65% when
        /// 95%+ of the notes are in streams of touching circles (<see cref="RxOwnerShape.Shape.TightStreamShare"/>) and those streams
        /// overlap (<see cref="RxOwnerShape.Shape.StreamSpaced"/> 0.45 or less), fading out to 80% of the notes and to StreamSpaced 0.65.
        /// Reads only the map, so no rate or CS takes the map out of it.</summary>
        public static double OverlappingStreamMap(RxOwnerShape.Shape shape)
            => 1 - STREAM_MAP_OVERLAP_PENALTY * RxMath.SmoothStep(shape.TightStreamShare, STREAM_MAP_FROM, STREAM_MAP_FULL)
                                              * (1 - RxMath.SmoothStep(shape.StreamSpaced, OVERLAP_FULL, OVERLAP_END));

        // ---------------------------------------------------------------- aim slop

        private readonly record struct ShortRule(double Farm, double BpmFrom, double BpmTo, double ArSlope, double CsSlope, double CsReference, double RepeatPenalty);

        // Rate below 1.3 / 1.3 and above (blended over 1.25–1.35). Slopes are per division; the CS reference is the middle of the farm CS range.
        private static readonly ShortRule rule_low_rate = new ShortRule(0.93, 240, 339, 0.009, 0.0052, 3.85, 0.0084);
        private static readonly ShortRule rule_high_rate = new ShortRule(0.94, 340, 404, 0.0076, 0.0049, 4.35, 0.0087);

        public const double FARM_AR = 10.7;
        public const double REPEAT_PENALTY_LIMIT = 0.15;

        public readonly record struct AimSlopParts(double Value, double Farm, double Repeats, double ShortValue, double LongValue);

        /// <summary>
        /// "AimSlop": maps up to 90 s (before the rate) — wide back-and-forth farm in its BPM window gets 93% / 94% (rate under / from 1.3),
        /// back to 100% and beyond with lower AR and higher CS; faster than the window no AR nerf; repeats −0.84% / −0.87% each.
        /// Longer maps — by their loaded parts (98% / 100% / 103% + 5% per part from the 3rd + 0.2% per second; over 8 min 95 / 97 / 100 + 2% +
        /// 0.15%), repeats −0.67% each, DA lower CS −0.65% and higher AR −0.23% per division. Repeats at most −15%.
        /// </summary>
        public static AimSlopParts AimSlop(RxOwnerShape.Shape shape, RxLoadSections.Result load, Play play)
        {
            double seconds = shape.LengthMs / 1000;
            double bpm = shape.MainBpm * play.ClockRate;
            (int repeats, int most) = Repeats(shape, load);

            double coverage = shape.LengthMs > 0 ? load.TotalSeconds * play.ClockRate * 1000 / shape.LengthMs : 0;
            double farm = RxMath.SmoothStep(shape.WideBackAndForthShare, 0.3, 0.6) * RxMath.SmoothStep(coverage, 0.5, 0.8) * (most >= 3 ? 1 : 0);

            double high = RxMath.SmoothStep(play.ClockRate, 1.25, 1.35);
            double shortValue = (1 - high) * shortRule(rule_low_rate, bpm, farm, repeats, play) + high * shortRule(rule_high_rate, bpm, farm, repeats, play);

            double longValue = longRule(load, seconds, repeats, play);
            double lengthWeight = RxMath.SmoothStep(seconds, 80, 100);
            return new AimSlopParts((1 - lengthWeight) * shortValue + lengthWeight * longValue, farm, repeats, shortValue, longValue);
        }

        private static double shortRule(ShortRule rule, double bpm, double farm, int repeats, Play play)
        {
            double inWindow = RxMath.SmoothStep(bpm, rule.BpmFrom - 10, rule.BpmFrom + 10) * (1 - RxMath.SmoothStep(bpm, rule.BpmTo - 10, rule.BpmTo + 10));
            double above = RxMath.SmoothStep(bpm, rule.BpmTo - 10, rule.BpmTo + 10);
            double ar = Math.Max(0, divisions(FARM_AR - play.ScreenAr));

            double windowValue = rule.Farm + rule.ArSlope * ar + rule.CsSlope * Math.Max(0, divisions(play.AimSlopCs - rule.CsReference));
            double aboveValue = 1 + 0.0072 * ar + 0.0057 * Math.Max(0, divisions(play.AimSlopCs - 3.8));

            double value = 1 + farm * (inWindow * (windowValue - 1) + above * (aboveValue - 1));
            return value - Math.Min(REPEAT_PENALTY_LIMIT, repeats * rule.RepeatPenalty);
        }

        private static double longRule(RxLoadSections.Result load, double seconds, int repeats, Play play)
        {
            double mid = 0, longParts = 0, midSeconds = 0;
            foreach (double s in load.Seconds)
            {
                double atLeast10 = RxMath.SmoothStep(s, 9, 11), atLeast30 = RxMath.SmoothStep(s, 28, 32);
                mid += atLeast10 * (1 - atLeast30);
                midSeconds += s * atLeast10 * (1 - atLeast30);
                longParts += atLeast30;
            }

            double many = RxMath.SmoothStep(mid, 2, 3);
            double under8 = (1 - many) * (0.98 + 0.02 * Math.Min(1, longParts)) + many * (1.03 + 0.05 * Math.Max(0, mid - 2) + 0.002 * midSeconds);
            double over8 = (1 - many) * (0.95 + 0.02 * Math.Min(1, longParts)) + many * (1.00 + 0.02 * Math.Max(0, mid - 2) + 0.0015 * midSeconds);
            double parts = under8 + (over8 - under8) * RxMath.SmoothStep(seconds, 450, 510);

            double csDown = 0.0065 * Math.Max(0, divisions(play.BaseCs - play.PlayedCs));
            double arUp = 0.0023 * Math.Max(0, divisions(play.PlayedAr - play.BaseAr));
            return parts - Math.Min(REPEAT_PENALTY_LIMIT, repeats * 0.0067) - csDown - arUp;
        }

        /// <summary>
        /// Repeated patterns inside the loaded parts: for every pattern repeated 3+ times, the number of its (non-overlapping) copies.
        /// Returns their sum and the most copies of one pattern.
        /// </summary>
        public static (int Repeats, int Most) Repeats(RxOwnerShape.Shape shape, RxLoadSections.Result load)
        {
            bool loaded(int index) => index < shape.NoteTimes.Length && load.Contains(shape.NoteTimes[index]);

            var blocks = shape.Blocks.Where(b => loaded(b.Start)).ToList();
            var blockIndex = blocks.Select((b, i) => (b.Start, i)).ToDictionary(p => p.Start, p => p.i);
            int[] parent = Enumerable.Range(0, blocks.Count).ToArray();
            int find(int i) => parent[i] == i ? i : parent[i] = find(parent[i]);

            int[] copies = new int[blocks.Count];
            for (int i = 0; i < blocks.Count; i++)
            {
                var taken = new List<int> { blocks[i].Start };
                foreach (int start in blocks[i].Matches)
                {
                    if (!loaded(start) || taken.Any(t => Math.Abs(t - start) < RxOwnerShape.PATTERN_NOTES))
                        continue;

                    taken.Add(start);
                    if (blockIndex.TryGetValue(start, out int other))
                        parent[find(other)] = find(i);
                }

                copies[i] = taken.Count;
            }

            var patterns = new Dictionary<int, int>();
            for (int i = 0; i < blocks.Count; i++)
                patterns[find(i)] = Math.Max(patterns.GetValueOrDefault(find(i)), copies[i]);

            return (patterns.Values.Where(n => n >= 3).Sum(), patterns.Count > 0 ? patterns.Values.Max() : 0);
        }

        private static double divisions(double difference) => Math.Round(difference * 10, MidpointRounding.AwayFromZero);
    }
}
