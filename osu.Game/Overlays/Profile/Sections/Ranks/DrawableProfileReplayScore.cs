using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Profile.Sections.Ranks
{
    public partial class DrawableProfileReplayScore : DrawableProfileScore
    {
        public DrawableProfileReplayScore(SoloScoreInfo score)
            : base(score)
        {
        }

        protected override Drawable CreateDrawablePerformance() => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = (Score.ReplayViews ?? 0).ToLocalisableString("N0"),
                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                },
                new OsuSpriteText
                {
                    Text = ReplayViewsStrings.Views,
                    Font = OsuFont.GetFont(size: 10),
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                }
            }
        };
    }
}
