// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation
{
    /// <summary>
    /// A box the player cannot walk through, turned by <paramref name="Rotation"/> degrees clockwise
    /// about its own centre.
    /// </summary>
    /// <remarks>
    /// Rotation used to be absent, which is why the kinds that block movement offered no turn at all: the
    /// collision would have stayed square while the object on screen was not.
    /// </remarks>
    internal readonly record struct Obstacle(Vector2 Centre, Vector2 Size, float Rotation = 0);

    /// <summary>
    /// A mob population the simulation should keep alive within an area.
    /// </summary>
    /// <param name="EntityId">Identifier of the mob spawn entity.</param>
    /// <param name="Centre">Centre of the spawn area.</param>
    /// <param name="Size">Size of the spawn area.</param>
    /// <param name="Count">Maximum number of alive mobs.</param>
    /// <param name="MaxHealth">Maximum health of each mob.</param>
    /// <param name="ContactDamage">Damage dealt on contact.</param>
    /// <param name="DetectionRadius">Radius to detect players.</param>
    /// <param name="ProjectileCount">Number of projectiles fired.</param>
    /// <param name="ProjectileDamage">Damage of each projectile.</param>
    /// <param name="ProjectileSpeed">Speed of projectiles.</param>
    /// <param name="ProjectileRange">Range of projectiles.</param>
    /// <param name="AttackCooldown">Cooldown between attacks in milliseconds.</param>
    /// <param name="Speed">
    /// How fast its mobs move, in world units per second. Zero holds them still, which is how a turret
    /// is authored.
    /// </param>
    /// <param name="Chases">
    /// Whether its mobs walk towards a player they have noticed, rather than only tracing their patrol.
    /// </param>
    internal readonly record struct MobSpawnDefinition(
        string EntityId,
        Vector2 Centre,
        Vector2 Size,
        int Count,
        int MaxHealth,
        int ContactDamage,
        float DetectionRadius,
        int ProjectileCount,
        int ProjectileDamage,
        float ProjectileSpeed,
        float ProjectileRange,
        int AttackCooldown,
        float Speed = MobSpawnRules.DEFAULT_SPEED,
        bool Chases = false);

    /// <summary>
    /// Ranges accepted for a mob's own movement. Here rather than on the entity kind, so the simulation can
    /// clamp what it is given without any entities around.
    /// </summary>
    internal static class MobSpawnRules
    {
        /// <summary>Speed of a mob whose zone does not say, matching how they moved before it could.</summary>
        public const float DEFAULT_SPEED = 60;

        public const float MIN_SPEED = 0;

        /// <summary>Faster than a player can walk is a chase nobody escapes, so this is a little under it.</summary>
        public const float MAX_SPEED = 240;
    }

    /// <summary>
    /// The part of a room the simulation cares about: where the walls are, where the player starts,
    /// and what should be fighting them.
    /// </summary>
    /// <remarks>
    /// Built by the screen from the live entities, so the simulation never has to know that entities
    /// are drawables — which is what makes it testable without a game loop.
    /// </remarks>
    internal sealed class RoomLayout
    {
        public static readonly RoomLayout EMPTY = new RoomLayout(DodgeWorld.Model.DodgeWorldDefaults.MAP_SIZE, Vector2.Zero,
            new List<Obstacle>(), new List<MobSpawnDefinition>());

        public Vector2 Size { get; }

        public Vector2 Spawn { get; }

        public IReadOnlyList<Obstacle> Obstacles { get; }

        public IReadOnlyList<MobSpawnDefinition> SpawnZones { get; }

        /// <summary>
        /// The repeating hazards of the room: emitters and beams, which make a course to cross rather
        /// than a fight to win.
        /// </summary>
        public IReadOnlyList<HazardDefinition> Hazards { get; }

        public RoomLayout(Vector2 size, Vector2 spawn, IReadOnlyList<Obstacle> obstacles,
                          IReadOnlyList<MobSpawnDefinition> spawnZones, IReadOnlyList<HazardDefinition>? hazards = null)
        {
            Size = size;
            Spawn = spawn;
            Obstacles = obstacles;
            SpawnZones = spawnZones;
            Hazards = hazards ?? Array.Empty<HazardDefinition>();
        }
    }
}
