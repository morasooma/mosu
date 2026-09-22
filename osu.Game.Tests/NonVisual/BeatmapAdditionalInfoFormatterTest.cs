// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BeatmapAdditionalInfoFormatterTest
    {
        [Test]
        public void TestUnavailablePerformance()
        {
            Assert.That(BeatmapAdditionalInfoFormatter.Format(new StarDifficulty(4.2, 1234)),
                Is.EqualTo("Combo: 1234x | PP: -"));
        }

        [Test]
        public void TestPersistedAdditionalInfo()
        {
            Assert.That(BeatmapAdditionalInfoFormatter.Format(new StarDifficulty(5.2, 123, "Combo: 123x | PP: 321 pp (Aim: 200pp)")),
                Is.EqualTo("Combo: 123x | PP: 321 pp (Aim: 200pp)"));
        }

        [Test]
        public void TestPerformanceBreakdown()
        {
            var difficulty = new DifficultyAttributes
            {
                StarRating = 6.2,
                MaxCombo = 987,
            };
            var performance = new TestPerformanceAttributes
            {
                Total = 456.4,
            };

            Assert.That(BeatmapAdditionalInfoFormatter.Format(new StarDifficulty(difficulty, performance)),
                Is.EqualTo("Combo: 987x | PP: 456 pp (Aim: 321pp)"));
        }

        private sealed class TestPerformanceAttributes : PerformanceAttributes
        {
            public override IEnumerable<PerformanceDisplayAttribute> GetAttributesForDisplay()
            {
                yield return new PerformanceDisplayAttribute(nameof(Total), "Total", Total);
                yield return new PerformanceDisplayAttribute("Aim", "Aim", 321.2);
            }
        }
    }
}
