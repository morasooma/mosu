// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Overlays.Mods
{
    /// <summary>
    /// On the mod select overlay, this provides a local updating view of the aggregate score multiplier coming from mods.
    /// </summary>
    public partial class RankingInformationDisplay : ModFooterInformationDisplay
    {
        public const float HEIGHT = 42;

        public Bindable<double> ModMultiplier = new BindableDouble(1);

        public Bindable<bool> Ranked { get; } = new BindableBool(true);

        private readonly BindableWithCurrent<double> current = new BindableWithCurrent<double>();

        private const float transition_duration = 200;

        private RollingCounter<double> counter = null!;

        private Box flashLayer = null!;
        private TextWithTooltip rankedText = null!;
        private IBindable<Colour4> themeColour = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // You would think that we could add this to `Content`, but borders don't mix well
            // with additive blending children elements.
            AddInternal(new Container
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                RelativeSizeAxes = Axes.Both,
                Shear = OsuGame.SHEAR,
                CornerRadius = ShearedButton.CORNER_RADIUS,
                Masking = true,
                Children = new Drawable[]
                {
                    flashLayer = new Box
                    {
                        Alpha = 0,
                        Blending = BlendingParameters.Additive,
                        RelativeSizeAxes = Axes.Both,
                    }
                }
            });

            LeftContent.AddRange(new Drawable[]
            {
                new Container
                {
                    Width = 50,
                    RelativeSizeAxes = Axes.Y,
                    Margin = new MarginPadding(10),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = rankedText = new TextWithTooltip
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Shear = -OsuGame.SHEAR,
                        Font = OsuFont.Default.With(size: 17, weight: FontWeight.SemiBold)
                    }
                }
            });

            RightContent.Add(new Container
            {
                Width = 40,
                RelativeSizeAxes = Axes.Y,
                Margin = new MarginPadding(10),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = counter = new EffectCounter
                {
                    Shear = -OsuGame.SHEAR,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Current = { BindTarget = ModMultiplier }
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ModMultiplier.BindValueChanged(e =>
            {
                updateCounterColour();

                flash();

                const float move_amount = 4;
                if (e.NewValue > e.OldValue)
                    counter.MoveToY(Math.Max(-move_amount * 2, counter.Y - move_amount)).Then().MoveToY(0, transition_duration * 2, Easing.OutQuint);
                else
                    counter.MoveToY(Math.Min(move_amount * 2, counter.Y + move_amount)).Then().MoveToY(0, transition_duration * 2, Easing.OutQuint);
            }, true);

            // required to prevent the counter initially rolling up from 0 to 1
            // due to `Current.Value` having a nonstandard default value of 1.
            counter.SetCountWithoutRolling(ModMultiplier.Value);

            Ranked.BindValueChanged(e =>
            {
                flash();

                if (e.NewValue)
                {
                    rankedText.Text = ModSelectOverlayStrings.Ranked;
                    rankedText.TooltipText = ModSelectOverlayStrings.RankedExplanation;
                    updateRankedVisuals();
                }
                else
                {
                    rankedText.Text = ModSelectOverlayStrings.Unranked;
                    rankedText.TooltipText = ModSelectOverlayStrings.UnrankedExplanation;
                    updateRankedVisuals();
                }
            }, true);

            themeColour = ColourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                updateRankedVisuals();
                updateCounterColour();
            }, true);
        }

        private void updateCounterColour()
        {
            Colour4 colour = ModMultiplier.Value > ModMultiplier.Default
                ? colours.ForModType(ModType.DifficultyIncrease)
                : ModMultiplier.Value < ModMultiplier.Default
                    ? colours.ForModType(ModType.DifficultyReduction)
                    : ColourProvider.Content1;

            counter.FadeColour(colour, OverlayColourProvider.ThemeTransitionDuration(transition_duration), Easing.OutQuint);
        }

        private void updateRankedVisuals()
        {
            if (Ranked.Value)
            {
                double duration = OverlayColourProvider.ThemeTransitionDuration(transition_duration);
                rankedText.FadeColour(OverlayColourProvider.IsLightTheme ? ColourProvider.Content1 : Colour4.White, duration, Easing.OutQuint);
                FrontBackground.FadeColour(ColourProvider.Background3, duration, Easing.OutQuint);
            }
            else
            {
                double duration = OverlayColourProvider.ThemeTransitionDuration(transition_duration);
                rankedText.FadeColour(ColourProvider.Background5, duration, Easing.OutQuint);
                FrontBackground.FadeColour(colours.Orange1, duration, Easing.OutQuint);
            }
        }

        private void flash()
        {
            flashLayer
                .FadeOutFromOne()
                .FadeTo(0.15f, 60, Easing.OutQuint)
                .Then().FadeOut(500, Easing.OutQuint);
        }

        private partial class TextWithTooltip : OsuSpriteText, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }

        private partial class EffectCounter : RollingCounter<double>, IHasTooltip
        {
            protected override double RollingDuration => 250;

            protected override LocalisableString FormatCount(double count) => ModUtils.FormatScoreMultiplier(count);

            protected override OsuSpriteText CreateSpriteText() => new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = OsuFont.Default.With(size: 17, weight: FontWeight.SemiBold)
            };

            public LocalisableString TooltipText => ModSelectOverlayStrings.ScoreMultiplier;
        }
    }
}
