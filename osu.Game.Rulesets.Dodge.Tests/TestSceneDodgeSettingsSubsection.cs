// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Dodge.Configuration;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public partial class TestSceneDodgeSettingsSubsection : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Test]
        public void TestPlayfieldDimUsesIndependentDodgeSetting()
        {
            DodgeSettingsSubsection subsection = null!;
            FormSliderBar<double> playfieldDimSlider = null!;
            DodgeRulesetConfigManager rulesetConfig = null!;
            DrawableDodgeRuleset drawableRuleset = null!;
            var ruleset = new DodgeRuleset();

            AddStep("create settings", () =>
            {
                rulesetConfig = (DodgeRulesetConfigManager)RulesetConfigs.GetConfigFor(ruleset)!;
                Child = subsection = new DodgeSettingsSubsection(ruleset);
            });
            AddUntilStep("settings loaded", () => subsection.IsLoaded);
            AddStep("find playfield dim slider", () =>
            {
                playfieldDimSlider = subsection.ChildrenOfType<FormSliderBar<double>>()
                                               .Single(slider => ((IBindableNumber<double>)slider.Current).MaxValue == 1
                                                                 && slider.Current.Value == DodgeRulesetConfigManager.DEFAULT_PLAYFIELD_DIM);
            });
            AddAssert("slider uses dodge default", () => playfieldDimSlider.Current.Value, () => Is.EqualTo(DodgeRulesetConfigManager.DEFAULT_PLAYFIELD_DIM));
            AddStep("change playfield dim", () => playfieldDimSlider.Current.Value = 0.65);
            AddAssert("dodge config updated", () => rulesetConfig.Get<double>(DodgeRulesetSetting.PlayfieldDim), () => Is.EqualTo(0.65));
            AddStep("create gameplay", () => Child = drawableRuleset = new DrawableDodgeRuleset(ruleset, new Beatmap<DodgeHitObject>(), []));
            AddUntilStep("gameplay loaded", () => drawableRuleset.Playfield.Player.IsLoaded);
            AddAssert("arena fill uses configured dim", () => drawableRuleset.Playfield.ArenaBackgroundAlpha, () => Is.EqualTo(0.65f).Within(0.001f));
        }

        [Test]
        public void TestGrazeIndicatorBrightnessUsesIndependentDodgeSetting()
        {
            DodgeSettingsSubsection subsection = null!;
            FormSliderBar<double> grazeBrightnessSlider = null!;
            DodgeRulesetConfigManager rulesetConfig = null!;
            DrawableDodgeRuleset drawableRuleset = null!;
            var ruleset = new DodgeRuleset();

            AddStep("create settings", () =>
            {
                rulesetConfig = (DodgeRulesetConfigManager)RulesetConfigs.GetConfigFor(ruleset)!;
                Child = subsection = new DodgeSettingsSubsection(ruleset);
            });
            AddUntilStep("settings loaded", () => subsection.IsLoaded);
            AddStep("find graze brightness slider", () =>
            {
                grazeBrightnessSlider = subsection.ChildrenOfType<FormSliderBar<double>>()
                                                   .Single(slider => slider.Current.Value == DodgeRulesetConfigManager.DEFAULT_GRAZE_INDICATOR_BRIGHTNESS);
            });
            AddAssert("slider uses brighter dodge default", () => grazeBrightnessSlider.Current.Value,
                () => Is.EqualTo(DodgeRulesetConfigManager.DEFAULT_GRAZE_INDICATOR_BRIGHTNESS));
            AddStep("change graze brightness", () => grazeBrightnessSlider.Current.Value = 0.6);
            AddAssert("dodge config updated", () => rulesetConfig.Get<double>(DodgeRulesetSetting.GrazeIndicatorBrightness), () => Is.EqualTo(0.6));
            AddStep("create gameplay", () => Child = drawableRuleset = new DrawableDodgeRuleset(ruleset, new Beatmap<DodgeHitObject>(), []));
            AddUntilStep("gameplay loaded", () => drawableRuleset.Playfield.Player.IsLoaded);
            AddAssert("graze circle uses configured brightness", () => drawableRuleset.Playfield.GrazeIndicatorBrightness, () => Is.EqualTo(0.6f).Within(0.001f));
        }
    }
}
