using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Utils;
using FooterButtonMods = osu.Game.Screens.Select.FooterButtonMods;
using LegacyFooterButtonMods = osu.Game.Screens.SelectLegacy.FooterButtonMods;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    [NonParallelizable]
    public partial class TestSceneStableScoreMultiplierDisplay : OsuTestScene
    {
        private bool previousStable;
        private FooterButtonMods button = null!;
        private TestLegacyFooterButtonMods legacyButton = null!;
        private ModSelectFooterContent footer = null!;
        private readonly Bindable<IReadOnlyList<Mod>> selectedMods = new Bindable<IReadOnlyList<Mod>>();

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [SetUp]
        public void SaveProfile() => previousStable = MosuServerEnvironment.UsesStableProtocol;

        [TearDown]
        public void RestoreProfile() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestClassicHasNoStablePenalty(int mode)
        {
            createDisplays(mode);
            assertMultiplier(1);
            AddUntilStep("Classic does not show unranked badge", () => button.ChildrenOfType<FooterButtonMods.UnrankedBadge>().Single().Alpha == 0);
            AddAssert("mod footer permits stable Classic", () => footer.ChildrenOfType<RankingInformationDisplay>().Single().Ranked.Value);
        }

        [Test]
        public void TestStableMultipliersUpdateWithSelectedMods()
        {
            createDisplays(0);
            assertMultiplier(1);
            AddStep("select Classic and Double Time", () => selectedMods.Value = new Mod[] { new OsuRuleset().CreateMod<ModClassic>()!, new OsuRuleset().CreateMod<ModDoubleTime>()! });
            assertMultiplier(1.12);
            AddStep("select Classic and Hidden", () => selectedMods.Value = new Mod[] { new OsuRuleset().CreateMod<ModClassic>()!, new OsuRuleset().CreateMod<ModHidden>()! });
            assertMultiplier(1.06);
            AddStep("select Classic and Easy", () => selectedMods.Value = new Mod[] { new OsuRuleset().CreateMod<ModClassic>()!, new OsuRuleset().CreateMod<ModEasy>()! });
            assertMultiplier(0.5);
        }

        private void createDisplays(int mode)
        {
            AddStep("create stable multiplier displays", () =>
            {
                Clear();
                MosuServerEnvironment.UsesStableProtocol = true;
                Ruleset ruleset = mode switch
                {
                    0 => new OsuRuleset(),
                    1 => new TaikoRuleset(),
                    2 => new CatchRuleset(),
                    _ => new ManiaRuleset(),
                };
                Ruleset.Value = ruleset.RulesetInfo;
                Beatmap.Value = CreateWorkingBeatmap(ruleset.RulesetInfo);
                selectedMods.Value = new[] { ruleset.CreateMod<ModClassic>()! };
                var overlay = new UserModSelectOverlay
                {
                    Ruleset = { Value = ruleset.RulesetInfo },
                    Beatmap = { Value = Beatmap.Value },
                };
                Add(button = new FooterButtonMods(overlay)
                {
                    Ruleset = { Value = ruleset.RulesetInfo },
                    Mods = selectedMods,
                });
                Add(legacyButton = new TestLegacyFooterButtonMods
                {
                    Y = 100,
                    Current = selectedMods,
                });
                Add(footer = new ModSelectFooterContent(overlay)
                {
                    State = { Value = Visibility.Visible },
                    Ruleset = { BindTarget = overlay.Ruleset },
                    Beatmap = { BindTarget = overlay.Beatmap },
                    ActiveMods = { BindTarget = selectedMods },
                });
            });
            AddUntilStep("displays loaded", () => button.IsLoaded && legacyButton.IsLoaded && footer.IsLoaded);
        }

        private void assertMultiplier(double expected)
        {
            AddAssert($"mod footer multiplier is {expected}", () => footer.ChildrenOfType<RankingInformationDisplay>().Single().ModMultiplier.Value,
                () => Is.EqualTo(expected).Within(1e-12));
            AddAssert("carousel button multiplier is correct", () => button.ChildrenOfType<OsuSpriteText>().First(text => text.Text.ToString().Contains('x')).Text.ToString(),
                () => Is.EqualTo(ModUtils.FormatScoreMultiplier(expected).ToString()));
            AddUntilStep("legacy carousel button multiplier is correct", () => legacyButton.DisplayedMultiplier ==
                (expected == 1 ? string.Empty : ModUtils.FormatScoreMultiplier(expected).ToString()));
        }

        private partial class TestLegacyFooterButtonMods : LegacyFooterButtonMods
        {
            public string DisplayedMultiplier => MultiplierText.Text.ToString();
        }
    }
}
