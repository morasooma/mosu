// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
using osu.Game.Tests.Visual;
using osu.Game.Tests.Resources;

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

            AddStep("create legacy text", () => Child = text = new StrokedLegacyText { Text = "legacy score" });
            AddUntilStep("wait for load", () => text.IsLoaded);
            AddAssert("single glyph layer", () => text.ChildrenOfType<OsuSpriteText>().Count(), () => Is.EqualTo(1));
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

            AddStep("enable old preview layout", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true));
            AddAssert("legacy preview panels updated", () => songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.Carousel.SetPanelBackground>().Any());

            AddStep("disable old preview layout", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));

            AddStep("enable additional difficulty info", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkDifficultyAdditionalInfo, true));
            AddUntilStep("wait for panels to display additional info", () =>
                songSelect.ChildrenOfType<osu.Game.Screens.SelectLegacy.Carousel.DrawableCarouselBeatmap>().Any(d => d.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("Combo:"))));

            AddStep("disable additional difficulty info", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkDifficultyAdditionalInfo, false));
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
