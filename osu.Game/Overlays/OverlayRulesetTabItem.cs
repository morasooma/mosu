// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osuTK.Graphics;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Extensions;
using osu.Game.Graphics.Containers;

namespace osu.Game.Overlays
{
    public partial class OverlayRulesetTabItem : TabItem<RulesetInfo>, IHasTooltip, IHasPopover
    {
        private Color4 accentColour;
        protected virtual bool AllowSpecialRulesetPopover => Online.MosuServerEnvironment.SupportsSpecialRulesets;
        protected virtual bool GroupSpecialRulesetsWithBase => AllowSpecialRulesetPopover;

        protected virtual Color4 AccentColour
        {
            get => accentColour;
            set
            {
                accentColour = value;
                icon.FadeColour(value, 120, Easing.OutQuint);
            }
        }

        protected override Container<Drawable> Content { get; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Drawable icon;

        public LocalisableString TooltipText => !ShouldShowPopover ? Value.Name : string.Empty;

        private Sample selectSample = null!;
        private IBindable<Colour4> themeColour = null!;

        private readonly OverlayRulesetSelector overlayRulesetSelector;

        public OverlayRulesetTabItem(RulesetInfo value, OverlayRulesetSelector overlayRulesetSelector)
            : base(value)
        {
            this.overlayRulesetSelector = overlayRulesetSelector;

            AutoSizeAxes = Axes.Both;

            AddRangeInternal(new Drawable[]
            {
                Content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Child = icon = new ConstrainedIconContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(20f),
                        Icon = value.CreateInstance().CreateIcon(),
                    },
                },
                new HoverSounds(HoverSampleSet.TabSelect)
            });

            Enabled.Value = true;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            selectSample = audio.Samples.Get(@"UI/tabselect-select");

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateState(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindValueChanged(_ => updateState(), true);
        }

        public override bool PropagatePositionalInputSubTree => Enabled.Value && base.PropagatePositionalInputSubTree;

        protected override bool OnHover(HoverEvent e)
        {
            base.OnHover(e);
            updateState();
            if (ShouldShowPopover)
                this.ShowPopover();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        protected override void OnActivated() => updateState();

        protected override void OnDeactivated() => updateState();

        protected override void OnActivatedByUser() => selectSample.Play();

        private void updateState()
        {
            AccentColour = Enabled.Value ? getActiveColour() : colourProvider.Foreground1;
        }

        protected bool IsActive => IsHovered || Active.Value;

        protected bool IsCurrentRuleset => Value == null || overlayRulesetSelector.Current.Value == null || !GroupSpecialRulesetsWithBase
            ? IsActive
            : overlayRulesetSelector.Current.Value.CreateNormalRuleset().Equals(Value);

        private Color4 getActiveColour() => IsActive || IsCurrentRuleset ? (OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White) : colourProvider.Highlight1;

        public Popover? GetPopover()
        {
            return ShouldShowPopover ? new RulesetsPopover(Value, overlayRulesetSelector) : null;
        }

        /// <summary>
        /// Whether a popover showing special ruleset variants should be displayed for this tab.
        /// On the Mosu server, only osu! has a special ruleset (osu!relax).
        /// </summary>
        private bool ShouldShowPopover =>
            AllowSpecialRulesetPopover
            && Value.HasSpecialRuleset()
            && (!Online.MosuServerEnvironment.OnlyOsuRelax || Value.ShortName == RulesetInfo.OSU_MODE_SHORTNAME);
    }
}
