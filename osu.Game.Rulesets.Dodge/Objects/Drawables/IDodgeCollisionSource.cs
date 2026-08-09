// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// A projectile source whose collision pass must run after the player has
    /// consumed input for the current frame.
    /// </summary>
    internal interface IDodgeCollisionSource
    {
        double CollisionStartTime { get; }

        double CollisionEndTime { get; }

        bool CollisionProcessingComplete { get; }

        void ProcessCollisions(
            double currentTime,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            bool allowSweptCollision,
            bool isRewind);
    }
}
