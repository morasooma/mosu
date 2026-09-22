// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A player instance which submits to a room backing. This is generally used by playlists and multiplayer.
    /// </summary>
    public abstract partial class RoomSubmittingPlayer : SubmittingPlayer
    {
        protected readonly PlaylistItem PlaylistItem;
        protected readonly Room Room;

        protected RoomSubmittingPlayer(Room room, PlaylistItem playlistItem, PlayerConfiguration? configuration = null)
            : base(configuration)
        {
            Room = room;
            PlaylistItem = playlistItem;
        }    }
}
