using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayFailIndicatorLifetimeTest
    {
        [Test]
        public void TestDisposeBeforeLoading()
        {
            using var clock = new GameplayClockContainer(new StopwatchClock(), false, false);
            var indicator = new ReplayFailIndicator(clock);
            Assert.DoesNotThrow(indicator.Dispose);
            Assert.DoesNotThrow(indicator.Dispose);
        }
    }
}
