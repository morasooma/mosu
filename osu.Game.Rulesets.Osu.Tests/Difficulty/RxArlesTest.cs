// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxArlesTest
    {
        [Test]
        public void TestWideJumpsWithFlowBursts()
        {
            var beatmap = new Beatmap<OsuHitObject>();
            Vector2[] positions = { new Vector2(100, 100), new Vector2(400, 100), new Vector2(360, 140),
                new Vector2(320, 180), new Vector2(60, 260), new Vector2(360, 320) };
            double time = 0;
            for (int i = 0; i < 120; i++)
            {
                int part = i % positions.Length;
                time += part is 2 or 3 ? 75 : 250;
                beatmap.HitObjects.Add(new HitCircle { Position = positions[part], StartTime = time, Scale = 0.5f });
            }

            RxArles.Analysis result = RxArles.Analyze(beatmap);
            Assert.That(result.IsArles, Is.True);
            Assert.That(result.Multiplier, Is.EqualTo(1 + MosuPpBalance.Current.ArlesBonus).Within(1e-12));
        }

        [TestCase(250, 300)] // Fullscreen jumps alone have no burst/control evidence.
        [TestCase(75, 300)] // Repeated fast wide jumps alone have no slow/control evidence.
        [TestCase(75, 40)] // Tight straight flow.
        [TestCase(75, 0)] // Stacks.
        public void TestSimplePatternsReceiveNoBonus(double gap, float spacing)
        {
            var beatmap = new Beatmap<OsuHitObject>();
            for (int i = 0; i < 120; i++)
                beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(i % 2 * spacing, 100), StartTime = i * gap, Scale = 0.5f });

            RxArles.Analysis result = RxArles.Analyze(beatmap);
            Assert.That(result.IsArles, Is.False);
            Assert.That(result.Multiplier, Is.EqualTo(1));
        }

        [Test]
        public void TestEmptyBeatmapReceivesNoBonus()
        {
            RxArles.Analysis result = RxArles.Analyze(new Beatmap<OsuHitObject>());
            Assert.That(result.IsArles, Is.False);
            Assert.That(result.Multiplier, Is.EqualTo(1));
        }
    }
}
