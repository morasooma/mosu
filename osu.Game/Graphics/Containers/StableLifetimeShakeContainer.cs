// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// A <see cref="ShakeContainer"/> variant for subtrees whose direct child set stabilises after construction.
    /// This keeps the same shake behaviour while skipping direct child-life scans afterwards.
    /// </summary>
    public partial class StableLifetimeShakeContainer : ShakeContainer
    {
    }
}