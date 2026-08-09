// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Allows a ruleset with a non-legacy beatmap format to participate in editor change tracking.
    /// </summary>
    public interface ICustomEditorBeatmapStateHandler
    {
        void WriteEditorState(EditorBeatmap beatmap, Stream stream);

        void ApplyEditorState(EditorBeatmap beatmap, byte[] previousState, byte[] newState);
    }
}
