// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuPatternClassifierTest
    {
        private const float radius = 20f;
        private const double beat_length = 500;

        [Test]
        public void TestClassifyTightStraightStream()
        {
            OsuPatternInfo pattern = classify(
                new Vector2(0, 0),
                new Vector2(40, 0),
                new Vector2(80, 0),
                new Vector2(120, 0),
                new Vector2(160, 0));

            Assert.Multiple(() =>
            {
                Assert.That(pattern.Kind, Is.EqualTo(OsuPatternKind.Stream));
                Assert.That(pattern.StreamShape, Is.EqualTo(OsuStreamShapeKind.Straight));
                Assert.That(pattern.StreamSpacing, Is.EqualTo(OsuStreamSpacingKind.Tight));
            });
        }

        [Test]
        public void TestClassifySpacedZigZagStream()
        {
            OsuPatternInfo pattern = classify(
                new Vector2(0, 0),
                new Vector2(100, 28),
                new Vector2(0, 56),
                new Vector2(100, 84),
                new Vector2(0, 112));

            Assert.Multiple(() =>
            {
                Assert.That(pattern.Kind, Is.EqualTo(OsuPatternKind.Stream));
                Assert.That(pattern.StreamShape, Is.EqualTo(OsuStreamShapeKind.ZigZag));
                Assert.That(pattern.StreamSpacing, Is.EqualTo(OsuStreamSpacingKind.Spaced));
            });
        }

        [Test]
        public void TestClassifyVariableSpacingStream()
        {
            OsuPatternInfo pattern = classify(
                new Vector2(0, 0),
                new Vector2(40, 0),
                new Vector2(104, 0),
                new Vector2(188, 0),
                new Vector2(296, 0));

            Assert.Multiple(() =>
            {
                Assert.That(pattern.Kind, Is.EqualTo(OsuPatternKind.Stream));
                Assert.That(pattern.StreamShape, Is.EqualTo(OsuStreamShapeKind.Straight));
                Assert.That(pattern.StreamSpacing, Is.EqualTo(OsuStreamSpacingKind.Variable));
            });
        }

        [Test]
        public void TestClassifyLowDensityBrokenFlowAsFlowAim()
        {
            OsuPatternInfo pattern = classify(
                new[]
                {
                    new Vector2(0, 0),
                    new Vector2(56, 10),
                    new Vector2(118, 6),
                    new Vector2(182, 22),
                    new Vector2(246, 18),
                },
                new[] { 0d, 245d, 500d, 750d, 1000d });

            Assert.Multiple(() =>
            {
                Assert.That(pattern.SupportsFlowAim, Is.True);
                Assert.That(pattern.SupportsRailHandoff, Is.True);
                Assert.That(pattern.IsLowDensityFlow, Is.True);
            });
        }

        [Test]
        public void TestClassifyGentleFlow()
        {
            OsuPatternInfo pattern = classify(
                new[]
                {
                    new Vector2(0, 0),
                    new Vector2(42, 6),
                    new Vector2(84, 10),
                    new Vector2(126, 15),
                    new Vector2(168, 18),
                },
                new[] { 0d, 235d, 470d, 705d, 940d });

            Assert.Multiple(() =>
            {
                Assert.That(pattern.SupportsFlowAim, Is.True);
                Assert.That(pattern.IsGentleFlow, Is.True);
            });
        }

        [Test]
        public void TestClassifyCompressedMovingFlowAsStreamNotStack()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(0, 0), 0, 32f),
                createNode(new Vector2(10, 1), 110, 32f),
                createNode(new Vector2(20, 2), 220, 32f),
                createNode(new Vector2(30, 3), 330, 32f),
                createNode(new Vector2(40, 4), 440, 32f),
            };

            OsuPatternInfo pattern = OsuPatternClassifier.Classify(nodes, 2, beat_length);

            Assert.Multiple(() =>
            {
                Assert.That(pattern.Kind, Is.EqualTo(OsuPatternKind.Stream));
                Assert.That(pattern.SupportsFlowAim, Is.True);
            });
        }

        [Test]
        public void TestClassifyCompressedStreamStartAsFlowInsteadOfStack()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(0, 0), 0, 32f),
                createNode(new Vector2(8, 1), 90, 32f),
                createNode(new Vector2(16, 2), 180, 32f),
                createNode(new Vector2(24, 3), 270, 32f),
                createNode(new Vector2(32, 4), 360, 32f),
            };

            OsuPatternInfo pattern = OsuPatternClassifier.Classify(nodes, 0, beat_length);

            Assert.Multiple(() =>
            {
                Assert.That(pattern.Kind, Is.Not.EqualTo(OsuPatternKind.Stack));
                Assert.That(pattern.SupportsFlowAim, Is.True);
            });
        }

        [Test]
        public void TestClassifyTripleAsBurst()
        {
            OsuPatternInfo pattern = classify(
                new Vector2(0, 0),
                new Vector2(48, 0),
                new Vector2(96, 0));

            Assert.That(pattern.Kind, Is.EqualTo(OsuPatternKind.Burst));
        }

        [Test]
        public void TestClassifyStack()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(100, 100), 0),
                createNode(new Vector2(104, 101), 90),
                createNode(new Vector2(140, 100), 180),
            };

            OsuPatternInfo pattern = OsuPatternClassifier.Classify(nodes, 1, beat_length);

            Assert.That(pattern.Kind, Is.EqualTo(OsuPatternKind.Stack));
        }

        [Test]
        public void TestSegmentAnalyzerKeepsJumpChainOutOfFlow()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(0, 0), 0, 20f),
                createNode(new Vector2(180, 40), 260, 20f),
                createNode(new Vector2(40, 210), 520, 20f),
                createNode(new Vector2(240, 80), 780, 20f),
            };

            OsuPatternState[] states = OsuPatternSegmentAnalyzer.Analyze(nodes, _ => beat_length);

            Assert.Multiple(() =>
            {
                Assert.That(states[1].Candidate, Is.EqualTo(OsuPatternSegmentKind.Jump));
                Assert.That(states[2].Candidate, Is.EqualTo(OsuPatternSegmentKind.Jump));
                Assert.That(states[1].SupportsFlowPath, Is.False);
            });
        }

        [Test]
        public void TestSegmentAnalyzerPreservesCompressedStreamSegment()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(0, 0), 0, 32f),
                createNode(new Vector2(9, 1), 95, 32f),
                createNode(new Vector2(18, 2), 190, 32f),
                createNode(new Vector2(27, 3), 285, 32f),
                createNode(new Vector2(36, 4), 380, 32f),
            };

            OsuPatternState[] states = OsuPatternSegmentAnalyzer.Analyze(nodes, _ => beat_length);

            Assert.Multiple(() =>
            {
                Assert.That(states[0].Candidate, Is.EqualTo(OsuPatternSegmentKind.Stream));
                Assert.That(states[2].Candidate, Is.EqualTo(OsuPatternSegmentKind.Stream));
                Assert.That(states[0].SegmentEndIndex, Is.EqualTo(4));
            });
        }

        [Test]
        public void TestSegmentAnalyzerPromotesFlowEntryIntoUpcomingStream()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(0, 0), 0, 28f),
                createNode(new Vector2(34, 4), 110, 28f),
                createNode(new Vector2(68, 7), 220, 28f),
                createNode(new Vector2(102, 10), 330, 28f),
            };

            OsuPatternState[] states = OsuPatternSegmentAnalyzer.Analyze(nodes, _ => beat_length);

            Assert.Multiple(() =>
            {
                Assert.That(states[0].SupportsFlowPath, Is.True);
                Assert.That(states[0].Candidate, Is.EqualTo(OsuPatternSegmentKind.Stream));
            });
        }

        [Test]
        public void TestSegmentAnalyzerMarksSecondNoteOfFastTripleAsBurstFlow()
        {
            OsuPatternNode[] nodes =
            {
                createNode(new Vector2(0, 0), 0, 28f),
                createNode(new Vector2(36, 0), 83, 28f),
                createNode(new Vector2(72, 0), 166, 28f),
            };

            OsuPatternState[] states = OsuPatternSegmentAnalyzer.Analyze(nodes, _ => beat_length);

            Assert.Multiple(() =>
            {
                Assert.That(states[1].PatternInfo.Kind, Is.EqualTo(OsuPatternKind.Burst));
                Assert.That(states[1].PatternInfo.ChainLength, Is.EqualTo(3));
                Assert.That(states[1].SupportsFlowPath, Is.True);
            });
        }

        private static OsuPatternInfo classify(params Vector2[] positions)
        {
            OsuPatternNode[] nodes = new OsuPatternNode[positions.Length];

            for (int i = 0; i < positions.Length; i++)
                nodes[i] = createNode(positions[i], i * 120);

            return OsuPatternClassifier.Classify(nodes, positions.Length / 2, beat_length);
        }

        private static OsuPatternInfo classify(Vector2[] positions, double[] times)
        {
            Assert.That(positions.Length, Is.EqualTo(times.Length));

            OsuPatternNode[] nodes = new OsuPatternNode[positions.Length];

            for (int i = 0; i < positions.Length; i++)
                nodes[i] = createNode(positions[i], times[i]);

            return OsuPatternClassifier.Classify(nodes, positions.Length / 2, beat_length);
        }

        private static OsuPatternNode createNode(Vector2 position, double time)
            => new OsuPatternNode(position, time, radius, true);

        private static OsuPatternNode createNode(Vector2 position, double time, float nodeRadius)
            => new OsuPatternNode(position, time, nodeRadius, true);
    }
}
