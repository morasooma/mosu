// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Input.StateChanges
{
    /// <summary>
    /// Non-generic view of a button input, used for privacy-preserving input aggregation.
    /// </summary>
    public interface IButtonInput
    {
        int PressedCount { get; }

        int ReleasedCount { get; }
    }
}
