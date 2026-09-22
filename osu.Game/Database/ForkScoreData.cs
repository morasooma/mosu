// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.Database
{
    [Explicit]
    public class ForkScoreData : RealmObject
    {
        [PrimaryKey]
        public Guid ScoreID { get; set; }

        public string TagCoopReplayJson { get; set; } = string.Empty;
    }
}
