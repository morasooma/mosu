// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class RelaxAimIntentTest
    {
        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestNearPassCommitsToRhythmAtDifferentRates(double rate)
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, 1.15f), 970 * rate, 1000 * rate, 5 * rate, rate);
            intent.Observe(new Vector2(0.7f, 1.15f), 990 * rate, 1000 * rate, 5 * rate, rate);

            Assert.Multiple(() =>
            {
                Assert.That(intent.PressTime, Is.EqualTo(1000 * rate));
                Assert.That(intent.IsReady(999 * rate), Is.False);
                Assert.That(intent.IsReady(1000 * rate), Is.True);
                Assert.That(intent.IsReady(1041 * rate), Is.False);
            });
        }

        [TestCase(1)]
        [TestCase(8)]
        [TestCase(16)]
        [TestCase(32)]
        public void TestSweptPassDoesNotRequireAnInsideFrame(double frameDuration)
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-2, 1.15f), 1000 - frameDuration / 2, 1000, 5, 1);
            intent.Observe(new Vector2(2, 1.15f), 1000 + frameDuration / 2, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.EqualTo(1005).Within(0.001));
        }

        [TestCase(1.36f)]
        [TestCase(2)]
        [TestCase(5)]
        public void TestDistantPassDoesNotCommit(float distance)
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, distance), 980, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, distance), 1000, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.Null);
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(16)]
        public void TestSlowMovementAccumulatesAtHighFrameRates(int frameDuration)
        {
            var intent = new RelaxController.AimIntentState();
            for (int elapsed = 0; elapsed <= 32; elapsed += frameDuration)
                intent.Observe(new Vector2(-0.06f + elapsed * 0.004f, 1.15f), 968 + elapsed, 1000, 5, 1);

            Assert.That(intent.PressTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestRewindingClearsCommitment()
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, 1.15f), 970, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), 990, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), 980, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.Null);
        }

        [TestCase(0)]
        [TestCase(0.01f)]
        [TestCase(0.03f)]
        public void TestStationaryCursorAndJitterDoNotCommit(float movement)
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(0, 1.15f), 980, 1000, 5, 1);
            intent.Observe(new Vector2(movement, 1.15f), 1000, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.Null);
        }

        [TestCase(800)]
        [TestCase(940)]
        [TestCase(1080)]
        public void TestPassOutsideObservationWindowDoesNotCommit(double time)
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, 1.15f), time - 10, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), time + 10, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.Null);
        }

        [Test]
        public void TestLatePassHasReactionWithoutWaitingForHitbox()
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, 1.15f), 1010, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), 1030, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.EqualTo(1025).Within(0.001));
        }

        [Test]
        public void TestJumpEdgePassDoesNotAuthorisePress()
        {
            Assert.That(RelaxController.CanUsePassToAuthorisePress(isJump: true, isStream: false), Is.False);
        }

        [Test]
        public void TestStreamPassMayAuthorisePress()
        {
            Assert.That(RelaxController.CanUsePassToAuthorisePress(isJump: false, isStream: true), Is.True);
        }

        [TestCase(0.99f, 0.01f, false)]
        [TestCase(0.75f, 0.2f, true)]
        [TestCase(0.92f, 0.1f, true)]
        public void TestJumpPressWaitsForInteriorCommitment(float normalisedDistance, float inwardProgress, bool expected)
        {
            Assert.That(RelaxController.HasJumpPressCommitment(normalisedDistance, inwardProgress), Is.EqualTo(expected));
        }

        [Test]
        public void TestCommitmentDoesNotFollowLaterAimCorrections()
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, 1.15f), 970, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), 990, 1000, 5, 1);
            intent.Observe(Vector2.Zero, 1005, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestStationarySamplesDoNotRenewExpiredCommitment()
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(-0.7f, 1.15f), 970, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), 990, 1000, 5, 1);
            intent.Observe(new Vector2(0.7f, 1.15f), 1041, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.Null);
        }

        [TestCase(980, -4, 4)]
        [TestCase(930, -0.7f, 0.7f)]
        [TestCase(1000, -0.7f, 0.7f)]
        [TestCase(1001, -0.7f, 0.7f)]
        public void TestDiscontinuousSamplesDoNotCommit(double firstTime, float fromX, float toX)
        {
            var intent = new RelaxController.AimIntentState();
            intent.Observe(new Vector2(fromX, 1.15f), firstTime, 1000, 5, 1);
            intent.Observe(new Vector2(toX, 1.15f), 1000, 1000, 5, 1);
            Assert.That(intent.PressTime, Is.Null);
        }
    }
}
