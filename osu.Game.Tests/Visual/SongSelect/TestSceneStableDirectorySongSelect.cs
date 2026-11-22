using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Database;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    [NonParallelizable]
    public partial class TestSceneStableDirectorySongSelect : SongSelectTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private StableBeatmapLoadResult stable = null!;

        private void loadStableDirectory()
        {
            AddStep("create isolated stable directory", () =>
            {
                Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
                var storage = LocalStorage.GetStorageForDirectory("stable-source");
                StableBeatmapProviderTest.WriteDatabase(storage.GetFullPath("osu!.db"), 20250107, DateTimeOffset.UtcNow.AddDays(-1));
                stable = new StableBeatmapProvider(Rulesets).GetBeatmaps(new StableStorage(storage.GetFullPath("."), null!));
                foreach (var map in stable.BeatmapSets.SelectMany(s => s.Beatmaps))
                {
                    string path = stable.BeatmapPaths[map.ID];
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, $"""
                        osu file format v14

                        [General]
                        Mode: 0

                        [Metadata]
                        Title: Test
                        Artist: Artist
                        Creator: Mapper
                        Version: Hard

                        [Difficulty]
                        HPDrainRate: 5
                        CircleSize: 4
                        OverallDifficulty: 5
                        ApproachRate: 7
                        SliderMultiplier: 1.4
                        SliderTickRate: 1

                        [TimingPoints]
                        0,{(map.OnlineID == 1 ? 300 : 250)},4,1,0,100,1,0

                        [HitObjects]
                        256,192,1000,1,0,0:0:0:0:
                        256,192,2000,1,0,0:0:0:0:
                        256,192,3000,1,0,0:0:0:0:
                        """);
                }

                // Feed the real store's incremental stable-directory publication path, without
                // requiring an OsuGame or touching the user's configured stable installation.
                typeof(RealmDetachedBeatmapStore).GetMethod("beginStableReplacement", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(DetachedBeatmapStore, new object[] { stable });
            });
            AddUntilStep("stable sets published", () => DetachedBeatmapStore.GetBeatmapSets(null).Count == 2);
            LoadSongSelect();
            AddUntilStep("stable map selected", () => StablePathManager.IsStableBeatmap(Beatmap.Value.BeatmapInfo.ID));
        }

        [Test]
        public void TestF2RefreshesDecodedStatistics()
        {
            loadStableDirectory();
            assertCurrentStatistics();
            for (int i = 0; i < 4; i++)
            {
                Guid previous = Guid.Empty;
                AddStep("remember selection", () => previous = Beatmap.Value.BeatmapInfo.ID);
                AddStep("press F2", () => InputManager.Key(Key.F2));
                AddUntilStep("random selects another stable map", () => Beatmap.Value.BeatmapInfo.ID != previous);
                assertCurrentStatistics();
            }
        }

        private void assertCurrentStatistics()
        {
            AddUntilStep("current statistics rendered", () =>
            {
                var info = Beatmap.Value.BeatmapInfo;
                var panel = SongSelect.ChildrenOfType<LegacyBeatmapInfoPanel>().SingleOrDefault(p => p.IsPresent);
                if (panel == null || info.TotalObjectCount != 3)
                    return false;
                string[] text = panel.ChildrenOfType<SpriteText>().Select(t => t.Text.ToString()).ToArray();
                int bpm = info.OnlineID == 1 ? 200 : 240;
                return text.Any(t => t.Contains($"BPM: {bpm}", StringComparison.Ordinal))
                       && text.Any(t => t.Contains("Objects: 3", StringComparison.Ordinal))
                       && text.Contains("Circles: 3   Sliders: 0   Spinners: 0");
            });
        }

        [Test]
        public void TestRecentlyPlayedUpdatesWithoutImport()
        {
            loadStableDirectory();
            SortAndGroupBy(SortMode.LastPlayed, GroupMode.LastPlayed);
            Guid playedId = Guid.Empty;
            AddStep("mark previously unplayed stable map through gameplay path", () =>
            {
                var map = stable.BeatmapSets.SelectMany(s => s.Beatmaps).Single(b => b.LastPlayed == null);
                playedId = map.ID;
                Beatmaps.MarkPlayedAsync(map).GetAwaiter().GetResult();
            });
            AddUntilStep("store publishes updated timestamp", () => DetachedBeatmapStore.GetBeatmapSets(null)
                .SelectMany(s => s.Beatmaps).Single(b => b.ID == playedId).LastPlayed > DateTimeOffset.UtcNow.AddMinutes(-1));
            AddUntilStep("carousel receives history update", () => !Carousel.IsFiltering && carouselBeatmap(playedId).LastPlayed > DateTimeOffset.UtcNow.AddMinutes(-1));
            AddAssert("played map leaves never-played group", () =>
                Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single().GroupItems
                    .Where(g => g.Value.Any(i => i.Model is GroupedBeatmap b && b.Beatmap.ID == playedId))
                    .All(g => g.Key.Order != int.MaxValue));
            AddAssert("history survives client restart", () =>
            {
                var map = new BeatmapInfo { ID = playedId };
                new StableBeatmapHistory(LocalStorage).Apply(map);
                return map.LastPlayed > DateTimeOffset.UtcNow.AddMinutes(-1);
            });
            AddAssert("stable maps were never imported", () => stable.BeatmapPaths.Keys
                .All(id => Realm.Run(r => r.Find<BeatmapInfo>(id) == null)));
            AddStep("mark unplayed", () => Beatmaps.MarkNotPlayed(carouselBeatmap(playedId)));
            AddUntilStep("carousel returns map to unplayed", () => !Carousel.IsFiltering && carouselBeatmap(playedId).LastPlayed == null);
        }

        [Test]
        public void TestImportedHistoryStillUsesRealm()
        {
            BeatmapInfo map = null!;
            AddStep("import ordinary map", () =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(1, new[] { Rulesets.GetRuleset(0)! });
                Beatmaps.Import(set);
                map = set.Beatmaps.Single();
            });
            AddStep("mark imported map played", () => Beatmaps.MarkPlayed(map));
            AddAssert("timestamp stored in Realm", () => Realm.Run(r => r.Find<BeatmapInfo>(map.ID)!.LastPlayed) != null);
            AddStep("mark imported map unplayed", () => Beatmaps.MarkNotPlayed(map));
            AddAssert("timestamp cleared in Realm", () => Realm.Run(r => r.Find<BeatmapInfo>(map.ID)!.LastPlayed) == null);
            System.Threading.Tasks.Task pending = null!;
            AddStep("mark played from gameplay", () => pending = Beatmaps.MarkPlayedAsync(map));
            AddUntilStep("async write completed", () => pending.IsCompletedSuccessfully);
            AddAssert("gameplay timestamp stored in Realm", () => Realm.Run(r => r.Find<BeatmapInfo>(map.ID)!.LastPlayed) != null);
        }

        private BeatmapInfo carouselBeatmap(Guid id) => Carousel.GetCarouselItems()!.Select(i => i.Model)
            .OfType<GroupedBeatmap>().First(b => b.Beatmap.ID == id).Beatmap;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
        }
    }
}
