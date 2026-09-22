// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>
    /// Identifies one deterministic beatmap break for multiplayer skip voting.
    /// </summary>
    [MessagePackObject]
    public class MultiplayerBreakSkipRequest
    {
        [Key(0)]
        public int BreakIndex { get; set; }

        [Key(1)]
        public double BreakStartTime { get; set; }

        [Key(2)]
        public double BreakEndTime { get; set; }

        public bool Matches(MultiplayerBreakSkipRequest other) =>
            other != null
            && BreakIndex == other.BreakIndex
            && Math.Abs(BreakStartTime - other.BreakStartTime) < 0.001
            && Math.Abs(BreakEndTime - other.BreakEndTime) < 0.001;
    }
}
