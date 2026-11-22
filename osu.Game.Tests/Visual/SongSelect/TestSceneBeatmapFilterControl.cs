// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select.Filter;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneBeatmapFilterControl : SongSelectComponentsTestScene
    {
        private FilterControl filterControl = null!;
        [Resolved]
        private OsuConfigManager Config { get; set; } = null!;

        protected override Anchor ComponentAnchor => Anchor.TopRight;
        protected override float InitialRelativeWidth => 0.7f;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Config.SetValue(OsuSetting.ForkSongSelectModdedRatings, false);
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = filterControl = new FilterControl
                {
                    State = { Value = Visibility.Visible },
                    RelativeSizeAxes = Axes.X,
                },
            };
        });

        [Test]
        public void TestMetricSortRefreshesOnModsAndConfiguredRateChanges()
        {
            int changes = 0;
            OsuModDoubleTime dt = null!;
            AddStep("sort by difficulty", () =>
            {
                Config.SetValue(OsuSetting.ForkSongSelectModdedRatings, true);
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Difficulty);
                SelectedMods.Value = Array.Empty<Mod>();
                filterControl.CriteriaChanged += _ => changes++;
            });
            AddStep("select DT", () => { changes = 0; SelectedMods.Value = new Mod[] { dt = new OsuModDoubleTime() }; });
            AddUntilStep("mod change refreshes criteria", () => changes > 0);
            AddStep("edit DT rate", () => { changes = 0; dt.SpeedChange.Value = 1.8; });
            AddUntilStep("setting change refreshes criteria", () => changes > 0);
            AddStep("switch to PP and HR", () =>
            {
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Performance);
                changes = 0;
                SelectedMods.Value = new Mod[] { new OsuModHardRock() };
            });
            AddUntilStep("PP sort refreshes criteria", () => changes > 0);
            AddStep("restore sort and mods", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Title);
            });
        }

        [Test]
        public void TestDisabledOptionDoesNotRefreshForModsOrRateChanges()
        {
            int changes = 0;
            OsuModDoubleTime dt = null!;
            AddStep("use saved difficulty sorting", () =>
            {
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Difficulty);
                SelectedMods.Value = Array.Empty<Mod>();
                filterControl.CriteriaChanged += _ => changes++;
            });
            AddStep("select DT", () => { changes = 0; SelectedMods.Value = new Mod[] { dt = new OsuModDoubleTime() }; });
            AddWaitStep("allow callbacks", 5);
            AddAssert("no mod-driven reorder", () => changes == 0);
            AddStep("edit DT speed", () => dt.SpeedChange.Value = 1.8);
            AddWaitStep("allow debounce", 10);
            AddAssert("no speed-driven reorder", () => changes == 0);
            AddStep("enable experiment", () => Config.SetValue(OsuSetting.ForkSongSelectModdedRatings, true));
            AddStep("edit tracked speed", () => { changes = 0; dt.SpeedChange.Value = 1.9; });
            AddUntilStep("enabled setting tracks speed", () => changes > 0);
            AddStep("disable experiment", () => Config.SetValue(OsuSetting.ForkSongSelectModdedRatings, false));
            AddStep("edit untracked speed", () => { changes = 0; dt.SpeedChange.Value = 1.7; });
            AddWaitStep("allow cancelled debounce", 10);
            AddAssert("disabled setting stops tracking", () => changes == 0);
        }

        [Test]
        public void TestSearch()
        {
            AddStep("search for text", () => filterControl.Search("test search"));
        }
    }
}
