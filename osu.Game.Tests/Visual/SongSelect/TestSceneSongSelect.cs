// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning.Select;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;
using BeatmapCarousel = osu.Game.Screens.Select.BeatmapCarousel;
using FooterButtonMods = osu.Game.Screens.Select.FooterButtonMods;
using FooterButtonOptions = osu.Game.Screens.Select.FooterButtonOptions;
using FooterButtonRandom = osu.Game.Screens.Select.FooterButtonRandom;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneSongSelect : SongSelectTestScene
    {
        [Test]
        public void TestResultsScreenWhenClickingLeaderboardScore()
        {
            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddAssert("beatmap imported", () => Beatmaps.GetAllUsableBeatmapSets().Any(), () => Is.True);

            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("import score", () =>
            {
                var beatmapInfo = Beatmaps.GetAllUsableBeatmapSets().Single().Beatmaps.First();
                ScoreManager.Import(new ScoreInfo
                {
                    Hash = Guid.NewGuid().ToString(),
                    BeatmapHash = beatmapInfo.Hash,
                    BeatmapInfo = beatmapInfo,
                    Ruleset = new OsuRuleset().RulesetInfo,
                    User = new GuestUser(),
                });
            });

            AddStep("select ranking tab", () =>
            {
                InputManager.MoveMouseTo(SongSelect.ChildrenOfType<BeatmapDetailsArea.WedgeSelector<BeatmapDetailsArea.Header.Selection>>().Last());
                InputManager.Click(MouseButton.Left);
            });

            // probably should be done via dropdown menu instead of forcing this way?
            AddStep("set local scope", () =>
            {
                var current = LeaderboardManager.CurrentCriteria!;
                LeaderboardManager.FetchWithCriteria(current with
                {
                    Scope = BeatmapLeaderboardScope.Local,
                });
            });

            AddUntilStep("wait for score panel", () => SongSelect.ChildrenOfType<BeatmapLeaderboardScore>().Any());
            AddStep("click score panel", () =>
            {
                InputManager.MoveMouseTo(SongSelect.ChildrenOfType<BeatmapLeaderboardScore>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("wait for results screen", () => Stack.CurrentScreen is ResultsScreen);
        }

        [Test]
        public void TestSingleFilterWhenEntering()
        {
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            AddAssert("single filter", () => Carousel.FilterCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestCarouselRestoredAfterLeavingInfiniteGlass()
        {
            ImportBeatmapForRuleset(0);
            AddStep("start in InfiniteGlass", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass));
            LoadSongSelect();

            AddUntilStep("regular carousel hidden", () => Carousel.Alpha, () => Is.Zero.Within(0.001f));
            AddAssert("hidden carousel kept updating", () => Carousel.AlwaysPresent, () => Is.True);

            BeatmapInfo? infiniteGlassBeatmap = null;
            AddStep("store InfiniteGlass selection", () => infiniteGlassBeatmap = Beatmap.Value.BeatmapInfo);
            AddStep("select next map in InfiniteGlass", () => InputManager.Key(Key.Down));
            AddUntilStep("InfiniteGlass selection changed", () => Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(infiniteGlassBeatmap!.ID));

            addTransitionAndNavigationSteps(ForkSongSelectStyle.Modern, Key.Up);

            AddStep("return to InfiniteGlass", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass));
            AddUntilStep("regular carousel hidden again", () => Carousel.Alpha, () => Is.Zero.Within(0.001f));
            AddAssert("hidden carousel still updating", () => Carousel.AlwaysPresent, () => Is.True);

            BeatmapInfo? returnedInfiniteGlassBeatmap = null;
            AddStep("store returned InfiniteGlass selection", () => returnedInfiniteGlassBeatmap = Beatmap.Value.BeatmapInfo);
            AddStep("select next map after returning to InfiniteGlass", () => InputManager.Key(Key.Down));
            AddUntilStep("returned InfiniteGlass selection changed", () => Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(returnedInfiniteGlassBeatmap!.ID));

            addTransitionAndNavigationSteps(ForkSongSelectStyle.LegacySkinned, Key.Up);

            void addTransitionAndNavigationSteps(ForkSongSelectStyle targetStyle, Key navigationKey)
            {
                AddStep($"switch to {targetStyle}", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, targetStyle));
                AddUntilStep($"{targetStyle} carousel visible", () => Carousel.Alpha, () => Is.EqualTo(1).Within(0.001f));
                AddAssert($"{targetStyle} carousel present", () => Carousel.IsPresent, () => Is.True);
                AddAssert($"{targetStyle} forced presence released", () => Carousel.AlwaysPresent, () => Is.False);

                BeatmapInfo? previousBeatmap = null;
                AddStep($"store {targetStyle} selection", () => previousBeatmap = Beatmap.Value.BeatmapInfo);
                AddStep($"select adjacent map in {targetStyle}", () => InputManager.Key(navigationKey));
                AddUntilStep($"{targetStyle} selection changed", () => Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(previousBeatmap!.ID));
                AddAssert($"{targetStyle} selected track loaded", () => Beatmap.Value.TrackLoaded, () => Is.True);
            }
        }

        [TestCase(1024)]
        [TestCase(1366)]
        public void TestStableChromeKeepsSkinScale(float viewportWidth)
        {
            ScalingContainer scalingContainer = null!;
            MarginPadding originalPadding = default;
            float originalScale = 1;
            ForkSongSelectStyle originalStyle = default;

            AddStep("set stable viewport", () =>
            {
                scalingContainer = this.ChildrenOfType<ScalingContainer>().First();
                originalPadding = Stack.Padding;
                originalScale = Config.Get<float>(OsuSetting.UIScale);
                originalStyle = Config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);
                scalingContainer.RelativeSizeAxes = Axes.None;
                scalingContainer.Size = new Vector2(viewportWidth, 768);
                Stack.Padding = default;
                Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
            });
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            foreach (float scale in new[] { 1f, 1.25f, 0.8f })
            {
                AddStep($"set UI scale to {scale}", () => Config.SetValue(OsuSetting.UIScale, scale));
                AddUntilStep("screen applies UI scale", () => SongSelect.DrawHeight, () => Is.EqualTo(768 / scale).Within(0.01f));
                AddUntilStep("top keeps skin coordinates", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().DrawSize,
                    () => Is.EqualTo(new Vector2(viewportWidth, 768)).Using<Vector2>((actual, expected) => (actual - expected).Length < 0.01f));
                AddUntilStep("footer matches top coordinates", () => this.ChildrenOfType<LegacyFooter>().SingleOrDefault()?.DrawSize,
                    () => Is.EqualTo(new Vector2(viewportWidth, 768)).Using<Vector2>((actual, expected) => (actual - expected).Length < 0.01f));
            }

            AddStep("restore viewport and settings", () =>
            {
                scalingContainer.RelativeSizeAxes = Axes.Both;
                scalingContainer.Size = Vector2.One;
                Stack.Padding = originalPadding;
                Config.SetValue(OsuSetting.UIScale, originalScale);
                Config.SetValue(OsuSetting.ForkSongSelectStyle, originalStyle);
            });
        }

        [Test]
        public void TestStableSelectionUsesDifficultyRows()
        {
            AddStep("use stable style", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            AddUntilStep("difficulty rows loaded", () => Carousel.ChildrenOfType<PanelBeatmapStandalone>().Any(panel => panel.Item?.IsVisible == true));
            AddAssert("expanded set row is hidden", () => Carousel.ChildrenOfType<PanelBeatmapSet>().Any(panel => panel.Item?.IsVisible == true && panel.Expanded.Value), () => Is.False);
            AddAssert("selected difficulty is a visible full row", () => Carousel.ChildrenOfType<PanelBeatmapStandalone>().Any(panel => panel.Item?.IsVisible == true && panel.Selected.Value && panel.Item.Model is GroupedBeatmap grouped && grouped.Beatmap.Equals(Carousel.CurrentBeatmap)), () => Is.True);
            AddAssert("skin layer is above carousel", () => SongSelect.SkinLayerIsAboveCarousel, () => Is.True);

            BeatmapInfo? previousDifficulty = null;
            AddStep("store selected difficulty", () => previousDifficulty = Carousel.CurrentBeatmap);
            AddStep("select next difficulty", () => InputManager.Key(Key.Down));
            AddUntilStep("difficulty selection changes", () => Carousel.CurrentBeatmap, () => Is.Not.EqualTo(previousDifficulty));
            AddAssert("new selected difficulty stays visible", () => Carousel.ChildrenOfType<PanelBeatmapStandalone>().Any(panel => panel.Item?.IsVisible == true && panel.Selected.Value && panel.Item.Model is GroupedBeatmap grouped && grouped.Beatmap.Equals(Carousel.CurrentBeatmap)), () => Is.True);
            AddUntilStep("selected difficulty protrudes", () =>
            {
                var rows = Carousel.ChildrenOfType<PanelBeatmapStandalone>().Where(panel => panel.Item?.IsVisible == true).ToArray();
                var selected = rows.Single(panel => panel.Selected.Value);
                return rows.Where(panel => panel != selected).All(panel => selected.TopLevelContent.DrawPosition.X < panel.TopLevelContent.DrawPosition.X);
            });
        }

        [Test]
        public void TestModernMouseSelectionAfterLeavingInfiniteGlass()
        {
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            AddStep("start in Modern", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            LoadSongSelect();

            int initialFilterCount = 0;
            AddStep("store initial filter count", () => initialFilterCount = Carousel.FilterCount);
            AddStep("switch to InfiniteGlass", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass));
            AddUntilStep("Modern carousel hidden", () => Carousel.Alpha, () => Is.Zero.Within(0.001f));
            AddAssert("InfiniteGlass switch did not refilter", () => Carousel.FilterCount, () => Is.EqualTo(initialFilterCount));

            AddStep("switch back to Modern", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddUntilStep("returned Modern carousel visible", () => Carousel.Alpha, () => Is.EqualTo(1).Within(0.001f));
            AddAssert("Modern switch did not refilter", () => Carousel.FilterCount, () => Is.EqualTo(initialFilterCount));

            Panel? targetPanel = null;
            BeatmapInfo? targetBeatmap = null;

            AddUntilStep("find another visible Modern panel", () =>
            {
                targetPanel = Carousel.ChildrenOfType<Panel>().FirstOrDefault(panel =>
                    panel.IsPresent
                    && panel.Item?.IsVisible == true
                    && getBeatmap(panel.Item.Model) is BeatmapInfo panelBeatmap
                    && panelBeatmap.ID != Beatmap.Value.BeatmapInfo.ID);

                return targetPanel != null;
            });
            AddStep("click another Modern panel", () =>
            {
                targetBeatmap = getBeatmap(targetPanel!.Item!.Model);
                InputManager.MoveMouseTo(targetPanel.TopLevelContent);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("clicked Modern beatmap selected", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(targetBeatmap!.ID));

            static BeatmapInfo? getBeatmap(object model) => model switch
            {
                GroupedBeatmap groupedBeatmap => groupedBeatmap.Beatmap,
                GroupedBeatmapSet groupedBeatmapSet => groupedBeatmapSet.BeatmapSet.Beatmaps.FirstOrDefault(),
                _ => null,
            };
        }

        [Test]
        public void TestModernMouseSelectionAfterReenter()
        {
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            AddStep("use Modern", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            LoadSongSelect();

            Screens.Select.SongSelect? firstSongSelect = null;
            AddStep("store first song select", () => firstSongSelect = SongSelect);
            AddStep("leave song select", () => SongSelect.Exit());
            AddUntilStep("first song select exited", () => !firstSongSelect!.ValidForPush);

            LoadSongSelect();
            AddAssert("new song select instance", () => SongSelect, () => Is.Not.SameAs(firstSongSelect));

            Panel? targetPanel = null;
            BeatmapInfo? targetBeatmap = null;
            WorkingBeatmap? previousWorkingBeatmap = null;
            ITrack? previousTrack = null;

            AddUntilStep("find another visible panel after re-entering", () =>
            {
                targetPanel = Carousel.ChildrenOfType<Panel>().FirstOrDefault(panel =>
                    panel.IsPresent
                    && panel.Item?.IsVisible == true
                    && getBeatmap(panel.Item.Model) is BeatmapInfo panelBeatmap
                    && panelBeatmap.ID != Beatmap.Value.BeatmapInfo.ID);

                return targetPanel != null;
            });
            AddStep("click another panel after re-entering", () =>
            {
                previousWorkingBeatmap = Beatmap.Value;
                previousTrack = previousWorkingBeatmap.Track;
                targetBeatmap = getBeatmap(targetPanel!.Item!.Model);
                InputManager.MoveMouseTo(targetPanel.TopLevelContent);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("mouse selection works after re-entering", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(targetBeatmap!.ID));
            AddUntilStep("selected track loaded", () => Beatmap.Value.TrackLoaded);
            AddAssert("selected track replaced", () => Beatmap.Value.Track, () => Is.Not.SameAs(previousTrack));
            AddUntilStep("selected track playing", () => Beatmap.Value.Track.IsRunning);
            AddUntilStep("previous track stopped", () => previousTrack!.IsRunning, () => Is.False);

            static BeatmapInfo? getBeatmap(object model) => model switch
            {
                GroupedBeatmap groupedBeatmap => groupedBeatmap.Beatmap,
                GroupedBeatmapSet groupedBeatmapSet => groupedBeatmapSet.BeatmapSet.Beatmaps.FirstOrDefault(),
                _ => null,
            };
        }

        [Test]
        public void TestCookieDoesNothingIfNothingSelected()
        {
            var screensPushed = new List<IScreen>();

            LoadSongSelect();
            AddStep("subscribe to screen pushed", () => Stack.ScreenPushed += onScreenPushed);
            AddStep("click osu! cookie", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<OsuLogo>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("no screens pushed", () => screensPushed, () => Is.Empty);
            AddStep("unsubscribe from screen pushed", () => Stack.ScreenPushed -= onScreenPushed);

            void onScreenPushed(IScreen lastScreen, IScreen newScreen) => screensPushed.Add(lastScreen);
        }

        [Test]
        public void TestInvalidRulesetDoesNotEnterGameplay()
        {
            var screensPushed = new List<IScreen>();

            ImportBeatmapForRuleset(0);
            ImportBeatmapForRuleset(1);

            LoadSongSelect();
            AddStep("subscribe to screen pushed", () => Stack.ScreenPushed += onScreenPushed);

            AddStep("change ruleset to taiko", () => Ruleset.Value = Rulesets.AvailableRulesets.Single(r => r.OnlineID == 1));

            AddStep("disable converts", () => Config.SetValue(OsuSetting.ShowConvertedBeatmaps, false));

            AddUntilStep("wait for taiko beatmap selected", () => Beatmap.Value.BeatmapInfo.Ruleset.OnlineID, () => Is.EqualTo(1));

            AddStep("change ruleset back and start gameplay immediately", () =>
            {
                Ruleset.Value = Rulesets.AvailableRulesets.Single(r => r.OnlineID == 0);

                InputManager.MoveMouseTo(this.ChildrenOfType<OsuLogo>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("no screens pushed", () => screensPushed, () => Is.Empty);
            AddStep("unsubscribe from screen pushed", () => Stack.ScreenPushed -= onScreenPushed);

            AddUntilStep("wait for osu beatmap selected", () => Beatmap.Value.BeatmapInfo.Ruleset.OnlineID, () => Is.EqualTo(0));

            void onScreenPushed(IScreen lastScreen, IScreen newScreen) => screensPushed.Add(lastScreen);
        }

        #region Hotkeys

        [Test]
        public void TestDeleteHotkey()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);

            AddAssert("beatmap imported", () => Beatmaps.GetAllUsableBeatmapSets().Any(), () => Is.True);
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("press shift-delete", () =>
            {
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Key(Key.Delete);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });

            AddUntilStep("delete dialog shown", () => DialogOverlay.CurrentDialog, Is.InstanceOf<BeatmapDeleteDialog>);
            AddStep("confirm deletion", () => DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());

            AddAssert("beatmap set deleted", () => Beatmaps.GetAllUsableBeatmapSets().Any(), () => Is.False);
        }

        [Test]
        public void TestClearModsViaModButtonRightClick()
        {
            LoadSongSelect();

            AddStep("select NC", () => SelectedMods.Value = new[] { new OsuModNightcore() });
            AddAssert("mods selected", () => SelectedMods.Value, () => Has.Count.EqualTo(1));
            AddStep("right click mod button", () =>
            {
                InputManager.MoveMouseTo(ScreenFooter.ChildrenOfType<FooterButtonMods>().Single());
                InputManager.Click(MouseButton.Right);
            });
            AddAssert("not mods selected", () => SelectedMods.Value, () => Has.Count.EqualTo(0));
        }

        [Test]
        public void TestSpeedChange()
        {
            LoadSongSelect();
            AddStep("clear mods", () => SelectedMods.Value = Array.Empty<Mod>());

            decreaseModSpeed();
            AddAssert("half time activated at 0.95x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            decreaseModSpeed();
            AddAssert("half time speed changed to 0.9x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.9).Within(0.005));

            increaseModSpeed();
            AddAssert("half time speed changed to 0.95x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            increaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            increaseModSpeed();
            AddAssert("double time activated at 1.05x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));

            increaseModSpeed();
            AddAssert("double time speed changed to 1.1x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.1).Within(0.005));

            decreaseModSpeed();
            AddAssert("double time speed changed to 1.05x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));

            OsuModNightcore nc = new OsuModNightcore
            {
                SpeedChange = { Value = 1.05 }
            };
            AddStep("select NC", () => SelectedMods.Value = new[] { nc });

            increaseModSpeed();
            AddAssert("nightcore speed changed to 1.1x", () => SelectedMods.Value.OfType<ModNightcore>().Single().SpeedChange.Value, () => Is.EqualTo(1.1).Within(0.005));

            decreaseModSpeed();
            AddAssert("nightcore speed changed to 1.05x", () => SelectedMods.Value.OfType<ModNightcore>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));

            decreaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            decreaseModSpeed();
            AddAssert("daycore activated at 0.95x", () => SelectedMods.Value.OfType<ModDaycore>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            decreaseModSpeed();
            AddAssert("daycore activated at 0.95x", () => SelectedMods.Value.OfType<ModDaycore>().Single().SpeedChange.Value, () => Is.EqualTo(0.9).Within(0.005));

            increaseModSpeed();
            AddAssert("daycore activated at 0.95x", () => SelectedMods.Value.OfType<ModDaycore>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            OsuModDoubleTime dt = new OsuModDoubleTime
            {
                SpeedChange = { Value = 1.02 },
                AdjustPitch = { Value = true },
            };
            AddStep("select DT", () => SelectedMods.Value = new[] { dt });

            decreaseModSpeed();
            AddAssert("half time activated at 0.97x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.97).Within(0.005));
            AddAssert("adjust pitch preserved", () => SelectedMods.Value.OfType<ModHalfTime>().Single().AdjustPitch.Value, () => Is.True);

            OsuModHalfTime ht = new OsuModHalfTime
            {
                SpeedChange = { Value = 0.97 },
                AdjustPitch = { Value = true },
            };
            Mod[] modlist = { ht, new OsuModHardRock(), new OsuModHidden() };
            AddStep("select HT+HD", () => SelectedMods.Value = modlist);

            increaseModSpeed();
            AddAssert("double time activated at 1.02x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.02).Within(0.005));
            AddAssert("double time activated at 1.02x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().AdjustPitch.Value, () => Is.True);
            AddAssert("HD still enabled", () => SelectedMods.Value.OfType<ModHidden>().SingleOrDefault(), () => Is.Not.Null);
            AddAssert("HR still enabled", () => SelectedMods.Value.OfType<ModHardRock>().SingleOrDefault(), () => Is.Not.Null);

            AddStep("select WU", () => SelectedMods.Value = new[] { new ModWindUp() });
            increaseModSpeed();
            AddAssert("windup still active", () => SelectedMods.Value.First() is ModWindUp);

            AddStep("select AS", () => SelectedMods.Value = new[] { new ModAdaptiveSpeed() });
            increaseModSpeed();
            AddAssert("adaptive speed still active", () => SelectedMods.Value.First() is ModAdaptiveSpeed);

            OsuModDoubleTime dtWithAdjustPitch = new OsuModDoubleTime
            {
                SpeedChange = { Value = 1.05 },
                AdjustPitch = { Value = true },
            };
            AddStep("select DT x1.05", () => SelectedMods.Value = new[] { dtWithAdjustPitch });

            decreaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            decreaseModSpeed();
            AddAssert("half time activated at 0.95x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));
            AddAssert("half time has adjust pitch active", () => SelectedMods.Value.OfType<ModHalfTime>().Single().AdjustPitch.Value, () => Is.True);

            AddStep("turn off adjust pitch", () => SelectedMods.Value.OfType<ModHalfTime>().Single().AdjustPitch.Value = false);

            increaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            increaseModSpeed();
            AddAssert("double time activated at 1.05x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));
            AddAssert("double time has adjust pitch inactive", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().AdjustPitch.Value, () => Is.False);

            void increaseModSpeed() => AddStep("increase mod speed", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Up);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            void decreaseModSpeed() => AddStep("decrease mod speed", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Down);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
        }

        /// <summary>
        /// Last played and rank achieved may have changed, so we want to make sure filtering runs on resume to song select.
        /// </summary>
        [Test]
        public void TestFilteringRunsAfterReturningFromGameplay()
        {
            AddStep("import actual beatmap", () => Beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely());

            LoadSongSelect();

            AddUntilStep("wait for filtered", () => SongSelect.ChildrenOfType<BeatmapCarousel>().Single().FilterCount, () => Is.EqualTo(1));

            AddStep("enter gameplay", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () => Stack.CurrentScreen is Player);
            AddUntilStep("wait for fail", () => ((Player)Stack.CurrentScreen).GameplayState.HasFailed);

            AddStep("exit gameplay", () => Stack.CurrentScreen.Exit());

            AddUntilStep("wait for song select", () => Stack.CurrentScreen is Screens.Select.SongSelect);
            AddUntilStep("wait for filtered", () => SongSelect.ChildrenOfType<BeatmapCarousel>().Single().FilterCount, () => Is.EqualTo(2));
        }

        [Test]
        public void TestAutoplayShortcut()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);

            AddAssert("autoplay selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);

            AddUntilStep("wait for return to ss", () => SongSelect.IsCurrentScreen());

            AddAssert("no mods selected", () => SongSelect.Mods.Value.Count == 0);
        }

        [Test]
        public void TestAutoplayShortcutKeepsAutoplayIfSelectedAlready()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            ChangeMods(new OsuModAutoplay());

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);

            AddAssert("autoplay selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);

            AddUntilStep("wait for return to ss", () => SongSelect.IsCurrentScreen());

            AddAssert("autoplay still selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);
        }

        [Test]
        public void TestAutoplayShortcutReturnsInitialModsOnExit()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            ChangeMods(new OsuModRelax());

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);

            AddAssert("only autoplay selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);

            AddUntilStep("wait for return to ss", () => SongSelect.IsCurrentScreen());

            AddAssert("relax returned", () => SongSelect.Mods.Value.Single() is ModRelax);
        }

        [Test]
        public void TestModSelectCannotBeOpenedAfterConfirmingSelection()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            ChangeMods(new OsuModAutoplay());

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddStep("press F1", () => InputManager.PressKey(Key.F1));
            AddAssert("mod select not visible", () => this.ChildrenOfType<ModSelectOverlay>().Single().State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);
            AddAssert("osu! cookie visible", () => this.ChildrenOfType<OsuLogo>().Single().Alpha, () => Is.Not.Zero);
        }

        [Test]
        public void TestDropdownKeyboardNavigation()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();

            BeatmapInfo? firstBeatmap = null;
            AddStep("store first difficulty", () => firstBeatmap = Beatmap.Value.BeatmapInfo);

            AddStep("click sort dropdown", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<ShearedDropdown<SortMode>>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddStep("press up arrow", () => InputManager.Key(Key.Up));
            AddStep("press up arrow", () => InputManager.Key(Key.Up));

            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddAssert("sort mode is length", () => this.ChildrenOfType<ShearedDropdown<SortMode>>().Single().Current.Value, () => Is.EqualTo(SortMode.Length));
            AddAssert("beatmap not changed", () => Beatmap.Value.BeatmapInfo, () => Is.EqualTo(firstBeatmap));
        }

        #endregion

        #region Footer

        [Test]
        public void TestFooterMods()
        {
            LoadSongSelect();

            AddStep("one mod", () => SelectedMods.Value = new List<Mod> { new OsuModHidden() });
            AddStep("two mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock() });
            AddStep("three mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime() });
            AddStep("four mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime(), new OsuModClassic() });
            AddStep("five mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime(), new OsuModClassic(), new OsuModDifficultyAdjust() });

            AddStep("modified", () => SelectedMods.Value = new List<Mod> { new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + one", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + two", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + three",
                () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModClassic(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + four",
                () => SelectedMods.Value = new List<Mod>
                    { new OsuModHidden(), new OsuModHardRock(), new OsuModClassic(), new OsuModDifficultyAdjust(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });

            AddStep("clear mods", () => SelectedMods.Value = Array.Empty<Mod>());
            AddWaitStep("wait", 3);
            AddStep("one mod", () => SelectedMods.Value = new List<Mod> { new OsuModHidden() });

            AddStep("clear mods", () => SelectedMods.Value = Array.Empty<Mod>());
            AddWaitStep("wait", 3);
            AddStep("five mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime(), new OsuModClassic(), new OsuModDifficultyAdjust() });
        }

        [Test]
        public void TestFooterModOverlay()
        {
            LoadSongSelect();

            AddStep("Press F1", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<FooterButtonMods>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("Overlay visible", () => this.ChildrenOfType<ModSelectOverlay>().Single().State.Value == Visibility.Visible);
            AddStep("Hide", () => this.ChildrenOfType<ModSelectOverlay>().Single().Hide());
        }

        [Test]
        public void TestFooterRandom()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("press F2", () => InputManager.Key(Key.F2));
            AddAssert("next random invoked", () => nextRandomCalled && !previousRandomCalled);
        }

        [Test]
        public void TestFooterRandomViaMouse()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("click button", () =>
            {
                InputManager.MoveMouseTo(randomButton);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("next random invoked", () => nextRandomCalled && !previousRandomCalled);
        }

        [Test]
        public void TestFooterRewind()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("press Shift+F2", () =>
            {
                InputManager.PressKey(Key.LShift);
                InputManager.PressKey(Key.F2);
                InputManager.ReleaseKey(Key.F2);
                InputManager.ReleaseKey(Key.LShift);
            });

            AddAssert("previous random invoked", () => previousRandomCalled && !nextRandomCalled);
        }

        [Test]
        public void TestFooterRewindViaShiftMouseLeft()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("shift + click button", () =>
            {
                InputManager.PressKey(Key.LShift);
                InputManager.MoveMouseTo(randomButton);
                InputManager.Click(MouseButton.Left);
                InputManager.ReleaseKey(Key.LShift);
            });
            AddAssert("previous random invoked", () => previousRandomCalled && !nextRandomCalled);
        }

        [Test]
        public void TestFooterRewindViaMouseRight()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("right click button", () =>
            {
                InputManager.MoveMouseTo(randomButton);
                InputManager.Click(MouseButton.Right);
            });
            AddAssert("previous random invoked", () => previousRandomCalled && !nextRandomCalled);
        }

        private FooterButtonRandom randomButton => ScreenFooter.ChildrenOfType<FooterButtonRandom>().Single();

        [Test]
        public void TestFooterOptions()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);
            AddUntilStep("options enabled", () => this.ChildrenOfType<FooterButtonOptions>().Single().Enabled.Value);

            AddStep("click", () => this.ChildrenOfType<FooterButtonOptions>().Single().TriggerClick());
            AddUntilStep("popover displayed", () => this.ChildrenOfType<FooterButtonOptions.Popover>().Any(p => p.IsPresent));
        }

        [Test]
        public void TestSelectionChangedFromProtectedToNone()
        {
            ImportBeatmapForRuleset(0);
            AddStep("set protected on import", () => Realm.Write(r => r.All<BeatmapSetInfo>().First(s => !s.DeletePending).Protected = true));

            AddStep("selected protected", () => Beatmap.Value = Beatmaps.GetWorkingBeatmap(Beatmaps.GetAllUsableBeatmapSets().First(s => s.Protected).Beatmaps.First()));

            LoadSongSelect();

            AddUntilStep("beatmap deselected", () => Beatmap.IsDefault);
        }

        [Test]
        public void TestSelectionChangedFromProtectedToSomething()
        {
            ImportBeatmapForRuleset(0);
            AddStep("set protected on import", () => Realm.Write(r => r.All<BeatmapSetInfo>().First(s => !s.DeletePending).Protected = true));

            AddStep("selected protected", () => Beatmap.Value = Beatmaps.GetWorkingBeatmap(Beatmaps.GetAllUsableBeatmapSets().First(s => s.Protected).Beatmaps.First()));

            ImportBeatmapForRuleset(0);

            LoadSongSelect();

            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
            AddUntilStep("selection not protected", () => !Beatmap.Value.BeatmapSetInfo.Protected);
        }

        [Test]
        public void TestSelectAfterDeletion()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("delete all beatmaps", () => Beatmaps.Delete());
            AddUntilStep("beatmap not selected", () => Beatmap.IsDefault);

            AddStep("restore deleted", () => Beatmaps.UndeleteAll());
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
        }

        [Test]
        public void TestFooterOptionsState()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);

            AddUntilStep("options enabled", () => this.ChildrenOfType<FooterButtonOptions>().Single().Enabled.Value);
            AddStep("delete all beatmaps", () => Beatmaps.Delete());

            AddAssert("beatmap selected", () => !Beatmap.IsDefault);
            AddStep("select no beatmap", () => Beatmap.SetDefault());

            AddUntilStep("wait for no beatmap", () => Beatmap.IsDefault);
            AddAssert("options disabled", () => !this.ChildrenOfType<FooterButtonOptions>().Single().Enabled.Value);
        }

        /// <summary>
        /// tests that clicking the osu! logo immediately after selecting a different difficulty
        /// (before the selection debounce completes) starts the correct beatmap.
        /// this tests the fix for https://github.com/ppy/osu/issues/36074
        /// </summary>
        [Test]
        public void TestPlayCorrectBeatmapWhenSelectionNotFullyLoaded()
        {
            // import a beatmap set with multiple difficulties
            ImportBeatmapForRuleset(0);

            LoadSongSelect();

            // wait for initial beatmap to be selected
            AddUntilStep("wait for first beatmap selected", () => !Beatmap.IsDefault);

            BeatmapInfo? firstBeatmap = null;
            AddStep("store first difficulty", () => firstBeatmap = Beatmap.Value.BeatmapInfo);

            // start loading the first difficulty
            AddStep("click logo to start loading", () => this.ChildrenOfType<OsuLogo>().Single().TriggerClick());
            AddUntilStep("wait for player loader", () => Stack.CurrentScreen is PlayerLoader);

            // return to song select
            AddStep("press escape to return", () => InputManager.Key(Key.Escape));
            AddUntilStep("wait for return to song select", () => SongSelect.IsCurrentScreen());

            // press down and schedule logo click to happen shortly after (but before 150ms debounce)
            // this reproduces the race condition where Beatmap.Value hasn't updated yet
            AddStep("select next difficulty and click logo immediately", () =>
            {
                InputManager.Key(Key.Down);
                Schedule(() => this.ChildrenOfType<OsuLogo>().Single().TriggerClick());
            });

            AddUntilStep("wait for player loader", () => Stack.CurrentScreen is PlayerLoader);

            // verify we're loading the second difficulty, not the first
            // without the fix, this would fail because Beatmap.Value still has the old value
            AddAssert("player is loading second difficulty", () =>
                Beatmap.Value.BeatmapInfo.ID != firstBeatmap!.ID);

            AddUntilStep("wait for return to song select", () => SongSelect.IsCurrentScreen());

            Panel? targetPanel = null;
            BeatmapInfo? targetBeatmap = null;

            AddUntilStep("find another panel after returning from loader", () =>
            {
                targetPanel = Carousel.ChildrenOfType<Panel>().FirstOrDefault(panel =>
                    panel.IsPresent
                    && panel.Item?.IsVisible == true
                    && panel.Item.Model is GroupedBeatmap groupedBeatmap
                    && groupedBeatmap.Beatmap.ID != Beatmap.Value.BeatmapInfo.ID);

                return targetPanel != null;
            });
            AddStep("click another panel after returning from loader", () =>
            {
                targetBeatmap = ((GroupedBeatmap)targetPanel!.Item!.Model).Beatmap;
                InputManager.MoveMouseTo(targetPanel.TopLevelContent);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("selection works after returning from loader", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(targetBeatmap!.ID));
        }

        #endregion
    }
}
