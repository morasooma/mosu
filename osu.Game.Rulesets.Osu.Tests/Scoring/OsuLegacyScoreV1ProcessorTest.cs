// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.Scoring
{
    [TestFixture]
    public class OsuLegacyScoreV1ProcessorTest
    {
        [Test]
        public void TestCircleComboScoringAndRevert()
        {
            var beatmap = createBeatmap();
            var processor = new OsuLegacyScoreV1Processor(beatmap, Array.Empty<Mod>());

            JudgementResult first = circleResult(HitResult.Great);
            JudgementResult second = circleResult(HitResult.Great);
            JudgementResult third = circleResult(HitResult.Great);

            processor.ApplyResult(first);
            processor.ApplyResult(second);
            processor.ApplyResult(third);

            Assert.That(processor.TotalScore, Is.EqualTo(948));

            processor.RevertResult(third);
            Assert.That(processor.TotalScore, Is.EqualTo(600));
        }

        [Test]
        public void TestSliderTicksAndMissResetCombo()
        {
            var processor = new OsuLegacyScoreV1Processor(createBeatmap(), Array.Empty<Mod>());

            processor.ApplyResult(circleResult(HitResult.Great));
            processor.ApplyResult(new JudgementResult(new SliderTick(), new SliderTickJudgement()) { Type = HitResult.LargeTickHit });
            processor.ApplyResult(new JudgementResult(new SliderTick(), new SliderTickJudgement()) { Type = HitResult.LargeTickMiss });
            processor.ApplyResult(circleResult(HitResult.Great));

            Assert.That(processor.TotalScore, Is.EqualTo(610));
        }

        private static OsuBeatmap createBeatmap()
        {
            var beatmap = new OsuBeatmap
            {
                Difficulty = new BeatmapDifficulty
                {
                    DrainRate = 5,
                    OverallDifficulty = 5,
                    CircleSize = 5,
                },
            };

            beatmap.HitObjects.Add(new HitCircle { StartTime = 0 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 });
            return beatmap;
        }

        private static JudgementResult circleResult(HitResult result) => new JudgementResult(new HitCircle(), new OsuJudgement()) { Type = result };
    }
}
