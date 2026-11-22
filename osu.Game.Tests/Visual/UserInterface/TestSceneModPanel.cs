// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneModPanel : OsuManualInputManagerTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect);

        [TearDownSteps]
        public void TearDown()
        {
            AddStep("reset theme and shear", () =>
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
                OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition();
                OsuGame.DisableShear.Value = false;
            });
        }

        [Test]
        public void TestVariousPanels()
        {
            AddStep("create content", () => Child = new FillFlowContainer
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Spacing = new Vector2(0, 5),
                Children = new[]
                {
                    new ModPanel(new OsuModHalfTime()),
                    new ModPanel(new OsuModFlashlight()),
                    new ModPanel(new OsuModAutoplay()),
                    new ModPanel(new OsuModAlternate()),
                    new ModPanel(new OsuModApproachDifferent())
                }
            });
        }

        [Test]
        public void TestIncompatiblePanelUsesCustomThemeSurfaces()
        {
            IncompatibilityDisplayingModPanel panel = null;
            AddStep("create incompatible panel", () =>
            {
                SelectedMods.Value = new Mod[] { new OsuModHalfTime() };
                Child = panel = new IncompatibilityDisplayingModPanel(new OsuModDoubleTime()) { Width = 300 };
            });
            AddUntilStep("panel loaded", () => panel.IsLoaded);
            AddStep("apply non-green disabled surfaces", () =>
            {
                var theme = new CustomThemeDefinition();
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceElevated, Colour4.FromHex("#CC3355"));
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.Backdrop, Colour4.FromHex("#2233CC"));
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddUntilStep("incompatible foreground follows elevated surface", () =>
                panel.ChildrenOfType<Box>().Any(box => (Colour4)box.Colour == Colour4.FromHex("#CC3355")));
            AddUntilStep("incompatible background follows backdrop", () =>
                panel.ChildrenOfType<Box>().Any(box => (Colour4)box.Colour == Colour4.FromHex("#2233CC")));
            AddStep("change disabled colour live", () =>
            {
                var theme = new CustomThemeDefinition();
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceElevated, Colour4.FromHex("#AA44CC"));
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddUntilStep("incompatible foreground updates without hover", () =>
                panel.ChildrenOfType<Box>().Any(box => (Colour4)box.Colour == Colour4.FromHex("#AA44CC")));
        }

        [Test]
        public void TestIncompatibilityDisplay()
        {
            IncompatibilityDisplayingModPanel panel = null;

            AddStep("create panel with DT", () =>
            {
                Child = panel = new IncompatibilityDisplayingModPanel(new OsuModDoubleTime())
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.None,
                    Width = 300,
                };

                panel.Active.BindValueChanged(active =>
                {
                    SelectedMods.Value = active.NewValue
                        ? Array.Empty<Mod>()
                        : new[] { panel.Mod };
                });
            });

            clickPanel();
            AddAssert("panel active", () => panel.Active.Value);

            clickPanel();
            AddAssert("panel not active", () => !panel.Active.Value);

            AddStep("set incompatible mod", () => SelectedMods.Value = new[] { new OsuModHalfTime() });

            clickPanel();
            AddAssert("panel active", () => panel.Active.Value);

            void clickPanel() => AddStep("click panel", () =>
            {
                InputManager.MoveMouseTo(panel);
                InputManager.Click(MouseButton.Left);
            });
        }

        [Test]
        public void TestHighRadiusClampingWithShear()
        {
            ModPanel panel = null;

            AddStep("reset state", () =>
            {
                OsuGame.DisableShear.Value = false;
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
                OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition();
            });

            AddStep("create panel", () =>
            {
                Child = panel = new ModPanel(new OsuModDoubleTime())
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 300,
                };
            });

            AddUntilStep("wait for panel load", () => panel.IsLoaded);

            AddStep("set extreme card radius (30px)", () =>
            {
                var theme = new CustomThemeDefinition();
                theme.Geometry.CardRadius = 30f;
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("panel corner radius clamped to half height (21px) with shear", () =>
            {
                var content = panel.ChildrenOfType<Container>().First(c => c.Masking && c.CornerRadius > 0);
                return content.CornerRadius == ModSelectPanel.HEIGHT / 2f;
            });

            AddStep("disable shear", () => OsuGame.DisableShear.Value = true);

            AddAssert("panel corner radius clamped to half height (21px) without shear", () =>
            {
                var content = panel.ChildrenOfType<Container>().First(c => c.Masking && c.CornerRadius > 0);
                return content.CornerRadius == ModSelectPanel.HEIGHT / 2f;
            });
        }

        [Test]
        public void TestLiveModSelectThemeReactivity()
        {
            ModPanel panel = null;

            AddStep("reset mouse and theme", () =>
            {
                InputManager.MoveMouseTo(Vector2.Zero);
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
                OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition();
            });

            AddStep("create panel", () =>
            {
                Child = panel = new ModPanel(new OsuModDoubleTime())
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 300,
                };
            });

            AddUntilStep("wait for panel load", () => panel.IsLoaded);

            AddStep("set custom ModSelect background override", () =>
            {
                var theme = new CustomThemeDefinition();
                theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#FF2233"));
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("colour provider background updated", () => ((Colour4)colourProvider.Background3) == Colour4.FromHex("#FF2233"));

            AddUntilStep("panel ends follow the surface and control colour blend", () =>
            {
                var bg = panel.ChildrenOfType<Box>().First();
                var expected = osu.Framework.Utils.Interpolation.ValueAt<Colour4>(0.7f, colourProvider.Background3, colourProvider.Background2, 0, 1);
                return ((Colour4)bg.Colour) == expected;
            });
        }
    }
}
