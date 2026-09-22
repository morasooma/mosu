// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.RankedPlay
{
    /// <summary>
    /// A transient cursor sample sent during a break in osu!standard Ranked Play gameplay.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class RankedPlayCursorPositionRequest : MatchUserRequest
    {
        [Key(0)]
        public uint Sequence { get; set; }

        [Key(1)]
        public double GameplayTime { get; set; }

        [Key(2)]
        public float X { get; set; }

        [Key(3)]
        public float Y { get; set; }

        /// <summary>
        /// Bit 0 is the primary button and bit 1 is the secondary button.
        /// </summary>
        [Key(4)]
        public byte ButtonState { get; set; }

        [Key(5)]
        public ushort PingMilliseconds { get; set; }
    }
}
