// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    internal partial class RulesetSubmodeButton : ClickableContainer
    {
        private readonly bool active;
        private readonly OsuSpriteText text;
        private readonly Box activeMarker;

        private OverlayColourProvider colourProvider = null!;
        private IBindable<Colour4> themeColour = null!;

        public RulesetSubmodeButton(string label, bool active, Action action)
        {
            this.active = active;

            AutoSizeAxes = Axes.X;
            Height = 22;
            Padding = new MarginPadding { Horizontal = 2 };
            Action = action;

            InternalChildren = new Drawable[]
            {
                text = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = OsuFont.GetFont(size: 14),
                },
                activeMarker = new Box
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateState(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => updateState();

        private void updateState()
        {
            Color4 activeColour = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White;
            Color4 textColour = active || IsHovered ? activeColour : colourProvider.Highlight1;

            text.Font = OsuFont.GetFont(size: 14, weight: active ? FontWeight.SemiBold : FontWeight.Regular);
            text.FadeColour(textColour, 150, Easing.OutQuint);
            activeMarker.FadeColour(activeColour, 150, Easing.OutQuint);
            activeMarker.FadeTo(active ? 1 : 0, 150, Easing.OutQuint);
        }
    }
}
