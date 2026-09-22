// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#if DEBUG

using NUnit.Framework;
using osu.Game.Scoring;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class GameplayIntegrityDebugInjectorTest
    {
        [Test]
        public void TestAllSignalsAreInjected()
        {
            var report = new GameplayIntegrityReport
            {
                ReplayFrameRate = 60,
                ClockRate = 1,
                InputSources =
                [
                    new GameplayInputTimingReport
                    {
                        Handler = "Mouse",
                        EventCount = 1000,
                        IntervalCount = 999,
                        ButtonPressCount = 50,
                        ButtonReleaseCount = 50,
                        ExpectedIntervalMilliseconds = 1000.0 / 60,
                    },
                ],
            };

            GameplayIntegrityDebugInjector.Apply(report, GameplayIntegrityDebugScenario.AllSignals);

            Assert.That(report.DebugScenario, Is.EqualTo(nameof(GameplayIntegrityDebugScenario.AllSignals)));
            Assert.That(report.InputSources[0].EventCount, Is.Zero);
            Assert.That(report.Clock.PlaybackRateValid, Is.False);
            Assert.That(report.Difficulty.CustomApproachRateEnabled, Is.True);
            Assert.That(report.Assistance.AimAssistAdjustedFrameCount, Is.GreaterThan(0));
            Assert.That(report.Assistance.RelaxGeneratedPressCount, Is.GreaterThan(0));
        }
    }
}

#endif
