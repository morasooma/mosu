// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities
{
    /// <summary>
    /// Builds what the simulation needs straight from a stored room, without creating any drawables.
    /// </summary>
    /// <remarks>
    /// This is how the server runs a room: it has the published document and no graphics. The client
    /// builds its layout from the live entities instead, because in the editor the room being played is
    /// the one on screen rather than the one last saved.
    /// <para>
    /// Obstacles are left out on purpose. They only matter for moving a player, and a player is moved by
    /// their own client; mobs follow fixed paths and collide with nothing.
    /// </para>
    /// </remarks>
    internal static class DocumentRoomLayout
    {
        public static RoomLayout Describe(RoomDefinition room)
        {
            IEnumerable<MobSpawnDefinition> zones = room.Entities
                                                       .Where(entity => entity.Kind == EntityKinds.MOB_SPAWN)
                                                       .Select(MobSpawnKind.Describe);

            IEnumerable<HazardDefinition> hazards = room.Entities
                                                       .Where(entity => entity.Kind is EntityKinds.EMITTER or EntityKinds.BEAM)
                                                       .Select(entity => entity.Kind == EntityKinds.EMITTER
                                                           ? EmitterKind.Describe(entity)
                                                           : BeamKind.Describe(entity));

            return new RoomLayout(room.Size, room.Spawn, Array.Empty<Obstacle>(), zones.ToArray(), hazards.ToArray());
        }
    }
}
