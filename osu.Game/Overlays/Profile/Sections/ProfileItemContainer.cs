// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Sections
{
    public partial class ProfileItemContainer : Container
    {
        private const int hover_duration = 200;

        protected override Container<Drawable> Content => content;

        private readonly Box background;
        private readonly Container content;

        private Color4 idleColour;
        private IBindable<Colour4> themeColour = null!;

        protected Color4 IdleColour
        {
            get => idleColour;
            set
            {
                idleColour = value;
                fadeBackgroundColour();
            }
        }

        private Color4 hoverColour;

        protected Color4 HoverColour
        {
            get => hoverColour;
            set
            {
                hoverColour = value;
                fadeBackgroundColour();
            }
        }

        public ProfileItemContainer()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 6;

            AddRangeInternal(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Background2);
            themeColour.BindValueChanged(_ =>
            {
                IdleColour = colourProvider.Background2;
                HoverColour = colourProvider.Background1;
            }, true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            fadeBackgroundColour(hover_duration);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            fadeBackgroundColour(hover_duration);
        }

        private void fadeBackgroundColour(double fadeDuration = 0)
        {
            background.FadeColour(IsHovered ? HoverColour : IdleColour, fadeDuration, Easing.OutQuint);
        }
    }
}
