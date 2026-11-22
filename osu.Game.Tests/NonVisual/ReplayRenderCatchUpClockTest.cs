using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Online;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring.Render;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [HeadlessTest]
    [NonParallelizable]
    public partial class ReplayRenderCatchUpClockTest : OsuTestScene
    {
        private bool previousStableProtocol;

        [SetUp]
        public void SetUp() => previousStableProtocol = MosuServerEnvironment.UsesStableProtocol;

        [TearDown]
        public void TearDown() => MosuServerEnvironment.UsesStableProtocol = previousStableProtocol;

        [TestCase(false, 0, 102005)]
        [TestCase(true, 0, 102005)]
        [TestCase(true, 20180101, 102005)]
        [TestCase(true, 0, 104005)]
        [TestCase(true, 0, 1505)]
        [TestCase(true, 0, -500)]
        public void TestCatchUpConsumesReplayBeforeCapture(bool recordedTimes, int legacyVersion, double target)
        {
            var source = new ManualClock();
            var replay = new Replay { LegacyClientVersion = legacyVersion };
            for (int time = 0; time <= 103000; time += 10)
                replay.Frames.Add(new OsuReplayFrame(time, Vector2.Zero));

            FrameStabilityContainer stability = null!;
            OsuFramedReplayInputHandler handler = null!;
            CatchUpContainer container = null!;
            Task? catchUp = null;
            double expected = recordedTimes && legacyVersion == 0 && target is >= 0 and < 103000
                ? Math.Floor(target / 10) * 10
                : target;

            AddStep("create clock and replay", () =>
            {
                MosuServerEnvironment.UsesStableProtocol = recordedTimes;
                handler = new OsuFramedReplayInputHandler(replay);
                Child = container = new CatchUpContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(source),
                    Child = stability = new FrameStabilityContainer { ReplayInputHandler = handler },
                };
            });
            AddWaitStep("consume initial frame", 2);
            AddStep("seek without capturing", () =>
            {
                double catchUpTime = RenderReplayPlayer.GetInitialCatchUpTime(stability, replay, target);
                Assert.That(catchUpTime, Is.EqualTo(expected));
                catchUp = RenderReplayPlayer.WaitForInitialCatchUpAsync(catchUpTime,
                    token => container.AdvanceAsync(source, stability, target, token),
                    CancellationToken.None, TimeSpan.FromSeconds(10));
            });
            AddUntilStep("wait for actual replay catch-up", () => catchUp?.IsCompleted == true);
            AddAssert("catch-up succeeded", () => catchUp?.Status == TaskStatus.RanToCompletion);
            AddAssert("gameplay reached selected period", () => stability.CurrentTime, () => Is.EqualTo(expected));
            AddAssert("all earlier replay frames consumed", () => handler.CurrentFrame?.Time, () => Is.EqualTo(target < 0 ? (double?)null : Math.Min(103000, Math.Floor(target / 10) * 10)));
        }

        private sealed partial class CatchUpContainer : Container
        {
            public async Task<double> AdvanceAsync(ManualClock source, IFrameStableClock clock, double target, CancellationToken token)
            {
                var completion = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
                Scheduler.Add(() =>
                {
                    source.CurrentTime = target;
                    ScheduleAfterChildren(() => completion.TrySetResult(clock.CurrentTime));
                });
                return await completion.Task.WaitAsync(token).ConfigureAwait(false);
            }
        }
    }
}
