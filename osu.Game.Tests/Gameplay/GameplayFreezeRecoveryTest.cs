using NUnit.Framework;
using osu.Game.Beatmaps.Timing;

namespace osu.Game.Tests.Gameplay
{
    [TestFixture]
    public class GameplayFreezeRecoveryTest
    {
        [TestCase(1)]
        [TestCase(1.5)]
        [TestCase(0.75)]
        public void TestFrozenSourceRecoversMissingTime(double rate)
        {
            var recovery = new GameplayFreezeRecovery();
            Assert.That(recovery.GetMissingProgress(1000, 5000, rate, true), Is.Zero);
            Assert.That(recovery.GetMissingProgress(2000, 5000, rate, true), Is.EqualTo(1000 * rate));
            Assert.That(recovery.GetMissingProgress(2010, 5000 + 1010 * rate, rate, true), Is.Zero);
        }

        [Test]
        public void TestContinuingAudioDoesNotSeek()
        {
            var recovery = new GameplayFreezeRecovery();
            recovery.GetMissingProgress(1000, 5000, 1, true);
            Assert.That(recovery.GetMissingProgress(2000, 6000, 1, true), Is.Zero);
        }

        [Test]
        public void TestPauseAndRateChangeResetBaseline()
        {
            var recovery = new GameplayFreezeRecovery();
            recovery.GetMissingProgress(1000, 5000, 1, true);
            recovery.GetMissingProgress(2000, 5000, 1, false);
            Assert.That(recovery.GetMissingProgress(3000, 5000, 1, true), Is.Zero);
            Assert.That(recovery.GetMissingProgress(4000, 5000, 1.5, true), Is.Zero);
            recovery.Reset();
            Assert.That(recovery.GetMissingProgress(5000, 1000, 1.5, true), Is.Zero);
        }

        [Test]
        public void TestShortJitterAndRewindDoNotRecover()
        {
            var recovery = new GameplayFreezeRecovery();
            recovery.GetMissingProgress(1000, 5000, 1, true);
            Assert.That(recovery.GetMissingProgress(1100, 5000, 1, true), Is.Zero);
            Assert.That(recovery.GetMissingProgress(2100, 1000, 1, true), Is.Zero);
            Assert.That(recovery.GetMissingProgress(3100, 1000, -1, true), Is.Zero);
        }
    }
}
