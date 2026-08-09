// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.Select
{
    public sealed partial class BeatmapUploadHelpDialog : PopupDialog
    {
        public BeatmapUploadHelpDialog()
        {
            HeaderText = LeaderboardStrings.BeatmapUploadHelpHeader;
            BodyText = LeaderboardStrings.BeatmapUploadHelpBody;
            Icon = FontAwesome.Solid.QuestionCircle;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton(),
            };
        }
    }
}
