// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MessagePack;

namespace osu.Game.Online.Matchmaking.Requests
{
    [MessagePackObject]
    [Serializable]
    public class MatchmakingIssueDuelRequest
    {
        [Key(0)]
        public int UserId { get; set; }

        [Key(1)]
        public int PoolId { get; set; }

        // ── Custom duel pool filters ─────────────────────────────────────

        /// <summary>
        /// When <see langword="true"/>, beatmaps in <see cref="CustomBeatmapIds"/> are used
        /// as the pool instead of the official ranked pool.
        /// </summary>
        [Key(2)]
        public bool IsCustomPool { get; set; }

        /// <summary>Custom beatmap IDs for a private duel pool. Only used when <see cref="IsCustomPool"/> is true.</summary>
        [Key(3)]
        public List<int> CustomBeatmapIds { get; set; } = new List<int>();

        /// <summary>Minimum star rating filter for the duel pool.</summary>
        [Key(4)]
        public float? MinStarRating { get; set; }

        /// <summary>Maximum star rating filter for the duel pool.</summary>
        [Key(5)]
        public float? MaxStarRating { get; set; }

        /// <summary>Minimum AR filter.</summary>
        [Key(6)]
        public float? MinAR { get; set; }

        /// <summary>Maximum AR filter.</summary>
        [Key(7)]
        public float? MaxAR { get; set; }

        /// <summary>Minimum OD filter.</summary>
        [Key(8)]
        public float? MinOD { get; set; }

        /// <summary>Maximum OD filter.</summary>
        [Key(9)]
        public float? MaxOD { get; set; }

        /// <summary>Minimum CS filter.</summary>
        [Key(10)]
        public float? MinCS { get; set; }

        /// <summary>Maximum CS filter.</summary>
        [Key(11)]
        public float? MaxCS { get; set; }

        /// <summary>Minimum HP filter.</summary>
        [Key(12)]
        public float? MinHP { get; set; }

        /// <summary>Maximum HP filter.</summary>
        [Key(13)]
        public float? MaxHP { get; set; }

        /// <summary>
        /// When <see langword="true"/>, the duel pool's star rating values are set
        /// to <see cref="MinStarRating"/> and <see cref="MaxStarRating"/> overriding dynamic SR.
        /// </summary>
        [Key(14)]
        public bool UseCustomStarRating { get; set; }
    }
}
