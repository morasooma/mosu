// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;

namespace osu.Game.Online.Matchmaking
{
    /// <summary>
    /// A completed ranked-play match shown in the matchmaking lobby.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class RankedPlayRecentMatch
    {
        [Key(0)]
        public long RoomId { get; set; }

        [Key(1)]
        public int PoolId { get; set; }

        [Key(2)]
        public long CompletedAtUnixMilliseconds { get; set; }

        [Key(3)]
        public RankedPlayRoomState State { get; set; } = new RankedPlayRoomState();

        /// <summary>
        /// Whether <see cref="State"/> contains the final life and round-win values persisted at the end of the match.
        /// </summary>
        /// <remarks>
        /// Older history entries predate final-state persistence and must not display synthetic values as real results.
        /// </remarks>
        [Key(4)]
        public bool HasFinalState { get; set; }
    }
}
