// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;

namespace osu.Game.Online.Multiplayer.MatchTypes.TagCoop
{
    /// <summary>
    /// Additional data stored in lazer replay metadata for a Tag Co-op play.
    /// Older/default clients ignore this data and continue to play the ordinary replay track.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class TagCoopReplayMetadata
    {
        [JsonProperty("players")]
        public List<TagCoopReplayPlayer> Players { get; set; } = [];

        [JsonProperty("frames")]
        public List<TagCoopReplayFrame> Frames { get; set; } = [];

        public TagCoopReplayMetadata DeepClone() => new TagCoopReplayMetadata
        {
            Players = new List<TagCoopReplayPlayer>(Players),
            Frames = new List<TagCoopReplayFrame>(Frames),
        };
    }

    [Serializable]
    [MessagePackObject]
    [JsonObject(MemberSerialization.OptIn)]
    public readonly record struct TagCoopReplayPlayer
    {
        [Key(0)]
        [JsonProperty("user_id")]
        public int UserID { get; init; }

        [Key(1)]
        [JsonProperty("username")]
        public string Username { get; init; }
    }

    [Serializable]
    [MessagePackObject]
    [JsonObject(MemberSerialization.OptIn)]
    public readonly record struct TagCoopReplayFrame
    {
        [Key(0)]
        [JsonProperty("user_id")]
        public int UserID { get; init; }

        [Key(1)]
        [JsonProperty("sequence")]
        public uint Sequence { get; init; }

        [Key(2)]
        [JsonProperty("time")]
        public double GameplayTime { get; init; }

        [Key(3)]
        [JsonProperty("x")]
        public float X { get; init; }

        [Key(4)]
        [JsonProperty("y")]
        public float Y { get; init; }

        [Key(5)]
        [JsonProperty("buttons")]
        public byte ButtonState { get; init; }
    }
}
