// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    public partial class ProfileRulesetSelector : OverlayRulesetSelector
    {
        [Resolved]
        private UserProfileOverlay? profileOverlay { get; set; }

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private FillFlowContainer<RulesetSubmodeButton> submodeButtons = null!;
        private string? selectedVariant;
        private bool suppressNavigation;

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

            User.BindValueChanged(user =>
            {
                selectedVariant = user.NewValue?.Variant;
                updateState(user.NewValue);
                updateSubmodeButtons();
            }, true);
            Current.BindValueChanged(ruleset =>
            {
                if (suppressNavigation)
                {
                    updateSubmodeButtons();
                    return;
                }

                selectedVariant = null;
                updateSubmodeButtons();
                if (User.Value != null && !ruleset.NewValue.Equals(User.Value.Ruleset))
                    profileOverlay?.ShowUser(User.Value.User, ruleset.NewValue, null);
            });
        }

        private System.Collections.Generic.List<RulesetInfo> getItemsWithSpecialRulesets()
        {
            var baseItems = Items.ToList();

            if (Online.MosuServerEnvironment.SupportsSpecialRulesets)
            {
                if (Online.MosuServerEnvironment.OnlyOsuRelax)
                {
                    var osuRuleset = Rulesets.AvailableRulesets.FirstOrDefault(r => r.ShortName == RulesetInfo.OSU_MODE_SHORTNAME);
                    if (osuRuleset != null)
                        baseItems.Add(osuRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
                }
                else
                {
                    foreach (var ruleset in Rulesets.AvailableRulesets)
                    {
                        switch (ruleset.ShortName)
                        {
                            case RulesetInfo.OSU_MODE_SHORTNAME:
                                baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
                                baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID));
                                break;

                            case RulesetInfo.TAIKO_MODE_SHORTNAME:
                                baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID));
                                break;

                            case RulesetInfo.CATCH_MODE_SHORTNAME:
                                baseItems.Add(ruleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID));
                                break;
                        }
                    }
                }
            }

            return baseItems;
        }

        private void updateState(UserProfileData? user)
        {
            suppressNavigation = true;
            try
            {
                if (Online.MosuServerEnvironment.SupportsSpecialRulesets)
                {
                    Current.Value = getItemsWithSpecialRulesets().SingleOrDefault(ruleset => user?.Ruleset.MatchesOnlineID(ruleset) == true);
                    string defaultMode = user?.User.ServerPlayMode ?? user?.User.PlayMode ?? @"osu";
                    if (defaultMode.StartsWith("mania", System.StringComparison.OrdinalIgnoreCase))
                        defaultMode = "mania";
                    else if (defaultMode.Equals(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, System.StringComparison.OrdinalIgnoreCase))
                        defaultMode = RulesetInfo.OSU_MODE_SHORTNAME;
                    SetDefaultRuleset(Rulesets.GetRuleset(defaultMode).AsNonNull());
                }
                else
                {
                    Current.Value = getDisplayedRuleset(user?.Ruleset);
                    SetDefaultRuleset(getDisplayedRuleset(Rulesets.GetRuleset(user?.User.PlayMode ?? @"osu").AsNonNull()));
                }
            }
            finally
            {
                suppressNavigation = false;
            }
        }

        private void updateSubmodeButtons()
        {
            submodeButtons.Clear();
            var current = Current.Value;
            if (current == null)
                return;

            var normal = current.IsSpecialRuleset() ? current.CreateNormalRuleset() : current;
            switch (normal.ShortName)
            {
                case RulesetInfo.OSU_MODE_SHORTNAME when Online.MosuServerEnvironment.SupportsSpecialRulesets:
                    addSubmodeButton("Standard", !current.IsSpecialRuleset(), () => selectSubmode(normal, null));
                    addSubmodeButton("Relax", current.OnlineID == RulesetInfo.OSU_RELAX_ONLINE_ID,
                        () => selectSubmode(normal.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID), null));
                    break;

                case "mania":
                    addSubmodeButton("All", selectedVariant == null, () => selectSubmode(normal, null));
                    addSubmodeButton("4K", selectedVariant == "4K", () => selectSubmode(normal, "4K"));
                    addSubmodeButton("7K", selectedVariant == "7K", () => selectSubmode(normal, "7K"));
                    break;
            }
        }

        private void addSubmodeButton(string text, bool active, System.Action action) =>
            submodeButtons.Add(new RulesetSubmodeButton(text, active, action));

        private void selectSubmode(RulesetInfo ruleset, string? variant)
        {
            selectedVariant = variant;
            suppressNavigation = true;
            try
            {
                Current.Value = ruleset;
            }
            finally
            {
                suppressNavigation = false;
            }
            updateSubmodeButtons();

            if (User.Value != null && (!ruleset.Equals(User.Value.Ruleset)
                                      || !string.Equals(variant, User.Value.Variant, System.StringComparison.OrdinalIgnoreCase)))
                profileOverlay?.ShowUser(User.Value.User, ruleset, variant);
        }

        public void SetDefaultRuleset(RulesetInfo ruleset)
        {
            foreach (var tabItem in TabContainer)
                ((ProfileRulesetTabItem)tabItem).IsDefault = ((ProfileRulesetTabItem)tabItem).Value.Equals(ruleset);
        }

        private RulesetInfo getDisplayedRuleset(IRulesetInfo? ruleset)
        {
            RulesetInfo? displayedRuleset = ruleset as RulesetInfo;

            if (displayedRuleset?.IsSpecialRuleset() == true)
                displayedRuleset = displayedRuleset.CreateNormalRuleset();

            displayedRuleset ??= Rulesets.GetRuleset(RulesetInfo.OSU_MODE_SHORTNAME).AsNonNull();

            return Items.SingleOrDefault(item => item.Equals(displayedRuleset)) ?? displayedRuleset;
        }

        protected override TabItem<RulesetInfo> CreateTabItem(RulesetInfo value) => new ProfileRulesetTabItem(value, this);
    }
}
