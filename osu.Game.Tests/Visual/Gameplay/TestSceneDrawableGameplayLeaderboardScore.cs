// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Scoring;
using osu.Game.Rulesets.Scoring;
using osuTK;
using osu.Game.Graphics;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneDrawableGameplayLeaderboardScore : OsuTestScene
    {
        private readonly APIUser user = new APIUser { Username = "user" };
        private readonly BindableLong totalScore = new BindableLong();
        private readonly Bindable<int?> position = new Bindable<int?>();
        private readonly BindableBool quit = new BindableBool();
        private readonly BindableBool expanded = new BindableBool();

        public TestSceneDrawableGameplayLeaderboardScore()
        {
            AddSliderStep("total score", 0, 1_000_000, 500_000, s => totalScore.Value = s);
            AddSliderStep("position", 1, 100, 5, s => position.Value = s);
            AddToggleStep("toggle quit", q => quit.Value = q);
            AddToggleStep("toggle expanded", e => expanded.Value = e);
        }

        [Test]
        public void TestMorasoomaAppearanceAndStatistics()
        {
            DrawableGameplayLeaderboardScore row = null!;
            var oldMode = OverlayColourProvider.CurrentTheme.Value;
            var oldTheme = OverlayColourProvider.CustomTheme.Value;
            bool oldShear = OsuGame.DisableShear.Value;
            var info = new ScoreInfo { User = user, TotalScore = 123456, Accuracy = 0.9876, MaxCombo = 123, PP = 234.5 };
            info.Statistics[HitResult.Miss] = 3;
            info.Mods = new osu.Game.Rulesets.Mods.Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime(), new osu.Game.Rulesets.Osu.Mods.OsuModHidden() };
            AddStep("show themed solid row", () =>
            {
                var theme = new CustomThemeDefinition();
                theme.SetOverride(OverlayColour.Background3, Colour4.Purple);
                theme.SetOverride(OverlayColour.Content1, Colour4.Cyan);
                theme.SetRadius(GeometryRole.Card, 4);
                OverlayColourProvider.CustomTheme.Value = theme;
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                OsuGame.DisableShear.Value = true;
                Child = new Container
                {
                    Width = 400, RelativeSizeAxes = Axes.Y,
                    Child = row = new DrawableGameplayLeaderboardScore(new GameplayLeaderboardScore(info, true, GameplayLeaderboardScore.ComboDisplayMode.Highest))
                    {
                        Expanded = { Value = true }, Style = { Value = GameplayLeaderboardStyle.Morasooma },
                        PanelStyle = { Value = GameplayLeaderboardPanelStyle.Solid },
                        ShowMisses = { Value = true }, ShowPp = { Value = true }, ShowMods = { Value = true },
                    },
                };
            });
            AddUntilStep("statistics shown", () => row.ExtraInformation.Contains(234.5.ToString("0.##")) && row.ExtraInformation.Contains("× 3"));
            AddAssert("straight geometry", () => row.Shear, () => Is.EqualTo(Vector2.Zero));
            AddAssert("theme radius", () => row.PanelRadius, () => Is.EqualTo(4));
            AddAssert("solid theme background", () => (Colour4)row.PanelBackground.TopLeft, () => Is.EqualTo(Colour4.Purple));
            AddAssert("optional third row", () => row.Height, () => Is.EqualTo(54));
            AddAssert("misses stay judgement red", () => row.MissesColour, () => Is.EqualTo((Colour4)osu_colour.Red2));
            AddUntilStep("mods use actual icons", () => row.ModIconCount, () => Is.EqualTo(2));
            AddAssert("avatar fills expanded row", () => row.AvatarSize, () => Is.EqualTo(54));
            AddAssert("avatar aligned to row top", () => row.AvatarTop, () => Is.Zero);
            AddAssert("avatar centred in its slot", () => (row.AvatarCentre - row.AvatarSlotCentre).Length, () => Is.LessThan(0.1f));
            AddAssert("outline uses screen-space antialiasing", () => row.ChildrenOfType<LeaderboardPanelOutline>().Count(), () => Is.EqualTo(1));
            AddStep("enable interface shear", () => OsuGame.DisableShear.Value = false);
            AddWaitStep("shear layout settles", 3);
            AddAssert("sheared avatar remains centred", () => (row.AvatarCentre - row.AvatarSlotCentre).Length, () => Is.LessThan(0.1f));
            AddStep("disable interface shear again", () => OsuGame.DisableShear.Value = true);
            AddWaitStep("straight layout settles", 3);
            AddAssert("straight avatar remains centred", () => (row.AvatarCentre - row.AvatarSlotCentre).Length, () => Is.LessThan(0.1f));
            AddStep("large theme radius", () =>
            {
                var theme = new CustomThemeDefinition();
                theme.SetRadius(GeometryRole.Card, 30);
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("compact row is not a capsule", () => row.PanelRadius, () => Is.EqualTo(10));
            AddAssert("misses remain red after palette change", () => row.MissesColour, () => Is.EqualTo((Colour4)osu_colour.Red2));
            AddStep("change data and opacity", () =>
            {
                info.PP = null;
                info.Statistics[HitResult.Miss] = 8;
                row.PanelStyle.Value = GameplayLeaderboardPanelStyle.Transparent;
            });
            AddUntilStep("unknown pp is not zero", () => row.ExtraInformation.Contains("— pp") && row.ExtraInformation.Contains("× 8"));
            AddAssert("transparent panel", () => row.PanelBackground.TopLeft.Linear.A, () => Is.LessThan(1));
            AddStep("return to classic", () =>
            {
                row.Style.Value = GameplayLeaderboardStyle.Classic;
                row.ShowMisses.Value = row.ShowPp.Value = row.ShowMods.Value = false;
            });
            AddAssert("original row height", () => row.Height, () => Is.EqualTo(38));
            AddAssert("avatar returns to original size", () => row.AvatarSize, () => Is.EqualTo(38));
            AddAssert("original radius", () => row.PanelRadius, () => Is.EqualTo(10));
            AddStep("restore global settings", () =>
            {
                OverlayColourProvider.CustomTheme.Value = oldTheme;
                OverlayColourProvider.CurrentTheme.Value = oldMode;
                OsuGame.DisableShear.Value = oldShear;
            });
        }

        private static readonly OsuColour osu_colour = new OsuColour();

        private static readonly object?[][] leaderboard_variants =
        {
            new object?[] { false, null },
            new object?[] { true, null },
            new object?[] { false, osu_colour.TeamColourRed },
            new object?[] { true, osu_colour.TeamColourRed },
            new object?[] { false, osu_colour.TeamColourBlue },
            new object?[] { true, osu_colour.TeamColourBlue },
        };

        [TestCaseSource(nameof(leaderboard_variants))]
        public void TestVariants(bool tracked, Color4? teamColour)
        {
            AddStep("show", () =>
            {
                GameplayLeaderboardScore score = new GameplayLeaderboardScore(user, tracked, totalScore)
                {
                    Position = { BindTarget = position },
                    HasQuit = { BindTarget = quit },
                    TeamColour = teamColour,
                };
                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Width = 250,
                    Child = new DrawableGameplayLeaderboardScore(score)
                    {
                        Expanded = { BindTarget = expanded },
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    }
                };
            });
        }
    }
}
