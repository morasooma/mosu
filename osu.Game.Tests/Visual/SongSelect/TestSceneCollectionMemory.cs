using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneCollectionMemory : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private OsuConfigManager config = null!;
        private CollectionDropdown dropdown = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(Realm);
            Dependencies.Cache(config = new OsuConfigManager(LocalStorage));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset settings and collections", () =>
            {
                Clear();
                config.SetValue(OsuSetting.SongSelectRememberCollection, true);
                config.SetValue(OsuSetting.SongSelectCollectionFilter, string.Empty);
                Realm.Write(r =>
                {
                    r.RemoveAll<BeatmapCollection>();
                    r.Add(new BeatmapCollection(name: "Test collection"));
                });
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestRestoreSavedCollection(bool remember)
        {
            AddStep("configure saved selection", () =>
            {
                config.SetValue(OsuSetting.SongSelectRememberCollection, remember);
                config.SetValue(OsuSetting.SongSelectCollectionFilter, collectionId);
            });
            createDropdown();
            AddAssert("saved selection restored only when enabled", () =>
                (dropdown.Current.Value.Collection?.ID.ToString() == collectionId) == remember);
            AddAssert("disabled memory clears saved selection", () =>
                config.Get<string>(OsuSetting.SongSelectCollectionFilter) == (remember ? collectionId : string.Empty));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestSelectionOnReopening(bool remember)
        {
            AddStep("configure memory", () => config.SetValue(OsuSetting.SongSelectRememberCollection, remember));
            createDropdown();
            selectCollection();
            AddAssert("only persist when enabled", () =>
                config.Get<string>(OsuSetting.SongSelectCollectionFilter) == (remember ? collectionId : string.Empty));
            createDropdown();
            AddAssert("new dropdown respects memory setting", () =>
                (dropdown.Current.Value.Collection?.ID.ToString() == collectionId) == remember);
        }

        [Test]
        public void TestToggleMemoryPreservesCurrentFilter()
        {
            createDropdown();
            selectCollection();
            AddStep("disable memory", () => config.SetValue(OsuSetting.SongSelectRememberCollection, false));
            AddAssert("saved selection cleared", () => config.Get<string>(OsuSetting.SongSelectCollectionFilter) == string.Empty);
            AddAssert("current collection remains selected", () => dropdown.Current.Value.Collection?.ID.ToString() == collectionId);
            AddStep("rename selected collection", () => Realm.Write(r => r.All<BeatmapCollection>().Single().Name = "Renamed"));
            AddUntilStep("renamed collection remains selected", () => dropdown.Current.Value.CollectionName == "Renamed");
            AddAssert("rename does not persist selection", () => config.Get<string>(OsuSetting.SongSelectCollectionFilter) == string.Empty);
            AddStep("enable memory", () => config.SetValue(OsuSetting.SongSelectRememberCollection, true));
            AddAssert("current collection saved again", () => config.Get<string>(OsuSetting.SongSelectCollectionFilter) == collectionId);
            createDropdown();
            AddAssert("selected collection restored", () => dropdown.Current.Value.Collection?.ID.ToString() == collectionId);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestManageAndAllBeatmaps(bool remember)
        {
            AddStep("configure memory", () => config.SetValue(OsuSetting.SongSelectRememberCollection, remember));
            createDropdown();
            selectCollection();
            AddStep("select manage collections", () => dropdown.Current.Value = dropdown.ItemSource.OfType<ManageCollectionsFilterMenuItem>().Single());
            AddAssert("manage preserves selection", () => dropdown.Current.Value.Collection?.ID.ToString() == collectionId);
            AddAssert("manage preserves persistence policy", () =>
                config.Get<string>(OsuSetting.SongSelectCollectionFilter) == (remember ? collectionId : string.Empty));
            AddStep("select all beatmaps", () => dropdown.Current.Value = dropdown.ItemSource.OfType<AllBeatmapsCollectionFilterMenuItem>().Single());
            AddAssert("saved selection cleared", () => config.Get<string>(OsuSetting.SongSelectCollectionFilter) == string.Empty);
            createDropdown();
            AddAssert("all beatmaps restored", () => dropdown.Current.Value is AllBeatmapsCollectionFilterMenuItem);
        }

        private string collectionId => Realm.Run(r => r.All<BeatmapCollection>().Single().ID.ToString());

        private void selectCollection() => AddStep("select collection", () => dropdown.Current.Value = dropdown.ItemSource.Single(item => item.Collection != null));

        private void createDropdown()
        {
            AddStep("open new dropdown", () =>
            {
                Clear();
                Add(dropdown = new CollectionDropdown { Width = 300 });
            });
            AddUntilStep("collection list loaded", () => dropdown.ItemSource.Count == 3);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            config?.Dispose();
        }
    }
}
