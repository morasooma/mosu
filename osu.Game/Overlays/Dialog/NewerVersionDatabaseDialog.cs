// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.Dialog
{
    /// <summary>
    /// A dialog offering conversion of a local database which was created by a newer version of osu!
    /// (e.g. osu!lazer tachyon) and therefore cannot be opened by this client.
    /// </summary>
    public partial class NewerVersionDatabaseDialog : PopupDialog
    {
        public NewerVersionDatabaseDialog(LocalisableString header, LocalisableString body, Action onConvert)
        {
            HeaderText = header;
            BodyText = body;

            Icon = FontAwesome.Solid.ExclamationTriangle;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = @"Convert database",
                    Action = onConvert,
                },
                new PopupDialogCancelButton
                {
                    Text = @"Not now",
                },
            };
        }
    }
}
