// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class SpectatorReplayFrameConversionTest
    {
        [Test]
        public void TestCatchMovementIsPreservedAcrossBundles()
        {
            var replay = new Replay();
            var ruleset = new CatchRuleset();
            var beatmap = new CatchBeatmap();

            SpectatorPlayer.AppendReplayFrames(replay, ruleset, beatmap,
                [new LegacyReplayFrame(100, 128, null, ReplayButtonState.None)]);
            SpectatorPlayer.AppendReplayFrames(replay, ruleset, beatmap,
                [new LegacyReplayFrame(200, 256, null, ReplayButtonState.None)]);

            CatchReplayFrame[] frames = replay.Frames.Cast<CatchReplayFrame>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(frames, Has.Length.EqualTo(2));
                Assert.That(frames[0].Actions, Is.EqualTo(new[] { CatchAction.MoveRight }));
                Assert.That(frames[1].Actions, Is.Empty);
            });
        }
    }
}
