using NUnit.Framework;
using osu.Game.Rulesets.Osu.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.Scoring
{
    [TestFixture]
    public class StableSliderTrackingStateTest
    {
        [Test]
        public void TestLateAcquisitionCannotRecoverCheckpoint()
        {
            var buttons = new StableButtonState();
            var tracking = new StableSliderTrackingState();
            buttons.Update(1);
            tracking.Update(1000, 1000, false, false, buttons, false);
            tracking.Update(1065, 1000, false, true, buttons, false);
            Assert.Multiple(() =>
            {
                Assert.That(tracking.Tracking, Is.True);
                Assert.That(tracking.SlideStart, Is.EqualTo(1065));
                Assert.That(tracking.CanHitCheckpoint(1064), Is.False);
                Assert.That(tracking.CanHitCheckpoint(1065), Is.True);
            });
        }

        [Test]
        public void TestLiveAcquisitionUsesIntegerMilliseconds()
        {
            var buttons = new StableButtonState();
            buttons.Update(1);
            var tracking = new StableSliderTrackingState();
            tracking.Update(1064.9, 1000, false, true, buttons, false);
            Assert.That(tracking.CanHitCheckpoint(1064), Is.True);
            Assert.That(tracking.SlideStart, Is.EqualTo(1064));
        }

        [Test]
        public void TestReacquisitionResetsSlideStart()
        {
            var buttons = new StableButtonState();
            var tracking = new StableSliderTrackingState();
            buttons.Update(1);
            tracking.Update(1000, 1000, false, true, buttons, false);
            tracking.Update(1040, 1000, false, false, buttons, false);
            tracking.Update(1070, 1000, false, true, buttons, false);
            Assert.That(tracking.CanHitCheckpoint(1064), Is.False);
        }

        [Test]
        public void TestTrackingDoesNotBeginBeforeStart()
        {
            var buttons = new StableButtonState();
            var tracking = new StableSliderTrackingState();
            buttons.Update(1);
            tracking.Update(990, 1000, false, true, buttons, false);
            Assert.That(tracking.Tracking, Is.False);
            tracking.Update(1000, 1000, false, false, buttons, false);
            Assert.That(tracking.Tracking, Is.False, "An early head press does not grant expanded follow range.");
        }

        [Test]
        public void TestHeldKeyTransferUsesGlobalTransitionHistory()
        {
            var buttons = new StableButtonState();
            var tracking = new StableSliderTrackingState();
            buttons.Update(1);
            tracking.Update(900, 1000, false, true, buttons, false);
            buttons.Update(3);
            tracking.Update(1000, 1000, false, true, buttons, false);
            Assert.That(tracking.Tracking, Is.True);
            buttons.Update(1);
            tracking.Update(1010, 1000, false, true, buttons, false);
            Assert.That(tracking.Tracking, Is.False, "Releasing the new key cannot transfer back to the pre-held key.");
            buttons.Update(0);
            tracking.Update(1020, 1000, false, true, buttons, false);
            buttons.Update(1);
            tracking.Update(1030, 1000, false, true, buttons, false);
            Assert.That(tracking.Tracking, Is.True);
        }

        [Test]
        public void TestHeadPressSelectsNewKeyAfterTrackingAlreadyStarted()
        {
            var buttons = new StableButtonState();
            var tracking = new StableSliderTrackingState();
            buttons.Update(1);
            tracking.Update(1000, 1000, false, true, buttons, false);
            buttons.Update(3);
            tracking.HandleHeadPress(OsuAction.RightButton, 1000);
            tracking.Update(1000, 1000, false, true, buttons, false);
            buttons.Update(1);
            tracking.Update(1010, 1000, false, true, buttons, false);
            Assert.That(tracking.Tracking, Is.False, "The head press must replace the key acquired by slider tracking.");
        }

        [Test]
        public void TestRewindRestoresAcquisitionAndHeldKey()
        {
            var buttons = new StableButtonState();
            buttons.Update(1);
            var tracking = new StableSliderTrackingState();
            tracking.Update(1000, 1000, false, true, buttons, false);
            tracking.Update(1050, 1000, false, false, buttons, false);
            tracking.Update(1070, 1000, false, true, buttons, false);
            Assert.That(tracking.CanHitCheckpoint(1064), Is.False);
            tracking.Rewind(1040);
            Assert.That(tracking.CanHitCheckpoint(1064), Is.True);
            Assert.That(tracking.SlideStart, Is.EqualTo(1000));
            tracking.Rewind(999);
            Assert.That(tracking.Tracking, Is.False);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void TestKeylessRelax(bool relax, bool expected)
        {
            var tracking = new StableSliderTrackingState();
            tracking.Update(1000, 1000, false, true, new StableButtonState(), relax);
            Assert.That(tracking.Tracking, Is.EqualTo(expected));
            tracking.Update(1010, 1000, false, false, new StableButtonState(), relax);
            Assert.That(tracking.Tracking, Is.False);
        }

        [Test]
        public void TestUnchangedMaskDoesNotAdvanceHistory()
        {
            var buttons = new StableButtonState();
            buttons.Update(1);
            buttons.Update(3);
            buttons.Update(3);
            buttons.Update(1);
            Assert.Multiple(() =>
            {
                Assert.That(buttons.PreviousButtons, Is.EqualTo(3));
                Assert.That(buttons.PreviousButtons2, Is.EqualTo(1));
                Assert.That(buttons.NewButtons, Is.Zero);
            });
        }
    }
}
