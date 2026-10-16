// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using MosuPpRxCs;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class RxSectionTypesTest
    {
        private const int sections = 20;

        [Test]
        public void TestSectionSharesFollowTheDifficultyWeighting()
        {
            double[] shares = RxSectionTypes.SectionShares(new[] { 1f, 3f, 2f, 1f });

            Assert.Multiple(() =>
            {
                Assert.That(shares.Sum(), Is.EqualTo(1).Within(1e-9));
                Assert.That(shares[1], Is.GreaterThan(shares[2]));
                Assert.That(shares[2], Is.GreaterThan(shares[0]));
            });
        }

        [Test]
        public void TestMapWithoutStreams()
            => Assert.That(RxSectionTypes.StreamShare(jumpsThenStreams(streams: false), result(1, 1)), Is.EqualTo(0));

        [Test]
        public void TestStreamsEarnTheirSectionsShare()
        {
            // First half jumps, second half a straight stream; with equal section peaks the streams earn about half of the PP.
            double even = RxSectionTypes.StreamShare(jumpsThenStreams(streams: true), result(1, 1));
            // The same map whose stream sections are three times as hard: the streams earn most of it.
            double hardStreams = RxSectionTypes.StreamShare(jumpsThenStreams(streams: true), result(1, 3));

            Assert.Multiple(() =>
            {
                Assert.That(even, Is.InRange(0.4, 0.6));
                Assert.That(hardStreams, Is.GreaterThan(0.7));
            });
        }

        [TestCase(0, 1)]
        [TestCase(0.5, 0.5)]
        [TestCase(1, 0)]
        public void TestStreamNerfOnlyHitsTheStreamsShare(double streamWeight, double expectedShareKept)
        {
            double nerf = MosuPpBalance.Current.StreamNerf;
            Assert.That(RxStreamMapNerf.Multiplier(2.0, streamWeight), Is.EqualTo(1 - nerf * (1 - expectedShareKept)).Within(1e-12));
        }

        /// <summary>4 s of jumps (one note per 200 ms), then 4 s of a straight stream (one note per 100 ms); CS 4.</summary>
        private static RxBeatmap jumpsThenStreams(bool streams)
        {
            var beatmap = new RxBeatmap { CircleSize = 4 };

            for (int i = 0; i < 20; i++)
                beatmap.HitObjects.Add(circle(i * 200, i % 2 == 0 ? new RxVec2(100, 100) : new RxVec2(400, 300)));

            for (int i = 0; i < 40; i++)
            {
                beatmap.HitObjects.Add(streams
                    ? circle(4000 + i * 100, new RxVec2(20 + i * 30, 200))
                    : circle(4000 + i * 200, i % 2 == 0 ? new RxVec2(100, 100) : new RxVec2(400, 300)));
            }

            return beatmap;
        }

        private static RxHitObject circle(double time, RxVec2 position)
            => new RxHitObject { StartTime = time, Position = position, Kind = RxHitObjectKind.Circle };

        /// <summary>Equal aim and speed PP; jump sections 0–9 have peak 1, stream sections 10–19 <paramref name="streamPeak"/>.</summary>
        private static RxNativePerformanceResult result(float jumpPeak, float streamPeak)
        {
            float[] peaks = Enumerable.Range(0, sections).Select(k => k < sections / 2 ? jumpPeak : streamPeak).ToArray();

            return new RxNativePerformanceResult
            {
                Difficulty = new RxDifficultyAttributes
                {
                    AimSectionPeaks = peaks,
                    SpeedSectionPeaks = peaks,
                    FirstSectionEnd = 400,
                    SectionLength = 400,
                },
                PpAim = 100,
                PpSpeed = 100,
            };
        }
    }
}
