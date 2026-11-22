using System;
using NUnit.Framework;
using osu.Game.Graphics.Cursor;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class TooltipPositionFilterTest
    {
        [Test]
        public void FirstPositionAndResetAreImmediate()
        {
            var filter = new TooltipPositionFilter();
            Assert.That(filter.Filter(new Vector2(100), 0), Is.EqualTo(new Vector2(100)));
            filter.Filter(new Vector2(110), 16);
            filter.Reset();
            Assert.That(filter.Filter(new Vector2(200), 32), Is.EqualTo(new Vector2(200)));
        }

        [TestCase(60)]
        [TestCase(144)]
        [TestCase(240)]
        public void SuppressesStationaryPenJitter(int fps)
        {
            var filter = new TooltipPositionFilter();
            filter.Filter(Vector2.Zero, 0);
            double inputEnergy = 0;
            double outputEnergy = 0;
            for (int frame = 1; frame <= fps * 3; frame++)
            {
                double seconds = (double)frame / fps;
                var input = new Vector2((float)(3 * Math.Sin(2 * Math.PI * 12 * seconds)), 0);
                var output = filter.Filter(input, seconds * 1000);
                if (frame > fps)
                {
                    inputEnergy += input.LengthSquared;
                    outputEnergy += output.LengthSquared;
                }
            }

            Assert.That(outputEnergy / inputEnergy, Is.LessThan(0.12));
        }

        [TestCase(60)]
        [TestCase(144)]
        [TestCase(240)]
        public void FastMovementHasShortLagAndNoOvershoot(int fps)
        {
            var filter = new TooltipPositionFilter();
            filter.Filter(Vector2.Zero, 0);
            Vector2 result = Vector2.Zero;
            for (int frame = 1; frame <= fps; frame++)
            {
                var input = new Vector2(600f * frame / fps, 600f * frame / fps);
                result = filter.Filter(input, frame * 1000.0 / fps);
                Assert.That(result.X, Is.InRange(0, input.X));
                Assert.That(result.X, Is.EqualTo(result.Y));
            }

            // At 600 units/s, 24 units corresponds to at most 40 ms trailing distance.
            Assert.That(600 - result.X, Is.LessThan(24));
            for (int frame = 1; frame <= fps / 4; frame++)
                result = filter.Filter(new Vector2(600), 1000 + frame * 1000.0 / fps);
            Assert.That((new Vector2(600) - result).Length, Is.LessThan(1));
        }

        [Test]
        public void ComparableResponseAtDifferentFrameRates()
        {
            float response(int fps)
            {
                var filter = new TooltipPositionFilter();
                filter.Filter(Vector2.Zero, 0);
                Vector2 result = Vector2.Zero;
                for (int frame = 1; frame <= fps; frame++)
                    result = filter.Filter(new Vector2(100f * frame / fps, 0), frame * 1000.0 / fps);
                return result.X;
            }

            Assert.That(response(60), Is.EqualTo(response(240)).Within(2));
        }

        [TestCase(101)]
        [TestCase(-1)]
        public void ClockDiscontinuityResets(double time)
        {
            var filter = new TooltipPositionFilter();
            filter.Filter(Vector2.Zero, 0);
            Assert.That(filter.Filter(new Vector2(10), time), Is.EqualTo(new Vector2(10)));
        }

        [Test]
        public void TeleportResets()
        {
            var filter = new TooltipPositionFilter();
            filter.Filter(Vector2.Zero, 0);
            Assert.That(filter.Filter(new Vector2(1000), 16), Is.EqualTo(new Vector2(1000)));
        }

        [Test]
        public void SameTimestampDoesNotCorruptState()
        {
            var filter = new TooltipPositionFilter();
            filter.Filter(Vector2.Zero, 0);
            Assert.That(filter.Filter(new Vector2(10), 0), Is.EqualTo(Vector2.Zero));
            Assert.That(filter.Filter(new Vector2(10), 16).X, Is.InRange(0.01f, 9.99f));
        }
    }
}
