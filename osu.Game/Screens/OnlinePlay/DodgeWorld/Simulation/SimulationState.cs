// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation
{
    /// <summary>
    /// What the player is asking to do this frame.
    /// </summary>
    /// <param name="Movement">
    /// Sum of the held direction keys. Need not be normalised — the simulation does that, so that
    /// holding two keys is not faster than holding one.
    /// </param>
    /// <param name="Attack">Direction of a requested swing, or <c>null</c> for no attack this frame.</param>
    internal readonly record struct PlayerInput(Vector2 Movement, Vector2? Attack)
    {
        public static readonly PlayerInput NONE = new PlayerInput(Vector2.Zero, null);
    }

    /// <summary>
    /// One player's simulated position and health.
    /// </summary>
    /// <remarks>
    /// A room can hold several of these: the client simulates only the local player, while the server
    /// simulates everyone in the room so that its mobs can see and hurt all of them.
    /// </remarks>
    internal sealed class PlayerState
    {
        public const int MAXIMUM_HEALTH = 100;

        /// <summary>
        /// Who this is. Zero for the local player of a simulation that has no server behind it.
        /// </summary>
        public int UserId { get; }

        public PlayerState(int userId = 0)
        {
            UserId = userId;
        }

        public Vector2 Position { get; set; }

        public int Health { get; private set; } = MAXIMUM_HEALTH;

        public bool Alive => Health > 0;

        /// <summary>
        /// What one of this player's swings takes off a mob: the sword in their hand rather than a rule of
        /// the room. Set from the published world at either end, never sent by a client.
        /// </summary>
        public int SwordDamage { get; private set; } = Model.WeaponSkin.DEFAULT_DAMAGE;

        public void SetSwordDamage(int value) =>
            SwordDamage = Math.Clamp(value, Model.WeaponSkin.MIN_DAMAGE, Model.WeaponSkin.MAX_DAMAGE);

        /// <summary>Simulation time at which this player may swing again.</summary>
        internal double NextAttackTime { get; set; }

        /// <summary>Simulation time at which touching a mob may hurt this player again.</summary>
        internal double NextContactDamageTime { get; set; }

        /// <summary>Which way the next swing sweeps, alternating so consecutive swings differ.</summary>
        internal bool NextSwingClockwise { get; set; }

        /// <summary>
        /// Last direction the player moved in, for the view to face them.
        /// </summary>
        public Vector2 Facing { get; set; } = Vector2.UnitY;

        /// <summary>
        /// Whether the player moved during the last step.
        /// </summary>
        public bool Moving { get; set; }

        /// <summary>
        /// Distance covered during the last step, used to advance the walk cycle.
        /// </summary>
        public float StepDistance { get; set; }

        /// <summary>
        /// Where hits against the player are measured from.
        /// </summary>
        public Vector2 Torso => Position + CombatRules.PLAYER_TORSO_OFFSET;

        /// <summary>Applies damage and reports whether this was fatal.</summary>
        public bool ApplyDamage(int amount)
        {
            Health = Math.Max(0, Health - Math.Max(0, amount));
            return Health <= 0;
        }

        public void HealFull() => Health = MAXIMUM_HEALTH;

        /// <summary>
        /// Overwrites health with a value decided elsewhere, which is what happens when the server owns
        /// combat: the client is told the result rather than working it out.
        /// </summary>
        public void SetHealth(int value) => Health = Math.Clamp(value, 0, MAXIMUM_HEALTH);
    }

    /// <summary>
    /// One live mob.
    /// </summary>
    internal sealed class MobState
    {
        /// <summary>
        /// Identity stable for this mob's whole life, so the view can match its drawable to it.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The spawn zone that produced this mob, and that its reward is claimed against.
        /// </summary>
        public string SpawnZoneId { get; }

        public int MaximumHealth { get; }

        public int Health { get; private set; }

        public int ContactDamage { get; }

        public float DetectionRadius { get; }

        public int ProjectileCount { get; }

        public int ProjectileDamage { get; }

        public float ProjectileSpeed { get; }

        public float ProjectileRange { get; }

        public int AttackCooldown { get; }

        /// <summary>How fast this mob moves, in world units per second.</summary>
        public float Speed { get; }

        /// <summary>Whether it walks towards a player it has noticed instead of only patrolling.</summary>
        public bool Chases { get; }

        public Vector2 Position { get; set; }

        public bool Alive => Health > 0;

        /// <summary>
        /// Where hits against this mob are measured from, and where its volleys originate.
        /// </summary>
        public Vector2 Torso => Position + CombatRules.MOB_TORSO_OFFSET;

        /// <summary>Simulation time at which this mob may fire again.</summary>
        internal double NextAttackTime { get; set; }

        /// <summary>Centre of the wander path.</summary>
        internal Vector2 PathCentre { get; }

        /// <summary>Half-extents of the wander path.</summary>
        internal Vector2 PathRadius { get; }

        /// <summary>Offset into the wander path, so mobs from one zone do not move in lockstep.</summary>
        internal float PathPhase { get; }

        internal float PathSpeed { get; }

        public MobState(int id, in MobSpawnDefinition definition, int index, Vector2 pathCentre, Vector2 pathRadius,
                        float pathPhase, float pathSpeed)
        {
            Id = id;
            SpawnZoneId = definition.EntityId;
            MaximumHealth = Math.Max(1, definition.MaxHealth);
            Health = MaximumHealth;
            ContactDamage = definition.ContactDamage;
            DetectionRadius = definition.DetectionRadius;
            ProjectileCount = definition.ProjectileCount;
            ProjectileDamage = definition.ProjectileDamage;
            ProjectileSpeed = definition.ProjectileSpeed;
            ProjectileRange = definition.ProjectileRange;
            AttackCooldown = definition.AttackCooldown;
            Speed = Math.Clamp(definition.Speed, MobSpawnRules.MIN_SPEED, MobSpawnRules.MAX_SPEED);
            Chases = definition.Chases;
            PathCentre = pathCentre;
            PathRadius = pathRadius;
            PathPhase = pathPhase;
            PathSpeed = pathSpeed;
            Index = index;
        }

        internal int Index { get; }

        /// <summary>Applies damage and reports whether this killed the mob.</summary>
        public bool ApplyDamage(int amount)
        {
            if (!Alive)
                return false;

            Health = Math.Max(0, Health - Math.Max(0, amount));
            return Health <= 0;
        }
    }

    /// <summary>
    /// One projectile in flight.
    /// </summary>
    internal sealed class ProjectileState
    {
        public int Id { get; }

        public Vector2 Position { get; set; }

        public Vector2 Direction { get; }

        public float Speed { get; }

        public int Damage { get; }

        private readonly float maximumRange;
        private float travelled;

        /// <summary>
        /// Whether the projectile still has range left. Lifetime is measured in distance rather than
        /// time, so a slow projectile is not cut short.
        /// </summary>
        public bool InRange => travelled < maximumRange;

        public ProjectileState(int id, Vector2 origin, Vector2 direction, float speed, float maximumRange, int damage)
        {
            Id = id;
            Position = origin;
            Direction = direction.LengthSquared > 0 ? Vector2.Normalize(direction) : Vector2.UnitX;
            Speed = speed;
            this.maximumRange = maximumRange;
            Damage = damage;
        }

        public void Advance(float elapsedSeconds)
        {
            float distance = Math.Max(0, elapsedSeconds) * Speed;
            travelled += distance;
            Position += Direction * distance;
        }
    }

    /// <summary>
    /// A volley a mob has committed to, shown to the player before it fires.
    /// </summary>
    internal sealed class TelegraphState
    {
        public int Id { get; }

        public Vector2 Origin { get; }

        public Vector2[] Directions { get; }

        public float Range { get; }

        /// <summary>Simulation time at which the projectiles appear.</summary>
        public double FireTime { get; }

        internal MobState Mob { get; }

        public TelegraphState(int id, MobState mob, Vector2 origin, Vector2[] directions, float range, double fireTime)
        {
            Id = id;
            Mob = mob;
            Origin = origin;
            Directions = directions;
            Range = range;
            FireTime = fireTime;
        }
    }

    /// <summary>
    /// A swing the player has just started, for the view to animate.
    /// </summary>
    internal readonly record struct SwingRequest(Vector2 Origin, Vector2 Direction, float AngleDegrees, bool Clockwise);

    internal enum HazardKind
    {
        /// <summary>Fires a fan of projectiles once per cycle.</summary>
        Emitter,

        /// <summary>Holds a lethal line for part of each cycle.</summary>
        Beam,
    }

    /// <summary>
    /// A hazard that repeats forever, for building a course to cross rather than a fight to win.
    /// </summary>
    /// <remarks>
    /// Everything a hazard does is a function of the time since the room was entered, so two clients
    /// stepping the same room reach the same pattern, and an author can stagger several hazards by
    /// <see cref="PhaseMilliseconds"/> alone. Nothing here reacts to a player: a course is a rhythm to
    /// read, not an enemy to fight.
    /// </remarks>
    /// <param name="EntityId">Identifier of the hazard entity.</param>
    /// <param name="Kind">The hazard kind.</param>
    /// <param name="Origin">Origin location of the hazard.</param>
    /// <param name="DirectionDegrees">Firing direction in degrees.</param>
    /// <param name="TurnDegrees">Added to the direction on every cycle, which is what makes a spinner.</param>
    /// <param name="CycleMilliseconds">Total cycle duration in milliseconds.</param>
    /// <param name="PhaseMilliseconds">Phase offset in milliseconds.</param>
    /// <param name="ProjectileCount">Number of projectiles fired.</param>
    /// <param name="SpreadDegrees">Angular spread in degrees.</param>
    /// <param name="Damage">Damage dealt.</param>
    /// <param name="ProjectileSpeed">Speed of projectiles.</param>
    /// <param name="Range">Projectile range for an emitter; the length of the line for a beam.</param>
    /// <param name="Width">Beam thickness. Unused by an emitter.</param>
    /// <param name="ActiveMilliseconds">How long a beam stays lethal within its cycle.</param>
    internal readonly record struct HazardDefinition(
        string EntityId,
        HazardKind Kind,
        Vector2 Origin,
        float DirectionDegrees,
        float TurnDegrees,
        int CycleMilliseconds,
        int PhaseMilliseconds,
        int ProjectileCount,
        float SpreadDegrees,
        int Damage,
        float ProjectileSpeed,
        float Range,
        float Width,
        int ActiveMilliseconds);

    /// <summary>
    /// One running hazard: its definition, and where it is in its cycle.
    /// </summary>
    internal sealed class HazardState
    {
        public int Id { get; }

        public HazardDefinition Definition { get; }

        public string EntityId => Definition.EntityId;

        public HazardKind Kind => Definition.Kind;

        public Vector2 Origin => Definition.Origin;

        /// <summary>The direction of this cycle, in radians.</summary>
        public float Angle { get; internal set; }

        /// <summary>Whether a beam is lethal right now.</summary>
        public bool Active { get; internal set; }

        /// <summary>Whether a beam is showing itself before becoming lethal.</summary>
        public bool Warning { get; internal set; }

        /// <summary>The far end of a beam's centre line.</summary>
        public Vector2 End => Origin + new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * Definition.Range;

        /// <summary>
        /// The cycle whose shot has already been fired.
        /// </summary>
        internal long FiredCycle;

        /// <summary>
        /// The cycle the room was entered on. Nothing harms anybody during it — a course should be walked
        /// into, not spawned into — and it is what the rest of the cycles are counted from, so the first
        /// thing the player sees points the way the author pointed it.
        /// </summary>
        internal readonly long StartCycle;

        /// <summary>When each player may next be burned by this beam.</summary>
        internal readonly Dictionary<int, double> NextDamageTime = new Dictionary<int, double>();

        public HazardState(int id, in HazardDefinition definition, long startingCycle)
        {
            Id = id;
            Definition = definition;
            StartCycle = startingCycle;
            FiredCycle = startingCycle;
            Angle = MathF.PI / 180 * definition.DirectionDegrees;
        }
    }
}
