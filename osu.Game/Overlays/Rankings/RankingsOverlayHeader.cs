// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Rankings
{
    public partial class RankingsOverlayHeader : TabControlOverlayHeader<RankingsScope>
    {
        public Bindable<RulesetInfo> Ruleset => rulesetSelector.Current;

        public Bindable<string?> Variant => rulesetSelector.Variant;

        public Bindable<CountryCode> Country => countryFilter.Current;

        private RankingsRulesetSelector rulesetSelector = null!;
        private CountryFilter countryFilter = null!;

        protected override OverlayTitle CreateTitle() => new RankingsTitle();

        protected override Drawable CreateTabControlContent() => rulesetSelector = new RankingsRulesetSelector
        {
            Scope = { BindTarget = Current }
        };

        protected override Drawable CreateContent() => countryFilter = new CountryFilter();

        protected override Drawable CreateBackground() => new OverlayHeaderBackground("Headers/rankings");

        private partial class RankingsTitle : OverlayTitle
        {
            public RankingsTitle()
            {
                Title = PageTitleStrings.MainRankingControllerDefault;
                Description = NamedOverlayComponentStrings.RankingsDescription;
                Icon = OsuIcon.Ranking;
            }
        }

        private partial class RankingsRulesetSelector : OverlayRulesetSelector
        {
            public readonly Bindable<string?> Variant = new Bindable<string?>();
            public readonly Bindable<RankingsScope> Scope = new Bindable<RankingsScope>();

            private FillFlowContainer<RulesetSubmodeButton> submodeButtons = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.Y;
                RelativeSizeAxes = Axes.X;
                Padding = new MarginPadding { Bottom = 32 };

                TabContainer.Anchor = Anchor.TopRight;
                TabContainer.Origin = Anchor.TopRight;

                AddInternal(submodeButtons = new FillFlowContainer<RulesetSubmodeButton>
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(14, 0),
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Y = 28,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Current.BindValueChanged(ruleset =>
                {
                    if (ruleset.NewValue == null || ruleset.NewValue.CreateNormalRuleset().ShortName != "mania")
                        Variant.Value = null;

                    updateSubmodeButtons();
                }, true);
                Scope.BindValueChanged(scope =>
                {
                    if (!supportsVariant(scope.NewValue))
                        Variant.Value = null;

                    updateSubmodeButtons();
                }, true);
                Variant.BindValueChanged(_ => updateSubmodeButtons(), true);
            }

            private void updateSubmodeButtons()
            {
                submodeButtons.Clear();

                RulesetInfo current = Current.Value;
                if (current == null)
                    return;

                RulesetInfo normal = current.IsSpecialRuleset() ? current.CreateNormalRuleset() : current;

                if (normal.ShortName == "mania" && supportsVariant(Scope.Value))
                {
                    addVariantButton("All", Variant.Value == null, null);
                    addVariantButton("4K", Variant.Value == "4K", "4K");
                    addVariantButton("7K", Variant.Value == "7K", "7K");
                    return;
                }

                if (!Online.MosuServerEnvironment.SupportsSpecialRulesets)
                    return;

                switch (normal.ShortName)
                {
                    case RulesetInfo.OSU_MODE_SHORTNAME:
                        addSubmodeButton("Standard", !current.IsSpecialRuleset(), normal);
                        addSubmodeButton("Relax", current.OnlineID == RulesetInfo.OSU_RELAX_ONLINE_ID,
                            normal.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));

                        if (!Online.MosuServerEnvironment.OnlyOsuRelax)
                        {
                            addSubmodeButton("Autopilot", current.OnlineID == RulesetInfo.OSU_AUTOPILOT_ONLINE_ID,
                                normal.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID));
                        }

                        break;

                    case RulesetInfo.TAIKO_MODE_SHORTNAME when !Online.MosuServerEnvironment.OnlyOsuRelax:
                        addSubmodeButton("Standard", !current.IsSpecialRuleset(), normal);
                        addSubmodeButton("Relax", current.OnlineID == RulesetInfo.TAIKO_RELAX_ONLINE_ID,
                            normal.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID));
                        break;

                    case RulesetInfo.CATCH_MODE_SHORTNAME when !Online.MosuServerEnvironment.OnlyOsuRelax:
                        addSubmodeButton("Standard", !current.IsSpecialRuleset(), normal);
                        addSubmodeButton("Relax", current.OnlineID == RulesetInfo.CATCH_RELAX_ONLINE_ID,
                            normal.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID));
                        break;
                }
            }

            private void addSubmodeButton(string text, bool active, RulesetInfo ruleset) =>
                submodeButtons.Add(new RulesetSubmodeButton(text, active, () => Current.Value = ruleset));

            private void addVariantButton(string text, bool active, string? variant) =>
                submodeButtons.Add(new RulesetSubmodeButton(text, active, () => Variant.Value = variant));

            private static bool supportsVariant(RankingsScope scope) => scope is RankingsScope.Performance or RankingsScope.TopScorePp or RankingsScope.Score;

            protected override TabItem<RulesetInfo> CreateTabItem(RulesetInfo value) => new RankingsRulesetTabItem(value, this);
        }

        private partial class RankingsRulesetTabItem : OverlayRulesetTabItem
        {
            protected override bool AllowSpecialRulesetPopover => false;

            protected override bool GroupSpecialRulesetsWithBase => Online.MosuServerEnvironment.SupportsSpecialRulesets;

            public RankingsRulesetTabItem(RulesetInfo value, OverlayRulesetSelector overlayRulesetSelector)
                : base(value, overlayRulesetSelector)
            {
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Best-score PP is a Morasooma-only extension. Standard third-party lazer
            // APIs expose performance and ranked-score leaderboards, but no equivalent
            // endpoint for this tab.
            if (Online.MosuServerEnvironment.IsThirdPartyServer && !Online.MosuServerEnvironment.UsesStableProtocol)
                TabControl.RemoveItem(RankingsScope.TopScorePp);

            Current.BindValueChanged(scope =>
            {
                rulesetSelector.FadeTo(showRulesetSelector(scope.NewValue) ? 1 : 0, 200, Easing.OutQuint);
            }, true);

            bool showRulesetSelector(RankingsScope scope)
            {
                switch (scope)
                {
                    case RankingsScope.Performance:
                    case RankingsScope.TopScorePp:
                    case RankingsScope.Score:
                    case RankingsScope.Country:
                    case RankingsScope.Playlists:
                        return true;

                    default:
                        return false;
                }
            }
        }
    }
}
