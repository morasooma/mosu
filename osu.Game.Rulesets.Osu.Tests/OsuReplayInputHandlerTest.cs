// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Input.StateChanges;
using osu.Game.Replays;
using osu.Game.Online;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using static osu.Game.Input.Handlers.ReplayInputHandler;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuReplayInputHandlerTest
    {
        [Test]
        public void TestReplayActionsPreservedByDefault()
        {
            var handler = createHandler(relaxEnabled: true, ignoreReplayActionsWhenRelaxEnabled: false);

            List<IInput> inputs = collectInputs(handler);

            Assert.Multiple(() =>
            {
                Assert.That(inputs.OfType<OsuInputManager.ReplayCursorPositionInput>().Count(), Is.EqualTo(1));
                Assert.That(inputs.OfType<ReplayState<OsuAction>>().Single().PressedActions, Is.EquivalentTo(new[] { OsuAction.LeftButton }));
            });
        }

        [Test]
        public void TestReplayActionsIgnoredWhenRelaxOverrideEnabled()
        {
            var handler = createHandler(relaxEnabled: true, ignoreReplayActionsWhenRelaxEnabled: true);

            List<IInput> inputs = collectInputs(handler);

            Assert.Multiple(() =>
            {
                Assert.That(inputs.OfType<OsuInputManager.ReplayCursorPositionInput>().Count(), Is.EqualTo(1));
                Assert.That(inputs.OfType<ReplayState<OsuAction>>().Single().PressedActions, Is.Empty);
            });
        }

        [TestCase(false, 500)]
        [TestCase(true, 0)]
        public void TestIntermediateFrameTime(bool stable, double expectedTime)
        {
            bool previousStable = MosuServerEnvironment.UsesStableProtocol;
            try
            {
                MosuServerEnvironment.UsesStableProtocol = stable;
                var handler = createHandler(relaxEnabled: false, ignoreReplayActionsWhenRelaxEnabled: false);

                Assert.That(handler.SetFrameFromTime(500), Is.EqualTo(expectedTime));
                Assert.That(handler.SetFrameFromTime(1000), Is.EqualTo(1000));
                Assert.That(collectInputs(handler).OfType<ReplayState<OsuAction>>().Single().PressedActions, Is.Empty);
                Assert.That(handler.SetFrameFromTime(1100), Is.EqualTo(1100), "Time must advance after the last recorded frame.");
                Assert.That(handler.SetFrameFromTime(500), Is.EqualTo(1000), "Reverse playback first consumes the current frame boundary.");
                Assert.That(handler.SetFrameFromTime(500), Is.EqualTo(500), "Seeking backwards must not be clamped to a future frame.");
            }
            finally
            {
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            }
        }

        [Test]
        public void TestStableDuplicateFrameTimesPreserveInputChanges()
        {
            bool previousStable = MosuServerEnvironment.UsesStableProtocol;
            try
            {
                MosuServerEnvironment.UsesStableProtocol = true;
                var handler = new OsuFramedReplayInputHandler(new Replay
                {
                    Frames = new List<ReplayFrame>
                    {
                        new OsuReplayFrame(0, osuTK.Vector2.Zero),
                        new OsuReplayFrame(100, osuTK.Vector2.Zero, OsuAction.LeftButton),
                        new OsuReplayFrame(100, osuTK.Vector2.Zero, OsuAction.LeftButton, OsuAction.RightButton),
                        new OsuReplayFrame(200, osuTK.Vector2.Zero),
                    },
                    HasReceivedAllFrames = true,
                }) { GamefieldToScreenSpace = static position => position };

                Assert.That(handler.SetFrameFromTime(0), Is.Zero);
                Assert.That(handler.SetFrameFromTime(150), Is.EqualTo(100));
                Assert.That(collectInputs(handler).OfType<ReplayState<OsuAction>>().Single().PressedActions, Is.EqualTo(new[] { OsuAction.LeftButton }));
                Assert.That(handler.SetFrameFromTime(150), Is.EqualTo(100));
                Assert.That(collectInputs(handler).OfType<ReplayState<OsuAction>>().Single().PressedActions, Is.EqualTo(new[] { OsuAction.LeftButton, OsuAction.RightButton }));
                Assert.That(handler.SetFrameFromTime(150), Is.EqualTo(100));
                Assert.That(handler.SetFrameFromTime(200), Is.EqualTo(200));
            }
            finally
            {
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            }
        }

        [TestCase(20150414, 1)]
        [TestCase(20190506, 0)]
        public void TestLegacyVersionControlsAutomaticFrames(int version, double expectedTime)
        {
            bool previousStable = MosuServerEnvironment.UsesStableProtocol;
            try
            {
                MosuServerEnvironment.UsesStableProtocol = true;
                var replay = new Replay
                {
                    LegacyClientVersion = version,
                    Frames = new List<ReplayFrame>
                    {
                        new OsuReplayFrame(0, new osuTK.Vector2(10, 20), OsuAction.LeftButton),
                        new OsuReplayFrame(10, new osuTK.Vector2(30, 40)),
                    },
                };
                Assert.That(replay.DeepClone().LegacyClientVersion, Is.EqualTo(version));
                var handler = new OsuFramedReplayInputHandler(replay) { GamefieldToScreenSpace = static position => position };
                Assert.That(handler.SetFrameFromTime(0), Is.Zero);
                collectInputs(handler);
                Assert.That(handler.SetFrameFromTime(5), Is.EqualTo(expectedTime));
                Assert.That(handler.IsRecordedGameplayFrame, Is.EqualTo(version >= 20190506));
                var cursor = collectInputs(handler).OfType<OsuInputManager.ReplayCursorPositionInput>().Single();
                Assert.That(cursor.Position, Is.EqualTo(new osuTK.Vector2(10, 20)), "Automatic frames must hold the recorded cursor.");
                if (version < 20190506)
                {
                    for (int time = 2; time <= 10; time++)
                        Assert.That(handler.SetFrameFromTime(10), Is.EqualTo(time));
                    Assert.That(handler.IsRecordedGameplayFrame, Is.True);
                }
            }
            finally
            {
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            }
        }

        private static OsuFramedReplayInputHandler createHandler(bool relaxEnabled, bool ignoreReplayActionsWhenRelaxEnabled)
        {
            var handler = new OsuFramedReplayInputHandler(
                new Replay
                {
                    Frames = new List<ReplayFrame>
                    {
                        new OsuReplayFrame(0, new osuTK.Vector2(128, 256), OsuAction.LeftButton),
                        new OsuReplayFrame(1000, new osuTK.Vector2(256, 256)),
                    },
                    HasReceivedAllFrames = true,
                },
                new BindableBool(relaxEnabled),
                ignoreReplayActionsWhenRelaxEnabled)
            {
                GamefieldToScreenSpace = static position => position
            };

            Assert.That(handler.SetFrameFromTime(0), Is.EqualTo(0));
            return handler;
        }

        private static List<IInput> collectInputs(OsuFramedReplayInputHandler handler)
        {
            var inputs = new List<IInput>();
            handler.CollectPendingInputs(inputs);
            return inputs;
        }
    }
}
