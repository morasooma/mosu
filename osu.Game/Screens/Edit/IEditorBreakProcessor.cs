// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Allows a ruleset beatmap processor to customise automatic editor breaks.
    /// </summary>
    public interface IEditorBreakProcessor
    {
        /// <summary>
        /// Whether automatic breaks must be regenerated even when hit object start/end times are unchanged.
        /// </summary>
        bool AlwaysRegenerateAutomaticBreaks { get; }

        /// <summary>
        /// Returns objects which delimit automatic breaks.
        /// Non-gameplay objects should be omitted.
        /// </summary>
        IEnumerable<HitObject> GetBreakRelevantHitObjects();

        /// <summary>
        /// Applies ruleset-specific constraints after automatic breaks have been generated.
        /// </summary>
        void PostProcessAutomaticBreaks(EditorBeatmap beatmap);
    }
}
