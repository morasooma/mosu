// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Utils;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapSearchGeneralFilterRow : BeatmapSearchMultipleSelectionFilterRow<SearchGeneral>
    {
        public readonly IBindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

        public BeatmapSearchGeneralFilterRow()
            : base(BeatmapsStrings.ListingSearchFiltersGeneral)
        {
        }

        protected override MultipleSelectionFilter CreateMultipleSelectionFilter() => new GeneralFilter
        {
            Ruleset = { BindTarget = Ruleset }
        };

        private partial class GeneralFilter : MultipleSelectionFilter
        {
            public readonly IBindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

            protected override MultipleSelectionFilterTabItem CreateTabItem(SearchGeneral value)
            {
                switch (value)
                {
                    case SearchGeneral.Recommended:
                        return new RecommendedDifficultyTabItem
                        {
                            Ruleset = { BindTarget = Ruleset }
                        };

                    default:
                        return new MultipleSelectionFilterTabItem(value);
                }
            }
        }

        private partial class RecommendedDifficultyTabItem : MultipleSelectionFilterTabItem
        {
            public readonly IBindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

            [Resolved]
            private DifficultyRecommender? recommender { get; set; }

            [Resolved]
            private IAPIProvider api { get; set; } = null!;

            [Resolved]
            private RulesetStore rulesets { get; set; } = null!;

            public RecommendedDifficultyTabItem()
                : base(SearchGeneral.Recommended)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (recommender != null)
                    recommender.StarRatingUpdated += updateText;

                Ruleset.BindValueChanged(_ => updateText(), true);
            }

            private void updateText()
            {
                // fallback to profile default game mode if beatmap listing mode filter is set to Any
                // TODO: find a way to update `PlayMode` when the profile default game mode has changed
                RulesetInfo? ruleset = Ruleset.Value.IsLegacyRuleset() ? Ruleset.Value : rulesets.GetRuleset(api.LocalUser.Value.PlayMode);

                if (ruleset == null) return;

                double? starRating = recommender?.GetRecommendedStarRatingFor(ruleset);

                if (starRating != null)
                    Text.Text = LocalisableString.Interpolate($"{Value.GetLocalisableDescription()} ({starRating.Value.FormatStarRating()})");
                else
                    Text.Text = Value.GetLocalisableDescription();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (recommender != null)
                    recommender.StarRatingUpdated -= updateText;
            }
        }

    }
}
