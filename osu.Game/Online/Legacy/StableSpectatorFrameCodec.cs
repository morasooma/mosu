using System;
using System.Collections.Generic;
using System.IO;
using osu.Game.Replays.Legacy;

namespace osu.Game.Online.Legacy
{
    public enum StableReplayAction : byte
    {
        Standard, NewSong, Skip, Completion, Fail, Pause, Unpause, SongSelect, WatchingOther
    }

    public sealed record StableSpectatorFrames(int Extra, IReadOnlyList<LegacyReplayFrame> Frames,
                                             StableReplayAction Action, StableBanchoScoreFrame Score, ushort Sequence);

    /// <summary>Stable Bancho replay frame bundle (protocol 18+), not an .osr compressed stream.</summary>
    public static class StableSpectatorFrameCodec
    {
        public static byte[] Encode(StableSpectatorFrames bundle)
        {
            if (bundle.Score.ScoreV2)
                throw new NotSupportedException("Encoding stable ScoreV2 spectator frames requires its extra score fields.");
            if (bundle.Frames.Count > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(bundle));
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(bundle.Extra);
            writer.Write((ushort)bundle.Frames.Count);
            foreach (var frame in bundle.Frames)
            {
                writer.Write((byte)frame.ButtonState);
                writer.Write((byte)0);
                writer.Write(frame.MouseX ?? 0);
                writer.Write(frame.MouseY ?? 0);
                writer.Write(checked((int)frame.Time));
            }
            writer.Write((byte)bundle.Action);
            writer.Write(StableBanchoPacketCodec.CreateScoreFramePacket(bundle.Score).Payload);
            writer.Write(bundle.Sequence);
            return stream.ToArray();
        }

        public static StableSpectatorFrames Decode(byte[] payload)
        {
            using var stream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(stream);
            int extra = reader.ReadInt32();
            int count = reader.ReadUInt16();
            if (stream.Length - stream.Position < count * 14L + 32)
                throw new InvalidDataException("Truncated stable spectator frame bundle.");
            var frames = new List<LegacyReplayFrame>(count);
            for (int i = 0; i < count; i++)
            {
                var buttons = (ReplayButtonState)reader.ReadByte();
                if (reader.ReadByte() != 0)
                    buttons = ReplayButtonState.Right1;
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                frames.Add(new LegacyReplayFrame(reader.ReadInt32(), x, y, buttons));
            }
            var action = (StableReplayAction)reader.ReadByte();
            var score = StableBanchoPacketCodec.DecodeScoreFrame(reader.ReadBytes(29));
            if (score.ScoreV2)
            {
                // ScoreV2 appends two doubles to the otherwise 29-byte score frame.
                reader.ReadDouble();
                reader.ReadDouble();
            }
            ushort sequence = reader.ReadUInt16();
            return new StableSpectatorFrames(extra, frames, action, score, sequence);
        }
    }
}
