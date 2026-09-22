// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Select;
using osu.Game.Screens.SelectLegacy.Carousel;

namespace osu.Game.Tests.NonVisual.Filtering
{
    [TestFixture]
    public class LegacyFilterMatchingTest
    {
        [Test]
        public void TestManiaKeyCountQuery()
        {
            var mania = new ManiaRuleset().RulesetInfo;
            var beatmaps = new[]
            {
                new CarouselBeatmap(new BeatmapInfo(mania, new BeatmapDifficulty { CircleSize = 4 })),
                new CarouselBeatmap(new BeatmapInfo(mania, new BeatmapDifficulty { CircleSize = 7 })),
            };
            var criteria = new FilterCriteria { Ruleset = mania };

            FilterQueryParser.ApplyQueries(criteria, "key=4");

            foreach (var beatmap in beatmaps)
                beatmap.Filter(criteria);

            Assert.That(beatmaps.Where(b => !b.Filtered.Value).Select(b => b.BeatmapInfo.Difficulty.CircleSize), Is.EqualTo(new[] { 4 }));
        }
    }
}
