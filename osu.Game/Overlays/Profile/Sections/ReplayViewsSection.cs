using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Profile.Sections.Historical;
using osu.Game.Overlays.Profile.Sections.Ranks;

namespace osu.Game.Overlays.Profile.Sections
{
    public partial class ReplayViewsSection : ProfileSection
    {
        public override LocalisableString Title => ReplayViewsStrings.Title;
        public override string Identifier => "replay_views";

        public ReplayViewsSection()
        {
            Children = new Drawable[]
            {
                new ReplaysSubsection(User),
                new PaginatedScoreContainer(ScoreType.MostViewed, User, ReplayViewsStrings.MostViewed)
            };
        }
    }
}
