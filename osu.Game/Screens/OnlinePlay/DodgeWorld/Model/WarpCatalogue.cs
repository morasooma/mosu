// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// One warp as the travel menu needs it: where it goes and what it costs.
    /// </summary>
    /// <param name="RoomId">The room the warp stands in, which is where travelling to it leads.</param>
    /// <param name="RoomName">That room's name, as shown in the travel menu.</param>
    /// <param name="EntityId">The warp's own id, unique within its room.</param>
    /// <param name="Name">The warp's label, falling back to its id when it has none.</param>
    /// <param name="Position">Where in the room the player arrives.</param>
    /// <param name="UnlockCost">Coins charged once to open it.</param>
    /// <param name="TravelCost">Coins charged each time it is travelled to.</param>
    internal readonly record struct WarpPoint(
        string RoomId,
        string RoomName,
        string EntityId,
        string Name,
        Vector2 Position,
        int UnlockCost,
        int TravelCost);

    /// <summary>
    /// Finds the warps of a world.
    /// </summary>
    /// <remarks>
    /// Reads the stored rooms rather than the live entities, because the travel menu lists warps in
    /// rooms that are not loaded — only one room exists as drawables at a time.
    /// </remarks>
    internal static class WarpCatalogue
    {
        public const int MAXIMUM_COST = 1_000_000;

        /// <summary>
        /// Every warp in <paramref name="rooms"/>, in room then placement order.
        /// </summary>
        public static IReadOnlyList<WarpPoint> Describe(IEnumerable<RoomDefinition> rooms) => rooms
            .SelectMany(room => room.Entities
                                    .Where(entity => entity.Kind == EntityKinds.WARP)
                                    .Select(entity => Describe(room, entity)))
            .ToArray();

        public static WarpPoint Describe(RoomDefinition room, EntityRecord entity) => new WarpPoint(
            room.Id,
            room.Name,
            entity.Id,
            string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Id : entity.DisplayName,
            // A warp with corrupt coordinates leads to the middle of its room rather than nowhere.
            float.IsFinite(entity.X) && float.IsFinite(entity.Y) ? new Vector2(entity.X, entity.Y) : Vector2.Zero,
            Math.Clamp(entity.WarpUnlockCost ?? 0, 0, MAXIMUM_COST),
            Math.Clamp(entity.WarpTravelCost ?? 0, 0, MAXIMUM_COST));
    }
}
