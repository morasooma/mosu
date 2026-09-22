// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.DodgeWorld
{
    /// <summary>
    /// What a kill earned, sent to the player who earned it.
    /// </summary>
    /// <remarks>
    /// The client never asks for a reward: the server saw the kill, asked the API to grant it, and reports
    /// what the API decided. That is the whole point of routing rewards this way — a claim a client can
    /// make is a claim a client can forge.
    /// <para>
    /// The player's standing comes along with the amounts, because the client shows it and must not be the
    /// one working it out. Level thresholds live on the server.
    /// </para>
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldReward
    {
        /// <summary>The spawn zone the defeated mob came from, which is what the reward is set on.</summary>
        [Key(0)]
        public string ZoneId { get; set; } = string.Empty;

        [Key(1)]
        public bool Granted { get; set; }

        /// <summary>
        /// Why nothing was granted, as the API named it — <c>maximum_farm_level</c> when the zone's level
        /// cap was passed, <c>reward_cooldown</c> when the same zone paid out a moment ago. Null on success.
        /// </summary>
        [Key(2)]
        public string? Reason { get; set; }

        [Key(3)]
        public int Experience { get; set; }

        [Key(4)]
        public int Coins { get; set; }

        [Key(5)]
        public int Level { get; set; }

        /// <summary>Experience towards the next level, not the total ever earned.</summary>
        [Key(6)]
        public long TotalExperience { get; set; }

        [Key(7)]
        public long ExperienceForNextLevel { get; set; }

        [Key(8)]
        public long TotalCoins { get; set; }
    }
}
