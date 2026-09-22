// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation
{
    /// <summary>
    /// Every tunable that governs Dodge World movement and combat.
    /// </summary>
    /// <remarks>
    /// Values live here rather than on the drawables so that the simulation can be exercised without
    /// a game loop, and so that a number used by both the rules and the animation cannot drift apart.
    /// </remarks>
    public static class CombatRules
    {
        // -- player movement

        /// <summary>Walking speed, in world units per second.</summary>
        public const float PLAYER_SPEED = 245;

        /// <summary>Half-extent of the player's square collision box.</summary>
        /// <remarks>
        /// The drawn body is 32 units across, so this is exactly half of it. The hitbox and the sprite are
        /// the same square on purpose: a player judges a dodge by what they can see, and a box that does
        /// not match the drawing makes every near miss feel arbitrary.
        /// </remarks>
        public const float PLAYER_HALF_SIZE = 16;

        /// <summary>How close the player must be to an NPC for the interact prompt to appear.</summary>
        public const float INTERACTION_DISTANCE = 125;

        /// <summary>Padding kept between the player and the room edge.</summary>
        public const float ROOM_EDGE_PADDING = 42;

        /// <summary>Editor placement grid, in world units.</summary>
        public const float GRID_SIZE = 16;

        // -- body reference points
        //
        // Entities are anchored at their feet (Origin.BottomCentre), but hits should register against
        // the body, so these offsets lift a position from the feet to the torso. Each one is the centre
        // of the body that kind actually draws, so that nothing is ever hurt by something it can see
        // passing beside it.

        /// <summary>
        /// Feet-to-torso offset for the player: the centre of the 32-unit body <c>WorldPlayer</c> draws
        /// ten units above the feet.
        /// </summary>
        public static readonly Vector2 PLAYER_TORSO_OFFSET = new Vector2(0, -26);

        /// <summary>Feet-to-torso offset for a mob, being the centre of its 38-unit body.</summary>
        public static readonly Vector2 MOB_TORSO_OFFSET = new Vector2(0, -27);

        /// <summary>Where the sword is held, relative to the player's feet.</summary>
        public static readonly Vector2 WEAPON_ORIGIN_OFFSET = new Vector2(0, -27);

        // -- player attack

        /// <summary>Minimum time between player swings, in milliseconds.</summary>
        public const double ATTACK_COOLDOWN = 360;

        /// <summary>Duration of the swing animation, in milliseconds.</summary>
        public const double ATTACK_SWING_DURATION = 210;

        /// <summary>Total angular width of the damaging arc, in degrees.</summary>
        public const float ATTACK_ARC_DEGREES = 135;

        /// <summary>Reach of the damaging arc, in world units.</summary>
        public const float ATTACK_RADIUS = 96;

        /// <summary>Radius the held weapon orbits around the swing origin.</summary>
        public const float WEAPON_HAND_ORBIT_RADIUS = 14;

        // What a swing takes off a mob is not here: it belongs to the sword being swung, so it lives on
        // the weapon skin in the world document and reaches the simulation through PlayerState.

        // -- mob attacks

        /// <summary>
        /// Distance between the two bodies' centres at which a mob deals contact damage.
        /// </summary>
        /// <remarks>
        /// Both sides are measured at the torso. It used to compare the mob's torso against the player's
        /// feet, which made a mob standing level with the player harmless and one standing below them
        /// dangerous from further away than it looked.
        /// </remarks>
        public const float MOB_CONTACT_DISTANCE = 38;

        /// <summary>Minimum time between two instances of contact damage, in milliseconds.</summary>
        public const double CONTACT_DAMAGE_COOLDOWN = 700;

        /// <summary>How long a mob telegraphs its volley before firing, in milliseconds.</summary>
        public const double MOB_ATTACK_TELEGRAPH_DURATION = 650;

        /// <summary>Delay before a defeated mob returns, in milliseconds.</summary>
        public const double MOB_RESPAWN_DELAY = 4000;

        /// <summary>
        /// The shortest cycle a hazard may run on. A hazard firing every frame is not an obstacle, it is
        /// a wall, and an author asking for one has made a mistake.
        /// </summary>
        public const int HAZARD_MIN_CYCLE = 250;

        /// <summary>
        /// How long a beam shows itself before it becomes lethal. A beam that appears already deadly is
        /// not an obstacle to learn, it is a death to memorise.
        /// </summary>
        public const double BEAM_WARNING_DURATION = 600;

        /// <summary>
        /// How high above the point a hazard device is placed at its projectiles leave it and its beam
        /// starts.
        /// </summary>
        /// <remarks>
        /// A device is anchored at its feet like everything else, but the housing the player sees is the
        /// middle of a 96-unit box, so shots fired from the anchor came out from under the device rather
        /// than from it. Read from the same constant on both sides, so a course fires from the same place
        /// whether this client or the server is running it.
        /// </remarks>
        public const float HAZARD_MUZZLE_HEIGHT = 48;

        /// <summary>Radius of a projectile, matching the circle <c>WorldProjectile</c> draws.</summary>
        public const float PROJECTILE_RADIUS = 6;

        /// <summary>
        /// Where a hazard device placed at <paramref name="position"/> fires from.
        /// </summary>
        public static Vector2 HazardMuzzle(Vector2 position, float scale) =>
            position + new Vector2(0, -HAZARD_MUZZLE_HEIGHT * (float.IsFinite(scale) ? scale : 1));

        /// <summary>
        /// Whether a circle of <paramref name="radius"/> at <paramref name="point"/> touches the player's
        /// body, whose centre is at <paramref name="torso"/>.
        /// </summary>
        /// <remarks>
        /// A circle against the square that is drawn, rather than a distance between two points: measuring
        /// centre to centre made a shot passing a corner count as a hit and one arriving flat at a side
        /// count as a miss, neither of which is what the player was looking at.
        /// </remarks>
        public static bool TouchesPlayerBody(Vector2 point, float radius, Vector2 torso)
        {
            float horizontal = Math.Max(0, Math.Abs(point.X - torso.X) - PLAYER_HALF_SIZE);
            float vertical = Math.Max(0, Math.Abs(point.Y - torso.Y) - PLAYER_HALF_SIZE);

            return horizontal * horizontal + vertical * vertical <= radius * radius;
        }

        /// <summary>
        /// Whether the player's square body, centred on <paramref name="torso"/>, overlaps a box of
        /// <paramref name="size"/> centred on <paramref name="centre"/> and turned
        /// <paramref name="rotationDegrees"/> clockwise.
        /// </summary>
        /// <remarks>
        /// Two rectangles miss each other exactly when one of the four sides' directions separates them, so
        /// this is exact: a turned wall blocks precisely where it is drawn.
        /// </remarks>
        public static bool TouchesPlayerBox(Vector2 torso, Vector2 centre, Vector2 size, float rotationDegrees)
        {
            Vector2 half = new Vector2(Math.Abs(size.X) / 2, Math.Abs(size.Y) / 2);
            Vector2 delta = centre - torso;

            if (rotationDegrees % 360 == 0)
            {
                return Math.Abs(delta.X) < PLAYER_HALF_SIZE + half.X
                       && Math.Abs(delta.Y) < PLAYER_HALF_SIZE + half.Y;
            }

            float radians = rotationDegrees * MathF.PI / 180;
            float cos = MathF.Abs(MathF.Cos(radians));
            float sin = MathF.Abs(MathF.Sin(radians));

            // The player's own axes: the box's reach across each of them.
            if (Math.Abs(delta.X) >= PLAYER_HALF_SIZE + half.X * cos + half.Y * sin)
                return false;

            if (Math.Abs(delta.Y) >= PLAYER_HALF_SIZE + half.X * sin + half.Y * cos)
                return false;

            // The box's own axes: the square's reach across each of them, which is the same for both.
            float squareReach = PLAYER_HALF_SIZE * (cos + sin);
            Vector2 axis = new Vector2(MathF.Cos(radians), MathF.Sin(radians));

            if (Math.Abs(delta.X * axis.X + delta.Y * axis.Y) >= squareReach + half.X)
                return false;

            return Math.Abs(delta.Y * axis.X - delta.X * axis.Y) < squareReach + half.Y;
        }

        /// <summary>
        /// How far from a beam's centre line the player's body still reaches, for a beam pointing along
        /// <paramref name="angleRadians"/>.
        /// </summary>
        /// <remarks>
        /// The body is a square and the beam is a band at any angle, so the square's reach across the band
        /// depends on how it is turned: 16 units when the beam is level with it, and 22.6 when it arrives
        /// diagonally. Exact, and it means a beam hurts precisely when it is drawn over the player.
        /// </remarks>
        public static float BeamReach(float beamWidth, float angleRadians) =>
            beamWidth / 2 + PLAYER_HALF_SIZE * (MathF.Abs(MathF.Sin(angleRadians)) + MathF.Abs(MathF.Cos(angleRadians)));

        // -- presentation

        /// <summary>Camera slack around the player before the view starts following.</summary>
        public static readonly Vector2 CAMERA_DEADZONE = new Vector2(175, 105);

        // How far the camera leans towards where the player is heading lives on CameraLookAhead,
        // which owns that behaviour.

        /// <summary>Camera follow time constant while walking, in milliseconds.</summary>
        public const double CAMERA_SMOOTHING = 135;

        /// <summary>Camera follow time constant while a dialogue is open, in milliseconds.</summary>
        public const double CAMERA_DIALOGUE_SMOOTHING = 210;

        /// <summary>Grace period after a room transition before passages can trigger again.</summary>
        public const double PASSAGE_COOLDOWN = 1000;

        /// <summary>
        /// <paramref name="value"/> turned <paramref name="degrees"/> clockwise about the origin, which is
        /// the direction everything in the world is turned in.
        /// </summary>
        public static Vector2 Rotate(Vector2 value, float degrees)
        {
            if (degrees % 360 == 0)
                return value;

            float radians = degrees * MathF.PI / 180;
            float sin = MathF.Sin(radians);
            float cos = MathF.Cos(radians);

            return new Vector2(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
        }

        /// <summary>Snaps a world position to the editor placement grid.</summary>
        public static Vector2 SnapToGrid(Vector2 position) => new Vector2(
            MathF.Round(position.X / GRID_SIZE) * GRID_SIZE,
            MathF.Round(position.Y / GRID_SIZE) * GRID_SIZE);
    }
}
