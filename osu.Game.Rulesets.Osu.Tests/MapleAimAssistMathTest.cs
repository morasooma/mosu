using NUnit.Framework;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class MapleAimAssistMathTest
    {
        [Test]
        public void TestV2MatchesExtractedSmoothingEquation()
        {
            Vector2 offset = MapleAimAssistMath.UpdateV2(Vector2.Zero, Vector2.Zero, new Vector2(2, 0),
                new Vector2(20, 0), 0.6f, 1, 384, 30, true);
            Assert.That(offset.X, Is.EqualTo(1.25f).Within(0.00001));
            Assert.That(offset.Y, Is.Zero);
        }

        [TestCase(false, 2f)]
        [TestCase(true, 1.75f)]
        public void TestV2RequiresPreviousTargetAndMovement(bool hasPrevious, float movement)
        {
            Assert.That(MapleAimAssistMath.UpdateV2(Vector2.Zero, Vector2.Zero, new Vector2(movement, 0),
                new Vector2(20, 0), 0.6f, 1, 384, 30, hasPrevious), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestV2FovMatchesExtractedTimingEquation()
        {
            Assert.That(MapleAimAssistMath.V2Radius(900, 1000, 600, 100, 0.6f, 1), Is.EqualTo(33.6f).Within(0.00001));
            Assert.That(MapleAimAssistMath.V2Radius(10000, 1000, 600, 100, 0.6f, 1), Is.EqualTo(60).Within(0.00001));
        }

        [Test]
        public void TestV3MatchesExtractedMovementEquation()
        {
            Vector2 offset = v3(Vector2.Zero, new Vector2(10, 0), new Vector2(50, 0));
            Assert.That(offset.X, Is.EqualTo(7.3f).Within(0.00001));
            Assert.That(MapleAimAssistMath.V3Radius(1.2f, 32), Is.EqualTo(93.44f).Within(0.00001));
        }

        [Test]
        public void TestV3DoesNotMoveStationaryHand()
            => Assert.That(v3(new Vector2(5, 0), Vector2.Zero, new Vector2(50, 0)), Is.EqualTo(new Vector2(5, 0)));

        [Test]
        public void TestV3SlowsInsideCircle()
        {
            Assert.That(v3(Vector2.Zero, new Vector2(10, 0), new Vector2(20, 0)).X,
                Is.EqualTo(7.3f * 0.65f).Within(0.00001));
        }

        [Test]
        public void TestV3SliderMultiplierAndInnerDeadzone()
        {
            Vector2 offset = MapleAimAssistMath.UpdateV3(Vector2.Zero, Vector2.Zero, new Vector2(10, 0), new Vector2(5, 0),
                1.2f, 0.6f, 1, 32, true, true, true);
            Assert.That(offset.X, Is.EqualTo(0.65f * 0.73f * (5 / 11.2f) * 0.6f * 5).Within(0.00001));
            Assert.That(MapleAimAssistMath.UpdateV3(Vector2.Zero, Vector2.Zero, new Vector2(10, 0), new Vector2(50, 0),
                1.2f, 0, 1, 32, true, true, true), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestV3ClampsEachAxisLikeExtractedCode()
        {
            Vector2 offset = v3(new Vector2(19, 19), new Vector2(100, 100), new Vector2(50, 50));
            Assert.That(offset.X, Is.EqualTo(19.2f).Within(0.00001));
            Assert.That(offset.Y, Is.EqualTo(19.2f).Within(0.00001));
        }

        [Test]
        public void TestReleaseDependsOnMovementAndNeverCrossesZero()
        {
            Vector2 offset = new Vector2(2, -3);
            Assert.That(MapleAimAssistMath.Resync(offset, Vector2.Zero, 0.3f), Is.EqualTo(offset));
            Assert.That(MapleAimAssistMath.Resync(offset, new Vector2(100, 200), 0.3f), Is.EqualTo(new Vector2(1.97f, -2.94f)));
            Assert.That(MapleAimAssistMath.Resync(offset, new Vector2(10000, 10000), 1000), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestV3OutsideFovReleasesInsteadOfAcquiring()
        {
            Assert.That(v3(Vector2.Zero, new Vector2(10, 0), new Vector2(200, 0)), Is.EqualTo(Vector2.Zero));
            Assert.That(MapleAimAssistMath.UpdateV3(Vector2.Zero, Vector2.Zero, new Vector2(10, 0), new Vector2(50, 0),
                1.2f, 0.6f, 1, 32, false, false, false), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestZeroPowerClearsOldOffset()
        {
            Assert.That(MapleAimAssistMath.UpdateV2(Vector2.One, Vector2.Zero, Vector2.Zero, Vector2.One,
                0, 1, 384, 0, true), Is.EqualTo(Vector2.Zero));
            Assert.That(MapleAimAssistMath.UpdateV3(Vector2.One, Vector2.Zero, Vector2.Zero, Vector2.One,
                0, 0.6f, 1, 32, false, false, true), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void TestScreenScaleDoesNotChangePlayfieldCorrection()
        {
            Vector2 normal = v3(Vector2.Zero, new Vector2(10, 0), new Vector2(50, 0));
            Vector2 doubled = MapleAimAssistMath.UpdateV3(Vector2.Zero, Vector2.Zero, new Vector2(20, 0), new Vector2(100, 0),
                1.2f, 0.6f, 2, 64, false, false, true);
            Assert.That(doubled, Is.EqualTo(normal * 2));
        }

        [TestCase(0.5f)]
        [TestCase(0.7f)]
        [TestCase(1f)]
        [TestCase(1.6f)]
        [TestCase(2.25f)]
        public void TestV2FieldUnitsAreInvariantUnderScreenScaleAndTranslation(float scale)
        {
            Vector2 origin = new Vector2(143, 87);
            Vector2 raw = new Vector2(240, 190);
            Vector2 target = new Vector2(260, 195);
            Vector2 movement = new Vector2(3, 1);
            Vector2 previousOffset = new Vector2(2, -1);
            float fov = MapleAimAssistMath.V2Radius(900, 1000, 600, 100, 0.6f, 1);
            Vector2 expected = MapleAimAssistMath.UpdateV2(previousOffset, raw, movement, target, 0.6f, 1, 384, fov, true);
            Vector2 actual = MapleAimAssistMath.UpdateV2(previousOffset * scale, origin + raw * scale, movement * scale,
                origin + target * scale, 0.6f, scale, 384 * scale, fov * scale, true);
            Assert.That(Vector2.Distance(actual / scale, expected), Is.LessThan(0.0001f));
        }

        private static Vector2 v3(Vector2 offset, Vector2 movement, Vector2 target)
            => MapleAimAssistMath.UpdateV3(offset, Vector2.Zero, movement, target, 1.2f, 0.6f, 1, 32, false, false, true);
    }
}
