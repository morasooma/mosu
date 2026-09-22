// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation
{
    /// <summary>
    /// All Dodge World movement and combat, with no dependency on drawables or on a game clock.
    /// </summary>
    /// <remarks>
    /// The screen feeds this elapsed time and player intent, then mirrors the resulting state onto
    /// drawables. Keeping the rules here is what makes them testable: a fight can be played out by
    /// calling <see cref="Step"/> in a loop, with no framework, no rendering and no real time passing.
    /// <para>
    /// Time is accumulated internally rather than read from a clock, so a test can advance the world
    /// in exact increments, and so pausing genuinely freezes everything instead of letting cooldowns
    /// and scheduled volleys run on unobserved.
    /// </para>
    /// </remarks>
    internal sealed class WorldSimulation
    {
        /// <summary>
        /// Milliseconds of simulated time since the current room was loaded.
        /// </summary>
        public double CurrentTime { get; private set; }

        /// <summary>
        /// Whether this simulation moves players from their input.
        /// </summary>
        /// <remarks>
        /// False on the server, where a player's position arrives from their own client and is not to be
        /// recomputed: movement stays client-authoritative, while everything that can hurt somebody does
        /// not. Turning this off means <see cref="SetPlayerPosition"/> is the only thing that moves a
        /// player.
        /// </remarks>
        public bool MovesPlayers { get; init; } = true;

        /// <summary>
        /// Everyone in the room. One entry on a client; one per connected player on the server.
        /// </summary>
        public IReadOnlyList<PlayerState> Players => players;

        public IReadOnlyList<MobState> Mobs => mobs;

        public IReadOnlyList<ProjectileState> Projectiles => projectiles;

        public IReadOnlyList<TelegraphState> Telegraphs => telegraphs;

        /// <summary>The room's repeating hazards, in the order the room declares them.</summary>
        public IReadOnlyList<HazardState> Hazards => hazards;

        public event Action<MobState>? MobSpawned;

        /// <summary>
        /// A mob reached zero health. Its reward is claimed and its drawable retired in response.
        /// </summary>
        public event Action<MobState, PlayerState>? MobDefeated;

        public event Action<MobState>? MobDamaged;

        /// <summary>
        /// A mob left play without being defeated, because its room was unloaded.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="MobDefeated"/>: there is no death to animate and no reward to
        /// claim, the drawable simply has to go. Without this the mobs of a room stayed on screen
        /// after moving to another one.
        /// </remarks>
        public event Action<MobState>? MobRemoved;

        public event Action<ProjectileState>? ProjectileSpawned;

        public event Action<ProjectileState>? ProjectileExpired;

        public event Action<TelegraphState>? TelegraphStarted;

        public event Action<TelegraphState>? TelegraphEnded;

        public event Action<PlayerState, SwingRequest>? PlayerSwung;

        /// <summary>
        /// A player was hurt, and by how much. Separate from <see cref="PlayerHealthChanged"/>, which also
        /// carries healing and the plain setting of a number.
        /// </summary>
        public event Action<PlayerState, int>? PlayerDamaged;

        /// <summary>
        /// A player's health changed.
        /// </summary>
        public event Action<PlayerState>? PlayerHealthChanged;

        /// <summary>
        /// A player was defeated. Health is left at zero and they are left where they fell — deciding
        /// where death sends somebody is a rule of the world, not of the simulation, so the screen
        /// answers it by loading a room.
        /// </summary>
        public event Action<PlayerState>? PlayerDied;

        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly Dictionary<int, PlayerInput> inputs = new Dictionary<int, PlayerInput>();
        private readonly List<MobState> mobs = new List<MobState>();
        private readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        private readonly List<TelegraphState> telegraphs = new List<TelegraphState>();
        private readonly List<HazardState> hazards = new List<HazardState>();
        private readonly List<PendingRespawn> pendingRespawns = new List<PendingRespawn>();

        private RoomLayout layout = RoomLayout.EMPTY;
        private int nextMobId;
        private int nextProjectileId;
        private int nextTelegraphId;
        private int nextHazardId;

        /// <summary>
        /// Discards the current room and populates the new one, resetting the player and the clock.
        /// </summary>
        public void LoadRoom(RoomLayout newLayout)
        {
            CurrentTime = 0;

            ReplaceLayout(newLayout);

            // After the layout, so that players are placed at the new room's entrance rather than the
            // one they came from. Health is not touched: it belongs to the player, not to the room, and
            // healing on every doorway would make damage meaningless.
            foreach (PlayerState player in players)
                placeAtSpawn(player);
        }

        /// <summary>
        /// Swaps the room's layout, leaving the players where they are.
        /// </summary>
        /// <remarks>
        /// Used when the layout changes under a room that is already being stood in: an edit in the
        /// editor, or the server taking the room's mobs over. Moving the player back to the entrance
        /// because a zone was resized would be its own bug.
        /// </remarks>
        public void ReplaceLayout(RoomLayout newLayout)
        {
            layout = newLayout;

            clearCombat();

            foreach (MobState mob in mobs.ToArray())
                MobRemoved?.Invoke(mob);

            mobs.Clear();

            foreach (MobSpawnDefinition zone in newLayout.SpawnZones)
            {
                for (int i = 0; i < zone.Count; i++)
                    spawnMob(zone, i);
            }

            hazards.Clear();

            foreach (HazardDefinition definition in newLayout.Hazards)
            {
                // Never before the room's own first cycle, so a phase reads as a delay: two devices on the
                // same cycle with different phases fire in the order the author wrote them, rather than the
                // phased one going first because its grid happens to start sooner.
                long start = Math.Max(0, cycleIndexOf(definition, CurrentTime));

                hazards.Add(new HazardState(nextHazardId++, definition, start));
            }
        }

        /// <summary>
        /// Brings a player into the room, at full health and at its entrance. Returns the player that is
        /// already there when called twice for the same one.
        /// </summary>
        public PlayerState AddPlayer(int userId = 0)
        {
            PlayerState? existing = FindPlayer(userId);

            if (existing != null)
                return existing;

            var player = new PlayerState(userId);
            placeAtSpawn(player);
            // Somebody arriving in the world for the first time starts whole.
            player.HealFull();
            players.Add(player);
            return player;
        }

        public void RemovePlayer(int userId)
        {
            PlayerState? player = FindPlayer(userId);

            if (player == null)
                return;

            players.Remove(player);
            inputs.Remove(userId);
        }

        public PlayerState? FindPlayer(int userId)
        {
            foreach (PlayerState player in players)
            {
                if (player.UserId == userId)
                    return player;
            }

            return null;
        }

        /// <summary>
        /// Records what a player is asking to do. Kept until the next <see cref="Step"/> consumes it, so
        /// that a request made between steps is not lost.
        /// </summary>
        public void SetInput(int userId, in PlayerInput input) => inputs[userId] = input;

        /// <summary>
        /// Moves a player from outside, for a simulation that does not move players itself.
        /// </summary>
        public void SetPlayerPosition(int userId, Vector2 position, Vector2 facing, bool moving)
        {
            PlayerState? player = FindPlayer(userId);

            if (player == null || !float.IsFinite(position.X) || !float.IsFinite(position.Y))
                return;

            Vector2 clamped = ClampToRoom(position);
            player.StepDistance = (clamped - player.Position).Length;
            player.Position = clamped;
            player.Moving = moving;

            if (float.IsFinite(facing.X) && float.IsFinite(facing.Y) && facing.LengthSquared > 0)
                player.Facing = Vector2.Normalize(facing);
        }

        /// <summary>
        /// Puts a player at the room's entrance and forgets what they were in the middle of doing.
        /// </summary>
        /// <remarks>
        /// Health is deliberately not part of this. Rooms are doorways, not checkpoints; whoever decides
        /// that a player should be whole again — death, on the screen — says so on its own.
        /// </remarks>
        private void placeAtSpawn(PlayerState player)
        {
            player.Position = FreeSpotNear(layout.Spawn);
            player.Moving = false;
            player.StepDistance = 0;
            player.NextAttackTime = 0;
            player.NextContactDamageTime = 0;
        }

        /// <summary>
        /// Moves the player, without touching health, mobs or the clock.
        /// </summary>
        /// <remarks>
        /// Arriving at a warp lands on the warp itself rather than at the room's entrance, which is the
        /// one case where the player's position is decided from outside.
        /// </remarks>
        public void PlacePlayer(Vector2 position, int userId = 0)
        {
            PlayerState? player = FindPlayer(userId);

            if (player == null)
                return;

            player.Position = FreeSpotNear(position);
            player.Moving = false;
            player.StepDistance = 0;
        }

        /// <summary>
        /// The closest spot to <paramref name="position"/> the player actually fits in.
        /// </summary>
        /// <remarks>
        /// Everything that puts a player somewhere goes through this, because a spot inside a wall is a
        /// spot they can never leave: walking is refused along both axes while the box overlaps, so an
        /// arrival point behind a door or under a table was the end of that visit to the room. Which is
        /// exactly where an arrival point tends to be — it is placed beside the way back, and the way back
        /// is usually up against something.
        /// </remarks>
        public Vector2 FreeSpotNear(Vector2 position)
        {
            Vector2 candidate = ClampToRoom(position);

            // Straight out of whatever it landed in. Repeated, because coming out of one box can put the
            // player into the next one along.
            for (int attempt = 0; attempt < 8 && !CanOccupy(candidate); attempt++)
                candidate = ClampToRoom(pushedOutOfObstacles(candidate));

            if (CanOccupy(candidate))
                return candidate;

            // Wedged: nothing came of stepping aside, so look outwards for open floor instead.
            for (float radius = search_step; radius <= search_limit; radius += search_step)
            {
                for (int step = 0; step < search_directions; step++)
                {
                    float angle = step * MathF.PI * 2 / search_directions;
                    Vector2 probe = ClampToRoom(position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);

                    if (CanOccupy(probe))
                        return probe;
                }
            }

            // A room with nowhere to stand. Better where the author asked than at a corner of the map.
            return ClampToRoom(position);
        }

        private const float search_step = 24;
        private const float search_limit = 600;
        private const int search_directions = 12;

        /// <summary>
        /// Moves the player clear of the first obstacle they overlap, out of its nearest side.
        /// </summary>
        /// <remarks>
        /// Measured in the obstacle's own frame, so a turned box pushes out of a side it actually has
        /// rather than out of its bounding box.
        /// </remarks>
        private Vector2 pushedOutOfObstacles(Vector2 position)
        {
            Vector2 centre = position + CombatRules.PLAYER_TORSO_OFFSET;

            foreach (Obstacle obstacle in layout.Obstacles)
            {
                if (!CombatRules.TouchesPlayerBox(centre, obstacle.Centre, obstacle.Size, obstacle.Rotation))
                    continue;

                float radians = obstacle.Rotation * MathF.PI / 180;
                Vector2 along = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
                Vector2 across = new Vector2(-along.Y, along.X);
                Vector2 delta = centre - obstacle.Centre;

                float localX = delta.X * along.X + delta.Y * along.Y;
                float localY = delta.X * across.X + delta.Y * across.Y;

                // The square's reach across either of the box's axes, which is wider than half of it
                // once the box is turned.
                float reach = CombatRules.PLAYER_HALF_SIZE * (MathF.Abs(along.X) + MathF.Abs(along.Y));
                float overlapX = reach + Math.Abs(obstacle.Size.X) / 2 - Math.Abs(localX);
                float overlapY = reach + Math.Abs(obstacle.Size.Y) / 2 - Math.Abs(localY);

                if (overlapX <= 0 || overlapY <= 0)
                    continue;

                // Out of the nearer side, which is the shortest way out and so the one that keeps the
                // player closest to where they were meant to arrive.
                if (overlapX < overlapY)
                    return position + along * ((localX < 0 ? -1 : 1) * (overlapX + 1));

                return position + across * ((localY < 0 ? -1 : 1) * (overlapY + 1));
            }

            return position;
        }

        /// <summary>
        /// Adds a single mob outside of any stored spawn zone. Used by tests to set up a fight.
        /// </summary>
        public MobState SpawnMob(in MobSpawnDefinition definition, int index = 0)
        {
            MobState mob = spawnMob(definition, index);
            // A hand-placed mob should stay where it was asked for rather than snapping onto a
            // wander path derived from a zone it does not belong to.
            mob.Position = definition.Centre;
            return mob;
        }

        /// <summary>
        /// Advances the world by <paramref name="elapsedMilliseconds"/>.
        /// </summary>
        /// <remarks>
        /// Not calling this is how the world is paused, which is why nothing here reads a clock.
        /// </remarks>
        public void Step(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds <= 0)
                return;

            CurrentTime += elapsedMilliseconds;

            float elapsedSeconds = (float)(elapsedMilliseconds / 1000);

            foreach (PlayerState player in players.ToArray())
            {
                PlayerInput input = inputs.TryGetValue(player.UserId, out PlayerInput stored) ? stored : PlayerInput.NONE;

                if (MovesPlayers)
                    updatePlayer(player, elapsedSeconds, input);

                if (input.Attack is Vector2 attack)
                    Swing(attack, player.UserId);
            }

            // Cleared after being applied so that an attack is not repeated on the next step, which
            // would otherwise turn one click into a stream of swings.
            inputs.Clear();

            updateMobPaths(elapsedSeconds);

            updateContactDamage();
            updateMobVolleys();
            resolveTelegraphs();
            updateHazards();
            updateProjectiles(elapsedSeconds);
            resolveRespawns();
        }

        #region hazards

        /// <summary>
        /// Which cycle of a hazard a moment in time falls in. Negative before the first one, which is how
        /// a phase delays a hazard's first firing.
        /// </summary>
        private static long cycleIndexOf(in HazardDefinition definition, double time)
        {
            int cycle = Math.Max(CombatRules.HAZARD_MIN_CYCLE, definition.CycleMilliseconds);
            return (long)Math.Floor((time - definition.PhaseMilliseconds) / cycle);
        }

        /// <summary>
        /// Runs the room's course: emitters fire on their cycle, beams hold their line for part of theirs.
        /// </summary>
        /// <remarks>
        /// Driven entirely by <see cref="CurrentTime"/> rather than by counting down timers, so a hazard
        /// cannot drift out of step with another and a long frame skips a shot instead of firing it late.
        /// <para>
        /// Nothing harms anybody during the cycle the room was entered on. A player should walk into a
        /// course, not be spawned inside a live beam, and it means the first shot and the first flash both
        /// point exactly where the author pointed them — the turn is counted from there.
        /// </para>
        /// </remarks>
        private void updateHazards()
        {
            foreach (HazardState hazard in hazards)
            {
                HazardDefinition definition = hazard.Definition;
                int cycle = Math.Max(CombatRules.HAZARD_MIN_CYCLE, definition.CycleMilliseconds);
                long index = cycleIndexOf(definition, CurrentTime);
                double intoCycle = CurrentTime - definition.PhaseMilliseconds - index * (double)cycle;
                long turns = index - hazard.StartCycle - 1;
                bool running = index > hazard.StartCycle;

                if (running)
                    hazard.Angle = MathF.PI / 180 * (definition.DirectionDegrees + definition.TurnDegrees * turns);

                switch (definition.Kind)
                {
                    case HazardKind.Emitter:
                        if (!running || index <= hazard.FiredCycle)
                            continue;

                        hazard.FiredCycle = index;
                        fire(hazard);
                        break;

                    case HazardKind.Beam:
                        bool active = running && intoCycle < definition.ActiveMilliseconds;

                        hazard.Active = active;

                        // The warning belongs to the window about to open, which is the next cycle's.
                        hazard.Warning = !active && intoCycle >= cycle - CombatRules.BEAM_WARNING_DURATION;

                        if (active)
                            burn(hazard);

                        break;
                }
            }
        }

        /// <summary>
        /// Fires an emitter's fan. The spread is centred on the emitter's direction, so a spread of zero
        /// is a single aimed stream and one of 360 is a ring.
        /// </summary>
        private void fire(HazardState hazard)
        {
            HazardDefinition definition = hazard.Definition;

            if (definition.ProjectileCount <= 0)
                return;

            float spread = MathF.PI / 180 * definition.SpreadDegrees;

            // A full ring divides evenly; anything less spans the arc end to end, so the outermost shots
            // sit on the edges the author drew rather than inside them.
            bool ring = definition.SpreadDegrees >= 360;
            float step = definition.ProjectileCount == 1 ? 0
                : ring ? MathF.PI * 2 / definition.ProjectileCount
                : spread / (definition.ProjectileCount - 1);

            float first = definition.ProjectileCount == 1 || ring ? hazard.Angle : hazard.Angle - spread / 2;

            for (int i = 0; i < definition.ProjectileCount; i++)
            {
                float angle = first + step * i;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

                var projectile = new ProjectileState(nextProjectileId++, hazard.Origin, direction,
                    definition.ProjectileSpeed, definition.Range, definition.Damage);

                projectiles.Add(projectile);
                ProjectileSpawned?.Invoke(projectile);
            }
        }

        /// <summary>
        /// Hurts whoever is standing in a live beam, on the same throttle as walking into a mob.
        /// </summary>
        private void burn(HazardState hazard)
        {
            foreach (PlayerState player in players.ToArray())
            {
                if (!player.Alive)
                    continue;

                if (hazard.NextDamageTime.TryGetValue(player.UserId, out double next) && CurrentTime < next)
                    continue;

                float reach = CombatRules.BeamReach(hazard.Definition.Width, hazard.Angle);

                if (distanceToSegment(player.Torso, hazard.Origin, hazard.End) > reach)
                    continue;

                hazard.NextDamageTime[player.UserId] = CurrentTime + CombatRules.CONTACT_DAMAGE_COOLDOWN;
                damagePlayer(player, hazard.Definition.Damage);
            }
        }

        private static float distanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 span = to - from;
            float lengthSquared = span.LengthSquared;

            if (lengthSquared <= 0)
                return (point - from).Length;

            float along = Math.Clamp(Vector2.Dot(point - from, span) / lengthSquared, 0, 1);
            return (point - (from + span * along)).Length;
        }

        #endregion

        #region player

        private void updatePlayer(PlayerState player, float elapsedSeconds, in PlayerInput input)
        {
            Vector2 direction = input.Movement;

            if (direction.LengthSquared <= 0)
            {
                player.Moving = false;
                player.StepDistance = 0;
                return;
            }

            direction = Vector2.Normalize(direction);

            Vector2 movement = direction * (elapsedSeconds * CombatRules.PLAYER_SPEED);
            Vector2 next = player.Position;

            // Standing inside something already: a wall dropped on them in the editor, or a layout that
            // changed under their feet. Then every direction is allowed, because walking out through a
            // wall is better than never walking again.
            bool trapped = !CanOccupy(player.Position);

            // Axes are resolved separately so that walking into a wall at an angle slides along it
            // instead of stopping dead.
            if (trapped || CanOccupy(new Vector2(next.X + movement.X, next.Y)))
                next.X += movement.X;

            if (trapped || CanOccupy(new Vector2(next.X, next.Y + movement.Y)))
                next.Y += movement.Y;

            next = ClampToRoom(next);

            if (next == player.Position)
            {
                player.Moving = false;
                player.StepDistance = 0;
                return;
            }

            player.StepDistance = (next - player.Position).Length;
            player.Position = next;
            player.Facing = direction;
            player.Moving = true;
        }

        /// <summary>
        /// Whether the player's collision box fits at <paramref name="position"/>.
        /// </summary>
        public bool CanOccupy(Vector2 position)
        {
            Vector2 centre = position + CombatRules.PLAYER_TORSO_OFFSET;

            foreach (Obstacle obstacle in layout.Obstacles)
            {
                if (CombatRules.TouchesPlayerBox(centre, obstacle.Centre, obstacle.Size, obstacle.Rotation))
                    return false;
            }

            return true;
        }

        public Vector2 ClampToRoom(Vector2 position, float padding = CombatRules.ROOM_EDGE_PADDING) => new Vector2(
            Math.Clamp(position.X, -layout.Size.X / 2 + padding, layout.Size.X / 2 - padding),
            Math.Clamp(position.Y, -layout.Size.Y / 2 + padding, layout.Size.Y / 2 - padding));

        /// <summary>
        /// Swings at <paramref name="direction"/>, damaging every mob inside the arc.
        /// </summary>
        /// <remarks>
        /// Damage lands immediately; the swing animation the view plays in response is decorative.
        /// </remarks>
        public void Swing(Vector2 direction, int userId = 0)
        {
            PlayerState? player = FindPlayer(userId);

            if (player == null || !player.Alive || CurrentTime < player.NextAttackTime || direction.LengthSquared <= 0)
                return;

            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
                return;

            direction = Vector2.Normalize(direction);
            player.NextAttackTime = CurrentTime + CombatRules.ATTACK_COOLDOWN;

            bool clockwise = player.NextSwingClockwise;
            player.NextSwingClockwise = !player.NextSwingClockwise;

            Vector2 origin = player.Position + CombatRules.WEAPON_ORIGIN_OFFSET;
            float angle = MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI;

            PlayerSwung?.Invoke(player, new SwingRequest(origin, direction, angle, clockwise));

            float minimumDot = MathF.Cos(CombatRules.ATTACK_ARC_DEGREES / 2 * MathF.PI / 180);

            foreach (MobState mob in mobs.ToArray())
            {
                Vector2 delta = mob.Torso - origin;
                float distance = delta.Length;

                if (distance > CombatRules.ATTACK_RADIUS || distance <= 0 || Vector2.Dot(delta / distance, direction) < minimumDot)
                    continue;

                if (!mob.ApplyDamage(player.SwordDamage))
                {
                    MobDamaged?.Invoke(mob);
                    continue;
                }

                mobs.Remove(mob);
                scheduleRespawn(mob.SpawnZoneId);
                MobDefeated?.Invoke(mob, player);
            }
        }

        /// <summary>
        /// Hurts one player, without touching anything else in the room.
        /// </summary>
        /// <remarks>
        /// Death used to clear everything in flight, which is right for a room with one player in it and
        /// wrong for a shared one: one player dying must not disarm the volley aimed at the next.
        /// The room is cleared when it is reloaded, which is what death leads to on the client.
        /// </remarks>
        private void damagePlayer(PlayerState player, int amount)
        {
            bool fatal = player.ApplyDamage(amount);
            PlayerDamaged?.Invoke(player, amount);
            PlayerHealthChanged?.Invoke(player);

            if (fatal)
                PlayerDied?.Invoke(player);
        }

        #endregion

        #region mobs

        private MobState spawnMob(in MobSpawnDefinition definition, int index)
        {
            Vector2 radius = new Vector2(
                Math.Max(0, definition.Size.X / 2 - wander_inset),
                Math.Max(0, definition.Size.Y / 2 - wander_inset));

            var mob = new MobState(nextMobId++, definition, index, definition.Centre, radius,
                pathPhase: index * 1.73f,
                pathSpeed: patrolAngularSpeed(definition, radius, index));

            mob.Position = pathPositionAt(mob, CurrentTime);
            mobs.Add(mob);
            MobSpawned?.Invoke(mob);

            return mob;
        }

        private const float wander_inset = 24;

        /// <summary>
        /// How fast a mob works along its patrol curve, so that the speed its zone asks for is the speed
        /// it actually walks at.
        /// </summary>
        /// <remarks>
        /// The curve is traced by an angle, so dividing by the zone's extent turns units per second into
        /// that angle. A little slower than the mob can walk, so one coming back from a chase can catch its
        /// own patrol point; uneven per index, or a zone's mobs would move as one.
        /// </remarks>
        private static float patrolAngularSpeed(in MobSpawnDefinition definition, Vector2 radius, int index)
        {
            float extent = Math.Max(1, (radius.X + radius.Y) / 2);
            float speed = Math.Clamp(definition.Speed, MobSpawnRules.MIN_SPEED, MobSpawnRules.MAX_SPEED) * patrol_speed_fraction;

            return speed / (1000 * extent) * (1 + index % 3 * 0.17f);
        }

        private const float patrol_speed_fraction = 0.8f;

        /// <summary>
        /// The point on a mob's patrol: a fixed Lissajous curve within its spawn zone, offset by its
        /// index so that a zone's mobs do not move as one.
        /// </summary>
        private static Vector2 pathPositionAt(MobState mob, double time)
        {
            float phase = (float)time * mob.PathSpeed + mob.PathPhase;

            return mob.PathCentre + new Vector2(
                MathF.Sin(phase) * mob.PathRadius.X,
                MathF.Cos(phase * 0.73f) * mob.PathRadius.Y);
        }

        /// <summary>
        /// Moves every mob: along its patrol, or towards whoever it has noticed.
        /// </summary>
        /// <remarks>
        /// A patrolling mob's position is a function of the time alone, so it never drifts. A chasing one
        /// is walked towards its target, which is its own patrol point while nobody is in range — that is
        /// what lets it come back without snapping to the curve.
        /// <para>Mobs still walk through walls: obstacles are not part of a room the server runs.</para>
        /// </remarks>
        private void updateMobPaths(float elapsedSeconds)
        {
            foreach (MobState mob in mobs)
            {
                Vector2 patrol = pathPositionAt(mob, CurrentTime);

                if (!mob.Chases)
                {
                    mob.Position = patrol;
                    continue;
                }

                Vector2 target = patrol;
                PlayerState? noticed = nearestPlayerTo(mob.Torso);

                if (noticed != null && (noticed.Torso - mob.Torso).Length <= mob.DetectionRadius)
                {
                    target = noticed.Position;

                    // Stopping just inside touching distance: close enough that contact damage keeps
                    // ticking, far enough that the two bodies do not become one.
                    Vector2 approach = target - mob.Position;
                    float distance = approach.Length;
                    float keep = CombatRules.MOB_CONTACT_DISTANCE * contact_standoff;

                    if (distance <= keep)
                        continue;

                    target = mob.Position + approach / distance * (distance - keep);
                }

                mob.Position = ClampToRoom(stepTowards(mob.Position, target, mob.Speed * elapsedSeconds));
            }
        }

        /// <summary>
        /// How much of touching distance a chasing mob keeps between itself and the player.
        /// </summary>
        private const float contact_standoff = 0.7f;

        /// <summary>
        /// <paramref name="from"/> moved at most <paramref name="distance"/> towards <paramref name="to"/>,
        /// stopping exactly on it rather than overshooting and jittering around it.
        /// </summary>
        private static Vector2 stepTowards(Vector2 from, Vector2 to, float distance)
        {
            Vector2 delta = to - from;
            float length = delta.Length;

            if (distance <= 0 || length <= 0)
                return from;

            return length <= distance ? to : from + delta / length * distance;
        }

        private void updateContactDamage()
        {
            foreach (PlayerState player in players.ToArray())
            {
                if (!player.Alive || CurrentTime < player.NextContactDamageTime)
                    continue;

                MobState? touching = mobs.FirstOrDefault(mob =>
                    mob.Alive && (mob.Torso - player.Torso).Length < CombatRules.MOB_CONTACT_DISTANCE);

                if (touching == null)
                    continue;

                player.NextContactDamageTime = CurrentTime + CombatRules.CONTACT_DAMAGE_COOLDOWN;
                damagePlayer(player, touching.ContactDamage);
            }
        }

        /// <summary>
        /// The living player closest to a point, which is who a mob aims at.
        /// </summary>
        private PlayerState? nearestPlayerTo(Vector2 point)
        {
            PlayerState? nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (PlayerState player in players)
            {
                if (!player.Alive)
                    continue;

                float distance = (player.Torso - point).LengthSquared;

                if (distance >= nearestDistance)
                    continue;

                nearest = player;
                nearestDistance = distance;
            }

            return nearest;
        }

        private void updateMobVolleys()
        {
            foreach (MobState mob in mobs)
            {
                if (!mob.Alive || mob.ProjectileCount == 0 || CurrentTime < mob.NextAttackTime)
                    continue;

                Vector2 origin = mob.Torso;
                PlayerState? target = nearestPlayerTo(origin);

                if (target == null)
                    continue;

                Vector2 delta = target.Torso - origin;

                if (delta.LengthSquared > mob.DetectionRadius * mob.DetectionRadius)
                    continue;

                // The first shot is aimed; the rest complete an even ring around the mob.
                float aimed = MathF.Atan2(delta.Y, delta.X);

                Vector2[] directions = Enumerable.Range(0, mob.ProjectileCount)
                                                 .Select(index => aimed + index * MathF.PI * 2 / mob.ProjectileCount)
                                                 .Select(angle => new Vector2(MathF.Cos(angle), MathF.Sin(angle)))
                                                 .ToArray();

                mob.NextAttackTime = CurrentTime + mob.AttackCooldown;

                var telegraph = new TelegraphState(nextTelegraphId++, mob, origin, directions, mob.ProjectileRange,
                    CurrentTime + CombatRules.MOB_ATTACK_TELEGRAPH_DURATION);

                telegraphs.Add(telegraph);
                TelegraphStarted?.Invoke(telegraph);
            }
        }

        private void resolveTelegraphs()
        {
            for (int i = telegraphs.Count - 1; i >= 0; i--)
            {
                TelegraphState telegraph = telegraphs[i];

                if (CurrentTime < telegraph.FireTime)
                    continue;

                telegraphs.RemoveAt(i);
                TelegraphEnded?.Invoke(telegraph);

                // A mob killed mid-telegraph does not get to fire.
                if (!telegraph.Mob.Alive || !mobs.Contains(telegraph.Mob))
                    continue;

                foreach (Vector2 direction in telegraph.Directions)
                {
                    var projectile = new ProjectileState(nextProjectileId++, telegraph.Origin, direction,
                        telegraph.Mob.ProjectileSpeed, telegraph.Range, telegraph.Mob.ProjectileDamage);

                    projectiles.Add(projectile);
                    ProjectileSpawned?.Invoke(projectile);
                }
            }
        }

        private void updateProjectiles(float elapsedSeconds)
        {
            foreach (ProjectileState projectile in projectiles.ToArray())
            {
                projectile.Advance(elapsedSeconds);

                // A projectile is spent on whoever it reaches first, so two players cannot be hurt by
                // the same shot.
                PlayerState? hit = players.FirstOrDefault(player =>
                    player.Alive && CombatRules.TouchesPlayerBody(projectile.Position, CombatRules.PROJECTILE_RADIUS, player.Torso));

                if (hit != null)
                {
                    removeProjectile(projectile);
                    damagePlayer(hit, projectile.Damage);
                    continue;
                }

                if (!projectile.InRange)
                    removeProjectile(projectile);
            }
        }

        private void removeProjectile(ProjectileState projectile)
        {
            projectiles.Remove(projectile);
            ProjectileExpired?.Invoke(projectile);
        }

        /// <summary>
        /// Finds the zone a respawn belongs to. It can be absent when the zone was deleted in the
        /// editor while the respawn was pending.
        /// </summary>
        private bool tryFindSpawnZone(string spawnZoneId, out MobSpawnDefinition zone)
        {
            foreach (MobSpawnDefinition candidate in layout.SpawnZones)
            {
                if (candidate.EntityId != spawnZoneId)
                    continue;

                zone = candidate;
                return true;
            }

            zone = default;
            return false;
        }

        private void scheduleRespawn(string spawnZoneId) =>
            pendingRespawns.Add(new PendingRespawn(spawnZoneId, CurrentTime + CombatRules.MOB_RESPAWN_DELAY));

        private void resolveRespawns()
        {
            for (int i = pendingRespawns.Count - 1; i >= 0; i--)
            {
                PendingRespawn pending = pendingRespawns[i];

                if (CurrentTime < pending.Time)
                    continue;

                pendingRespawns.RemoveAt(i);

                if (!tryFindSpawnZone(pending.SpawnZoneId, out MobSpawnDefinition zone))
                    continue;

                int alive = mobs.Count(mob => mob.SpawnZoneId == pending.SpawnZoneId);

                if (alive >= zone.Count)
                    continue;

                spawnMob(zone, alive);
            }
        }

        /// <summary>
        /// Drops everything in flight. Used on death and on room change, so that a volley aimed in one
        /// room cannot land in another.
        /// </summary>
        private void clearCombat()
        {
            foreach (ProjectileState projectile in projectiles.ToArray())
                ProjectileExpired?.Invoke(projectile);

            projectiles.Clear();

            foreach (TelegraphState telegraph in telegraphs.ToArray())
                TelegraphEnded?.Invoke(telegraph);

            telegraphs.Clear();
            pendingRespawns.Clear();
        }

        #endregion

        private readonly record struct PendingRespawn(string SpawnZoneId, double Time);
    }
}
