// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModMosuMalevich : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableToScoreProcessor, IApplicableToPlayer
    {
        public override string Name => "Malevich Square";

        public override string Acronym => "MSQ";

        public override ModType Type => ModType.Mosu;

        public override LocalisableString Description => MosuModsStrings.ModMalevichDescription;

        public override bool Ranked => true;

        public override bool HasImplementation => true;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModFlashlight),
            typeof(ModHidden),
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModMalevichMaxCombo), nameof(MosuModsStrings.ModMalevichMaxComboDescription), 0,
            SettingControlType = typeof(SettingsSlider<int>))]
        public BindableInt MaxCombo { get; } = new BindableInt(200)
        {
            Default = 200,
            MinValue = 10,
            MaxValue = 1000,
        };

        private readonly BindableNumber<int> currentCombo = new BindableInt();

        private readonly IBindable<bool> isBreakTime = new Bindable<bool>();

        private MalevichSquare? square;

        public void ApplyToPlayer(Player player)
        {
            isBreakTime.BindTo(player.IsBreakTime);
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            currentCombo.BindTo(scoreProcessor.Combo);
            currentCombo.BindValueChanged(combo =>
            {
                square?.OnComboChanged(combo.NewValue, MaxCombo.Value, isBreakTime.Value);
            }, true);
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            square = new MalevichSquare();

            drawableRuleset.Overlays.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(-1),
                Child = square,
                Depth = float.MinValue,
            });
        }

        private partial class MalevichSquare : Container
        {
            private readonly Box box;

            private const double resize_duration = 200;
            private const float max_size = 0.9f;

            public MalevichSquare()
            {
                RelativeSizeAxes = Axes.Both;
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Size = Vector2.Zero;
                Alpha = 0;

                Child = box = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                };
            }

            public void OnComboChanged(int combo, int maxCombo, bool isBreak)
            {
                if (isBreak)
                {
                    this.ResizeTo(Vector2.Zero, resize_duration, Easing.OutQuint);
                    this.FadeOut(resize_duration, Easing.OutQuint);
                    return;
                }

                float progress = Math.Clamp((float)combo / maxCombo, 0f, 1f);
                float size = MathF.Sqrt(progress) * max_size;

                this.ResizeTo(new Vector2(size), resize_duration, Easing.OutQuint);
                this.FadeIn(resize_duration, Easing.OutQuint);
            }
        }
    }
}
