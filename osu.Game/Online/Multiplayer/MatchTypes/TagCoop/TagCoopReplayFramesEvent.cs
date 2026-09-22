// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.TagCoop
{
    /// <summary>
    /// A validated post-game native replay batch broadcast to every Tag Co-op participant.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class TagCoopReplayFramesEvent : MatchServerEvent
    {
        [Key(0)]
        public int UserID { get; set; }

        [Key(1)]
        public uint BatchSequence { get; set; }

        [Key(2)]
        public bool IsFinal { get; set; }

        [Key(3)]
        public TagCoopReplayFrame[] Frames { get; set; } = [];
    }
}
