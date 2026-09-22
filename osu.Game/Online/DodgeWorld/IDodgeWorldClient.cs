// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;

namespace osu.Game.Online.DodgeWorld
{
    /// <summary>
    /// What the Dodge World hub tells a client about the room it is in.
    /// </summary>
    public interface IDodgeWorldClient : IStatefulUserHubClient
    {
        Task UserJoined(int userId, DodgeWorldPlayerState state);

        Task UserLeft(int userId);

        Task UserStateChanged(int userId, DodgeWorldPlayerState state);

        /// <summary>
        /// Everything hostile in the room, as of a moment on the server. Sent to everyone in the room
        /// several times a second while anything is happening.
        /// </summary>
        Task RoomStateChanged(DodgeWorldRoomSnapshot snapshot);

        /// <summary>
        /// A mob died. Sent to the whole room, so that every client can retire it at once rather than
        /// waiting for the next snapshot to leave it out.
        /// </summary>
        Task MobDefeated(int mobId, int byUserId, string zoneId);

        /// <summary>
        /// What a kill earned. Sent only to the player the server credited with it.
        /// </summary>
        Task RewardGranted(DodgeWorldReward reward);

        /// <summary>
        /// Somebody swung, so that other clients can draw the swing. The swinger has already drawn it.
        /// </summary>
        Task UserAttacked(int userId, float directionX, float directionY);

        /// <summary>
        /// The receiving player's health, as decided by the server.
        /// </summary>
        Task HealthChanged(int health);

        /// <summary>
        /// The receiving player was killed by the room.
        /// </summary>
        Task Died();
    }
}
