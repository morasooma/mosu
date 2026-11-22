using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneFavouriteSettings : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OsuGame game = new OsuGame();
        protected override bool DisplayCursorForManualInput => false;
        private SettingsOverlay settings = null!;
        private OsuConfigManager config = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config) => this.config = config;

        [SetUpSteps]
        public void Setup()
        {
            AddStep("create settings", () =>
            {
                settings?.Hide();
                settings?.Expire();
                config.SetValue(OsuSetting.ForkFavouriteSettings, "[]");
                Add(settings = new SettingsOverlay());
                settings.Show();
            });
            AddUntilStep("main settings loaded", () => settings.SectionsReady);
        }

        [Test]
        public void TestFavouriteHeaderControlsHaveVisibleGaps()
        {
            FavouriteSettingsPanel panel = null!;
            SettingsButtonV2 add = null!;
            SettingsButtonV2 remove = null!;
            SettingsSearchTextBox search = null!;
            AddStep("open favourites", () => settings.ShowFavouriteSettings());
            AddUntilStep("panel ready", () => (panel = settings.ChildrenOfType<FavouriteSettingsPanel>().Single()).SectionsReady);
            AddStep("find header controls", () =>
            {
                add = panel.ChildrenOfType<SettingsButtonV2>().Single(b => b.Text == osu.Game.Localisation.FavouriteSettingsStrings.Add);
                remove = panel.ChildrenOfType<SettingsButtonV2>().Single(b => b.Text == osu.Game.Localisation.FavouriteSettingsStrings.Remove);
                search = panel.ChildrenOfType<SettingsSearchTextBox>().Single();
            });
            AddWaitStep("settle header layout", 10);
            AddAssert("search separated from add", () => add.ScreenSpaceDrawQuad.TopLeft.Y - search.ScreenSpaceDrawQuad.BottomLeft.Y >= 8);
            AddAssert("buttons share one row", () => System.Math.Abs(remove.ScreenSpaceDrawQuad.TopLeft.Y - add.ScreenSpaceDrawQuad.TopLeft.Y) < 1);
            AddAssert("buttons separated horizontally", () => remove.ScreenSpaceDrawQuad.TopLeft.X - add.ScreenSpaceDrawQuad.TopRight.X >= 6);
            AddAssert("compact button height", () => add.Height == 36 && remove.Height == 36);
            AddAssert("half-width buttons", () => add.DrawWidth < search.DrawWidth * 0.55f && remove.DrawWidth < search.DrawWidth * 0.55f);
            AddAssert("no description in header", () => !panel.SectionsContainer.ExpandableHeader!.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>()
                .Any(t => t.Text.Equals(osu.Game.Localisation.FavouriteSettingsStrings.Description)));
        }

        [Test]
        public void TestShortcutHintUsesRemappedBindingOnReopen()
        {
            FavouriteSettingsPanel panel = null!;
            string? previousKeys = null;
            var realm = Dependencies.Get<osu.Game.Database.RealmAccess>();
            var bindings = Dependencies.Get<osu.Game.Input.RealmKeyBindingStore>();
            AddStep("open favourites", () => settings.ShowFavouriteSettings());
            AddUntilStep("panel ready", () => (panel = settings.ChildrenOfType<FavouriteSettingsPanel>().Single()).SectionsReady);
            AddAssert("hint shows actual binding", () => panel.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().Any(t => t.Text.Equals(
                osu.Game.Localisation.FavouriteSettingsStrings.Shortcut(bindings.GetBindingsStringFor(GlobalAction.ToggleFavouriteSettings)))));
            AddStep("remap shortcut", () =>
            {
                panel.Hide();
                realm.Write(r =>
                {
                    var binding = r.All<RealmKeyBinding>().Single(b => b.RulesetName == null && b.ActionInt == (int)GlobalAction.ToggleFavouriteSettings);
                    previousKeys = binding.KeyCombination.ToString();
                    binding.KeyCombination = new osu.Framework.Input.Bindings.KeyCombination(osu.Framework.Input.Bindings.InputKey.F7);
                });
                settings.ShowFavouriteSettings();
            });
            AddUntilStep("panel reopened", () => panel.State.Value == Visibility.Visible);
            AddAssert("hint reflects remapped F7", () => panel.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().Any(t => t.Text.Equals(
                osu.Game.Localisation.FavouriteSettingsStrings.Shortcut(bindings.GetBindingsStringFor(GlobalAction.ToggleFavouriteSettings))) && t.Text.ToString().Contains("F7")));
            AddStep("restore binding", () => realm.Write(r => r.All<RealmKeyBinding>().Single(b => b.RulesetName == null
                && b.ActionInt == (int)GlobalAction.ToggleFavouriteSettings).KeyCombination = new osu.Framework.Input.Bindings.KeyCombination(previousKeys!)));
        }

        [Test]
        public void TestAiSwitchesAndMultipleSelectionsRemainIndependent()
        {
            SettingsItemV2[] rows = null!;
            bool[] original = null!;
            string[] ids = { "OsuSetting.ForkAiBackgroundBlur", "OsuSetting.ForkAiStillLife", "OsuSetting.GameplayCursorSize", "OsuSetting.ForkPracticeMode" };
            AddStep("enter selection", () => settings.BeginFavouriteSelection());
            AddStep("find four original controls", () =>
            {
                rows = ids.Select(id => settings.FavouriteEntries.First(e => e.Item.FavouriteId == id).Row).Cast<SettingsItemV2>().ToArray();
                original = new[] { config.Get<bool>(OsuSetting.ForkAiBackgroundBlur), config.Get<bool>(OsuSetting.ForkAiStillLife) };
            });
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                AddStep("scroll to chosen control", () => settings.SectionsContainer.ScrollTo(rows[index]));
                AddWaitStep("settle scroll", 20);
                AddStep("click to pin", () => { InputManager.MoveMouseTo(rows[index]); InputManager.Click(MouseButton.Left); });
                AddUntilStep("only requested control added", () => settings.Favourites!.Ids.SequenceEqual(ids.Take(index + 1)));
            }
            AddAssert("AI switches not changed", () => config.Get<bool>(OsuSetting.ForkAiBackgroundBlur) == original[0]
                && config.Get<bool>(OsuSetting.ForkAiStillLife) == original[1]);
            AddStep("finish selection", () => settings.FinishFavouriteSelection());
            AddUntilStep("all four controls in favourites", () => rows.All(r => r.FindClosestParent<FavouriteSettingsPanel>()?.State.Value == Visibility.Visible));
            AddWaitStep("settle favourite rows", 10);
            AddAssert("all four controls present", () => rows.All(r => r.IsPresent && r.DrawHeight > 0));
            AddAssert("rows do not overlap", () => rows.Zip(rows.Skip(1), (a, b) => b.ScreenSpaceDrawQuad.TopLeft.Y >= a.ScreenSpaceDrawQuad.BottomLeft.Y).All(v => v));
        }

        [Test]
        public void TestAddingSettingsClearsPreviousFavouriteSearch()
        {
            FavouriteSettingsPanel panel = null!;
            string[] ids = { "OsuSetting.ForkMenuLogo", "OsuSetting.NotifyOnFriendPresenceChange" };
            AddStep("open existing favourite", () =>
            {
                settings.Favourites!.Toggle(ids[0]);
                settings.ShowFavouriteSettings();
            });
            AddUntilStep("panel ready", () => (panel = settings.ChildrenOfType<FavouriteSettingsPanel>().Single()).SectionsReady);
            AddStep("search favourites", () => panel.ChildrenOfType<SettingsSearchTextBox>().Single().Current.Value = "logo");
            AddStep("add another setting", () =>
            {
                settings.BeginFavouriteSelection();
                settings.Favourites!.Toggle(ids[1]);
                settings.FinishFavouriteSelection();
            });
            AddUntilStep("panel reopened", () => panel.State.Value == Visibility.Visible);
            AddUntilStep("both favourites visible without stale filter", () => panel.ChildrenOfType<SettingsItemV2>()
                .Count(r => ids.Contains(r.FavouriteId) && r.IsPresent) == 2);
            AddAssert("search cleared after adding", () => panel.ChildrenOfType<SettingsSearchTextBox>().Single().Current.Value == string.Empty);
        }

        [Test]
        public void TestCompactLayoutAfterOriginalRowsWereLaidOut()
        {
            FavouriteSettingsPanel panel = null!;
            SettingsItemV2[] rows = null!;
            string[] ids = { "OsuSetting.ForkMenuLogo", "OsuSetting.ForkReplayRenderDebugTraceMode", "OsuSetting.NotifyOnFriendPresenceChange" };
            AddStep("find controls in distant sections", () => rows = ids.Select(id => settings.FavouriteEntries.First(e => e.Item.FavouriteId == id).Row).Cast<SettingsItemV2>().ToArray());
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                AddStep("lay out original section", () => settings.SectionsContainer.ScrollTo(rows[index]));
                AddUntilStep("original row initialized", () => rows[index].IsLoaded);
                AddWaitStep("settle original layout", 20);
            }
            AddAssert("original rows have nonzero flow offsets", () => rows.Any(r => r.Y > 100));
            AddStep("pin and open favourites", () =>
            {
                foreach (string id in ids)
                    settings.Favourites!.Toggle(id);
                settings.ShowFavouriteSettings();
            });
            AddUntilStep("all rows borrowed", () => rows.All(r => (panel = r.FindClosestParent<FavouriteSettingsPanel>()!)?.State.Value == Visibility.Visible));
            AddWaitStep("settle favourite layout", 20);
            AddAssert("borrowed rows start at host origin", () => rows.All(r => System.Math.Abs(r.Y) < 1));
            AddAssert("first setting visible without scrolling", () => rows[0].ScreenSpaceDrawQuad.TopLeft.Y >= panel.SectionsContainer.FixedHeader!.ScreenSpaceDrawQuad.BottomLeft.Y
                && rows[0].ScreenSpaceDrawQuad.BottomLeft.Y <= panel.ScreenSpaceDrawQuad.BottomLeft.Y);
            AddAssert("no giant gaps between favourites", () => rows.Zip(rows.Skip(1), (a, b) => b.ScreenSpaceDrawQuad.TopLeft.Y - a.ScreenSpaceDrawQuad.BottomLeft.Y)
                .All(gap => gap >= 0 && gap < 50));
        }

        [Test]
        public void TestPersistedMixedSettingsAfterSearchingOriginals()
        {
            FavouriteSettingsPanel panel = null!;
            SettingsItemV2[] rows = null!;
            string[] ids = { "ui:GraphicsAdapterStrings.Caption", "OsuSetting.ForkMenuLogo", "OsuSetting.ForkReplayRenderDebugTraceMode",
                "ui:ServerProfilesSettings:profileDropdown", "OsuSetting.NotifyOnFriendPresenceChange" };
            AddStep("restore mixed favourite list", () =>
            {
                config.SetValue(OsuSetting.ForkFavouriteSettings, System.Text.Json.JsonSerializer.Serialize(ids));
                settings.SectionsContainer.SearchTerm = "friend";
                settings.ShowFavouriteSettings();
            });
            AddUntilStep("favourite content loaded", () => (panel = settings.ChildrenOfType<FavouriteSettingsPanel>().Single()).SectionsReady);
            AddStep("find all saved controls", () => rows = ids.Select(id => panel.ChildrenOfType<SettingsItemV2>().Single(r => r.FavouriteId == id)).ToArray());
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                AddStep("scroll saved favourite into view", () => panel.SectionsContainer.ScrollTo(rows[index]));
                AddWaitStep("settle scrolling", 20);
                AddAssert("saved row visible", () => rows[index].IsPresent && rows[index].DrawHeight > 0
                    && rows[index].ScreenSpaceDrawQuad.TopLeft.Y >= panel.SectionsContainer.FixedHeader.ScreenSpaceDrawQuad.BottomLeft.Y - 1
                    && rows[index].ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(panel.ScreenSpaceDrawQuad.AABBFloat));
            }
        }

        [Test]
        public void TestBorrowedControlsRestoreAndPersistValues()
        {
            SettingsItemV2 row = null!;
            Drawable parent = null!;
            bool original = false;
            AddStep("pin practice mode", () =>
            {
                row = settings.ChildrenOfType<SettingsItemV2>().Single(i => i.FavouriteId == "OsuSetting.ForkPracticeMode");
                parent = row.Parent;
                original = config.Get<bool>(OsuSetting.ForkPracticeMode);
                settings.Favourites!.Toggle(row.FavouriteId!);
                settings.ShowFavouriteSettings();
            });
            AddUntilStep("row appears in favourites", () => row.FindClosestParent<FavouriteSettingsPanel>()?.State.Value == Visibility.Visible);
            AddStep("change favourite setting", () => ((FormCheckBox)row.Control).Current.Value = !original);
            AddAssert("real config changed", () => config.Get<bool>(OsuSetting.ForkPracticeMode) == !original);
            AddStep("close favourites", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().Hide());
            AddUntilStep("row restored to original parent", () => row.Parent == parent);
            AddAssert("value retained", () => ((FormCheckBox)row.Control).Current.Value == !original);
            for (int i = 0; i < 3; i++)
            {
                AddStep("open again", () => settings.ShowFavouriteSettings());
                AddUntilStep("same row reused", () => row.FindClosestParent<FavouriteSettingsPanel>()?.State.Value == Visibility.Visible);
                AddStep("close overlay", () => settings.Hide());
                AddUntilStep("restored after overlay close", () => row.Parent == parent);
            }
            AddStep("restore test value", () => config.SetValue(OsuSetting.ForkPracticeMode, original));
        }

        [Test]
        public void TestRemoveOnlyUnpinsAndRestoresSiblingOrder()
        {
            SettingsItemV2 row = null!;
            SettingsItemV2 other = null!;
            Drawable parent = null!;
            Drawable[] ordered = null!;
            bool original = false;
            AddStep("pin two neighbouring controls", () =>
            {
                row = settings.ChildrenOfType<SettingsItemV2>().Single(i => i.FavouriteId == "OsuSetting.ForkPracticeMode");
                other = settings.ChildrenOfType<SettingsItemV2>().Single(i => i.FavouriteId == "OsuSetting.ForkReduceVolumeOutsideGameplay");
                parent = row.Parent;
                var flow = (FillFlowContainer)parent;
                ordered = flow.Children.OrderBy(flow.GetLayoutPosition).ToArray();
                original = config.Get<bool>(OsuSetting.ForkPracticeMode);
                settings.Favourites!.Toggle(other.FavouriteId!);
                settings.Favourites.Toggle(row.FavouriteId!);
                settings.ShowFavouriteSettings();
            });
            AddUntilStep("both controls borrowed", () => row.FindClosestParent<FavouriteSettingsPanel>()?.State.Value == Visibility.Visible
                && other.FindClosestParent<FavouriteSettingsPanel>() != null);
            AddStep("enable removal", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().ChildrenOfType<SettingsButtonV2>()
                .Single(b => b.Text == osu.Game.Localisation.FavouriteSettingsStrings.Remove).TriggerClick());
            AddStep("scroll row into view", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().SectionsContainer.ScrollTo(row));
            AddWaitStep("settle layout", 20);
            AddStep("remove by clicking control", () => { InputManager.MoveMouseTo(row); InputManager.Click(MouseButton.Left); });
            AddUntilStep("unpinned and returned", () => !settings.Favourites!.Contains(row.FavouriteId!) && row.Parent == parent);
            AddAssert("value not reset", () => config.Get<bool>(OsuSetting.ForkPracticeMode) == original);
            AddStep("close panel", () => settings.Hide());
            AddUntilStep("both rows restored", () => other.Parent == parent);
            AddAssert("original visual order", () => ((FillFlowContainer)parent).Children.OrderBy(((FillFlowContainer)parent).GetLayoutPosition).SequenceEqual(ordered));
        }

        [Test]
        public void TestSelectionClickDoesNotChangeValueAndEscapeReturns()
        {
            SettingsItemV2 row = null!;
            bool original = false;
            AddStep("enter selection", () =>
            {
                row = settings.ChildrenOfType<SettingsItemV2>().Single(i => i.FavouriteId == "OsuSetting.ForkPracticeMode");
                original = config.Get<bool>(OsuSetting.ForkPracticeMode);
                settings.BeginFavouriteSelection();
                settings.SectionsContainer.ScrollTo(row);
            });
            AddWaitStep("settle layout", 20);
            AddStep("click checkbox in selection mode", () =>
            {
                InputManager.MoveMouseTo(row);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("setting pinned", () => settings.Favourites!.Contains(row.FavouriteId!));
            AddAssert("checkbox value unchanged", () => config.Get<bool>(OsuSetting.ForkPracticeMode) == original);
            AddStep("escape selection", () => InputManager.Key(Key.Escape));
            AddUntilStep("favourite panel opens", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().State.Value == Visibility.Visible);
            AddAssert("selection exited", () => !settings.SelectingFavourites);
        }

        [Test]
        public void TestNestedPerformanceControlAndSearch()
        {
            SettingsItemV2 row = null!;
            Drawable parent = null!;
            AddStep("load nested settings", () => settings.ShowFavouriteSettings());
            AddUntilStep("nested setting registered", () => settings.FavouriteEntries.Any(e => e.Item.FavouriteId == "OsuSetting.ForkSkinPerformanceFreezeAnimations"));
            AddStep("pin nested optimisation", () =>
            {
                var entry = settings.FavouriteEntries.First(e => e.Item.FavouriteId == "OsuSetting.ForkSkinPerformanceFreezeAnimations");
                row = (SettingsItemV2)entry.Row;
                parent = row.Parent;
                settings.Favourites!.Toggle(row.FavouriteId!);
                settings.ChildrenOfType<FavouriteSettingsPanel>().Single().Populate();
            });
            AddUntilStep("nested row in favourites", () => row.FindClosestParent<FavouriteSettingsPanel>()?.State.Value == Visibility.Visible);
            AddAssert("warning retained", () => row.Note.Value != null);
            AddStep("filter out", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().SectionsContainer.SearchTerm = "does-not-exist-12345");
            AddUntilStep("row filtered out", () => !row.MatchingFilter);
            AddStep("hide overlay", () => settings.Hide());
            AddUntilStep("nested row restored", () => row.Parent == parent && row.MatchingFilter);
        }

        [Test]
        public void TestActualDefaultHotkeyDoesNotTriggerRegularSettings()
        {
            AddStep("press Ctrl Alt O", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.AltLeft);
                InputManager.Key(Key.O);
                InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("favourites opened by real input", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().State.Value == Visibility.Visible);
            AddStep("press hotkey again", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.AltLeft);
                InputManager.Key(Key.O);
                InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("overlay closes", () => settings.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestHotkeyTogglesAlreadyOpenPanel()
        {
            AddStep("open via action", () => settings.ToggleFavouriteSettings());
            AddUntilStep("favourites visible", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().State.Value == Visibility.Visible);
            AddStep("toggle off", () => settings.ToggleFavouriteSettings());
            AddUntilStep("settings hidden", () => settings.State.Value == Visibility.Hidden);
            AddAssert("nested panel hidden too", () => settings.ChildrenOfType<FavouriteSettingsPanel>().Single().State.Value == Visibility.Hidden);
        }
    }
}
