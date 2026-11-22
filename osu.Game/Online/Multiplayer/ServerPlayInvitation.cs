using System;
using MessagePack;
using osu.Game.Online.Matchmaking;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>A server-wide invitation, separate from personal invitations and match acceptance.</summary>
    [MessagePackObject]
    public class ServerPlayInvitation
    {
        [Key(0)]
        public string Id { get; set; } = string.Empty;
        [Key(1)]
        public int UserId { get; set; }
        [Key(2)]
        public string Username { get; set; } = string.Empty;
        [Key(3)]
        public long? RoomId { get; set; }
        [Key(4)]
        public string RoomName { get; set; } = string.Empty;
        [Key(5)]
        public int[] Participants { get; set; } = Array.Empty<int>();
        [Key(6)]
        public MatchmakingPool? Pool { get; set; }
        [Key(7)]
        public bool RandomMods { get; set; }
        [Key(8)]
        public int Elo { get; set; }
        [Key(9)]
        public DateTimeOffset ValidUntil { get; set; }
    }
}
