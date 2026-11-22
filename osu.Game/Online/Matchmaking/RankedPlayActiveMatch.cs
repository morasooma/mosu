using System;
using MessagePack;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;

namespace osu.Game.Online.Matchmaking
{
    /// <summary>
    /// Public discovery information for an ongoing match. Contains no hands, passwords or private room state.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class RankedPlayActiveMatch
    {
        [Key(0)]
        public long RoomId { get; set; }

        [Key(1)]
        public int PoolId { get; set; }

        [Key(2)]
        public int[] UserIds { get; set; } = [];

        [Key(3)]
        public int CurrentRound { get; set; }

        [Key(4)]
        public RankedPlayStage Stage { get; set; }
    }
}
