// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeBulletPlacementUtilsTest
    {
        [Test]
        public void TestCursorDistanceDoesNotChangeLockedTravel()
        {
            var start = new Vector2(100, 100);
            const float speed = 0.2f;
            const double duration = 1000;

            Vector2 near = DodgeBulletPlacementUtils.GetEndPositionForSpeed(start, new Vector2(110, 100), speed, duration);
            Vector2 far = DodgeBulletPlacementUtils.GetEndPositionForSpeed(start, new Vector2(500, 100), speed, duration);

            Assert.Multiple(() =>
            {
                Assert.That(near, Is.EqualTo(new Vector2(300, 100)));
                Assert.That(far, Is.EqualTo(near));
            });
        }

        [Test]
        public void TestCursorStillControlsDirection()
        {
            var start = new Vector2(100, 100);

            Vector2 end = DodgeBulletPlacementUtils.GetEndPositionForSpeed(start, new Vector2(100, 500), 0.2f, 1000);

            Assert.That(end, Is.EqualTo(new Vector2(100, 300)));
        }

        [Test]
        public void TestSpeedCanBeInheritedFromPreviousBullet()
        {
            float? speed = DodgeBulletPlacementUtils.GetTravelSpeed(new Vector2(100, 100), new Vector2(300, 100), 1000);

            Assert.That(speed, Is.EqualTo(0.2f));
        }
    }
}
