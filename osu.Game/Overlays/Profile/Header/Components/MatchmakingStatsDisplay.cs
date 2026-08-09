// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header.Components
{
    public partial class MatchmakingStatsDisplay : CompositeDrawable, IHasCustomTooltip<MatchmakingStatsTooltipData>
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuSpriteText rankText = null!;
        private OsuSpriteText label = null!;
        private Box contentBackground = null!;
        private Box rankBackground = null!;
        private IBindable<Colour4> themeColour = null!;

        public MatchmakingStatsDisplay()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    AutoSizeAxes = Axes.X,
                    Height = MainDetails.BADGE_HEIGHT,
                    CornerRadius = 6,
                    BorderThickness = 2,
                    BorderColour = colourProvider.Background4,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        contentBackground = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Padding = new MarginPadding(3f),
                            AutoSizeAxes = Axes.X,
                            RelativeSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                label = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = UsersStrings.ShowMatchmakingTitle,
                                    Margin = new MarginPadding { Horizontal = 5f, },
                                    Font = OsuFont.GetFont(size: 12)
                                },
                                new Container
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    CornerRadius = 3,
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        rankBackground = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colourProvider.Background6,
                                        },
                                        rankText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            UseFullGlyphHeight = false,
                                            Colour = colourProvider.Content2,
                                            Margin = new MarginPadding { Horizontal = 10f, Vertical = 5f }
                                        },
                                    }
                                },
                            }
                        },
                    }
                },
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                contentBackground.Colour = colourProvider.Background4;
                rankBackground.Colour = colourProvider.Background6;
                label.Colour = colourProvider.Content1;
                rankText.Colour = colourProvider.Content2;
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(_ => updateDisplay(), true);
        }

        private void updateDisplay()
        {
            if (User.Value == null)
            {
                Hide();
                return;
            }

            APIUserMatchmakingStatistics[] stats = User.Value.User.MatchmakingStatistics;

            if (stats.Length == 0)
            {
                Hide();
                return;
            }

            int? highestRank = null;

            foreach (var stat in stats)
            {
                if (stat.Pool.Active && stat.Rank != null)
                {
                    if (highestRank == null || stat.Rank < highestRank)
                        highestRank = stat.Rank;
                }
            }

            rankText.Text = highestRank == null ? "-" : $"#{highestRank:N0}";

            TooltipContent = new MatchmakingStatsTooltipData(colourProvider, stats.OrderByDescending(s => s.PoolId).ToArray());

            Show();
        }

        public ITooltip<MatchmakingStatsTooltipData> GetCustomTooltip() => new MatchmakingStatsTooltip();

        public MatchmakingStatsTooltipData? TooltipContent { get; private set; }
    }
}
