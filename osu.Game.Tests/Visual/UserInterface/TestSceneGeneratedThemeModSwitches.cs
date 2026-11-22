using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneGeneratedThemeModSwitches : OsuTestScene
    {
        [Test]
        public void TestPanelEndsAreNotNearBlackAgainstGreyTextPlate()
        {
            TestPanel panel = null!;
            AddStep("reproduce black ends and grey middle", () =>
            {
                var theme = new CustomThemeDefinition(ThemeMode.Dark);
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#111111"));
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.ControlIdle, Colour4.FromHex("#333333"));
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                OverlayColourProvider.CustomTheme.Value = theme;
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect)) },
                    Child = panel = new TestPanel { Width = 300, RelativeSizeAxes = Axes.None },
                };
            });
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddAssert("exposed ends lifted towards grey plate", () =>
            {
                panel.FinishTransforms(true);
                return panel.EndColour.R > 0.15f && panel.EndColour.R < 0.2f
                    && Math.Abs(panel.EndColour.R - panel.MiddleColour.R) < 0.05f;
            });
            AddStep("activate", () => panel.Active.Value = true);
            AddAssert("selected mod retains its original accent background", () =>
            {
                panel.FinishTransforms(true);
                return panel.EndColour.G > panel.EndColour.R;
            });
            AddStep("restore theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        private partial class TestPanel : ModPanel
        {
            public TestPanel() : base(new OsuModEasy()) { }
            public Colour4 EndColour => Background.Colour;
            public Colour4 MiddleColour => TextBackground.Colour;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestInactiveIconsUseReadableTextRole(bool light)
        {
            ModSwitchSmall small = null!;
            OverlayColourProvider provider = null!;
            AddStep("load generated palette and switches", () =>
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                OverlayColourProvider.CustomTheme.Value = ImageThemeGenerator.Generate(0xff987632, new CustomThemeDefinition(), new ThemeInterfaceSettings(), light);
                provider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), provider) },
                    Child = small = new ModSwitchSmall(new OsuModEasy()),
                };
            });
            AddWaitStep("finish colour transforms", 4);
            AddAssert("icon uses secondary text not dark surface", () =>
            {
                small.FinishTransforms(true);
                return (Colour4)small.ChildrenOfType<SpriteIcon>().Single().Colour == (Colour4)provider.Content2;
            });
            AddAssert("acronym uses readable secondary text", () =>
                (Colour4)small.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == "EZ").Colour == (Colour4)provider.Content2);
            AddStep("change text independently", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetOverride(OverlayColour.Content2, Colour4.Cyan);
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.TextSecondary, Colour4.Cyan);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddWaitStep("reactive colour update", 4);
            AddAssert("both icon and acronym follow text changes", () =>
            {
                small.FinishTransforms(true);
                return (Colour4)small.ChildrenOfType<SpriteIcon>().Single().Colour == Colour4.Cyan
                    && (Colour4)small.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == "EZ").Colour == Colour4.Cyan;
            });
            AddStep("restore theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }
    }
}
