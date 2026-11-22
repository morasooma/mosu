// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;

namespace osu.Game.Graphics.Cursor
{
    public partial class OsuTooltipContainer : TooltipContainer
    {
        private readonly TooltipPositionFilter positionFilter = new TooltipPositionFilter();
        private Vector2 lastDrawSize;

        protected override ITooltip CreateTooltip() => new OsuTooltip();

        protected override Vector2 FilterTooltipCursorPosition(Vector2 position)
        {
            if (DrawSize != lastDrawSize)
            {
                positionFilter.Reset();
                lastDrawSize = DrawSize;
            }

            return positionFilter.Filter(position, Time.Current);
        }

        protected override void ResetTooltipPosition() => positionFilter.Reset();

        public OsuTooltipContainer(CursorContainer cursor)
            : base(cursor)
        {
        }

        protected override Container CreateContentContainer() => new StableLifetimeContainer
        {
            RelativeSizeAxes = Axes.Both,
        };

        protected override double AppearDelay => (1 - CurrentTooltip.Alpha) * base.AppearDelay; // reduce appear delay if the tooltip is already partly visible.

        public partial class OsuTooltip : Tooltip
        {
            private const float max_width = 500;

            private readonly Box background;
            private readonly TextFlowContainer text;
            private IBindable<ThemeMode>? themeMode;
            private IBindable<Colour4>? themeColour;

            private LocalisableString lastContent;

            public override void SetContent(LocalisableString content)
            {
                if (content.Equals(lastContent))
                    return;

                text.Text = content;

                if (IsPresent)
                {
                    AutoSizeDuration = (float)InterfaceAnimation.MovementDuration(250);
                    if (InterfaceAnimation.AllowDecorativeMovement)
                        background.FlashColour(OsuColour.Gray(0.4f), 1000, Easing.OutQuint);
                }
                else
                    AutoSizeDuration = 0;

                lastContent = content;
            }

            public OsuTooltip()
            {
                AutoSizeEasing = Easing.OutQuint;

                CornerRadius = 5;
                Masking = true;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Color4.Black.Opacity(40),
                    Radius = 5,
                };
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.9f,
                    },
                    text = new TextFlowContainer(f =>
                    {
                        f.Font = OsuFont.GetFont(weight: FontWeight.Regular);
                    })
                    {
                        Margin = new MarginPadding(5),
                        AutoSizeAxes = Axes.Both,
                        MaximumSize = new Vector2(max_width, float.PositiveInfinity),
                    }
                };
            }

            [BackgroundDependencyLoader(true)]
            private void load(OsuColour colour, OverlayColourProvider? colourProvider)
            {
                if (colourProvider != null)
                {
                    themeColour = colourProvider.GetColourBindable(OverlayColour.Background5);
                    themeColour.BindValueChanged(_ =>
                    {
                        if (OverlayColourProvider.IsLightTheme)
                        {
                            background.Colour = colourProvider.Background5;
                            text.Colour = colourProvider.Content1;
                        }
                        else
                        {
                            background.Colour = colour.Gray3;
                            text.Colour = Color4.White;
                        }
                    }, true);
                }
                else
                {
                    themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
                    themeMode.BindValueChanged(_ =>
                    {
                        if (OverlayColourProvider.IsLightTheme)
                        {
                            background.Colour = Color4Extensions.FromHex(@"f5f5f5");
                            text.Colour = Color4Extensions.FromHex(@"1a1a1a");
                        }
                        else
                        {
                            background.Colour = colour.Gray3;
                            text.Colour = Color4.White;
                        }
                    }, true);
                }
            }

            protected override void PopIn()
            {
                this.FadeIn(InterfaceAnimation.FadeDuration(300), Easing.OutQuint);
            }

            protected override void PopOut() => this.Delay(InterfaceAnimation.FadeDuration(150)).FadeOut(InterfaceAnimation.FadeDuration(300), Easing.OutQuint);

            // The container stabilises the cursor anchor for all tooltip types.
            public override void Move(Vector2 pos) => Position = pos;
        }
    }
}
