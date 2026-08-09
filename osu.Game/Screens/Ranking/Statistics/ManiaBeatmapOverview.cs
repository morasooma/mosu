// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Screens.Ranking.Statistics
{
    public partial class ManiaBeatmapOverview : CompositeDrawable
    {
        public ManiaBeatmapOverview(ManiaBeatmapSummary summary)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new SimpleStatisticTable(2, new SimpleStatisticItem[]
            {
                new LabelledStatistic("Keys") { Value = $"{summary.KeyCount}K" },
                new LabelledStatistic("Objects") { Value = summary.TotalObjectCount.ToString("N0") },
                new LabelledStatistic("Regular notes") { Value = summary.RegularNoteCount.ToString("N0") },
                new LabelledStatistic("Long notes") { Value = summary.LongNoteCount.ToString("N0") },
                new LabelledStatistic("LN share") { Value = $"{summary.LongNoteRatio:0}%" },
                new LabelledStatistic("Density") { Value = $"{summary.Density:0.0}/s" },
                new LabelledStatistic("Length") { Value = summary.Length.ToFormattedDuration().ToString() },
                new LabelledStatistic("BPM") { Value = summary.EffectiveBpm > 0 ? summary.EffectiveBpm.ToString() : "-" },
                new LabelledStatistic("Star rating") { Value = summary.StarRating >= 0 ? summary.StarRating.ToString("0.00") : "-" },
            });
        }

        private partial class LabelledStatistic : SimpleStatisticItem<string>
        {
            public LabelledStatistic(string name)
                : base(name)
            {
            }
        }
    }
}
