// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Edit
{
    /// <summary>
    /// Allows a ruleset editor to opt into a compact, lane-based timeline
    /// without changing the standard timeline presentation of other rulesets.
    /// </summary>
    public interface IEditorTimelineLayoutProvider
    {
        int TimelineLaneCount { get; }

        EditorTimelineLane GetTimelineLane(HitObject hitObject);

        EditorTimelineLane GetTimelineLaneByIndex(int index);

        /// <summary>
        /// Returns a stable equality key for objects which should share one
        /// collapsed timeline marker, or <see langword="null"/> to keep the
        /// object independent.
        /// </summary>
        object? GetTimelineGroupKey(HitObject hitObject);

        /// <summary>
        /// Returns the end of the span represented by an object on the timeline.
        /// This may be later than the editable object duration when its visual or
        /// state remains active afterwards.
        /// </summary>
        double GetTimelineDisplayEndTime(HitObject hitObject) => hitObject.GetEndTime();
    }

    public readonly record struct EditorTimelineLane(int Index, LocalisableString Name, Colour4 Colour, string ShortName);
}
