// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.TagCoop
{
    [Serializable]
    [MessagePackObject]
    public class TagCoopPingRequest : MatchUserRequest
    {
        [Key(0)]
        public long Nonce { get; set; }
    }
}
