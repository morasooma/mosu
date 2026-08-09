// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Audio;

namespace osu.Game.Rulesets.Dodge.Skinning
{
    /// <summary>
    /// Looks up the Dodge-specific collision sample.
    /// If the active skin does not provide one, skin fallback resolves the sample
    /// from the built-in game resources.
    /// </summary>
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
