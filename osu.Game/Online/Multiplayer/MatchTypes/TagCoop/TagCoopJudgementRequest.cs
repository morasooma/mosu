// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Online.Multiplayer.MatchTypes.TagCoop
{
    /// <summary>
    /// A judgement made by the player who owns the hit object's combo.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class TagCoopJudgementRequest : MatchUserRequest
    {
        [Key(0)]
        public int ObjectIndex { get; set; }

        [Key(1)]
        public HitResult Result { get; set; }
    }
}
