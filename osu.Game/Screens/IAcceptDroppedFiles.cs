// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens
{
    /// <summary>
    /// A screen that takes files dropped onto the window itself, instead of letting the game import them.
    /// </summary>
    /// <remarks>
    /// Added for the Dodge World editor, which imports images. Nothing in the game's own importers
    /// recognises a PNG, so before this a picture dropped onto the window was read, found to be nothing,
    /// and silently discarded — the drop appeared to do nothing at all.
    /// </remarks>
    public interface IAcceptDroppedFiles
    {
        /// <summary>
        /// Whether this screen wants <paramref name="path"/>.
        /// </summary>
        /// <remarks>
        /// Asked off the update thread, from the window's own event handling, so this must answer from the
        /// path alone and touch nothing else. Answering true means the game will not import the file.
        /// </remarks>
        bool ClaimsDroppedFile(string path);

        /// <summary>
        /// Handles a file this screen claimed. Called on the update thread.
        /// </summary>
        void HandleDroppedFile(string path);
    }
}
