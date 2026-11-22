// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// A text box with a caption that stays visible once the field holds a value.
    /// </summary>
    internal partial class LabelledEditorField : CompositeDrawable, IEditorFieldControl
    {
        private readonly OsuTextBox textBox;

        public string Value => textBox.Current.Value;

        /// <summary>
        /// Replaces the displayed value without raising the commit callback.
        /// </summary>
        public void SetValue(string value) => textBox.Text = value;

        public LabelledEditorField(LocalisableString label, LocalisableString? hint, string value, OsuColour colours, Action onCommit)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
            };

            content.Add(WrappedText.Paragraph(new CaseTransformableString(label, Casing.UpperCase), colours.Pink1, 11, FontWeight.Bold));

            if (!LocalisableString.IsNullOrEmpty(hint))
                content.Add(WrappedText.Paragraph(hint.Value, colours.GrayA, 10));

            content.Add(textBox = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Height = 32,
                Text = value,
                CommitOnFocusLost = true,
            });

            textBox.OnCommit += (_, _) => onCommit();

            InternalChild = content;
        }
    }
}
