// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// One control in the property panel, whatever it looks like.
    /// </summary>
    /// <remarks>
    /// A property is described by its kind rather than by its control, so the panel is free to show a
    /// yes-or-no property as a switch and everything else as a text box while reading them all the same
    /// way.
    /// </remarks>
    internal interface IEditorFieldControl
    {
        /// <summary>What the control currently holds, in the form the field parses.</summary>
        string Value { get; }

        /// <summary>Shows a value without reporting it as a change.</summary>
        void SetValue(string value);
    }
}
