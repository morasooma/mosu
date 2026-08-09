// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// A container that skips direct child lifetime scans once its direct child set has stabilised.
    /// Intended for small local subtrees with static direct children.
    /// </summary>
    public partial class StableLifetimeContainer : DirectChildrenLifeOptimisingContainer
    {
    }
}