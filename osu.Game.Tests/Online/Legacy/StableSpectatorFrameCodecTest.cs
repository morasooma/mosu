using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Online.Legacy;
using osu.Game.Replays.Legacy;

namespace osu.Game.Tests.Online.Legacy
{
    [TestFixture]
    public class StableSpectatorFrameCodecTest
    {
        [Test]
        public void RoundTripFramesAndScore()
        {
            var original = new StableSpectatorFrames(123,
                new[] { new LegacyReplayFrame(456, 100, 200, ReplayButtonState.Left2) },
                StableReplayAction.Standard,
                new StableBanchoScoreFrame { Time = 456, Count300 = 12, TotalScore = 12345, CurrentCombo = 12 }, 42);
            byte[] bytes = StableSpectatorFrameCodec.Encode(original);
            Assert.That(bytes.Length, Is.EqualTo(52));
            var decoded = StableSpectatorFrameCodec.Decode(bytes);
            Assert.Multiple(() =>
            {
                Assert.That(decoded.Extra, Is.EqualTo(123));
                Assert.That(decoded.Frames[0].Time, Is.EqualTo(456));
                Assert.That(decoded.Frames[0].MouseX, Is.EqualTo(100));
                Assert.That(decoded.Frames[0].MouseY, Is.EqualTo(200));
                Assert.That(decoded.Frames[0].ButtonState, Is.EqualTo(ReplayButtonState.Left2));
                Assert.That(decoded.Score.Count300, Is.EqualTo(12));
                Assert.That(decoded.Score.TotalScore, Is.EqualTo(12345));
                Assert.That(decoded.Sequence, Is.EqualTo(42));
            });
        }

        [Test]
        public void EncodesStableWireOffsets()
        {
            byte[] payload = StableSpectatorFrameCodec.Encode(new StableSpectatorFrames(0,
                Array.Empty<LegacyReplayFrame>(), StableReplayAction.NewSong, new StableBanchoScoreFrame(), 7));
            Assert.That(payload.Length, Is.EqualTo(38));
            Assert.That(payload[6], Is.EqualTo(1));
            Assert.That(payload[36], Is.EqualTo(7));
        }

        [Test]
        public void RejectsTruncatedFrame()
        {
            Assert.Throws<InvalidDataException>(() => StableSpectatorFrameCodec.Decode(new byte[] { 0, 0, 0, 0, 1, 0 }));
        }
    }
}
