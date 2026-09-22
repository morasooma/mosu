// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Performance.Debug
{
    public readonly record struct PerformanceFreezeEvent(long Sequence, DateTimeOffset Time, PerformanceDebugSnapshot Snapshot, PerformanceFreezeCause Cause);
}