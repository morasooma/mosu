using NUnit.Framework;
using osu.Game.Rulesets.Osu.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Scoring
{
    [TestFixture]
    public class StableSliderPositionTest
    {
        [Test]
        public void TestRepeatUsesIntegerSegmentEndpoints()
        {
            var position = new StableSliderPosition(new[] { Vector2.Zero, new Vector2(10, 0), new Vector2(30, 0) }, 1000, 0.7, 2);
            Assert.Multiple(() =>
            {
                Assert.That(position.PositionAt(1000), Is.EqualTo(Vector2.Zero));
                Assert.That(position.PositionAt(1014), Is.EqualTo(new Vector2(10, 0)));
                Assert.That(position.PositionAt(1042), Is.EqualTo(new Vector2(30, 0)));
                Assert.That(position.PositionAt(1043).X, Is.LessThan(30), "Repeat must already move backwards after the integer endpoint.");
                Assert.That(position.PositionAt(1071), Is.EqualTo(new Vector2(10, 0)));
                Assert.That(position.PositionAt(1085), Is.EqualTo(Vector2.Zero));
            });
        }

        [Test]
        public void TestSharedTimestampSelectsFirstSegment()
        {
            var position = new StableSliderPosition(new[] { Vector2.Zero, new Vector2(0.2f, 0), new Vector2(10, 0) }, 1000, 1, 1);
            Assert.That(position.PositionAt(1000), Is.EqualTo(new Vector2(0.2f, 0)));
            Assert.That(position.PositionAt(1015), Is.EqualTo(new Vector2(10, 0)));
        }
    }
}
