// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;
using osu.Game.Online.Rooms;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.OnlinePlay.Playlists
{
    public partial class DeletePlaylistDialog : DeletionDialog
    {
        public DeletePlaylistDialog(Room room, Action deleteAction)
        {
            HeaderText = global::osu.Game.Localisation.ClientTranslationStrings.J5012;
            BodyText = room.Name;
            DangerousAction = deleteAction;
        }
    }
}
