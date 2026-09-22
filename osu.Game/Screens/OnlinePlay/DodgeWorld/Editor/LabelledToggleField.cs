// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// A yes-or-no property, as a switch.
    /// </summary>
    /// <remarks>
    /// These were text boxes holding the word «да» or «нет», which is a question with two answers dressed
    /// up as free text: nothing told the author which words were understood, and the value only took effect
    /// once the box was committed. A switch answers all of that by being a switch. It applies immediately —
    /// there is nothing to finish typing.
    /// </remarks>
    internal partial class LabelledToggleField : CompositeDrawable, IEditorFieldControl
    {
        private readonly OsuCheckbox checkbox;

        public string Value => EditorValue.Format(checkbox.Current.Value);

        public LabelledToggleField(string label, string? hint, string value, OsuColour colours, Action onChanged)
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

            if (!string.IsNullOrEmpty(hint))
                content.Add(WrappedText.Paragraph(hint, colours.GrayA, 10));

            content.Add(checkbox = new OsuCheckbox
            {
                RelativeSizeAxes = Axes.X,
                LabelText = label.ToUpperInvariant(),
                Current = { Value = EditorValue.Bool(value, false) },
            });

            // Applied as soon as it is switched, and only when a person switched it: rebuilding the panel
            // sets this up again with the stored value, which must not read as another edit.
            checkbox.Current.BindValueChanged(_ =>
            {
                if (!applying)
                    onChanged();
            });

            InternalChild = content;
        }

        private bool applying;

        /// <summary>
        /// Flips the switch the way a click on it does, so a test can check what follows from that without
        /// aiming a cursor at it.
        /// </summary>
        internal void FlipForTesting() => checkbox.Current.Value = !checkbox.Current.Value;

        /// <summary>
        /// Shows a value without reporting it as a change.
        /// </summary>
        public void SetValue(string value)
        {
            applying = true;
            checkbox.Current.Value = EditorValue.Bool(value, checkbox.Current.Value);
            applying = false;
        }
    }
}
