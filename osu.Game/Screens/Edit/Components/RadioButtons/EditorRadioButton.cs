// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Components.RadioButtons
{
    public partial class EditorRadioButton : OsuButton, IHasTooltip
    {
        /// <summary>
        /// Whether this <see cref="EditorRadioButton"/> is selected.
        /// Disable this bindable to disable the button.
        /// </summary>
        public readonly BindableBool Selected = new BindableBool();

        /// <summary>
        /// A function which creates a drawable icon to represent this item. If null, a sane default should be used.
        /// </summary>
        public readonly Func<Drawable?>? CreateIcon;

        public Hotkey? Hotkey { get; init; }

        private readonly Action? action;

        private OverlayColourProvider colourProvider = null!;
        private IBindable<Colour4>? themeColour;

        private Color4 defaultBackgroundColour;
        private Color4 defaultIconColour;
        private Color4 selectedBackgroundColour;
        private Color4 selectedIconColour;

        private Drawable icon = null!;

        internal Color4 SelectedBackgroundColour => selectedBackgroundColour;
        new internal Color4 DefaultBackgroundColour => defaultBackgroundColour;
        internal Color4 SelectedIconColour => selectedIconColour;
        internal Color4 DefaultIconColour => defaultIconColour;
        internal Color4 CurrentTextColour => SpriteText.Colour;
        internal Color4 CurrentIconColour => icon.Colour;

        public EditorRadioButton(LocalisableString label, Action? action, Func<Drawable?>? createIcon = null)
        {
            Text = label;
            CreateIcon = createIcon;
            this.action = action;
            Action = Select;

            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;

            Add(icon = (CreateIcon?.Invoke() ?? new Circle()).With(b =>
            {
                b.Blending = BlendingParameters.Additive;
                b.Anchor = Anchor.CentreLeft;
                b.Origin = Anchor.CentreLeft;
                b.Size = new Vector2(20);
                b.X = 10;
            }));

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateColours(), true);

            if (Hotkey != null)
            {
                SpriteText.Origin = Anchor.BottomLeft;
                SpriteText.Y = -1;

                Add(new HotkeyDisplay
                {
                    Hotkey = Hotkey.Value,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.TopLeft,
                    X = 40,
                    Y = 1,
                });
            }
        }

        private void updateColours()
        {
            defaultBackgroundColour = colourProvider.Background3;
            selectedBackgroundColour = colourProvider.Background1;

            if (OverlayColourProvider.IsLightTheme)
            {
                defaultIconColour = colourProvider.Light4;
                selectedIconColour = colourProvider.Content1;
                icon.Blending = BlendingParameters.Inherit;
            }
            else
            {
                defaultIconColour = defaultBackgroundColour.Darken(0.5f);
                selectedIconColour = selectedBackgroundColour.Lighten(0.5f);
                icon.Blending = BlendingParameters.Additive;
            }

            updateSelectionState();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Selected.BindValueChanged(selected =>
            {
                updateSelectionState();
                if (selected.NewValue)
                    action?.Invoke();
            }, true);

            Selected.BindDisabledChanged(disabled => Enabled.Value = !disabled, true);
            updateSelectionState();
        }

        /// <summary>
        /// Selects this <see cref="EditorRadioButton"/>.
        /// </summary>
        public void Select() => Selected.Value = true;

        /// <summary>
        /// Deselects this <see cref="EditorRadioButton"/>.
        /// </summary>
        public void Deselect() => Selected.Value = false;

        private void updateSelectionState()
        {
            if (!IsLoaded)
                return;

            BackgroundColour = Selected.Value ? selectedBackgroundColour : defaultBackgroundColour;
            icon.Colour = Selected.Value ? selectedIconColour : defaultIconColour;
            SpriteText.Colour = OsuColour.ForegroundTextColourFor(BackgroundColour);
        }

        protected override SpriteText CreateText() => new OsuSpriteText
        {
            Depth = -1,
            Origin = Anchor.CentreLeft,
            Anchor = Anchor.CentreLeft,
            X = 40f
        };

        public LocalisableString TooltipText { get; set; }
    }
}
