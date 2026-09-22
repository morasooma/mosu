// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.Dodge
{
    /// <summary>
    /// A live Dodge player position sent while playing a normal multiplayer match.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class DodgePlayerPositionRequest : MatchUserRequest
    {
        [Key(0)]
        public uint Sequence { get; set; }

        [Key(1)]
        public double GameplayTime { get; set; }

        [Key(2)]
        public float X { get; set; }

        [Key(3)]
        public float Y { get; set; }

        [Key(4)]
        public ushort PingMilliseconds { get; set; }
    }
}
