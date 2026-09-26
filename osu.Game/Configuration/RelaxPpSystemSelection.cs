// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Configuration
{
    /// <summary>
    /// Exposes the currently-selected Relax PP system to code without DI access
    /// (difficulty/performance calculators), mirroring the <see cref="Database.ForkDataStore.Instance"/>
    /// static-singleton pattern. Updated from game startup/config change.
    /// </summary>
    public static class RelaxPpSystemSelection
    {
        public static ForkRelaxPpSystem Current { get; set; } = ForkRelaxPpSystem.MosuRealistik;

        /// <summary>
        /// Whether relax PP goes through the managed Mosu/Realistik calculator (MosuRealistik and MosuPp).
        /// </summary>
        public static bool UsesManagedRealistik => Current is ForkRelaxPpSystem.MosuRealistik or ForkRelaxPpSystem.MosuPp;
    }
}
