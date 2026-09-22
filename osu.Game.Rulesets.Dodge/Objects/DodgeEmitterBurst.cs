// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// A scoring object for a repeated emitter burst. The final burst is represented by
    /// the parent <see cref="DodgeEmitter"/>; preceding bursts use these nested objects.
    /// </summary>
    public class DodgeEmitterBurst : DodgeHitObject
    {
        public int BurstIndex { get; init; }
    }
}
