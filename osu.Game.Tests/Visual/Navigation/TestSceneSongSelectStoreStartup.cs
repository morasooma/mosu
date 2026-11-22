using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;
using osu.Game.Tests.Database;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Navigation
{
    [HeadlessTest]
    public partial class TestSceneSongSelectStoreStartup : OsuGameTestScene
    {
        private SlowStableGame slowGame => (SlowStableGame)Game;
        private bool enableStableAtStartup;

        protected override TestOsuGame CreateTestGame()
        {
            var storage = LocalStorage.GetStorageForDirectory("stable-source");
            StableBeatmapProviderTest.WriteDatabase(storage.GetFullPath("osu!.db"), 20250107, DateTimeOffset.UtcNow);
            return new SlowStableGame(LocalStorage, API, new StableStorage(storage.GetFullPath("."), null!), enableStableAtStartup);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSlowStableScanDoesNotBlockLocalCarousel(bool keepStableEnabled)
        {
            enableStableAtStartup = true;
            AddUntilStep("stable scan has started", () => slowGame.ScanStarted.IsSet);
            AddUntilStep("store loads without waiting for stable disk", () => Game.Dependencies.Get<BeatmapStore>().IsLoaded);
            AddStep("import local mania map", () =>
            {
                var ruleset = Game.Dependencies.Get<RulesetStore>().GetRuleset(3)!;
                Game.Ruleset.Value = ruleset;
                Game.BeatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(1, new[] { ruleset }));
            });
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("local carousel receives maps while scan is blocked", () => Game.ChildrenOfType<BeatmapCarousel>().SingleOrDefault()
                is { IsLoaded: true, IsFiltering: false, MatchedBeatmapsCount: > 0 });
            AddStep("change stable setting during scan", () => Game.LocalConfig.SetValue(OsuSetting.ForkUseStableDirectoryDirectly, keepStableEnabled));
            AddStep("release stable disk", () => slowGame.ReleaseScan.Set());
            AddUntilStep("stable source reflects latest setting", () => Game.Dependencies.Get<BeatmapStore>().GetBeatmapSets(null).Count == (keepStableEnabled ? 3 : 1));
            AddWaitStep("allow cancelled scan completion to arrive", 10);
            AddAssert("cancelled scan never restores disabled maps", () => Game.Dependencies.Get<BeatmapStore>().GetBeatmapSets(null).Count == (keepStableEnabled ? 3 : 1));
        }

        [Test]
        public void TestDisabledStableStartupLoadsLocalCarousel()
        {
            enableStableAtStartup = false;
            AddUntilStep("store loads with stable disabled", () => Game.Dependencies.Get<BeatmapStore>().IsLoaded);
            AddStep("import local mania map", () =>
            {
                var ruleset = Game.Dependencies.Get<RulesetStore>().GetRuleset(3)!;
                Game.Ruleset.Value = ruleset;
                Game.BeatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(1, new[] { ruleset }));
            });
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("local carousel receives maps", () => Game.ChildrenOfType<BeatmapCarousel>().SingleOrDefault()
                is { IsLoaded: true, IsFiltering: false, MatchedBeatmapsCount: > 0 });
            AddAssert("disabled stable never touches the install", () => !slowGame.ScanStarted.IsSet);
        }

        public override void TearDownSteps()
        {
            AddStep("release any pending stable scan", () => slowGame.ReleaseScan.Set());
            base.TearDownSteps();
        }

        private partial class SlowStableGame : TestOsuGame
        {
            private readonly StableStorage stableStorage;
            private readonly bool enableStable;
            public readonly ManualResetEventSlim ScanStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseScan = new ManualResetEventSlim();

            public SlowStableGame(osu.Framework.Platform.Storage storage, osu.Game.Online.API.IAPIProvider api, StableStorage stableStorage, bool enableStable)
                : base(storage, api)
            {
                this.stableStorage = stableStorage;
                this.enableStable = enableStable;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                LocalConfig.SetValue(OsuSetting.ForkUseStableDirectoryDirectly, enableStable);
            }

            public override StableStorage GetStorageForStableInstall()
            {
                ScanStarted.Set();
                if (!ReleaseScan.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("The test did not release the simulated slow stable disk.");
                return stableStorage;
            }
        }
    }
}
