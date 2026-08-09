// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Edit
{
    /// <summary>
    /// Allows an object whose displayed end time is not simply
    /// <c>StartTime + Duration</c> to handle timeline edge dragging.
    /// </summary>
    public interface IEditorTimelineEndTimeAdjustable
    {
        void SetEditorTimelineEndTime(double endTime);
    }
}
