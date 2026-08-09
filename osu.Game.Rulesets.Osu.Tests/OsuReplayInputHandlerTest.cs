// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Input.StateChanges;
using osu.Game.Replays;
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
