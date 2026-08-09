// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Graphics.UserInterface;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osuTK.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Overlays.BeatmapSet
{
    public partial class LeaderboardScopeSelector : GradientLineTabControl<BeatmapLeaderboardScope>
    {
        private IBindable<Colour4> themeColour = null!;
        protected override bool AddEnumEntriesAutomatically => false;

        protected override TabItem<BeatmapLeaderboardScope> CreateTabItem(BeatmapLeaderboardScope value) => new ScopeSelectorTabItem(value);

        public LeaderboardScopeSelector()
        {
            AddItem(BeatmapLeaderboardScope.Global);
            AddItem(BeatmapLeaderboardScope.Country);
            AddItem(BeatmapLeaderboardScope.Friend);
            AddItem(BeatmapLeaderboardScope.Team);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                AccentColour = colourProvider.Highlight1;
                LineColour = colourProvider.Background1;
            }, true);
        }

        private partial class ScopeSelectorTabItem : PageTabItem
        {
            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private IBindable<Colour4> themeColour = null!;

            public ScopeSelectorTabItem(BeatmapLeaderboardScope value)
                : base(value)
            {
            }

            protected override LocalisableString CreateText()
            {
                switch (Value)
                {
                    case BeatmapLeaderboardScope.Global:
                        return BeatmapsetsStrings.ShowScoreboardGlobal;

                    case BeatmapLeaderboardScope.Country:
                        return BeatmapsetsStrings.ShowScoreboardCountry;

                    case BeatmapLeaderboardScope.Friend:
                        return BeatmapsetsStrings.ShowScoreboardFriend;

                    case BeatmapLeaderboardScope.Team:
                        return BeatmapsetsStrings.ShowScoreboardTeam;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                Text.FadeColour(AccentColour);

                return base.OnHover(e);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => Text.Colour = IsHovered ? AccentColour : (OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White), true);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);

                Text.FadeColour(OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White);
            }
        }
    }
}
