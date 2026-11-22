using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.RankedPlay
{
    [Serializable]
    [MessagePackObject]
    public class RankedPlayModBanRequest : MatchUserRequest
    {
        /// <summary>
        /// The random mod to exclude for this match, or null to skip the ban.
        /// </summary>
        [Key(0)]
        public string? Acronym { get; set; }

        /// <summary>
        /// False broadcasts a tentative selection without locking the ban.
        /// </summary>
        [Key(1)]
        public bool Confirm { get; set; } = true;
    }
}
