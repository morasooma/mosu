// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.TagCoop
{
    /// <summary>
    /// A post-game batch from the sender's ordinary local replay recorder.
    /// This is separate from the low-frequency live cursor stream.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class TagCoopReplayFramesRequest : MatchUserRequest
    {
        [Key(0)]
        public uint BatchSequence { get; set; }

        [Key(1)]
        public bool IsFinal { get; set; }

        [Key(2)]
        public TagCoopReplayFrame[] Frames { get; set; } = [];
    }
}
