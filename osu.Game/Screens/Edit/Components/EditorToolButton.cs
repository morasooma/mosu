// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Components
{
    public partial class EditorToolButton : OsuButton, IHasPopover
    {
        public BindableBool Selected { get; } = new BindableBool();

        private readonly Func<Drawable> createIcon;
        private readonly Func<Popover?> createPopover;

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

        public EditorToolButton(LocalisableString text, Func<Drawable> createIcon, Func<Popover?> createPopover)
        {
            Text = text;
            this.createIcon = createIcon;
            this.createPopover = createPopover;

            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;

            Add(icon = createIcon().With(b =>
            {
                b.Blending = BlendingParameters.Additive;
                b.Anchor = Anchor.CentreLeft;
                b.Origin = Anchor.CentreLeft;
                b.Size = new Vector2(20);
                b.X = 10;
            }));

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateColours(), true);

            Action = Selected.Toggle;
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

            Selected.BindValueChanged(_ => updateSelectionState(), true);
        }

        private void updateSelectionState()
        {
            if (!IsLoaded)
                return;

            BackgroundColour = Selected.Value ? selectedBackgroundColour : defaultBackgroundColour;
            icon.Colour = Selected.Value ? selectedIconColour : defaultIconColour;
            SpriteText.Colour = OsuColour.ForegroundTextColourFor(BackgroundColour);

            if (Selected.Value)
                this.ShowPopover();
            else
                this.HidePopover();
        }

        protected override SpriteText CreateText() => new OsuSpriteText
        {
            Depth = -1,
            Origin = Anchor.CentreLeft,
            Anchor = Anchor.CentreLeft,
            X = 40f
        };

        public Popover? GetPopover() => Enabled.Value
            ? createPopover()?.With(p =>
            {
                p.State.BindValueChanged(state =>
                {
                    if (state.NewValue == Visibility.Hidden)
                        Selected.Value = false;
                });
            })
            : null;
    }
}
