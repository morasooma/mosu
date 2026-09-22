// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Audio
{
    /// <summary>
    /// Looks up the sound of being hit.
    /// If the active skin does not provide one, skin fallback resolves the sample
    /// from the built-in game resources.
    /// </summary>
    /// <remarks>
    /// In the game rather than in the Dodge ruleset, because Dodge World plays it too and cannot see into
    /// the ruleset. Two copies of the lookup name would be two places to change.
    /// </remarks>
    public sealed class DodgeMissSampleInfo : ISampleInfo
    {
        public static readonly DodgeMissSampleInfo Default = new DodgeMissSampleInfo();

        public IEnumerable<string> LookupNames
        {
            get
            {
                yield return "Gameplay/dodge-miss";
            }
        }

        public int Volume => 100;

        private DodgeMissSampleInfo()
        {
        }
    }
}
