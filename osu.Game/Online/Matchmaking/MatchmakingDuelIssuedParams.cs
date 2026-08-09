// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MessagePack;

namespace osu.Game.Online.Matchmaking
{
    [MessagePackObject]
    [Serializable]
    public class MatchmakingDuelIssuedParams
    {
        [Key(0)]
        public Guid Id { get; set; }

        [Key(1)]
        public int UserId { get; set; }

        [Key(2)]
        public MatchmakingPool Pool { get; set; } = new MatchmakingPool();

        // ── Custom duel pool filters ─────────────────────────────────────

        /// <summary>
        /// When <see langword="true"/>, beatmaps in <see cref="CustomBeatmapIds"/> are used
        /// as the pool instead of the official ranked pool.
        /// </summary>
        [Key(3)]
        public bool IsCustomPool { get; set; }

        /// <summary>Custom beatmap IDs for a private duel pool.</summary>
        [Key(4)]
        public List<int> CustomBeatmapIds { get; set; } = new List<int>();

        [Key(5)]  public float? MinStarRating { get; set; }
        [Key(6)]  public float? MaxStarRating { get; set; }
        [Key(7)]  public float? MinAR { get; set; }
        [Key(8)]  public float? MaxAR { get; set; }
        [Key(9)]  public float? MinOD { get; set; }
        [Key(10)] public float? MaxOD { get; set; }
        [Key(11)] public float? MinCS { get; set; }
        [Key(12)] public float? MaxCS { get; set; }
        [Key(13)] public float? MinHP { get; set; }
        [Key(14)] public float? MaxHP { get; set; }

        /// <summary>
        /// When <see langword="true"/>, the star rating of the duel pool is fixed
        /// to <see cref="MinStarRating"/>–<see cref="MaxStarRating"/> instead of dynamic SR.
        /// </summary>
        [Key(15)]
        public bool UseCustomStarRating { get; set; }
    }
}
