// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.Dodge
{
    [Serializable]
    [MessagePackObject]
    public class DodgePlayerPositionEvent : MatchServerEvent
    {
        [Key(0)]
        public int UserID { get; set; }

        [Key(1)]
        public uint Sequence { get; set; }

        [Key(2)]
        public double GameplayTime { get; set; }

        [Key(3)]
        public float X { get; set; }

        [Key(4)]
        public float Y { get; set; }

        [Key(5)]
        public ushort PingMilliseconds { get; set; }
    }
}
