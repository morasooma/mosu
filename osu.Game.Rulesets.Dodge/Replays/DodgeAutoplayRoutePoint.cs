// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;

namespace osu.Game.Rulesets.Dodge.Replays
{
    /// <summary>
    /// One sampled point of the route selected by Dodge autoplay.
    /// Fallback points mark locally impossible or unsafe sections.
    /// </summary>
    public readonly record struct DodgeAutoplayRoutePoint(double Time, Vector2 Position, bool UsedFallback);
}
