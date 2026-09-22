// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace osu.Game.Online.Legacy
{
    public enum StableBanchoClientPacket : ushort
    {
        SendPublicMessage = 1,
        Logout = 2,
        Ping = 4,
        SendPrivateMessage = 25,
        PartLobby = 29,
        JoinLobby = 30,
        CreateMatch = 31,
        JoinMatch = 32,
        PartMatch = 33,
        MatchChangeSlot = 38,
        MatchReady = 39,
        MatchLock = 40,
        MatchChangeSettings = 41,
        MatchStart = 44,
        MatchScoreUpdate = 47,
        MatchComplete = 49,
        MatchChangeMods = 51,
        MatchLoadComplete = 52,
        MatchNoBeatmap = 54,
        MatchNotReady = 55,
        MatchFailed = 56,
        MatchHasBeatmap = 59,
        MatchSkipRequest = 60,
        ChannelJoin = 63,
        MatchTransferHost = 70,
        MatchChangeTeam = 77,
        ChannelPart = 78,
        MatchInvite = 87,
        MatchChangePassword = 90,
    }

    public enum StableBanchoServerPacket : ushort
    {
        UserId = 5,
        SendMessage = 7,
        UserStatistics = 11,
        Notification = 24,
        UpdateMatch = 26,
        NewMatch = 27,
        DisposeMatch = 28,
        MatchJoinSuccess = 36,
        MatchJoinFail = 37,
        MatchStart = 46,
        MatchScoreUpdate = 48,
        MatchTransferHost = 50,
        MatchAllPlayersLoaded = 53,
        MatchPlayerFailed = 57,
        MatchComplete = 58,
        MatchSkip = 61,
        ChannelJoinSuccess = 64,
        ChannelInfo = 65,
        ChannelKick = 66,
        ChannelAutoJoin = 67,
        UserPresence = 83,
        Restart = 86,
        ChannelInfoEnd = 89,
        MatchAbort = 106,
    }

    [Flags]
    public enum StableMatchSlotStatus : byte
    {
        Open = 1,
        Locked = 2,
        NotReady = 4,
        Ready = 8,
        NoBeatmap = 16,
        Playing = 32,
        Complete = 64,
        Quit = 128,
    }

    public sealed record StableBanchoPacket(ushort Id, byte[] Payload)
    {
        public StableBanchoPacket(StableBanchoClientPacket id, byte[]? payload = null)
            : this((ushort)id, payload ?? Array.Empty<byte>())
        {
        }
    }

    public sealed class StableBanchoMatch
    {
        public short Id { get; set; }
        public bool InProgress { get; set; }
        public int Mods { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        public string BeatmapName { get; set; } = string.Empty;
        public int BeatmapId { get; set; }
        public string BeatmapChecksum { get; set; } = string.Empty;
        public StableMatchSlotStatus[] SlotStatuses { get; set; } = new StableMatchSlotStatus[16];
        public byte[] SlotTeams { get; set; } = new byte[16];
        public int?[] SlotUserIds { get; set; } = new int?[16];
        public int HostUserId { get; set; }
        public byte RulesetId { get; set; }
        public byte WinCondition { get; set; }
        public byte TeamType { get; set; }
        public bool FreeMods { get; set; }
        public int[] SlotMods { get; set; } = new int[16];
        public int Seed { get; set; }

        public StableBanchoMatch Clone() => new StableBanchoMatch
        {
            Id = Id,
            InProgress = InProgress,
            Mods = Mods,
            Name = Name,
            Password = Password,
            HasPassword = HasPassword,
            BeatmapName = BeatmapName,
            BeatmapId = BeatmapId,
            BeatmapChecksum = BeatmapChecksum,
            SlotStatuses = (StableMatchSlotStatus[])SlotStatuses.Clone(),
            SlotTeams = (byte[])SlotTeams.Clone(),
            SlotUserIds = (int?[])SlotUserIds.Clone(),
            HostUserId = HostUserId,
            RulesetId = RulesetId,
            WinCondition = WinCondition,
            TeamType = TeamType,
            FreeMods = FreeMods,
            SlotMods = (int[])SlotMods.Clone(),
            Seed = Seed,
        };
    }

    public static class StableBanchoPacketCodec
    {
        private const int packet_header_length = 7;
        private const byte string_marker = 0x0b;
        private const byte occupied_slot_mask = 0x7c;

        public static byte[] Encode(params StableBanchoPacket[] packets)
        {
            using var output = new MemoryStream();
            byte[] header = new byte[packet_header_length];

            foreach (var packet in packets)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(header, packet.Id);
                header[2] = 0;
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(3), packet.Payload.Length);
                output.Write(header);
                output.Write(packet.Payload);
            }

            return output.ToArray();
        }

        public static IReadOnlyList<StableBanchoPacket> Decode(ReadOnlySpan<byte> data)
        {
            var packets = new List<StableBanchoPacket>();
            int offset = 0;

            while (offset < data.Length)
            {
                if (data.Length - offset < packet_header_length)
                    throw new InvalidDataException("Stable Bancho response ended inside a packet header.");

                ushort id = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
                int length = BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 3)..]);
                offset += packet_header_length;

                if (length < 0 || length > data.Length - offset)
                    throw new InvalidDataException($"Stable Bancho packet {id} has invalid payload length {length}.");

                packets.Add(new StableBanchoPacket(id, data.Slice(offset, length).ToArray()));
                offset += length;
            }

            return packets;
        }

        public static StableBanchoPacket CreateJoinMatchPacket(int matchId, string password)
        {
            using var payload = new MemoryStream();
            writeInt32(payload, matchId);
            writeString(payload, password);
            return new StableBanchoPacket(StableBanchoClientPacket.JoinMatch, payload.ToArray());
        }

        public static StableBanchoPacket CreateInt32Packet(StableBanchoClientPacket id, int value)
        {
            byte[] payload = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(payload, value);
            return new StableBanchoPacket(id, payload);
        }

        public static StableBanchoPacket CreatePublicMessagePacket(string message, string channel)
            => createMessagePacket(StableBanchoClientPacket.SendPublicMessage, message, channel);

        public static StableBanchoPacket CreatePrivateMessagePacket(string message, string username)
            => createMessagePacket(StableBanchoClientPacket.SendPrivateMessage, message, username);

        private static StableBanchoPacket createMessagePacket(StableBanchoClientPacket id, string message, string target)
        {
            using var payload = new MemoryStream();
            writeString(payload, string.Empty);
            writeString(payload, message);
            writeString(payload, target);
            writeInt32(payload, 0);
            return new StableBanchoPacket(id, payload.ToArray());
        }

        public static StableBanchoPacket CreateStringPacket(StableBanchoClientPacket id, string value)
        {
            using var payload = new MemoryStream();
            writeString(payload, value);
            return new StableBanchoPacket(id, payload.ToArray());
        }

        public static StableBanchoMessage DecodeMessage(ReadOnlySpan<byte> payload)
        {
            var reader = new SpanReader(payload);
            var message = new StableBanchoMessage
            {
                Sender = reader.ReadString(),
                Content = reader.ReadString(),
                Target = reader.ReadString(),
                SenderId = reader.ReadInt32(),
            };
            reader.EnsureFullyConsumed();
            return message;
        }

        public static StableBanchoChannel DecodeChannel(ReadOnlySpan<byte> payload)
        {
            var reader = new SpanReader(payload);
            var channel = new StableBanchoChannel
            {
                Name = reader.ReadString(),
                Topic = reader.ReadString(),
                PlayerCount = reader.ReadInt16(),
            };
            reader.EnsureFullyConsumed();
            return channel;
        }

        public static StableBanchoPacket CreateMatchPacket(StableBanchoClientPacket id, StableBanchoMatch match, bool includePassword = true)
            => new StableBanchoPacket(id, EncodeMatch(match, includePassword));

        public static StableBanchoMatch DecodeMatch(ReadOnlySpan<byte> payload)
        {
            var reader = new SpanReader(payload);
            var match = new StableBanchoMatch
            {
                Id = reader.ReadInt16(),
                InProgress = reader.ReadByte() == 1,
            };

            reader.ReadByte(); // legacy match type / powerplay byte.
            match.Mods = reader.ReadInt32();
            match.Name = reader.ReadString();
            match.Password = reader.ReadString(out bool passwordMarkerPresent);
            match.HasPassword = passwordMarkerPresent;
            match.BeatmapName = reader.ReadString();
            match.BeatmapId = reader.ReadInt32();
            match.BeatmapChecksum = reader.ReadString();

            for (int i = 0; i < 16; i++)
                match.SlotStatuses[i] = (StableMatchSlotStatus)reader.ReadByte();
            for (int i = 0; i < 16; i++)
                match.SlotTeams[i] = reader.ReadByte();
            for (int i = 0; i < 16; i++)
            {
                if (((byte)match.SlotStatuses[i] & occupied_slot_mask) != 0)
                    match.SlotUserIds[i] = reader.ReadInt32();
            }

            match.HostUserId = reader.ReadInt32();
            match.RulesetId = reader.ReadByte();
            match.WinCondition = reader.ReadByte();
            match.TeamType = reader.ReadByte();
            match.FreeMods = reader.ReadByte() == 1;

            if (match.FreeMods)
            {
                for (int i = 0; i < 16; i++)
                    match.SlotMods[i] = reader.ReadInt32();
            }

            match.Seed = reader.ReadInt32();
            reader.EnsureFullyConsumed();
            return match;
        }

        public static int DecodeInt32(ReadOnlySpan<byte> payload)
        {
            if (payload.Length != 4)
                throw new InvalidDataException($"Expected a 4-byte stable Bancho integer, got {payload.Length} bytes.");
            return BinaryPrimitives.ReadInt32LittleEndian(payload);
        }

        public static string DecodeString(ReadOnlySpan<byte> payload)
        {
            var reader = new SpanReader(payload);
            string value = reader.ReadString();
            reader.EnsureFullyConsumed();
            return value;
        }

        public static byte[] EncodeMatch(StableBanchoMatch match, bool includePassword)
        {
            using var payload = new MemoryStream();
            writeInt16(payload, match.Id);
            payload.WriteByte(match.InProgress ? (byte)1 : (byte)0);
            payload.WriteByte(0);
            writeInt32(payload, match.Mods);
            writeString(payload, match.Name);

            if (!string.IsNullOrEmpty(match.Password) && !includePassword)
            {
                payload.WriteByte(string_marker);
                payload.WriteByte(0);
            }
            else
                writeString(payload, match.Password);

            writeString(payload, match.BeatmapName);
            writeInt32(payload, match.BeatmapId);
            writeString(payload, match.BeatmapChecksum);

            for (int i = 0; i < 16; i++)
                payload.WriteByte((byte)match.SlotStatuses[i]);
            for (int i = 0; i < 16; i++)
                payload.WriteByte(match.SlotTeams[i]);
            for (int i = 0; i < 16; i++)
            {
                if (((byte)match.SlotStatuses[i] & occupied_slot_mask) != 0)
                    writeInt32(payload, match.SlotUserIds[i] ?? throw new InvalidDataException($"Occupied stable match slot {i} has no user ID."));
            }

            writeInt32(payload, match.HostUserId);
            payload.WriteByte(match.RulesetId);
            payload.WriteByte(match.WinCondition);
            payload.WriteByte(match.TeamType);
            payload.WriteByte(match.FreeMods ? (byte)1 : (byte)0);

            if (match.FreeMods)
            {
                for (int i = 0; i < 16; i++)
                    writeInt32(payload, match.SlotMods[i]);
            }

            writeInt32(payload, match.Seed);
            return payload.ToArray();
        }

        private static void writeInt16(Stream stream, short value)
        {
            Span<byte> buffer = stackalloc byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void writeInt32(Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        private static void writeString(Stream stream, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                stream.WriteByte(0);
                return;
            }

            byte[] encoded = Encoding.UTF8.GetBytes(value);
            stream.WriteByte(string_marker);
            writeUleb128(stream, encoded.Length);
            stream.Write(encoded);
        }

        private static void writeUleb128(Stream stream, int value)
        {
            do
            {
                byte current = (byte)(value & 0x7f);
                value >>= 7;
                if (value != 0)
                    current |= 0x80;
                stream.WriteByte(current);
            } while (value != 0);
        }

        public static StableBanchoUserPresence DecodeUserPresence(ReadOnlySpan<byte> payload)
        {
            var reader = new SpanReader(payload);
            var presence = new StableBanchoUserPresence
            {
                UserId = reader.ReadInt32(),
                Username = reader.ReadString(),
                UtcOffset = reader.ReadByte() - 24,
                CountryCode = reader.ReadByte(),
                PrivilegesAndRuleset = reader.ReadByte(),
                Longitude = reader.ReadSingle(),
                Latitude = reader.ReadSingle(),
                GlobalRank = reader.ReadInt32(),
            };
            reader.EnsureFullyConsumed();
            return presence;
        }

        public static StableBanchoUserStatistics DecodeUserStatistics(ReadOnlySpan<byte> payload)
        {
            var reader = new SpanReader(payload);
            var statistics = new StableBanchoUserStatistics
            {
                UserId = reader.ReadInt32(),
                Action = reader.ReadByte(),
                InfoText = reader.ReadString(),
                BeatmapChecksum = reader.ReadString(),
                Mods = reader.ReadInt32(),
                RulesetId = reader.ReadByte(),
                BeatmapId = reader.ReadInt32(),
                RankedScore = reader.ReadInt64(),
                Accuracy = reader.ReadSingle(),
                PlayCount = reader.ReadInt32(),
                TotalScore = reader.ReadInt64(),
                GlobalRank = reader.ReadInt32(),
                PerformancePoints = reader.ReadInt16(),
            };
            reader.EnsureFullyConsumed();
            return statistics;
        }

        public static StableBanchoPacket CreateScoreFramePacket(StableBanchoScoreFrame frame)
        {
            byte[] payload = new byte[29];
            BinaryPrimitives.WriteInt32LittleEndian(payload, frame.Time);
            payload[4] = frame.PlayerSlot;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(5), frame.Count300);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(7), frame.Count100);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(9), frame.Count50);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(11), frame.CountGeki);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(13), frame.CountKatu);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(15), frame.CountMiss);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(17), frame.TotalScore);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(21), frame.MaxCombo);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(23), frame.CurrentCombo);
            payload[25] = frame.Perfect ? (byte)1 : (byte)0;
            payload[26] = frame.CurrentHp;
            payload[27] = frame.TagByte;
            payload[28] = frame.ScoreV2 ? (byte)1 : (byte)0;
            return new StableBanchoPacket(StableBanchoClientPacket.MatchScoreUpdate, payload);
        }

        public static StableBanchoScoreFrame DecodeScoreFrame(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 29)
                throw new InvalidDataException($"Expected at least 29 bytes for a stable Bancho score frame, got {payload.Length}.");

            return new StableBanchoScoreFrame
            {
                Time = BinaryPrimitives.ReadInt32LittleEndian(payload),
                PlayerSlot = payload[4],
                Count300 = BinaryPrimitives.ReadUInt16LittleEndian(payload[5..]),
                Count100 = BinaryPrimitives.ReadUInt16LittleEndian(payload[7..]),
                Count50 = BinaryPrimitives.ReadUInt16LittleEndian(payload[9..]),
                CountGeki = BinaryPrimitives.ReadUInt16LittleEndian(payload[11..]),
                CountKatu = BinaryPrimitives.ReadUInt16LittleEndian(payload[13..]),
                CountMiss = BinaryPrimitives.ReadUInt16LittleEndian(payload[15..]),
                TotalScore = BinaryPrimitives.ReadInt32LittleEndian(payload[17..]),
                MaxCombo = BinaryPrimitives.ReadUInt16LittleEndian(payload[21..]),
                CurrentCombo = BinaryPrimitives.ReadUInt16LittleEndian(payload[23..]),
                Perfect = payload[25] != 0,
                CurrentHp = payload[26],
                TagByte = payload[27],
                ScoreV2 = payload[28] != 0,
            };
        }

        private ref struct SpanReader
        {
            private readonly ReadOnlySpan<byte> data;
            private int offset;

            public SpanReader(ReadOnlySpan<byte> data) => this.data = data;

            public byte ReadByte()
            {
                ensureAvailable(1);
                return data[offset++];
            }

            public short ReadInt16()
            {
                ensureAvailable(2);
                short value = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
                offset += 2;
                return value;
            }

            public int ReadInt32()
            {
                ensureAvailable(4);
                int value = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
                offset += 4;
                return value;
            }

            public long ReadInt64()
            {
                ensureAvailable(8);
                long value = BinaryPrimitives.ReadInt64LittleEndian(data[offset..]);
                offset += 8;
                return value;
            }

            public float ReadSingle()
            {
                ensureAvailable(4);
                float value = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
                offset += 4;
                return value;
            }

            public string ReadString() => ReadString(out _);

            public string ReadString(out bool markerPresent)
            {
                byte marker = ReadByte();
                if (marker == 0)
                {
                    markerPresent = false;
                    return string.Empty;
                }
                if (marker != string_marker)
                    throw new InvalidDataException($"Invalid stable Bancho string marker 0x{marker:x2}.");

                markerPresent = true;

                int length = readUleb128();
                ensureAvailable(length);
                string value = Encoding.UTF8.GetString(data.Slice(offset, length));
                offset += length;
                return value;
            }

            public void EnsureFullyConsumed()
            {
                if (offset != data.Length)
                    throw new InvalidDataException($"Stable Bancho payload has {data.Length - offset} unexpected trailing bytes.");
            }

            private int readUleb128()
            {
                int value = 0;
                int shift = 0;

                while (shift < 35)
                {
                    byte current = ReadByte();
                    value |= (current & 0x7f) << shift;
                    if ((current & 0x80) == 0)
                        return value;
                    shift += 7;
                }

                throw new InvalidDataException("Stable Bancho string length uses an invalid ULEB128 value.");
            }

            private void ensureAvailable(int length)
            {
                if (length < 0 || data.Length - offset < length)
                    throw new EndOfStreamException("Stable Bancho payload ended unexpectedly.");
            }
        }
    }

    public sealed class StableBanchoUserPresence
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public int UtcOffset { get; init; }
        public byte CountryCode { get; init; }
        public byte PrivilegesAndRuleset { get; init; }
        public float Longitude { get; init; }
        public float Latitude { get; init; }
        public int GlobalRank { get; init; }
    }

    public sealed class StableBanchoUserStatistics
    {
        public int UserId { get; init; }
        public byte Action { get; init; }
        public string InfoText { get; init; } = string.Empty;
        public string BeatmapChecksum { get; init; } = string.Empty;
        public int Mods { get; init; }
        public byte RulesetId { get; init; }
        public int BeatmapId { get; init; }
        public long RankedScore { get; init; }
        public float Accuracy { get; init; }
        public int PlayCount { get; init; }
        public long TotalScore { get; init; }
        public int GlobalRank { get; init; }
        public short PerformancePoints { get; init; }
    }

    public sealed class StableBanchoMessage
    {
        public string Sender { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public int SenderId { get; init; }
    }

    public sealed class StableBanchoChannel
    {
        public string Name { get; init; } = string.Empty;
        public string Topic { get; init; } = string.Empty;
        public int PlayerCount { get; init; }
        public bool IsJoined { get; init; }

        public StableBanchoChannel WithJoined(bool joined) => new StableBanchoChannel
        {
            Name = Name,
            Topic = Topic,
            PlayerCount = PlayerCount,
            IsJoined = joined,
        };
    }

    public sealed class StableBanchoScoreFrame
    {
        public int Time { get; init; }
        public byte PlayerSlot { get; init; }
        public ushort Count300 { get; init; }
        public ushort Count100 { get; init; }
        public ushort Count50 { get; init; }
        public ushort CountGeki { get; init; }
        public ushort CountKatu { get; init; }
        public ushort CountMiss { get; init; }
        public int TotalScore { get; init; }
        public ushort MaxCombo { get; init; }
        public ushort CurrentCombo { get; init; }
        public bool Perfect { get; init; }
        public byte CurrentHp { get; init; }
        public byte TagByte { get; init; }
        public bool ScoreV2 { get; init; }
    }

    public sealed class StableBanchoMatchScore
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public StableBanchoScoreFrame Frame { get; init; } = null!;
    }
}
