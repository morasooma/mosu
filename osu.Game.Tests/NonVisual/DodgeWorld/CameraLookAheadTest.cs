// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers how far the camera leans towards where the player is heading.
    /// </summary>
    /// <remarks>
    /// The first attempt leaned towards the direction held that frame, which made walking in circles
    /// nauseating: the view orbited the player. These are the cases that separate a camera that
    /// anticipates a walk from one that swings around.
    /// </remarks>
    [TestFixture]
    public class CameraLookAheadTest
    {
        private const double frame = 16;

        private CameraLookAhead lookAhead = null!;

        [SetUp]
        public void SetUp() => lookAhead = new CameraLookAhead();

        /// <summary>
        /// Walks for <paramref name="milliseconds"/>, asking <paramref name="direction"/> for the
        /// heading at each point in time.
        /// </summary>
        private Vector2 walk(double milliseconds, Func<double, Vector2> direction)
        {
            Vector2 offset = Vector2.Zero;

            for (double time = 0; time < milliseconds; time += frame)
                offset = lookAhead.Advance(frame, direction(time));

            return offset;
        }

        private static Vector2 walkingRight(double time) => new Vector2(1, 0);

        private static Vector2 still(double time) => Vector2.Zero;

        /// <summary>
        /// A circle completed every two seconds, which is what the complaint was about.
        /// </summary>
        private static Vector2 circling(double time)
        {
            float angle = (float)(time / 2000 * Math.PI * 2);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        [Test]
        public void SustainedWalkLeansTheCameraAhead()
        {
            Vector2 offset = walk(3000, walkingRight);

            Assert.That(offset.X, Is.GreaterThan(CameraLookAhead.DISTANCE.X * 0.8f));
            Assert.That(offset.Y, Is.EqualTo(0));
        }

        /// <summary>
        /// The point of the whole class: going round in circles must not move the camera much, because
        /// a view that orbits the player is what makes people ill.
        /// </summary>
        [Test]
        public void CirclingBarelyMovesTheCamera()
        {
            Vector2 straight = walk(3000, walkingRight);

            lookAhead.Reset();

            Vector2 circles = walk(3000, circling);

            Assert.That(circles.Length, Is.LessThan(straight.Length / 5));
            Assert.That(circles.Length, Is.LessThan(30));
        }

        /// <summary>
        /// Turning back has to actually turn the camera around, or the lean would be stuck pointing
        /// at where the player used to be going.
        /// </summary>
        [Test]
        public void TurningAroundEventuallyLeansTheOtherWay()
        {
            walk(3000, walkingRight);

            Vector2 offset = walk(3000, _ => new Vector2(-1, 0));

            Assert.That(offset.X, Is.LessThan(-CameraLookAhead.DISTANCE.X * 0.7f));
        }

        [Test]
        public void StoppingRecentresTheCamera()
        {
            walk(3000, walkingRight);

            Vector2 offset = walk(4000, still);

            Assert.That(offset.Length, Is.LessThan(3));
        }

        /// <summary>
        /// A step or two in a direction is not a commitment, and should not pan the view.
        /// </summary>
        [Test]
        public void ABriefStepHardlyMovesTheCamera()
        {
            Vector2 offset = walk(150, walkingRight);

            Assert.That(offset.Length, Is.LessThan(10));
        }

        /// <summary>
        /// Vertical space is scarcer than horizontal, and vertical camera movement is the more
        /// sickening of the two.
        /// </summary>
        [Test]
        public void VerticalLeanIsSmallerThanHorizontal()
        {
            Vector2 right = walk(4000, walkingRight);

            lookAhead.Reset();

            Vector2 down = walk(4000, _ => new Vector2(0, 1));

            Assert.That(down.Y, Is.LessThan(right.X));
        }
    }
}
