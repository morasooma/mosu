// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// One editable property of an entity, as the property editor should present it.
    /// </summary>
    /// <param name="Label">
    /// Always-visible caption. This is deliberately not a text box placeholder: a placeholder
    /// disappears as soon as the field holds a value, which left the editor showing a column of
    /// unexplained numbers.
    /// </param>
    /// <param name="Hint">Short explanation of the unit or meaning, shown under the label.</param>
    /// <param name="Read">Current value, formatted for display.</param>
    /// <param name="Write">Parses and applies a typed value, clamping it to what the field allows.</param>
    /// <param name="Toggle">
    /// Whether this is a yes-or-no property, which the panel shows as a switch. The value still passes
    /// through <see cref="Read"/> and <see cref="Write"/> as «да» or «нет», so a kind describes a switch
    /// the same way it describes anything else.
    /// </param>
    internal sealed record EntityField(
        string Label,
        string? Hint,
        Func<EditableWorldEntity, string> Read,
        Action<EditableWorldEntity, string> Write,
        bool Toggle = false);
}
