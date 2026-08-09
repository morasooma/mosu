// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Skinning.Components
{
    /// <summary>
    /// A skin-editable HUD counter for successful projectile grazes.
    /// </summary>
    public partial class DodgeGrazeCounter : FontAdjustableSkinComponent
    {
        [SettingSource("Show label", "Whether the GRAZE label is displayed above the count.")]
        public BindableBool ShowLabel { get; } = new BindableBool(true);

        [SettingSource("Label colour")]
        public BindableColour4 LabelColour { get; } = new BindableColour4(new Colour4(185, 130, 255, 255));

        private readonly BindableInt grazeCount = new BindableInt();
        private readonly FillFlowContainer content;
        private readonly SkinSpriteText label;
        private readonly SkinSpriteText count;

        public DodgeGrazeCounter()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = content = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Children = new Drawable[]
                {
                    label = new SkinSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Text = "GRAZE",
                    },
                    count = new SkinSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Text = "0",
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(JudgementCountController judgementCountController)
        {
            JudgementCount graze = judgementCountController.Counters.Single(counter => counter.Types.Contains(HitResult.SmallBonus));
            grazeCount.BindTo(graze.ResultCount);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ShowLabel.BindValueChanged(visible => label.FadeTo(visible.NewValue ? 1 : 0), true);
            LabelColour.BindValueChanged(colour => label.Colour = colour.NewValue, true);
            grazeCount.BindValueChanged(value =>
            {
                count.Text = value.NewValue.ToString();

                if (value.NewValue > value.OldValue)
                {
                    content.FinishTransforms();
                    content.ScaleTo(1.12f, 60).Then().ScaleTo(1, 120);
                }
            }, true);
        }

        protected override void SetFont(FontUsage font)
        {
            label.Font = font.With(size: 12, weight: FontWeight.Bold);
            count.Font = font.With(size: 24);
        }

        protected override void SetTextColour(Colour4 textColour) => count.Colour = textColour;

        protected override void Dispose(bool isDisposing)
        {
            grazeCount.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
