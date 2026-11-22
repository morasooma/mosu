// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;

namespace osu.Game.Online.Spectator
{
    /// <summary>
    /// An interface defining the spectator server instance.
    /// </summary>
    public interface ISpectatorServer
    {
        /// <summary>
        /// Request spectating data for the specified user. May be called on multiple users and offline users.
        /// For offline users, a subscription will be created and data will begin streaming on next play.
        /// </summary>
        /// <param name="userId">The user to subscribe to.</param>
        Task StartWatchingUser(int userId);

        /// <summary>
        /// Stop requesting spectating data for the specified user. Unsubscribes from receiving further data.
        /// </summary>
        /// <param name="userId">The user to unsubscribe from.</param>
        Task EndWatchingUser(int userId);
    }
}
