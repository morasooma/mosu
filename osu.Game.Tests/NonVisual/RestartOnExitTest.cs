using System;
using NUnit.Framework;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class RestartOnExitTest
    {
        [Test]
        public void TestDeferredRestartRunsOnceAfterDisposal()
        {
            var game = new DeferredRestartGame();
            int restarts = 0;
            game.RestartOnExitAction = () => restarts++;

            game.Dispose();
            Assert.That(restarts, Is.Zero);

            game.CompleteRestart();
            game.CompleteRestart();
            Assert.That(restarts, Is.EqualTo(1));
        }

        [Test]
        public void TestCancelledRestartDoesNotRun()
        {
            using var game = new DeferredRestartGame();
            int restarts = 0;
            game.RestartOnExitAction = () => restarts++;
            game.CancelRestartOnExit();
            game.CompleteRestart();
            Assert.That(restarts, Is.Zero);
        }

        [Test]
        public void TestFailedRestartIsNotRetried()
        {
            using var game = new DeferredRestartGame();
            int attempts = 0;
            game.RestartOnExitAction = () =>
            {
                attempts++;
                throw new InvalidOperationException("Updater launch failed");
            };

            Assert.Throws<InvalidOperationException>(game.CompleteRestart);
            Assert.DoesNotThrow(game.CompleteRestart);
            Assert.That(attempts, Is.EqualTo(1));
        }

        private partial class DeferredRestartGame : OsuGameBase
        {
            protected override void ExecuteRestartOnExit()
            {
            }

            public void CompleteRestart() => base.ExecuteRestartOnExit();
        }
    }
}
