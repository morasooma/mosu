// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.Dodge
{
    [Serializable]
    [MessagePackObject]
    public class DodgePingEvent : MatchServerEvent
    {
        [Key(0)]
        public long Nonce { get; set; }
    }
}
