// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania
{
    public readonly struct ManiaBeatmapSummary
    {
        public int KeyCount { get; }

        public int TotalObjectCount { get; }

        public int LongNoteCount { get; }

        public int RegularNoteCount => Math.Max(0, TotalObjectCount - LongNoteCount);

        public double LongNoteRatio => TotalObjectCount > 0 ? LongNoteCount * 100.0 / TotalObjectCount : 0;

        public double Length { get; }

        public int EffectiveBpm { get; }

        public double Density => Length > 0 ? TotalObjectCount / (Length / 1000.0) : 0;

        public double StarRating { get; }

        public bool HasObjectStatistics => TotalObjectCount > 0;

        public ManiaBeatmapSummary(int keyCount, int totalObjectCount, int longNoteCount, double length, int effectiveBpm, double starRating)
        {
            KeyCount = keyCount;
            TotalObjectCount = Math.Max(0, totalObjectCount);
            LongNoteCount = Math.Max(0, longNoteCount);
            Length = Math.Max(0, length);
            EffectiveBpm = Math.Max(0, effectiveBpm);
            StarRating = starRating;
        }

        public static ManiaBeatmapSummary? Create(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IReadOnlyList<Mod> mods)
        {
            if (rulesetInfo.OnlineID != 3)
                return null;

            var ruleset = rulesetInfo.CreateInstance();
            var method = ruleset.GetType().GetMethod("GetKeyCount");
            if (method == null)
                return null;

            double rate = ModUtils.CalculateRateWithMods(mods);
            if (method.Invoke(ruleset, new object[] { beatmapInfo, mods }) is not int keyCount)
                return null;

            return new ManiaBeatmapSummary(
                keyCount,
                beatmapInfo.TotalObjectCount,
                beatmapInfo.EndTimeObjectCount,
                beatmapInfo.Length / Math.Max(rate, 0.01),
                FormatUtils.RoundBPM(beatmapInfo.BPM, rate),
                beatmapInfo.StarRating);
        }
    }
}
