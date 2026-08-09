// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.Edit
{
    public partial class ExtractRulesetDifficultiesDialog : PopupDialog
    {
        public ExtractRulesetDifficultiesDialog(string rulesetName, int difficultyCount, Action extract)
        {
            HeaderText = EditorDialogsStrings.ExtractRulesetDifficultiesHeader(rulesetName, difficultyCount);
            Icon = FontAwesome.Regular.Clone;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EditorDialogsStrings.ExtractIntoSeparateSet,
                    Action = extract,
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
