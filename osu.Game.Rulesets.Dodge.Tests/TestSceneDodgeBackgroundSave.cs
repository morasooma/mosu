using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public partial class TestSceneDodgeBackgroundSave : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;
        protected override Ruleset CreateRuleset() => new DodgeRuleset();

        private RealmRulesetStore rulesets = null!;
        private BeatmapManager manager = null!;
        private OsuConfigManager config = null!;
        private BeatmapDifficultyCache difficulty = null!;
        private BeatmapUpdater updater = null!;
        private BlockingCache cache = null!;
        private WorkingBeatmap working = null!;
        private int notifications;
        private bool callbackOutsideTransaction;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            rulesets = new RealmRulesetStore(Realm, LocalStorage);
            manager = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default);
            config = new OsuConfigManager(LocalStorage);
            config.SetValue(OsuSetting.ForkBackgroundCalculationDutyPercent, 100);
            difficulty = new BeatmapDifficultyCache();
            cache = new BlockingCache(manager);
            updater = new BeatmapUpdater(cache, difficulty, API, LocalStorage, config)
            {
                PostNotification = _ => Interlocked.Increment(ref notifications)
            };
        }

        [Test]
        public void TestSaveReturnsWhilePreviousCalculationIsBlocked()
        {
            AddStep("create persisted dodge map", () =>
            {
                var source = new TestBeatmap(rulesets.GetRuleset(DodgeRuleset.ONLINE_ID)!, false);
                var persistedSource = new TestBeatmap(rulesets.GetRuleset(DodgeRuleset.ONLINE_ID)!, false);
                var set = manager.Import(persistedSource.BeatmapInfo.BeatmapSet!)!.Value.Detach();
                working = manager.CreateNewDifficulty(set, new TestWorkingBeatmap(source), source.BeatmapInfo.Ruleset);
                manager.ProcessBeatmap = (saved, scope) =>
                {
                    callbackOutsideTransaction = !saved.Realm!.IsInTransaction;
                    updater.Queue(saved.ToLive(Realm), scope);
                };
            });
            AddStep("first save queues calculation", () => saveBullet(1000));
            AddUntilStep("calculation worker blocked", () => cache.Started.IsSet);
            AddStep("second save does not wait for worker", () =>
            {
                var timer = Stopwatch.StartNew();
                saveBullet(2000);
                Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
                Assert.That(cache.Released.IsSet, Is.False);
                Assert.That(callbackOutsideTransaction, Is.True);
                Assert.That(Volatile.Read(ref notifications), Is.EqualTo(1));
                TestContext.WriteLine($"Dodge save with a blocked calculation: {timer.Elapsed.TotalMilliseconds:F1} ms");
            });
            AddStep("release calculation", () => cache.Released.Set());
            AddUntilStep("latest statistics persisted", () => Realm.Run(r =>
            {
                var info = r.Find<BeatmapInfo>(working.BeatmapInfo.ID);
                return info?.TotalObjectCount == 2;
            }));
            AddAssert("both bullets reload", () =>
                manager.GetWorkingBeatmap(working.BeatmapInfo, true).Beatmap.HitObjects.OfType<DodgeBullet>().Count() == 2);
        }

        private void saveBullet(double startTime)
        {
            var content = (Beatmap<DodgeHitObject>)working.GetPlayableBeatmap(working.BeatmapInfo.Ruleset);
            content.HitObjects.Add(new DodgeBullet
            {
                StartTime = startTime,
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(300, 200)
            });
            manager.Save(working.BeatmapInfo, content);
            working = manager.GetWorkingBeatmap(working.BeatmapInfo, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            cache?.Released.Set();
            updater?.Dispose();
            difficulty?.Dispose();
            config?.Dispose();
            rulesets?.Dispose();
            base.Dispose(isDisposing);
        }

        private sealed class BlockingCache : IWorkingBeatmapCache
        {
            private readonly IWorkingBeatmapCache inner;
            public readonly ManualResetEventSlim Started = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Released = new ManualResetEventSlim();

            public BlockingCache(IWorkingBeatmapCache inner) => this.inner = inner;

            public WorkingBeatmap GetWorkingBeatmap(BeatmapInfo? info) => inner.GetWorkingBeatmap(info);

            public WorkingBeatmap GetWorkingBeatmapForCalculation(BeatmapInfo info)
            {
                var result = inner.GetWorkingBeatmapForCalculation(info);
                Started.Set();
                if (!Released.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Dodge test calculation was not released.");
                return result;
            }

            public void Invalidate(BeatmapSetInfo info) => inner.Invalidate(info);
            public void Invalidate(BeatmapInfo info) => inner.Invalidate(info);
        }
    }
}
