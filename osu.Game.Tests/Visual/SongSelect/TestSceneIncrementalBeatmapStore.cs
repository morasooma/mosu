using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneIncrementalBeatmapStore : SongSelectTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [Test]
        public void TestInsertModifyDeletePreservesUnchangedInstances()
        {
            Guid firstId = Guid.NewGuid();
            Guid secondId = Guid.NewGuid();
            BeatmapSetInfo? first = null;
            AddUntilStep("store ready", () => DetachedBeatmapStore.IsLoaded);
            AddStep("insert first set", () => Realm.Write(r => r.Add(new BeatmapSetInfo { ID = firstId })));
            AddUntilStep("first visible", () => DetachedBeatmapStore.GetBeatmapSets(null).Any(s => s.ID == firstId));
            AddStep("remember first instance", () => first = DetachedBeatmapStore.GetBeatmapSets(null).Single(s => s.ID == firstId));
            AddStep("insert second set", () => Realm.Write(r => r.Add(new BeatmapSetInfo { ID = secondId })));
            AddUntilStep("second visible", () => DetachedBeatmapStore.GetBeatmapSets(null).Any(s => s.ID == secondId));
            AddAssert("first not re-cloned", () => ReferenceEquals(first, DetachedBeatmapStore.GetBeatmapSets(null).Single(s => s.ID == firstId)));
            AddStep("modify second", () => Realm.Write(r => r.Find<BeatmapSetInfo>(secondId)!.OnlineID = 123));
            AddUntilStep("modification visible", () => DetachedBeatmapStore.GetBeatmapSets(null).Single(s => s.ID == secondId).OnlineID == 123);
            AddStep("delete second", () => Realm.Write(r => r.Find<BeatmapSetInfo>(secondId)!.DeletePending = true));
            AddUntilStep("second removed", () => DetachedBeatmapStore.GetBeatmapSets(null).All(s => s.ID != secondId));
            AddAssert("first still same instance", () => ReferenceEquals(first, DetachedBeatmapStore.GetBeatmapSets(null).Single(s => s.ID == firstId)));
        }
    }
}
