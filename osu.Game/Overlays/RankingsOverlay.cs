// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Extensions;
using osu.Game.Overlays.Rankings;
using osu.Game.Users;
using osu.Game.Rulesets;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Rankings.Tables;

namespace osu.Game.Overlays
{
    public partial class RankingsOverlay : TabbableOnlineOverlay<RankingsOverlayHeader, RankingsScope>
    {
        protected Bindable<CountryCode> Country => Header.Country;

        private APIRequest lastRequest;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> parentRuleset { get; set; }

        [Cached]
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();

        private readonly Bindable<string> variant = new Bindable<string>();

        [Resolved]
        private IBindable<System.Collections.Generic.IReadOnlyList<osu.Game.Rulesets.Mods.Mod>> selectedMods { get; set; } = null!;

        public RankingsOverlay()
            : base(OverlayColourScheme.Green)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Header.Ruleset.BindTo(ruleset);
            Header.Variant.BindTo(variant);

            Country.BindValueChanged(_ =>
            {
                Scheduler.AddOnce(triggerTabChanged);
            });

            ruleset.BindValueChanged(_ =>
            {
                if (Header.Current.Value == RankingsScope.Playlists)
                    return;

                Scheduler.AddOnce(triggerTabChanged);
            });

            selectedMods.BindValueChanged(_ =>
            {
                if (Header.Current.Value == RankingsScope.Playlists)
                    return;

                // Ruleset is temporarily null while gameplay/replay loaders lease and
                // rebind the global selection. Mod changes may fire during that window.
                // Keep the last valid overlay state instead of trying to derive a
                // special RX/AP ruleset from a missing base ruleset.
                if (Online.MosuServerEnvironment.SupportsSpecialRulesets && ruleset.Value != null)
                    ruleset.Value = getRulesetForSelectedMods(ruleset.Value);

                Scheduler.AddOnce(triggerTabChanged);
            });

            variant.BindValueChanged(_ =>
            {
                if (supportsVariant(Header.Current.Value))
                    Scheduler.AddOnce(triggerTabChanged);
            });
        }

        private bool requiresRulesetUpdate = true;

        protected override void PopIn()
        {
            if (requiresRulesetUpdate)
            {
                RulesetInfo current = parentRuleset.Value;

                if (current != null)
                {
                    ruleset.Value = Online.MosuServerEnvironment.SupportsSpecialRulesets
                        ? getRulesetForSelectedMods(current)
                        : current;
                    requiresRulesetUpdate = false;
                }
            }

            base.PopIn();
        }

        protected override void OnTabChanged(RankingsScope tab)
        {
            if (!supportsCountry(Header.Current.Value))
                Country.SetDefault();

            Scheduler.AddOnce(triggerTabChanged);
        }

        private void triggerTabChanged() => base.OnTabChanged(Header.Current.Value);

        protected override RankingsOverlayHeader CreateHeader() => new RankingsOverlayHeader();

        public void ShowCountry(CountryCode requested)
        {
            if (requested == default)
                return;

            Show();

            Country.Value = requested;
        }

        protected override void CreateDisplayToLoad(RankingsScope tab)
        {
            lastRequest?.Cancel();

            if (Header.Current.Value == RankingsScope.Playlists)
            {
                LoadDisplay(new SpotlightsLayout
                {
                    Ruleset = { BindTarget = ruleset }
                });
                return;
            }

            var request = createScopedRequest();
            lastRequest = request;

            if (request == null)
            {
                LoadDisplay(Empty());
                return;
            }

            request.Success += () => Schedule(() => LoadDisplay(createTableFromResponse(request)));
            request.Failure += _ => Schedule(() => LoadDisplay(Empty()));

            api.Queue(request);
        }

        private APIRequest createScopedRequest()
        {
            var currentRuleset = ruleset.Value;

            if (currentRuleset == null)
                return null;

            // Morasooma exposes weekly movement and best-score PP through private API
            // routes. Other lazer servers expose the standard v2 rankings API instead.
            // Trying the private route on those servers results in an empty rankings
            // screen (usually after a 404), even though their v2 rankings are available.
            bool useStandardLazerRankings = Online.MosuServerEnvironment.IsThirdPartyServer
                                             && !Online.MosuServerEnvironment.UsesStableProtocol;

            return CreateScopedRequest(Header.Current.Value, currentRuleset, Country.Value, variant.Value, useStandardLazerRankings);
        }

        internal static APIRequest CreateScopedRequest(RankingsScope scope, RulesetInfo currentRuleset, CountryCode country, string variant,
                                                       bool useStandardLazerRankings)
        {
            switch (scope)
            {
                case RankingsScope.Performance:
                    return useStandardLazerRankings
                        ? new GetUserRankingsRequest(currentRuleset, UserRankingsType.Performance, countryCode: country, variant: variant)
                        : new GetWeeklyRankingsRequest(currentRuleset, WeeklyRankingsType.Performance, countryCode: country, variant: variant);

                case RankingsScope.TopScorePp:
                    // This is a Morasooma extension and is not part of the standard lazer API.
                    if (useStandardLazerRankings)
                        return null;

                    return new GetWeeklyRankingsRequest(currentRuleset, WeeklyRankingsType.BestScorePp, countryCode: country, variant: variant);

                case RankingsScope.Country:
                    return new GetCountryRankingsRequest(currentRuleset);

                case RankingsScope.Score:
                    return useStandardLazerRankings
                        ? new GetUserRankingsRequest(currentRuleset, UserRankingsType.Score, countryCode: country, variant: variant)
                        : new GetWeeklyRankingsRequest(currentRuleset, WeeklyRankingsType.Score, countryCode: country, variant: variant);

                case RankingsScope.Kudosu:
                    return new GetKudosuRankingsRequest();
            }

            return null;
        }

        private RulesetInfo getRulesetForSelectedMods(RulesetInfo currentRuleset)
        {
            RulesetInfo normal = currentRuleset.IsSpecialRuleset() ? currentRuleset.CreateNormalRuleset() : currentRuleset;
            bool isRelax = false;
            bool isAutopilot = false;

            if (selectedMods?.Value != null)
            {
                foreach (var mod in selectedMods.Value)
                {
                    isRelax |= mod is osu.Game.Rulesets.Mods.ModRelax;
                    isAutopilot |= mod.Acronym == "AP";
                }
            }

            if (isRelax)
            {
                if (normal.ShortName == RulesetInfo.OSU_MODE_SHORTNAME)
                    return normal.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);

                if (!Online.MosuServerEnvironment.OnlyOsuRelax)
                {
                    if (normal.ShortName == RulesetInfo.TAIKO_MODE_SHORTNAME)
                        return normal.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID);

                    if (normal.ShortName == RulesetInfo.CATCH_MODE_SHORTNAME)
                        return normal.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID);
                }
            }

            if (isAutopilot && !Online.MosuServerEnvironment.OnlyOsuRelax && normal.ShortName == RulesetInfo.OSU_MODE_SHORTNAME)
                return normal.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID);

            return normal;
        }

        private static bool supportsVariant(RankingsScope scope) => scope is RankingsScope.Performance or RankingsScope.TopScorePp or RankingsScope.Score;

        private static bool supportsCountry(RankingsScope scope) => scope is RankingsScope.Performance or RankingsScope.TopScorePp or RankingsScope.Score;

        private Drawable createTableFromResponse(APIRequest request)
        {
            switch (request)
            {
                case GetWeeklyRankingsRequest weeklyRequest:
                    if (weeklyRequest.Response == null)
                        return null;

                    switch (weeklyRequest.Type)
                    {
                        case WeeklyRankingsType.Performance:
                            return new PerformanceTable(1, weeklyRequest.Response.Users);

                        case WeeklyRankingsType.Score:
                            return new ScoresTable(1, weeklyRequest.Response.Users);

                        case WeeklyRankingsType.BestScorePp:
                            return new BestScorePpTable(1, weeklyRequest.Response.Users);
                    }

                    return null;

                case GetUserRankingsRequest userRequest:
                    if (userRequest.Response == null)
                        return null;

                    switch (userRequest.Type)
                    {
                        case UserRankingsType.Performance:
                            return new PerformanceTable(1, userRequest.Response.Users);

                        case UserRankingsType.Score:
                            return new ScoresTable(1, userRequest.Response.Users);
                    }

                    return null;

                case GetBestScorePpRankingsRequest bestScorePpRequest:
                    if (bestScorePpRequest.Response == null)
                        return null;

                    return new BestScorePpTable(1, bestScorePpRequest.Response.Users);

                case GetCountryRankingsRequest countryRequest:
                {
                    if (countryRequest.Response == null)
                        return null;

                    return new CountriesTable(1, countryRequest.Response.Countries);
                }

                case GetKudosuRankingsRequest kudosuRequest:
                    if (kudosuRequest.Response == null)
                        return null;

                    return new KudosuTable(1, kudosuRequest.Response.Users);
            }

            return null;
        }

        protected override void Dispose(bool isDisposing)
        {
            lastRequest?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
