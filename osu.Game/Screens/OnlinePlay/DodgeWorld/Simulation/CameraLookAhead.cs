// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation
{
    /// <summary>
    /// How far the camera leans past the player towards where they are heading.
    /// </summary>
    /// <remarks>
    /// Leaning towards the direction held right now is what makes a camera nauseating: walking in a
    /// circle rotates that direction continuously, so the view orbits the player at a speed unrelated
    /// to their own movement. Side-scrollers and top-down games avoid it the same way — the lean
    /// follows a heading averaged over about a second, not the current input.
    /// <para>
    /// The averaged heading does the work by itself. A steady walk converges on a unit vector, a
    /// circle averages to near nothing, and a stop decays to zero, so building up, cancelling out and
    /// recentring all come from one filter rather than from special cases.
    /// </para>
    /// <para>
    /// Its length is then squared into the result, which is what keeps a wandering player still: a
    /// heading half cancelled out leans a quarter as far, so only a committed direction moves the view
    /// much. Nothing here reads a clock, so the behaviour can be played out in a test.
    /// </para>
    /// </remarks>
    internal sealed class CameraLookAhead
    {
        /// <summary>
        /// How far a fully committed walk leans the camera, per axis, in world units.
        /// </summary>
        /// <remarks>
        /// Smaller vertically: a room is barely taller than the screen, so there is little vertical
        /// travel to spend, and vertical camera movement is the more sickening of the two.
        /// </remarks>
        public static readonly Vector2 DISTANCE = new Vector2(150, 90);

        /// <summary>
        /// Time constant of the heading average, in milliseconds. Long enough that a turn has to be
        /// sustained to move the camera, short enough that a corridor pans within a second or two.
        /// </summary>
        public const double AVERAGING = 1000;

        /// <summary>
        /// The averaged heading. Its length is how consistent the player's direction has been, from
        /// 0 (standing still, or going in circles) to 1 (walking a straight line).
        /// </summary>
        public Vector2 Heading { get; private set; }

        public Vector2 Offset { get; private set; }

        /// <summary>
        /// Advances the average by <paramref name="elapsedMilliseconds"/> of walking in
        /// <paramref name="direction"/>, and returns the resulting lean.
        /// </summary>
        /// <param name="elapsedMilliseconds">Time since the last update.</param>
        /// <param name="direction">A unit vector while walking, or zero while still.</param>
        public Vector2 Advance(double elapsedMilliseconds, Vector2 direction)
        {
            if (elapsedMilliseconds > 0)
                Heading += (direction - Heading) * (1 - (float)Math.Exp(-elapsedMilliseconds / AVERAGING));

            float commitment = Heading.Length;

            Offset = new Vector2(Heading.X * commitment * DISTANCE.X, Heading.Y * commitment * DISTANCE.Y);

            return Offset;
        }

        /// <summary>
        /// Drops the lean without panning, for when the view is being moved by something else.
        /// </summary>
        public void Reset()
        {
            Heading = Vector2.Zero;
            Offset = Vector2.Zero;
        }
    }
}
