// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MosuPerformanceConfigurationWarningsTest
    {
        [Test]
        public void TestSingleThreadedExecutionCreatesWarning()
        {
            var notification = MosuPerformanceConfigurationWarnings.CreateExecutionModeWarning(ExecutionMode.SingleThread);

            Assert.Multiple(() =>
            {
                Assert.That(notification, Is.Not.Null);
                Assert.That(notification!.Text.ToString(), Does.Contain("Single-threaded mode is enabled"));
                Assert.That(notification.IsImportant, Is.True);
                Assert.That(notification.Transient, Is.False);
            });
        }

        [Test]
        public void TestOtherExecutionModesDoNotCreateWarning()
        {
            Assert.That(MosuPerformanceConfigurationWarnings.CreateExecutionModeWarning(ExecutionMode.MultiThreaded), Is.Null);
        }
    }
}
