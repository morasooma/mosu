// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;

namespace osu.Game.Online.DodgeWorld
{
    /// <summary>
    /// The Dodge World realtime hub, as its clients call it.
    /// </summary>
    /// <remarks>
    /// The hub owns the room's mobs, their shots and the damage they do; each client owns where its own
    /// player walks. Movement stays with the client because predicting and reconciling it would cost far
    /// more than it is worth here, and because a client that lies about its position gains nothing it
    /// could not already have taken by lying about a kill. Everything that hurts somebody is decided
    /// server-side, which is what a lying client cannot do.
    /// </remarks>
    public interface IDodgeWorldServer
    {
        /// <summary>
        /// Enters a room, leaving whichever room was entered before.
        /// </summary>
        /// <param name="roomId">The room in the published world.</param>
        /// <param name="state">Where the local player is standing on arrival.</param>
        /// <returns>Who else is in the room, and whether the server is running its mobs.</returns>
        Task<DodgeWorldRoomJoin> JoinRoom(string roomId, DodgeWorldPlayerState state);

        /// <summary>
        /// Leaves the current room. Disconnecting does the same thing, so this is only for leaving the
        /// world while staying connected.
        /// </summary>
        Task LeaveRoom();

        /// <summary>
        /// Reports where the local player has moved to.
        /// </summary>
        Task UpdateState(DodgeWorldPlayerState state);

        /// <summary>
        /// Swings at the given direction. The server decides what was hit; the client plays the swing
        /// straight away because waiting for a round trip would make the sword feel broken.
        /// </summary>
        Task Attack(float directionX, float directionY);
    }
}
