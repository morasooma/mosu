// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Edit.Design
{
    /// <summary>
    /// Allows a ruleset to provide a specialised implementation of the editor's
    /// Design screen without leaking ruleset-specific checks into the editor.
    /// </summary>
    public interface IEditorDesignScreenProvider
    {
        EditorScreen CreateDesignScreen();
    }
}
