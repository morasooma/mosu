// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Screens.SelectLegacy;
using osu.Game.Screens.SelectLegacy.Carousel;
using osu.Game.Tests.Visual;
using osu.Game.Tests.Resources;
using osuTK.Input;
using LegacyBeatmapCarousel = osu.Game.Screens.SelectLegacy.BeatmapCarousel;
using LegacySetPanel = osu.Game.Screens.SelectLegacy.Carousel.DrawableCarouselBeatmapSet;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneLegacySongSelect : ScreenTestScene
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            // BeatmapStore is only registered by the full game; song select requires it.
            // It is a drawable, so it must also be added to the tree to start tracking realm changes.
            Dependencies.CacheAs<BeatmapStore>(beatmapStore = new RealmDetachedBeatmapStore());
            Add(beatmapStore);
        }

        private BeatmapStore beatmapStore = null!;

        [Test]
        public void TestLegacyTextUsesSingleGlyphLayer()
        {
            StrokedLegacyText text = null!;

            AddStep("create legacy text", () => Add(text = new StrokedLegacyText { Text = "legacy score" }));
            AddUntilStep("wait for load", () => text.IsLoaded);
            AddAssert("single glyph layer", () => text.ChildrenOfType<OsuSpriteText>().Count(), () => Is.EqualTo(1));
            AddStep("remove legacy text", () => Remove(text, true));
        }

        [Test]
        public void TestLegacySongSelectLoads()
        {
            PlaySongSelect songSelect = null!;

            AddRepeatStep("import beatmaps", () => Dependencies.Get<BeatmapManager>().Import(TestResources.CreateTestBeatmapSetInfo()), 3);

            AddStep("push legacy song select", () => Stack.Push(songSelect = new PlaySongSelect()));
            AddUntilStep("wait for load", () => Stack.CurrentScreen is PlaySongSelect { IsLoaded: true });

            AddAssert("sort dropdown items all have text", () =>
            {
                var empty = emptySortDropdownItemValues(songSelect);
                if (empty.Count > 0)
                    throw new AssertionException("Items without text: " + string.Join(", ", empty));

                return true;
            });

            AddAssert("sort dropdown menu minimum width is fed from tab widths", () =>
            {
                var dropdown = songSelect.ChildrenOfType<OsuTabDropdown<SortMode>>().Single();
                return dropdown.MinimumMenuWidth > 100;
            });

            AddUntilStep("wait for set panels to load", () => songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.Carousel.SetPanelBackground>().Any());

            AddStep("select another map through carousel", () =>
            {
                var carousel = songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.BeatmapCarousel>().Single();
                var next = beatmapStore.GetBeatmapSets(null).SelectMany(s => s.Beatmaps)
                    .First(b => !b.Equals(carousel.SelectedBeatmapInfo));
                Assert.That(carousel.SelectBeatmap(next), Is.True);
            });
            AddUntilStep("selection round trip preserves carousel selection", () =>
            {
                var carousel = songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.BeatmapCarousel>().Single();
                return carousel.SelectedBeatmapInfo?.Equals(songSelect.Beatmap.Value.BeatmapInfo) == true;
            });

            AddStep("enable old preview layout", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true));
            AddAssert("legacy preview panels updated", () => songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.Carousel.SetPanelBackground>().Any());

            AddStep("disable old preview layout", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));

            AddStep("enable additional difficulty info", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkDifficultyAdditionalInfo, true));
            AddUntilStep("wait for panels to display additional info", () =>
                songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.Carousel.DrawableCarouselBeatmap>().Any(d => d.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("Combo:"))));

            AddStep("disable additional difficulty info", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkDifficultyAdditionalInfo, false));
        }

        [Test]
        public void TestRandomSelectionBringsDistantMapIntoViewOnFirstFrame()
        {
            PlaySongSelect songSelect = null!;
            LegacyBeatmapCarousel carousel = null!;
            LegacyBeatmapCarousel.CarouselScrollContainer scroll = null!;
            BeatmapInfo initial = null!;
            BeatmapInfo? random = null;

            const int set_count = 40;
            var sets = new BeatmapSetInfo[set_count];

            AddStep("import sorted beatmap sets", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.SongSelectSortingMode, SortMode.Title);
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.RandomSelectAlgorithm, RandomSelectAlgorithm.RandomPermutation);
                var manager = Dependencies.Get<BeatmapManager>();
                for (int i = 0; i < set_count; i++)
                {
                    sets[i] = TestResources.CreateTestBeatmapSetInfo(1);
                    sets[i].Beatmaps.Single().Metadata.Title = $"Random navigation {i:D2}";
                    manager.Import(sets[i]);
                }
            });

            AddStep("push legacy song select", () => Stack.Push(songSelect = new PlaySongSelect()));
            AddUntilStep("wait for legacy song select", () => Stack.CurrentScreen == songSelect && songSelect.IsLoaded);
            AddStep("get carousel", () =>
            {
                carousel = songSelect.ChildrenOfType<LegacyBeatmapCarousel>().Single();
                scroll = carousel.ChildrenOfType<LegacyBeatmapCarousel.CarouselScrollContainer>().Single();
            });
            AddUntilStep("wait for initial selection", () => carousel.BeatmapSetsLoaded && carousel.AllowSelection
                && songSelect.Beatmap.Value.BeatmapInfo.Equals(carousel.SelectedBeatmapInfo));
            AddStep("remember current selection", () => initial = songSelect.Beatmap.Value.BeatmapInfo);
            AddStep("press F2 with the viewport far from the destination", () =>
            {
                carousel.SelectionChanged += startFarFrom;
                InputManager.Key(Key.F2);
            });
            AddUntilStep("random beatmap published", () => random != null && songSelect.Beatmap.Value.BeatmapInfo.Equals(random));
            AddAssert("F2 selects a different beatmap", () => !initial.Equals(random));
            AddStep("press Shift+F2 to rewind a long jump", () =>
            {
                carousel.SelectionChanged += startFarFrom;
                InputManager.PressKey(Key.LShift);
                InputManager.Key(Key.F2);
                InputManager.ReleaseKey(Key.LShift);
            });
            AddUntilStep("previous beatmap published", () => songSelect.Beatmap.Value.BeatmapInfo.Equals(initial));

            void startFarFrom(BeatmapInfo? destination)
            {
                carousel.SelectionChanged -= startFarFrom;
                Assert.That(destination, Is.Not.Null);
                random ??= destination;

                // Position the viewport at the opposite end before the carousel applies its pending scroll.
                // This covers a long jump regardless of which set the randomiser picks.
                int setIndex = Array.FindIndex(sets, set => set.ID == destination!.BeatmapSet!.ID);
                Assert.That(setIndex, Is.GreaterThanOrEqualTo(0));
                scroll.ScrollTo(setIndex < set_count / 2 ? scroll.ScrollableExtent : 0, animated: false);
                carousel.OnUpdate += assertNearTarget;
            }

            void assertNearTarget(Drawable _)
            {
                carousel.OnUpdate -= assertNearTarget;
                Assert.That(Math.Abs(scroll.Target - scroll.Current), Is.LessThan(scroll.DisplayableContent / 2),
                    "Random navigation should load the destination viewport without traversing the intervening sets.");
                Assert.That(scroll.OfType<LegacySetPanel>().Any(panel => panel.Item?.State.Value == CarouselItemState.Selected), Is.True,
                    "The destination panel should be attached on the first carousel update.");
            }
        }

        private static System.Collections.Generic.List<SortMode> emptySortDropdownItemValues(PlaySongSelect songSelect)
        {
            var dropdown = songSelect.ChildrenOfType<OsuTabDropdown<SortMode>>().SingleOrDefault();
            if (dropdown == null)
                throw new AssertionException("Sort tab overflow dropdown not found.");

            var menu = (Menu)typeof(Dropdown<SortMode>)
                .GetField("Menu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .GetValue(dropdown)!;

            return menu.Items
                .OfType<DropdownMenuItem<SortMode>>()
                .Where(i => string.IsNullOrEmpty(i.Text.Value.ToString()))
                .Select(i => i.Value)
                .ToList();
        }
    }
}
