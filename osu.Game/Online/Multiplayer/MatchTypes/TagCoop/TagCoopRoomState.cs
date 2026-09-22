// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.Multiplayer.MatchTypes.TagCoop
{
    /// <summary>
    /// Persistent room state for an unranked Tag Co-op match.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class TagCoopRoomState : StandardMatchRoomState
    {
        /// <summary>
        /// User IDs in combo play order. This order is frozen while gameplay is active.
        /// </summary>
        [Key(0)]
        public int[] PlayerOrder { get; set; } = Array.Empty<int>();

        public static TagCoopRoomState CreateDefault(byte? maxParticipants = null) => new TagCoopRoomState
        {
            Slots = maxParticipants == null ? null : new int?[maxParticipants.Value]
        };
    }
}
