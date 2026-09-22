// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using System.Linq;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Overlays.BeatmapSet
{
    public partial class BeatmapRulesetSelector : OverlayRulesetSelector
    {
        public readonly Bindable<RulesetInfo> LeaderboardRuleset = new Bindable<RulesetInfo>();

        private readonly Bindable<APIBeatmapSet?> beatmapSet = new Bindable<APIBeatmapSet?>();
        private FillFlowContainer<RulesetSubmodeButton> submodeButtons = null!;

        public APIBeatmapSet? BeatmapSet
        {
            get => beatmapSet.Value;
            set
            {
                // propagate value to tab items first to enable only available rulesets.
                beatmapSet.Value = value;

                Current.Value = TabContainer.TabItems.FirstOrDefault(t => t.Enabled.Value)?.Value;
            }
        }

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
                LeaderboardRuleset.Value = ruleset.NewValue;
                updateSubmodeButtons();
            }, true);
        }

        private void updateSubmodeButtons()
        {
            submodeButtons.Clear();

            RulesetInfo current = LeaderboardRuleset.Value;
            if (current == null || !Online.MosuServerEnvironment.SupportsSpecialRulesets)
                return;

            RulesetInfo normal = current.IsSpecialRuleset() ? current.CreateNormalRuleset() : current;
            if (normal.ShortName != RulesetInfo.OSU_MODE_SHORTNAME)
                return;

            addSubmodeButton("Standard", !current.IsSpecialRuleset(), normal);
            addSubmodeButton("Relax", current.OnlineID == RulesetInfo.OSU_RELAX_ONLINE_ID,
                normal.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
        }

        private void addSubmodeButton(string text, bool active, RulesetInfo ruleset) =>
            submodeButtons.Add(new RulesetSubmodeButton(text, active, () =>
            {
                LeaderboardRuleset.Value = ruleset;
                updateSubmodeButtons();
            }));

        protected override TabItem<RulesetInfo> CreateTabItem(RulesetInfo value) => new BeatmapRulesetTabItem(value, this)
        {
            BeatmapSet = { BindTarget = beatmapSet }
        };
    }
}
