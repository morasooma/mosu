// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarRulesetTabButton : TabItem<RulesetInfo>
    {
        private readonly RulesetButton ruleset;

        public ToolbarRulesetTabButton(RulesetInfo value)
            : base(value)
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;
            Child = ruleset = new RulesetButton
            {
                Active = false,
            };

            var rInstance = value.CreateInstance();

            ruleset.TooltipMain = rInstance.Description;
            ruleset.TooltipSub = ToolbarStrings.PlaySomeRuleset(rInstance.Description);
            ruleset.SetIcon(rInstance.CreateIcon());
        }

        protected override void OnActivated() => ruleset.Active = true;

        protected override void OnDeactivated() => ruleset.Active = false;

        private partial class RulesetButton : ToolbarButton
        {
            protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds();

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            [Resolved(canBeNull: true)]
            private OverlayColourProvider? colourProvider { get; set; }

            private IBindable<Colour4>? accentColourBindable;
            private bool isActive;

            public RulesetButton()
            {
                ButtonContent.Padding = new MarginPadding(PADDING)
                {
                    Bottom = 5
                };
            }

            [BackgroundDependencyLoader(true)]
            private void load(OverlayColourProvider? provider)
            {
                colourProvider = provider;

                if (colourProvider != null)
                {
                    accentColourBindable = colourProvider.GetColourBindable(OverlayColour.Colour3);
                    accentColourBindable.BindValueChanged(_ => Scheduler.AddOnce(updateColour));
                }
            }

            protected override void UpdateThemeColours(OverlayColourProvider provider)
            {
                base.UpdateThemeColours(provider);
                updateColour();
            }

            public bool Active
            {
                set => Scheduler.AddOnce(() =>
                {
                    isActive = value;
                    updateColour();
                });
            }

            private void updateColour()
            {
                if (!isActive)
                {
                    IconContainer.Colour = colours?.GrayF ?? Color4.Gray;
                    return;
                }

                if (OverlayColourProvider.CurrentTheme.Value == ThemeMode.Custom && colourProvider != null)
                    IconContainer.Colour = colourProvider.Colour3;
                else
                    IconContainer.Colour = Color4Extensions.FromHex("#00FFAA");
            }

            protected override bool OnClick(ClickEvent e)
            {
                Parent!.TriggerClick();
                return base.OnClick(e);
            }
        }
    }
}
