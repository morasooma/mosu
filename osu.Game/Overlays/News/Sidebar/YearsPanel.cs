// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.News.Sidebar
{
    public partial class YearsPanel : CompositeDrawable
    {
        private readonly Bindable<APINewsSidebar> metadata = new Bindable<APINewsSidebar>();

        private FillFlowContainer yearsFlow;
        private Box background = null!;
        private IBindable<Colour4> themeColour = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColours, Bindable<APINewsSidebar> metadata)
        {
            this.metadata.BindTo(metadata);

            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
            Masking = true;
            CornerRadius = 6;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(5),
                    Child = yearsFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(0, 5)
                    }
                }
            };

            themeColour = overlayColours.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => background.Colour = overlayColours.Background3, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            metadata.BindValueChanged(_ => recreateDrawables(), true);
        }

        private void recreateDrawables()
        {
            yearsFlow.Clear();

            if (metadata.Value == null)
            {
                Hide();
                return;
            }

            int currentYear = metadata.Value.CurrentYear;

            foreach (int y in metadata.Value.Years)
                yearsFlow.Add(new YearButton(y, y == currentYear));

            Show();
        }

        public partial class YearButton : OsuHoverContainer
        {
            public int Year { get; }

            [Resolved(canBeNull: true)]
            private NewsOverlay overlay { get; set; }

            private readonly bool isCurrent;
            private IBindable<Colour4> themeColour = null!;

            public YearButton(int year, bool isCurrent)
            {
                Year = year;
                this.isCurrent = isCurrent;

                RelativeSizeAxes = Axes.X;
                Width = 0.25f;
                Height = 15;

                Child = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(size: 12, weight: isCurrent ? FontWeight.SemiBold : FontWeight.Medium),
                    Text = year.ToString()
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ =>
                {
                    IdleColour = isCurrent ? (OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White) : colourProvider.Light2;
                    HoverColour = isCurrent ? (OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White) : colourProvider.Light1;
                }, true);
                Action = () =>
                {
                    if (!isCurrent)
                        overlay?.ShowYear(Year);
                };
            }
        }
    }
}
