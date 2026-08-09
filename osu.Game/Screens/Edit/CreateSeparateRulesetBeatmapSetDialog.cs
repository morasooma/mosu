// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Offers to keep a server-exclusive ruleset difficulty out of a source
    /// beatmap set belonging to another mode.
    /// </summary>
    public partial class CreateSeparateRulesetBeatmapSetDialog : PopupDialog
    {
        public CreateSeparateRulesetBeatmapSetDialog(string rulesetName, Action createSeparate, Action createInCurrentSet)
        {
            HeaderText = EditorDialogsStrings.CreateSeparateRulesetSetHeader(rulesetName);
            Icon = FontAwesome.Regular.Clone;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EditorDialogsStrings.CreateSeparateSet,
                    Action = createSeparate,
                },
                new PopupDialogCancelButton
                {
                    Text = EditorDialogsStrings.AddToCurrentSet,
                    Action = createInCurrentSet,
                },
                new PopupDialogCancelButton
                {
                    Text = EditorDialogsStrings.KeepEditing,
                    Action = () => { },
                },
            };
        }
    }
}
