// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Beatmaps
{
    [HeadlessTest]
    public partial class TestSceneBeatmapDifficultyCache : OsuTestScene
    {
        public const double BASE_STARS = 5.55;

        private static readonly Guid guid = Guid.NewGuid();

        private BeatmapSetInfo importedSet;

        private TestBeatmapDifficultyCache difficultyCache;

        private IBindable<StarDifficulty> starDifficultyBindable;
        private CancellationTokenSource previewCancellation;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; }

        [Resolved]
        private BeatmapDifficultyCache actualDifficultyCache { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuGameBase osu)
        {
            importedSet = BeatmapImportHelper.LoadQuickOszIntoOsu(osu).GetResultSafely();
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setup difficulty cache", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();

                Child = difficultyCache = new TestBeatmapDifficultyCache();

                starDifficultyBindable = difficultyCache.GetBindableDifficulty(importedSet.Beatmaps.First());
            });

            AddUntilStep($"star difficulty -> {BASE_STARS}", () => starDifficultyBindable.Value.Stars == BASE_STARS);
        }

        [TearDownSteps]
        public void TearDownPreviewRequests() => AddStep("cancel preview requests", () =>
        {
            previewCancellation?.Cancel();
            previewCancellation?.Dispose();
            previewCancellation = null;
        });

        // This probe calls the production ComputeValueAsync (decode, playable conversion, difficulty,
        // perfect-score PP and counts). The gate controls request order, not the calculation result.
        [TestCase(false)]
        [TestCase(true)]
        public void TestSongSelectVisibleCalculationMatchesRealCalculator(bool performance)
        {
            BeatmapInfo map = null;
            Task<StarDifficulty?> reference = null;
            Task<StarDifficulty?> visible = null;
            var mods = new Mod[] { new OsuModHidden(), new OsuModDoubleTime() };
            AddStep("calculate ordinary full reference", () =>
            {
                map = importedSet.Beatmaps.First();
                actualDifficultyCache.Clear();
                reference = actualDifficultyCache.GetDifficultyAsync(map, map.Ruleset, mods);
            });
            AddUntilStep("reference complete", () => reference.IsCompletedSuccessfully && reference.GetResultSafely()?.PerformanceAttributes != null);
            AddStep("cold visible calculation", () =>
            {
                actualDifficultyCache.Clear();
                visible = actualDifficultyCache.GetSongSelectDifficultyAsync(map, map.Ruleset, mods, performance, CancellationToken.None);
            });
            AddUntilStep("visible complete", () => visible.IsCompletedSuccessfully && visible.GetResultSafely() != null);
            AddAssert("same exact stars", () => visible.GetResultSafely().Value.Stars == reference.GetResultSafely().Value.Stars);
            AddAssert("same perfect-score PP when requested", () => !performance
                || visible.GetResultSafely().Value.PerformanceAttributes.Total == reference.GetResultSafely().Value.PerformanceAttributes.Total);
            AddAssert("preview does not retain PP", () => performance || visible.GetResultSafely().Value.PerformanceAttributes == null);
        }

        [TestCase("full-first")]
        [TestCase("preview-first")]
        [TestCase("no-overlap")]
        public void TestRealDifficultyJoinWorkload(string order)
        {
            RealDifficultyProbe probe = null;
            BeatmapInfo map = null;
            Task<StarDifficulty?> full = null;
            IBindable<StarDifficulty> preview = null;
            long allocatedBefore = 0;
            long elapsedStart = 0;
            int gen2Before = 0;
            double fullMilliseconds = 0;
            CancellationTokenSource owner = null;

            AddStep("install real calculation probe", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Child = probe = new RealDifficultyProbe();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                previewCancellation = new CancellationTokenSource();
                owner = new CancellationTokenSource();
                allocatedBefore = GC.GetTotalAllocatedBytes(true);
                gen2Before = GC.CollectionCount(2);
                elapsedStart = Stopwatch.GetTimestamp();

                if (order == "full-first")
                    full = probe.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token);
                else
                    preview = probe.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false);
            });
            AddUntilStep("first real worker entered", () => order == "full-first" ? probe.FullStarts == 1 : probe.PreviewStarts == 1);
            AddStep("request second consumer", () =>
            {
                if (order == "full-first")
                    preview = probe.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false);
                else if (order == "preview-first")
                    full = probe.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token);
                // no-overlap requests full only after preview has completed below.
                probe.Release();
            });
            if (order == "no-overlap")
            {
                AddUntilStep("first real preview completed", () => preview.Value.DifficultyAttributes != null);
                AddStep("request non-overlapping full", () => full = probe.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token));
            }
            AddUntilStep("real full and preview finished", () => full?.IsCompleted == true && preview?.Value.DifficultyAttributes != null);
            AddStep("record real result and allocation", () =>
            {
                var result = full.GetResultSafely();
                Assert.That(result?.DifficultyAttributes, Is.Not.Null);
                Assert.That(result?.PerformanceAttributes, Is.Not.Null);
                Assert.That(result?.CountStatistics, Is.Not.Null);
                Assert.That(preview.Value.Stars, Is.EqualTo(result.Value.Stars).Within(0.000001));
                Assert.That(preview.Value.MaxCombo, Is.EqualTo(result.Value.MaxCombo));
                fullMilliseconds = probe.FullElapsedMilliseconds;
                TestContext.Progress.WriteLine($"REAL-JOIN order={order} fullStarts={probe.FullStarts} previewStarts={probe.PreviewStarts} " +
                    $"allocatedMiB={(GC.GetTotalAllocatedBytes(true) - allocatedBefore) / 1048576.0:F3} " +
                    $"gen2={GC.CollectionCount(2) - gen2Before} wallMs={Stopwatch.GetElapsedTime(elapsedStart).TotalMilliseconds:F2} " +
                    $"fullWorkerMs={fullMilliseconds:F2} stars={result.Value.Stars:F6} combo={result.Value.MaxCombo}");
                owner.Dispose();
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMatchingFullConsumersShareOneCalculation(bool cancelSecond)
        {
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var secondOwner = new CancellationTokenSource();
            Task<StarDifficulty?> first = null;
            Task<StarDifficulty?> second = null;
            int starts = 0;
            AddStep("start matching full owner", () =>
            {
                var map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = _ =>
                {
                    Interlocked.Increment(ref starts);
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("full gate timed out");
                    return new StarDifficulty(6.5, 123);
                };
                first = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
                second = difficultyCache.GetDifficultyAsync(map, map.Ruleset, cancellationToken: secondOwner.Token);
            });
            AddUntilStep("owner running", () => entered.IsSet);
            AddStep("cancel only second if requested", () => { if (cancelSecond) secondOwner.Cancel(); });
            AddAssert("only one calculation admitted", () => starts, () => Is.EqualTo(1));
            AddStep("release calculation", release.Set);
            AddUntilStep("both consumers terminal", () => first.IsCompleted && second.IsCompleted);
            AddAssert("first result not poisoned", () => first.GetResultSafely()?.MaxCombo, () => Is.EqualTo(123));
            AddAssert("independent second lifetime", () => cancelSecond ? second.IsCanceled : second.GetResultSafely()?.MaxCombo == 123);
            AddStep("dispose gates", () => { entered.Dispose(); release.Dispose(); secondOwner.Dispose(); });
        }

        [Test]
        public void TestImmediateFullDoesNotWaitForMatchingDebounce()
        {
            var owner = new CancellationTokenSource();
            Task<StarDifficulty?> delayed = null;
            Task<StarDifficulty?> immediate = null;
            BeatmapInfo map = null;
            int calculations = 0;
            AddStep("start delayed full", () =>
            {
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeDifficulty = _ => { Interlocked.Increment(ref calculations); return new StarDifficulty(6.5, 123); };
                delayed = difficultyCache.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token, computationDelay: 5000);
            });
            AddStep("request immediate same-key full", () => immediate = difficultyCache.GetDifficultyAsync(map, map.Ruleset));
            AddUntilStep("immediate completed", () => immediate.IsCompleted);
            AddAssert("debounce still pending", () => !delayed.IsCompleted);
            AddAssert("one real calculation", () => calculations, () => Is.EqualTo(1));
            AddStep("cancel delayed admission", owner.Cancel);
            AddUntilStep("delayed canceled", () => delayed.IsCanceled);
            AddAssert("immediate result remains", () => difficultyCache.HasCachedResult(map));
            AddStep("dispose owner", owner.Dispose);
        }

        [Test]
        public void TestFullJoinerRetriesAfterCooperativeOwnerCancellation()
        {
            RealDifficultyProbe probe = null;
            BeatmapInfo map = null;
            var owner = new CancellationTokenSource();
            Task<StarDifficulty?> first = null;
            Task<StarDifficulty?> second = null;
            AddStep("load cancellable full probe", () => Add(probe = new RealDifficultyProbe()));
            AddUntilStep("probe ready", () => probe.IsLoaded);
            AddStep("start two equal full consumers", () =>
            {
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                first = probe.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token);
                second = probe.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("one worker admitted", () => probe.FullStarts == 1);
            AddStep("cancel first only", owner.Cancel);
            AddUntilStep("owner canceled, independent retry admitted", () => first.IsCanceled && probe.FullStarts == 2);
            AddAssert("survivor still waiting", () => !second.IsCompleted);
            AddStep("release retry", () => probe.Release());
            AddUntilStep("survivor computed", () => second.IsCompleted);
            AddAssert("survivor result available", () => second.GetResultSafely() is { DifficultyAttributes: not null, PerformanceAttributes: not null });
            AddAssert("survivor owns published cache", () => probe.HasHeavy(map));
            AddStep("dispose owner", owner.Dispose);
        }

        [TestCase("map")]
        [TestCase("all")]
        [TestCase("clear")]
        public void TestInvalidationSuppressesAllMatchingFullWaiters(string action)
        {
            RealDifficultyProbe probe = null;
            BeatmapInfo map = null;
            Task<StarDifficulty?> first = null;
            Task<StarDifficulty?> second = null;
            AddStep("load gated full probe", () => Add(probe = new RealDifficultyProbe()));
            AddUntilStep("probe ready", () => probe.IsLoaded);
            AddStep("start two matching full consumers", () =>
            {
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                first = probe.GetDifficultyAsync(map, map.Ruleset);
                second = probe.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("only one worker admitted", () => probe.FullStarts == 1);
            AddStep("invalidate and release", () =>
            {
                if (action == "clear") probe.Clear();
                else if (action == "all") probe.InvalidateAll();
                else probe.Invalidate(map, map);
                probe.Release();
            });
            AddUntilStep("both obsolete waiters terminal", () => first.IsCompleted && second.IsCompleted);
            AddAssert("neither waiter gets obsolete result", () => first.GetResultSafely() == null && second.GetResultSafely() == null);
            AddAssert("no old graph published", () => !probe.HasHeavy(map) && !probe.HasLight(map));
            AddStep("new full admitted", () => first = probe.GetDifficultyAsync(map, map.Ruleset));
            AddUntilStep("new full completes", () => first.IsCompleted);
            AddAssert("new full succeeds", () => first.GetResultSafely() is { DifficultyAttributes: not null, PerformanceAttributes: not null });
        }

        [Test]
        public void TestReplacedCanceledOwnerWaiterCannotRestartAfterInvalidation()
        {
            var owner = new CancellationTokenSource();
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            Task<StarDifficulty?> old = null;
            Task<StarDifficulty?> oldWaiter = null;
            Task<StarDifficulty?> replacement = null;
            BeatmapInfo map = null;
            int starts = 0;
            AddStep("start old owner and independent waiter", () =>
            {
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = _ =>
                {
                    Interlocked.Increment(ref starts);
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("orphan gate timeout");
                    return new StarDifficulty(6.5, 123);
                };
                old = difficultyCache.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token);
                oldWaiter = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("old owner in worker", () => entered.IsSet);
            AddStep("cancel old and replace dictionary slot", () =>
            {
                owner.Cancel();
                replacement = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("replacement in worker", () => starts == 2);
            AddStep("invalidate all active generations", () => { difficultyCache.Invalidate(map, map); release.Set(); });
            AddUntilStep("all consumers terminal", () => old.IsCompleted && oldWaiter.IsCompleted && replacement.IsCompleted);
            AddAssert("orphaned waiter and replacement suppressed", () => oldWaiter.GetResultSafely() == null && replacement.GetResultSafely() == null);
            AddAssert("no old cache republished", () => !difficultyCache.HasHeavy(map) && !difficultyCache.HasLight(map));
            AddAssert("no accidental retry", () => starts, () => Is.EqualTo(2));
            AddStep("dispose owner and gates", () => { owner.Dispose(); entered.Dispose(); release.Dispose(); });
        }

        [Test]
        public void TestPreviewRechecksFullAfterAdmissionDelay()
        {
            BeatmapInfo map = null;
            Task<StarDifficulty?> full = null;
            IBindable<StarDifficulty> preview = null;
            var fullStarted = new ManualResetEventSlim();
            var releaseFull = new ManualResetEventSlim();
            int previewCalculations = 0;

            AddStep("request delayed preview then matching full", () =>
            {
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                previewCancellation = new CancellationTokenSource();
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previewCalculations);
                        return new StarDifficulty(2, 3);
                    }
                    fullStarted.Set();
                    if (!releaseFull.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("full release timed out");
                    return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.25, MaxCombo = 123 },
                        new osu.Game.Rulesets.Difficulty.PerformanceAttributes());
                };
                preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, computationDelay: 100, calculatePerformance: false);
                full = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("full worker entered", () => fullStarted.IsSet);
            AddWaitStep("preview admission elapsed with full still pending", 5);
            AddAssert("preview did not duplicate conversion", () => previewCalculations, () => Is.Zero);
            AddStep("finish full", () => releaseFull.Set());
            AddUntilStep("preview receives full attributes", () => preview.Value.DifficultyAttributes != null && full.IsCompleted);
            AddAssert("matching combo delivered", () => preview.Value.MaxCombo, () => Is.EqualTo(123));
            AddStep("dispose gates", () =>
            {
                fullStarted.Dispose();
                releaseFull.Dispose();
            });
        }

        [Test]
        public void TestPreviewJoinsOnlyMatchingPendingFullAndSurvivesOwnerCancellation()
        {
            BeatmapInfo map = null;
            var fullStarted = new ManualResetEventSlim();
            var releaseFull = new ManualResetEventSlim();
            var owner = new CancellationTokenSource();
            Task<StarDifficulty?> full = null;
            IBindable<StarDifficulty> preview = null;
            int previewCalculations = 0;

            AddStep("start independent full calculation", () =>
            {
                previewCancellation = new CancellationTokenSource();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previewCalculations);
                        return new StarDifficulty(2, 3);
                    }

                    fullStarted.Set();
                    if (!releaseFull.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("full calculation did not release");
                    return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.25, MaxCombo = 123 },
                        new osu.Game.Rulesets.Difficulty.PerformanceAttributes());
                };
                full = difficultyCache.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token);
            });
            AddUntilStep("full worker entered", () => fullStarted.IsSet);
            AddStep("join preview then cancel owner", () =>
            {
                preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false);
                owner.Cancel();
                releaseFull.Set();
            });
            AddUntilStep("full completed", () => full.IsCompleted);
            AddUntilStep("preview released", () => preview.Value.Stars == 6.25 || previewCalculations > 0);
            AddAssert("preview did not start duplicate conversion", () => previewCalculations, () => Is.Zero);
            AddAssert("full attributes delivered", () => preview.Value.DifficultyAttributes != null && preview.Value.MaxCombo == 123);
        }

        [Test]
        public void TestRealRapidMapSwitchCancelsOldPreviewWithoutDiscardingNewFull()
        {
            RealDifficultyProbe probe = null;
            var oldPreviewOwner = new CancellationTokenSource();
            Task<StarDifficulty?> latestFull = null;
            IBindable<StarDifficulty> oldPreview = null;
            BeatmapInfo latestMap = null;

            AddStep("gate old preview", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Child = probe = new RealDifficultyProbe();
                var maps = importedSet.Beatmaps.Where(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID).Take(2).ToArray();
                Assert.That(maps, Has.Length.EqualTo(2));
                latestMap = maps[1];
                oldPreview = probe.GetBindableDifficulty(maps[0], oldPreviewOwner.Token, calculatePerformance: false);
            });
            AddUntilStep("old preview entered", () => probe.PreviewStarts == 1);
            AddStep("cancel old selection and start real full for new map", () =>
            {
                oldPreviewOwner.Cancel();
                latestFull = probe.GetDifficultyAsync(latestMap, latestMap.Ruleset);
                probe.Release();
            });
            AddUntilStep("new full finished", () => latestFull.IsCompleted);
            AddAssert("new full has playable attributes and PP", () =>
                latestFull.GetResultSafely() is { DifficultyAttributes: not null, PerformanceAttributes: not null, CountStatistics: not null }
                && probe.FullStarts == 1 && oldPreview.Value.DifficultyAttributes == null);
            AddStep("dispose old owner", () => oldPreviewOwner.Dispose());
        }

        [TestCase("full-owner")]
        [TestCase("preview-owner")]
        public void TestRealDifficultyIndependentCancellation(string cancelledOwner)
        {
            RealDifficultyProbe probe = null;
            BeatmapInfo map = null;
            Task<StarDifficulty?> full = null;
            IBindable<StarDifficulty> preview = null;
            CancellationTokenSource fullOwner = null;

            AddStep("start real pending full", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Child = probe = new RealDifficultyProbe();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                previewCancellation = new CancellationTokenSource();
                fullOwner = new CancellationTokenSource();
                full = probe.GetDifficultyAsync(map, map.Ruleset, cancellationToken: fullOwner.Token);
            });
            AddUntilStep("full gated before production calculation", () => probe.FullStarts == 1);
            AddStep("join preview and cancel one owner", () =>
            {
                preview = probe.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false);
                if (cancelledOwner == "full-owner")
                    fullOwner.Cancel();
                else
                    previewCancellation.Cancel();
                probe.Release();
            });
            if (cancelledOwner == "full-owner")
            {
                AddUntilStep("cancelled full and fallback preview finished", () => full.IsCompleted && preview.Value.DifficultyAttributes != null);
                AddAssert("full cancellation did not poison preview", () => full.IsCanceled && probe.PreviewStarts == 1);
                AddAssert("fallback calculated real stars", () => preview.Value.Stars, () => Is.GreaterThan(0));
            }
            else
            {
                AddUntilStep("uncancelled full finished", () => full.IsCompleted);
                AddAssert("preview cancellation did not poison full", () =>
                    full.GetResultSafely() is { DifficultyAttributes: not null, PerformanceAttributes: not null }
                    && probe.PreviewStarts == 0 && preview.Value.DifficultyAttributes == null);
            }
            AddStep("release owner resources", () => fullOwner.Dispose());
        }

        [Test]
        public void TestAnotherMapSupersessionDoesNotInvalidateJoinedPreview()
        {
            BeatmapInfo map = null;
            BeatmapInfo other = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            IBindable<StarDifficulty> preview = null;
            int previewCalculations = 0;

            AddStep("start matching full", () =>
            {
                previewCancellation = new CancellationTokenSource();
                var maps = importedSet.Beatmaps.Where(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID).Take(2).ToArray();
                Assert.That(maps, Has.Length.EqualTo(2));
                map = maps[0];
                other = maps[1];
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previewCalculations);
                        return new StarDifficulty(2, 3);
                    }

                    if (lookup.BeatmapInfo.ID == map.ID)
                    {
                        started.Set();
                        if (!release.Wait(TimeSpan.FromSeconds(5)))
                            throw new TimeoutException("matching full did not release");
                    }

                    return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.25, MaxCombo = 123 },
                        new osu.Game.Rulesets.Difficulty.PerformanceAttributes());
                };
                _ = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("matching full started", () => started.IsSet);
            AddStep("join then supersede a different map", () =>
            {
                preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false);
                _ = difficultyCache.GetDifficultyAsync(other, other.Ruleset);
                _ = difficultyCache.GetDifficultyAsync(other, other.Ruleset);
                release.Set();
            });
            AddUntilStep("joined stars delivered", () => preview.Value.Stars == 6.25);
            AddAssert("unrelated supersession cannot start preview conversion", () => previewCalculations, () => Is.Zero);
            AddAssert("full attributes retained", () => preview.Value.DifficultyAttributes != null && preview.Value.MaxCombo == 123);
        }

        [Test]
        public void TestInvalidatedFullDoesNotServePreview()
        {
            BeatmapInfo map = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            IBindable<StarDifficulty> preview = null;
            int previews = 0;
            Task<StarDifficulty?> obsolete = null;

            AddStep("start pending full", () =>
            {
                previewCancellation = new CancellationTokenSource();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previews);
                        return new StarDifficulty(7.75, 99);
                    }
                    started.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("full calculation did not release");
                    return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.25 },
                        new osu.Game.Rulesets.Difficulty.PerformanceAttributes());
                };
                obsolete = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("full worker entered", () => started.IsSet);
            AddStep("invalidate before preview", () => difficultyCache.InvalidateAll());
            // InvalidateAll schedules a refresh of already tracked bindables. Let that
            // refresh finish before registering the new consumer, otherwise this test
            // races two independent preview admissions rather than obsolete publication.
            AddStep("request new preview after refresh", () =>
                preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false));
            AddUntilStep("preview recalculated", () => preview.Value.Stars == 7.75);
            AddStep("release obsolete full", release.Set);
            AddUntilStep("obsolete full completed", () => obsolete.IsCompletedSuccessfully);
            AddAssert("invalidated full rejected", () => obsolete.GetResultSafely() == null);
            AddAssert("obsolete full not delivered", () => preview.Value.Stars == 7.75 && previews == 1);
        }

        [TestCase("map")]
        [TestCase("all")]
        [TestCase("clear")]
        [TestCase("cancel")]
        public void TestPendingInvalidationCannotRepopulateEitherTier(string action)
        {
            BeatmapInfo map = null;
            BeatmapInfo other = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var owner = new CancellationTokenSource();
            Task<StarDifficulty?> obsolete = null;
            int calculations = 0;
            int oldNumber = 0;

            AddStep("start gated full", () =>
            {
                var maps = importedSet.Beatmaps.Where(b => b.Ruleset.ShortName == "osu").Take(2).ToArray();
                map = maps[0];
                other = maps[1];
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    int number = Interlocked.Increment(ref calculations);
                    if (lookup.BeatmapInfo.ID == map.ID && oldNumber == 0)
                    {
                        oldNumber = number;
                        started.Set();
                        if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("old request not released");
                    }
                    return new StarDifficulty(number, number);
                };
                // First populate an unrelated key, then leave an old request in flight.
                Assert.That(difficultyCache.GetDifficultyAsync(other, other.Ruleset).GetResultSafely(), Is.Not.Null);
                obsolete = difficultyCache.GetDifficultyAsync(map, map.Ruleset, cancellationToken: owner.Token);
            });
            AddUntilStep("old calculation entered", () => started.IsSet);
            AddStep("invalidate or cancel and release old work", () =>
            {
                switch (action)
                {
                    case "all": difficultyCache.InvalidateAll(); break;
                    case "clear": difficultyCache.Clear(); break;
                    case "cancel": owner.Cancel(); break;
                    default: difficultyCache.Invalidate(map, map); break;
                }
                release.Set();
            });
            AddUntilStep("old request completed", () => obsolete.IsCompleted);
            if (action != "cancel")
                AddAssert("obsolete result not delivered", () => obsolete.GetResultSafely(), () => Is.Null);
            AddAssert("no stale heavy or light", () => !difficultyCache.HasCachedResult(map)
                && !difficultyCache.TryPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(map, map.Ruleset, null), out _));
            if (action is "map" or "cancel")
                AddAssert("unrelated cached result survives", () => difficultyCache.HasCachedResult(other)
                    && difficultyCache.TryPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(other, other.Ruleset, null), out _));
            AddStep("new request succeeds", () => Assert.That(difficultyCache.GetDifficultyAsync(map, map.Ruleset).GetResultSafely()?.Stars, Is.GreaterThan(oldNumber)));
            AddStep("dispose old owner", owner.Dispose);
        }

        [Test]
        public void TestPreviewRequestedBeforeFullStillCalculatesNormally()
        {
            BeatmapInfo map = null;
            IBindable<StarDifficulty> preview = null;
            int previewCalculations = 0;
            int fullCalculations = 0;

            AddStep("request preview before full", () =>
            {
                previewCancellation = new CancellationTokenSource();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (lookup.CalculatePerformance)
                        Interlocked.Increment(ref fullCalculations);
                    else
                        Interlocked.Increment(ref previewCalculations);
                    return new StarDifficulty(6.125, 123);
                };
                preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false);
            });
            AddUntilStep("preview calculated", () => preview.Value.Stars == 6.125);
            AddStep("request later full", () => _ = difficultyCache.GetDifficultyAsync(map, map.Ruleset));
            AddUntilStep("full calculated", () => fullCalculations == 1);
            AddAssert("preview not dropped", () => previewCalculations, () => Is.EqualTo(1));
        }

        [Test]
        public void TestCanceledPreviewDoesNotCancelPendingFull()
        {
            BeatmapInfo map = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var previewOwner = new CancellationTokenSource();
            Task<StarDifficulty?> full = null;
            int previews = 0;

            AddStep("start selected full", () =>
            {
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previews);
                        return new StarDifficulty(2, 3);
                    }
                    started.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("full calculation did not release");
                    return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.25 },
                        new osu.Game.Rulesets.Difficulty.PerformanceAttributes());
                };
                full = difficultyCache.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("full worker entered", () => started.IsSet);
            AddStep("join then cancel preview", () =>
            {
                _ = difficultyCache.GetBindableDifficulty(map, previewOwner.Token, calculatePerformance: false);
                previewOwner.Cancel();
                release.Set();
            });
            AddUntilStep("full still succeeds", () => full.IsCompletedSuccessfully);
            AddAssert("full stars correct", () => full.GetResultSafely()?.Stars, () => Is.EqualTo(6.25));
            AddAssert("no redundant preview", () => previews, () => Is.Zero);
        }

        [Test]
        public void TestPreviewWithDifferentModsDoesNotJoinPendingFull()
        {
            BeatmapInfo map = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            IBindable<StarDifficulty> preview = null;
            int previewCalculations = 0;

            AddStep("start modded full calculation", () =>
            {
                previewCancellation = new CancellationTokenSource();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previewCalculations);
                        return new StarDifficulty(8.125, 99);
                    }
                    started.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("full calculation did not release");
                    return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.25 },
                        new osu.Game.Rulesets.Difficulty.PerformanceAttributes());
                };
                _ = difficultyCache.GetDifficultyAsync(map, map.Ruleset, new[] { new OsuModHardRock() });
            });
            AddUntilStep("full worker entered", () => started.IsSet);
            AddStep("request unmodded preview", () => preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false));
            AddUntilStep("preview calculated independently", () => preview.Value.Stars == 8.125);
            AddAssert("one preview calculation", () => previewCalculations, () => Is.EqualTo(1));
            AddStep("release full worker", release.Set);
        }

        [Test]
        public void TestPreviewFallsBackAfterFaultedFull()
        {
            BeatmapInfo map = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            IBindable<StarDifficulty> preview = null;
            int previewCalculations = 0;

            AddStep("start faulting full calculation", () =>
            {
                previewCancellation = new CancellationTokenSource();
                map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    if (!lookup.CalculatePerformance)
                    {
                        Interlocked.Increment(ref previewCalculations);
                        return new StarDifficulty(7.125, 99);
                    }
                    started.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("full calculation did not release");
                    throw new InvalidOperationException("full calculation failed");
                };
                _ = difficultyCache.GetDifficultyAsync(map, map.Ruleset).ContinueWith(t => _ = t.Exception);
            });
            AddUntilStep("full worker entered", () => started.IsSet);
            AddStep("request preview while full is pending", () => preview = difficultyCache.GetBindableDifficulty(map, previewCancellation.Token, calculatePerformance: false));
            AddStep("fail selected calculation", release.Set);
            AddUntilStep("fallback preview delivered", () => preview.Value.Stars == 7.125);
            AddAssert("one fallback calculation", () => previewCalculations, () => Is.EqualTo(1));
        }

        [Test]
        public void TestFreshDisplayPreviewUsesPersistedModdedResultWithoutCalculation()
        {
            GatedQueueProbe probe = null;
            BeatmapInfo candidate = null;
            previewCancellation = new CancellationTokenSource();
            IBindable<StarDifficulty> preview = null;
            AddStep("seed validated modded display", () =>
            {
                var source = importedSet.Beatmaps.First(b => b.Ruleset.ShortName == "osu");
                candidate = new BeatmapInfo(source.Ruleset) { ID = Guid.NewGuid(), MD5Hash = source.MD5Hash };
                var mod = new OsuModDoubleTime();
                int version = source.Ruleset.CreateInstance().CreateDifficultyCalculator(beatmapManager.GetWorkingBeatmap(source)).Version;
                ForkDataStore.Instance.SetAdditionalInfo(candidate.ID, "osu", ForkDataStore.CreateAdditionalInfoModsKey("osu", new Mod[] { mod }),
                    new ForkDataStore.AdditionalInfoData(candidate.MD5Hash, version, 8.125, 777, "validated"));
                SelectedMods.Value = new Mod[] { mod };
                Add(probe = new GatedQueueProbe());
                preview = probe.GetBindableDifficulty(candidate, previewCancellation.Token, calculatePerformance: false, usePersistedAdditionalInfo: true);
            });
            AddUntilStep("persisted stars delivered in fresh cache", () => preview.Value.Stars == 8.125);
            AddAssert("no full or preview conversion", () => probe.FullStarts == 0 && probe.PreviewStarts == 0);
            AddAssert("display result not fake full attributes", () => preview.Value.DifficultyAttributes == null && preview.Value.MaxCombo == 777);
            AddStep("cleanup", () =>
            {
                previewCancellation.Cancel();
                SelectedMods.Value = Array.Empty<Mod>();
                probe.Expire();
            });
        }

        [Test]
        public void TestScrollingDefersNeighbourCalculationsButNotSelectedFull()
        {
            GatedQueueProbe probe = null;
            CancellationTokenSource owner = null;
            IBindable<StarDifficulty> preview = null;
            Task<StarDifficulty?> selected = null;
            AddStep("request neighbour during scrolling", () =>
            {
                owner = new CancellationTokenSource();
                Add(probe = new GatedQueueProbe());
                probe.ReleasePreview();
                probe.ReleaseFull();
                probe.DeferPreviewCalculations(1000);
                preview = probe.GetBindableDifficulty(importedSet.Beatmaps[0], owner.Token, calculatePerformance: false);
                selected = probe.GetDifficultyAsync(importedSet.Beatmaps[1], importedSet.Beatmaps[1].Ruleset);
            });
            AddUntilStep("selected finishes without neighbour work", () => selected?.IsCompletedSuccessfully == true && probe.FullStarts == 1);
            AddAssert("scrolling neighbour not converted", () => probe.PreviewStarts, () => Is.Zero);
            AddStep("stop scrolling", () => probe.DeferPreviewCalculations(0));
            AddUntilStep("settled neighbour converted", () => probe.PreviewStarts == 1 && preview.Value.Stars != importedSet.Beatmaps[0].StarRating);
            AddStep("cancel and dispose", () =>
            {
                owner.Cancel();
                owner.Dispose();
                probe.Expire();
            });
        }

        [Test]
        public void TestValidatedUnmoddedPreviewAvoidsCalculatorAndRejectsIneligibleRequests()
        {
            BeatmapInfo candidate = null;
            BeatmapInfo missingMap = null;
            int version = 0;
            int started = 0;
            IBindable<StarDifficulty> preview = null;
            IBindable<StarDifficulty> missing = null;
            previewCancellation = new CancellationTokenSource();
            string key = ForkDataStore.CreateAdditionalInfoModsKey("osu", null);
            const double persistedStars = 6.125;

            AddStep("discover version without candidate decode", () =>
            {
                var maps = importedSet.Beatmaps.Where(b => b.Ruleset.ShortName == "osu").ToArray();
                Assert.That(maps.Length, Is.GreaterThanOrEqualTo(3));
                // Use real imported checksums/rulesets, but isolated IDs: other tests can have
                // persisted full results for any of the imported difficulties in this same scene.
                candidate = new BeatmapInfo(maps[1].Ruleset) { ID = Guid.NewGuid(), MD5Hash = maps[1].MD5Hash };
                missingMap = new BeatmapInfo(maps[2].Ruleset) { ID = Guid.NewGuid(), MD5Hash = maps[2].MD5Hash };
                Assert.That(difficultyCache.GetAdditionalInfoDifficultyAsync(maps[0], maps[0].Ruleset, null, CancellationToken.None, 0)
                                           .GetResultSafely(), Is.Not.Null);
                version = maps[0].Ruleset.CreateInstance().CreateDifficultyCalculator(beatmapManager.GetWorkingBeatmap(maps[0])).Version;
                Assert.That(version, Is.GreaterThan(0));
                Assert.That(ForkDataStore.Instance.TryGetAdditionalInfo(missingMap.ID, "osu", key, missingMap.MD5Hash, version, out _), Is.False);
                Assert.That(difficultyCache.HasCachedPreviewResult(missingMap), Is.False);
                difficultyCache.ComputeDifficulty = _ =>
                {
                    Interlocked.Increment(ref started);
                    return new StarDifficulty(BASE_STARS, 0);
                };
                ForkDataStore.Instance.SetAdditionalInfo(candidate.ID, "osu", key,
                    new ForkDataStore.AdditionalInfoData(candidate.MD5Hash, version, persistedStars, 123, "persisted text"));
                Assert.That(ForkDataStore.Instance.TryGetAdditionalInfo(candidate.ID, "osu", key, candidate.MD5Hash, version, out _), Is.True);
            });
            AddStep("request eligible STAR-only preview", () =>
                preview = difficultyCache.GetBindableDifficulty(candidate, previewCancellation.Token, calculatePerformance: false));
            AddUntilStep("validated preview delivered", () => preview.Value.Stars == persistedStars);
            AddAssert("stored stars, combo, text and no fake attributes", () =>
                preview.Value.MaxCombo == 123 && preview.Value.CachedAdditionalInfo == "persisted text"
                && preview.Value.DifficultyAttributes == null && preview.Value.PerformanceAttributes == null && preview.Value.CountStatistics == null);
            AddAssert("no calculator started", () => started, () => Is.Zero);
            AddStep("request same-list persisted miss", () =>
            {
                Assert.That(ForkDataStore.Instance.TryGetAdditionalInfo(missingMap.ID, "osu", key, missingMap.MD5Hash, version, out _), Is.False);
                Assert.That(difficultyCache.HasCachedPreviewResult(missingMap), Is.False);
                missing = difficultyCache.GetBindableDifficulty(missingMap, previewCancellation.Token, calculatePerformance: false);
            });
            AddUntilStep("ineligible calculation started", () => started >= 1);
            AddAssert("missing output is calculated, not persisted", () => missing.Value.Stars == BASE_STARS);
            AddAssert("two-list workload starts exactly one calculator", () => started, () => Is.EqualTo(1));
            AddStep("invalid cases cannot consume persisted preview", () =>
            {
                var changedChecksum = new BeatmapInfo(candidate.Ruleset) { ID = candidate.ID, MD5Hash = "changed checksum" };
                Assert.That(difficultyCache.TryGetValidatedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(changedChecksum, candidate.Ruleset, null, false), out _), Is.False);
                var stale = new BeatmapInfo(candidate.Ruleset) { ID = Guid.NewGuid(), MD5Hash = candidate.MD5Hash };
                ForkDataStore.Instance.SetAdditionalInfo(stale.ID, "osu", key,
                    new ForkDataStore.AdditionalInfoData(stale.MD5Hash, version - 1, persistedStars, 123, "stale"));
                Assert.That(difficultyCache.TryGetValidatedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(stale, stale.Ruleset, null, false), out _), Is.False);
                var otherRuleset = new BeatmapInfo(new RulesetInfo { ShortName = "taiko" });
                Assert.That(difficultyCache.TryGetValidatedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(otherRuleset, candidate.Ruleset, null, false), out _), Is.False);
                Assert.That(difficultyCache.TryGetValidatedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(candidate, new RulesetInfo { ShortName = "taiko" }, null, false), out _), Is.False);
                Assert.That(difficultyCache.TryGetValidatedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(candidate, candidate.Ruleset, new Mod[] { new OsuModDoubleTime() }, false), out _), Is.False);
                Assert.That(difficultyCache.TryGetValidatedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(candidate, candidate.Ruleset, null, true), out _), Is.False);
            });
        }

        [Test]
        public void TestCountCompletionWaitsForFullResultAndReusesCacheHit()
        {
            BeatmapInfo first = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            Task<IReadOnlyList<StarDifficulty.CountStatistic>> pending = null;
            Task<IReadOnlyList<StarDifficulty.CountStatistic>> cached = null;
            int calls = 0;

            AddStep("block full calculation", () =>
            {
                first = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    Interlocked.Increment(ref calls);
                    started.Set();
                    release.Wait(TimeSpan.FromSeconds(4));
                    return new StarDifficulty(3, 4, "persisted");
                };
            });
            AddStep("request counts", () => pending = difficultyCache.GetBindableDifficultyWithCountStatistics(first, CancellationToken.None, 0).CountStatistics);
            AddUntilStep("calculation started", () => started.IsSet);
            AddAssert("initial placeholder is not terminal", () => pending.IsCompleted, () => Is.False);
            AddStep("finish calculation", release.Set);
            AddUntilStep("completion delivered", () => pending.IsCompleted);
            AddAssert("missing counts completes as null", () => pending.GetResultSafely(), () => Is.Null);
            AddStep("request cached counts", () => cached = difficultyCache.GetBindableDifficultyWithCountStatistics(first, CancellationToken.None, 0).CountStatistics);
            AddUntilStep("cached completion delivered", () => cached.IsCompleted);
            AddAssert("cached result has no counts", () => cached.GetResultSafely(), () => Is.Null);
            AddAssert("cache hit did not recalculate", () => calls, () => Is.EqualTo(1));
        }

        [Test]
        public void TestCanceledCountCompletionDoesNotPublishOldSelection()
        {
            BeatmapInfo first = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var cancellation = new CancellationTokenSource();
            Task<IReadOnlyList<StarDifficulty.CountStatistic>> pending = null;

            AddStep("block full calculation", () =>
            {
                first = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = lookup =>
                {
                    started.Set();
                    release.Wait(TimeSpan.FromSeconds(4));
                    return new StarDifficulty(3, 4, "persisted");
                };
            });
            AddStep("request counts", () => pending = difficultyCache.GetBindableDifficultyWithCountStatistics(first, cancellation.Token, 0).CountStatistics);
            AddUntilStep("calculation started", () => started.IsSet);
            AddStep("cancel selection", cancellation.Cancel);
            AddStep("release calculation", release.Set);
            AddUntilStep("completion delivered", () => pending.IsCompleted);
            AddAssert("canceled completion cannot request fallback", () => pending.IsCanceled, () => Is.True);
        }

        [Test]
        public void TestProductionCountSnapshotMatchesUnmoddedPlayableAndStars()
        {
            var first = importedSet.Beatmaps.First(b => b.Ruleset.ShortName == "osu");
            StarDifficulty? result = null;
            AddStep("calculate full difficulty", () => result = actualDifficultyCache.GetDifficultyAsync(first, first.Ruleset).GetResultSafely());
            AddAssert("difficulty and performance retained", () => result?.DifficultyAttributes != null && result?.PerformanceAttributes != null);
            AddAssert("full calculation contains exact unmodded playable counts", () =>
            {
                var expected = beatmapManager.GetWorkingBeatmap(first).GetPlayableBeatmap(first.Ruleset, Array.Empty<Mod>(), CancellationToken.None)
                                             .GetStatistics().ToArray();
                var actual = result?.CountStatistics;
                return actual != null && expected.Length == actual.Count && expected.Zip(actual).All(pair =>
                    pair.First.Name.Equals(pair.Second.Name) && pair.First.BarDisplayLength == pair.Second.BarDisplayLength
                    && pair.First.Content == pair.Second.Content);
            });
        }

        [Test]
        public void TestModChangeReplacesPendingCountWithoutAnotherFullRequest()
        {
            BeatmapInfo first = null;
            var started = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var cancellation = new CancellationTokenSource();
            Task<IReadOnlyList<StarDifficulty.CountStatistic>> old = null;
            Task<IReadOnlyList<StarDifficulty.CountStatistic>> latest = null;
            int requests = 0;

            AddStep("start pending full calculation", () =>
            {
                first = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                difficultyCache.ComputeAsynchronously = true;
                difficultyCache.ComputeDifficulty = _ =>
                {
                    started.Set();
                    release.Wait(TimeSpan.FromSeconds(4));
                    return new StarDifficulty(3, 4, "persisted");
                };
                var request = difficultyCache.GetBindableDifficultyWithCountStatistics(first, cancellation.Token, 0);
                old = request.CountStatistics;
                request.Difficulty.CountStatisticsRequested += task =>
                {
                    latest = task;
                    requests++;
                };
            });
            AddUntilStep("calculation underway", () => started.IsSet);
            AddStep("change mods", () => SelectedMods.Value = new[] { new OsuModDoubleTime() });
            AddUntilStep("new count request received", () => requests == 1);
            AddStep("release old calculation", release.Set);
            AddUntilStep("both requests terminal", () => old.IsCompleted && latest.IsCompleted);
            AddAssert("old request canceled", () => old.IsCanceled, () => Is.True);
            AddAssert("replacement completed", () => latest.IsCompletedSuccessfully, () => Is.True);
            AddAssert("latest replacement produced counts", () => latest.GetResultSafely(), () => Is.Null);
            AddStep("stop observing", cancellation.Cancel);
        }

        [Test]
        public void TestProductionFullCountCompletionDoesNotRunFallback()
        {
            BeatmapInfo first = null;
            Task<IReadOnlyList<StarDifficulty.CountStatistic>> pending = null;
            AddStep("request full counts", () =>
            {
                first = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                pending = actualDifficultyCache.GetBindableDifficultyWithCountStatistics(first, CancellationToken.None, 0).CountStatistics;
            });
            AddUntilStep("full result available", () => pending.IsCompleted);
            AddAssert("full result counts materialised, no fallback needed", () => pending.GetResultSafely(), () => Is.Not.Null);
        }

        [Test]
        public void TestPersistedMissDebounceDoesNotBlockImmediateRequest()
        {
            Task<StarDifficulty?> delayed = null;
            Task<StarDifficulty?> immediate = null;
            var delayedCancellation = new CancellationTokenSource();
            BeatmapInfo first = null;
            BeatmapInfo second = null;

            AddStep("start delayed persisted miss", () =>
            {
                var maps = importedSet.Beatmaps.Where(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID).Take(2).ToArray();
                Assert.That(maps, Has.Length.EqualTo(2));
                first = maps[0];
                second = maps[1];
                delayed = difficultyCache.GetAdditionalInfoDifficultyAsync(first, first.Ruleset, null, delayedCancellation.Token, 5000);
            });
            AddStep("start distinct immediate request", () =>
                immediate = difficultyCache.GetAdditionalInfoDifficultyAsync(second, second.Ruleset, null, CancellationToken.None, 0));
            AddUntilStep("one request finishes", () => immediate.IsCompleted || delayed.IsCompleted);
            AddAssert("immediate request finishes before delayed request", () => immediate.IsCompleted && !delayed.IsCompleted, () => Is.True);
            AddAssert("immediate result available", () => immediate.GetResultSafely(), () => Is.Not.Null);
            AddStep("cancel delayed request", () => delayedCancellation.Cancel());
            AddUntilStep("delayed request cancels", () => delayed.IsCompleted);
            AddAssert("delay was canceled", () => delayed.IsCanceled, () => Is.True);
            AddAssert("canceled request not stored", () => difficultyCache.HasCachedResult(first), () => Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestAdditionalInfoInvalidationOrCancellationBeforeWorkerSkipsBackfill(bool cancel)
        {
            BeatmapInfo map = null;
            Task<StarDifficulty?> pending = null;
            var owner = new CancellationTokenSource();
            int version = 0;
            string modsKey = ForkDataStore.CreateAdditionalInfoModsKey("osu", null);
            AddStep("start persisted miss with debounce", () =>
            {
                var source = importedSet.Beatmaps.First(b => b.Ruleset.ShortName == "osu");
                map = new BeatmapInfo(source.Ruleset) { ID = Guid.NewGuid(), MD5Hash = source.MD5Hash };
                version = source.Ruleset.CreateInstance().CreateDifficultyCalculator(beatmapManager.GetWorkingBeatmap(source)).Version;
                pending = actualDifficultyCache.GetAdditionalInfoDifficultyAsync(map, map.Ruleset, null, owner.Token, 600);
            });
            AddStep("invalidate or cancel before worker", () =>
            {
                if (cancel) owner.Cancel();
                else actualDifficultyCache.Invalidate(map, map);
            });
            AddUntilStep("old request terminal", () => pending.IsCompleted);
            AddAssert("obsolete result not delivered", () => cancel ? pending.IsCanceled : pending.GetResultSafely() == null);
            AddAssert("pre-worker cancellation or invalidation did not backfill", () =>
                !actualDifficultyCache.HasHeavy(map) && !actualDifficultyCache.HasLight(map)
                && ForkDataStore.Instance?.TryGetAdditionalInfo(map.ID, "osu", modsKey, map.MD5Hash, version, out _) != true);
            AddStep("dispose owner", owner.Dispose);
        }

        [Test]
        public void TestDelayedSelectedFullReusesCompletedRegularFullAndBackfillsDisplayInfo()
        {
            BeatmapInfo beatmap = null;
            Task<StarDifficulty?> canceled = null;
            Task<StarDifficulty?> selected = null;
            Task<StarDifficulty?> regular = null;
            var canceledSource = new CancellationTokenSource();
            int version = 0;
            string modsKey = ForkDataStore.CreateAdditionalInfoModsKey("osu", null);

            AddStep("start two delayed selected misses on a fresh map", () =>
            {
                beatmap = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
                actualDifficultyCache.Invalidate(beatmap, beatmap);
                version = beatmap.Ruleset.CreateInstance().CreateDifficultyCalculator(beatmapManager.GetWorkingBeatmap(beatmap)).Version;
                Assert.That(ForkDataStore.Instance?.TryGetAdditionalInfo(beatmap.ID, "osu", modsKey, beatmap.MD5Hash, version, out _), Is.Not.True);
                canceled = actualDifficultyCache.GetAdditionalInfoDifficultyAsync(beatmap, beatmap.Ruleset, null, canceledSource.Token, 5000);
                selected = actualDifficultyCache.GetAdditionalInfoDifficultyAsync(beatmap, beatmap.Ruleset, null, CancellationToken.None, 5000);
            });
            AddStep("request regular full before selected debounce expires", () =>
                regular = actualDifficultyCache.GetDifficultyAsync(beatmap, beatmap.Ruleset));
            AddUntilStep("regular full completed", () => regular.IsCompleted);
            AddAssert("regular full contains attributes, performance and counts", () =>
                regular.GetResultSafely() is { DifficultyAttributes: not null, PerformanceAttributes: not null, CountStatistics: not null });
            AddAssert("selected calculations still delayed", () => !selected.IsCompleted && !canceled.IsCompleted);
            AddStep("cancel only one selected waiter", canceledSource.Cancel);
            AddUntilStep("canceled waiter finished", () => canceled.IsCompleted);
            AddAssert("first waiter canceled", () => canceled.IsCanceled);
            AddUntilStep("surviving selected waiter finished", () => selected.IsCompleted);
            AddAssert("survivor reused exact regular full result", () =>
            {
                var expected = regular.GetResultSafely();
                var actual = selected.GetResultSafely();
                return expected != null && actual != null
                       && ReferenceEquals(expected.Value.DifficultyAttributes, actual.Value.DifficultyAttributes)
                       && ReferenceEquals(expected.Value.PerformanceAttributes, actual.Value.PerformanceAttributes)
                       && ReferenceEquals(expected.Value.CountStatistics, actual.Value.CountStatistics);
            });
            AddAssert("eligible store received display info", () =>
            {
                var store = ForkDataStore.Instance;
                return store == null || version <= 0 ||
                       store.TryGetAdditionalInfo(beatmap.ID, "osu", modsKey, beatmap.MD5Hash, version, out var info)
                       && !string.IsNullOrEmpty(info.Text);
            });
        }

        [Test]
        public void TestSelectedFullPublishesResultForFollowingRegularFull()
        {
            BeatmapInfo beatmap = null;
            Task<StarDifficulty?> selected = null;
            Task<StarDifficulty?> regular = null;

            AddStep("calculate selected full on a fresh map", () =>
            {
                beatmap = importedSet.Beatmaps.First(b => b.Ruleset.ShortName == "osu");
                actualDifficultyCache.Invalidate(beatmap, beatmap);
                int version = beatmap.Ruleset.CreateInstance().CreateDifficultyCalculator(beatmapManager.GetWorkingBeatmap(beatmap)).Version;
                Assert.That(ForkDataStore.Instance?.TryGetAdditionalInfo(beatmap.ID, "osu", ForkDataStore.CreateAdditionalInfoModsKey("osu", null),
                    beatmap.MD5Hash, version, out _), Is.Not.True);
                selected = actualDifficultyCache.GetAdditionalInfoDifficultyAsync(beatmap, beatmap.Ruleset, null, CancellationToken.None, 0);
            });
            AddUntilStep("selected full finished", () => selected.IsCompleted);
            AddAssert("selected computed production attributes and counts", () =>
                selected.GetResultSafely() is { DifficultyAttributes: not null, PerformanceAttributes: not null, CountStatistics: not null });
            AddStep("request regular full", () => regular = actualDifficultyCache.GetDifficultyAsync(beatmap, beatmap.Ruleset));
            AddUntilStep("regular full finished", () => regular.IsCompleted);
            AddAssert("regular reuses selected attributes, performance and counts", () =>
            {
                var expected = selected.GetResultSafely();
                var actual = regular.GetResultSafely();
                return expected != null && actual != null
                       && ReferenceEquals(expected.Value.DifficultyAttributes, actual.Value.DifficultyAttributes)
                       && ReferenceEquals(expected.Value.PerformanceAttributes, actual.Value.PerformanceAttributes)
                       && ReferenceEquals(expected.Value.CountStatistics, actual.Value.CountStatistics);
            });
        }

        [Test]
        [FlakyTest] // one fix attempted in https://github.com/ppy/osu/pull/37178, didn't work
        public void TestInvalidationFlow()
        {
            BeatmapInfo postEditBeatmapInfo = null;
            BeatmapInfo preEditBeatmapInfo = null;

            IBindable<StarDifficulty> bindableDifficulty = null;

            AddStep("get bindable stars", () =>
            {
                preEditBeatmapInfo = importedSet.Beatmaps.First();
                bindableDifficulty = actualDifficultyCache.GetBindableDifficulty(preEditBeatmapInfo);
            });

            AddUntilStep("wait for stars retrieved", () => bindableDifficulty.Value.Stars, () => Is.GreaterThan(0));

            AddStep("remove all hitobjects", () =>
            {
                var working = beatmapManager.GetWorkingBeatmap(preEditBeatmapInfo);

                ((IList<HitObject>)working.Beatmap.HitObjects).Clear();

                beatmapManager.Save(working.BeatmapInfo, working.Beatmap);
                postEditBeatmapInfo = working.BeatmapInfo;
            });

            AddAssert("stars is now zero", () => actualDifficultyCache.GetDifficultyAsync(postEditBeatmapInfo).GetResultSafely()!.Value.Stars, () => Is.Zero);
            AddUntilStep("bindable stars is now zero", () => bindableDifficulty.Value.Stars, () => Is.Zero);
        }

        [Test]
        public void TestStarDifficultyChangesOnModSettings()
        {
            OsuModDoubleTime dt = null;

            AddStep("set computation function", () => difficultyCache.ComputeDifficulty = lookup =>
            {
                var modRateAdjust = (ModRateAdjust)lookup.OrderedMods.SingleOrDefault(mod => mod is ModRateAdjust);
                return new StarDifficulty(BASE_STARS + modRateAdjust?.SpeedChange.Value ?? 0, 0);
            });

            AddStep("change selected mod to DT", () => SelectedMods.Value = new[] { dt = new OsuModDoubleTime { SpeedChange = { Value = 1.5 } } });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.5}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.5);

            AddStep("change DT speed to 1.25", () => dt.SpeedChange.Value = 1.25);
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.25}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.25);

            AddStep("change selected mod to NC", () => SelectedMods.Value = new[] { new OsuModNightcore { SpeedChange = { Value = 1.75 } } });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.75}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.75);
        }

        [Test]
        public void TestStarDifficultyChangesOnModSettingsCorrectlyTrackAcrossReferenceChanges()
        {
            OsuModDoubleTime dt = null;

            AddStep("set computation function", () => difficultyCache.ComputeDifficulty = lookup =>
            {
                var modRateAdjust = (ModRateAdjust)lookup.OrderedMods.SingleOrDefault(mod => mod is ModRateAdjust);
                return new StarDifficulty(BASE_STARS + modRateAdjust?.SpeedChange.Value ?? 0, 0);
            });

            AddStep("change selected mod to DT", () => SelectedMods.Value = new[] { dt = new OsuModDoubleTime { SpeedChange = { Value = 1.5 } } });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.5}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.5);

            AddStep("change DT speed to 1.25", () => dt.SpeedChange.Value = 1.25);
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.25}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.25);

            AddStep("reconstruct DT mod with same settings", () => SelectedMods.Value = new[] { dt = (OsuModDoubleTime)dt.DeepClone() });
            AddUntilStep($"star difficulty -> {BASE_STARS + 1.25}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 1.25);

            AddStep("change DT speed to 1.25", () => dt.SpeedChange.Value = 2);
            AddUntilStep($"star difficulty -> {BASE_STARS + 2}", () => starDifficultyBindable.Value.Stars == BASE_STARS + 2);
        }

        [Test]
        public void TestStarDifficultyAdjustHashCodeConflict()
        {
            OsuModDifficultyAdjust difficultyAdjust = null;

            AddStep("set computation function", () => difficultyCache.ComputeDifficulty = lookup =>
            {
                var modDifficultyAdjust = (ModDifficultyAdjust)lookup.OrderedMods.SingleOrDefault(mod => mod is ModDifficultyAdjust);
                return new StarDifficulty(BASE_STARS * (modDifficultyAdjust?.OverallDifficulty.Value ?? 1), 0);
            });

            AddStep("change selected mod to DA", () => SelectedMods.Value = new[] { difficultyAdjust = new OsuModDifficultyAdjust() });
            AddUntilStep($"star difficulty -> {BASE_STARS}", () => starDifficultyBindable.Value.Stars == BASE_STARS);

            AddStep("change DA difficulty to 0.5", () => difficultyAdjust.OverallDifficulty.Value = 0.5f);
            AddUntilStep($"star difficulty -> {BASE_STARS * 0.5f}", () => starDifficultyBindable.Value.Stars == BASE_STARS / 2);

            // hash code of 0 (the value) conflicts with the hash code of null (the initial/default value).
            // it's important that the mod reference and its underlying bindable references stay the same to demonstrate this failure.
            AddStep("change DA difficulty to 0", () => difficultyAdjust.OverallDifficulty.Value = 0);
            AddUntilStep("star difficulty -> 0", () => starDifficultyBindable.Value.Stars == 0);
        }

        [Test]
        public void TestLightPreviewSnapshotKeyAndBound()
        {
            var map = new BeatmapInfo { ID = Guid.NewGuid(), MD5Hash = "first" };
            var ruleset = new RulesetInfo { ShortName = "osu" };
            var otherRuleset = new RulesetInfo { ShortName = "taiko" };
            var lookup = new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, null, false);
            var fullLookup = new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, null, true);
            var heavy = new StarDifficulty(4.25, 420, "large additional-info payload");

            difficultyCache.SeedPreview(fullLookup, heavy);
            Assert.That(difficultyCache.TryPreview(lookup, out var snapshot), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Stars, Is.EqualTo(4.25));
                Assert.That(snapshot.MaxCombo, Is.EqualTo(420));
                Assert.That(snapshot.CachedAdditionalInfo, Is.Null);
                Assert.That(snapshot.DifficultyAttributes, Is.Null);
                Assert.That(snapshot.PerformanceAttributes, Is.Null);
                Assert.That(snapshot.CountStatistics, Is.Null);
                Assert.That(difficultyCache.TryPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(map, otherRuleset, null, false), out _), Is.False);
                Assert.That(difficultyCache.TryPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, new[] { new OsuModHardRock() }, false), out _), Is.False);
            });

            map.MD5Hash = "second";
            Assert.That(difficultyCache.TryPreview(lookup, out _), Is.False, "checksum change must not reuse old preview");
            difficultyCache.SeedPreview(lookup, new StarDifficulty(7, 700));
            difficultyCache.Invalidate(map, map);
            Assert.That(difficultyCache.TryPreview(lookup, out _), Is.False);
            difficultyCache.SeedPreview(lookup, heavy);
            difficultyCache.InvalidateAll();
            Assert.That(difficultyCache.TryPreview(lookup, out _), Is.False);

            for (int i = 0; i < 257; i++)
            {
                var next = new BeatmapInfo { ID = Guid.NewGuid(), MD5Hash = i.ToString() };
                difficultyCache.SeedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(next, ruleset, null, false), heavy);
            }
            Assert.That(difficultyCache.PreviewCount, Is.EqualTo(256));
        }

        [Test]
        public void TestLightPreviewNeverAnswersFullDifficulty()
        {
            var map = importedSet.Beatmaps.Last(b => b.Ruleset.ShortName == "osu" && b.ID != importedSet.Beatmaps.First().ID);
            Task<StarDifficulty?> full = null;
            int calls = 0;
            AddStep("seed lightweight preview", () =>
            {
                difficultyCache.SeedPreview(new BeatmapDifficultyCache.DifficultyCacheLookup(map, map.Ruleset, null, false), new StarDifficulty(1.5, 10));
                difficultyCache.ComputeDifficulty = _ =>
                {
                    calls++;
                    return new StarDifficulty(3.5, 20);
                };
            });
            AddStep("request full", () => full = difficultyCache.GetDifficultyAsync(map, map.Ruleset));
            AddUntilStep("full calculated", () => full.IsCompleted);
            AddAssert("full never uses light result", () => (full.GetResultSafely()?.Stars, calls), () => Is.EqualTo((3.5, 1)));
        }

        [Test]
        public void TestKeyEqualsWithDifferentModInstances()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHardRock(), new OsuModHidden() });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHardRock(), new OsuModHidden() });

            Assert.That(key1, Is.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()));
        }

        [Test]
        public void TestKeyEqualsWithDifferentModOrder()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHardRock(), new OsuModHidden() });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 }, new Mod[] { new OsuModHidden(), new OsuModHardRock() });

            Assert.That(key1, Is.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()));
        }

        [Test]
        public void TestKeyDoesntEqualWithDifferentModSettings()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.1 } } });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.9 } } });

            Assert.That(key1, Is.Not.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
        }

        [Test]
        public void TestKeyEqualWithMatchingModSettings()
        {
            var key1 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.25 } } });
            var key2 = new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = guid }, new RulesetInfo { OnlineID = 0 },
                new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.25 } } });

            Assert.That(key1, Is.EqualTo(key2));
            Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()));
        }

        [TestCase(1.3, DifficultyRating.Easy)]
        [TestCase(1.993, DifficultyRating.Easy)]
        [TestCase(1.998, DifficultyRating.Normal)]
        [TestCase(2.4, DifficultyRating.Normal)]
        [TestCase(2.693, DifficultyRating.Normal)]
        [TestCase(2.698, DifficultyRating.Hard)]
        [TestCase(3.5, DifficultyRating.Hard)]
        [TestCase(3.993, DifficultyRating.Hard)]
        [TestCase(3.997, DifficultyRating.Insane)]
        [TestCase(5.0, DifficultyRating.Insane)]
        [TestCase(5.292, DifficultyRating.Insane)]
        [TestCase(5.297, DifficultyRating.Expert)]
        [TestCase(6.2, DifficultyRating.Expert)]
        [TestCase(6.493, DifficultyRating.Expert)]
        [TestCase(6.498, DifficultyRating.ExpertPlus)]
        [TestCase(8.3, DifficultyRating.ExpertPlus)]
        public void TestDifficultyRatingMapping(double starRating, DifficultyRating expectedBracket)
        {
            var actualBracket = StarDifficulty.GetDifficultyRating(starRating);

            ClassicAssert.AreEqual(expectedBracket, actualBracket);
        }

        [Test]
        public void TestModRefreshPreservesPreviewAdmissionAndAdmitsFullImmediately()
        {
            GatedQueueProbe probe = null;
            var previewOwners = new List<CancellationTokenSource>();
            CancellationTokenSource fullOwner = null;

            AddStep("request 100 delayed cold previews and full", () =>
            {
                Child = probe = new GatedQueueProbe();
                fullOwner = new CancellationTokenSource();
                var ruleset = importedSet.Beatmaps.First().Ruleset;
                for (int i = 0; i < 100; i++)
                {
                    var previewOwner = new CancellationTokenSource();
                    previewOwners.Add(previewOwner);
                    _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = ruleset },
                        previewOwner.Token, computationDelay: 3000, calculatePerformance: false);
                }
                _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = ruleset },
                    fullOwner.Token, computationDelay: 3000);
            });
            AddStep("refresh mods A -> B -> C", () =>
            {
                SelectedMods.Value = new Mod[] { new OsuModHardRock() };
                SelectedMods.Value = Array.Empty<Mod>();
                SelectedMods.Value = new Mod[] { new OsuModHardRock() };
            });
            AddUntilStep("selected full starts on refresh", () => probe.FullStarts == 1);
            AddAssert("neighbour preview still waiting for admission", () => probe.PreviewStarts, () => Is.Zero);
            AddStep("cancel previews before original debounce", () =>
            {
                foreach (var previewOwner in previewOwners) previewOwner.Cancel();
            });
            AddUntilStep("all requests ended", () => probe.ActiveRequests == 0);
            AddStep("verify obsolete preview never entered calculator", () =>
            {
                Assert.That(probe.PreviewStarts, Is.Zero);
                Assert.That(probe.FullStarts, Is.EqualTo(1));
                TestContext.Progress.WriteLine($"MOD_REFRESH burst=100 preview_starts={probe.PreviewStarts} full_starts={probe.FullStarts} active_requests={probe.ActiveRequests}");
                // Once selection settles, preview admission still progresses.
                probe.ReleasePreview();
                _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = importedSet.Beatmaps.First().Ruleset },
                    fullOwner.Token, computationDelay: 20, calculatePerformance: false);
            });
            AddUntilStep("settled preview computes", () => probe.PreviewStarts == 1 && probe.ActiveRequests == 0);
            AddStep("dispose owners", () =>
            {
                fullOwner.Cancel();
                fullOwner.Dispose();
                foreach (var previewOwner in previewOwners) previewOwner.Dispose();
            });
        }

        [Test]
        public void TestObsoletePreviewQueueDoesNotBlockSelectedFull()
        {
            GatedQueueProbe probe = null;
            BeatmapInfo map = null;
            Task<StarDifficulty?> full = null;
            var owners = new List<CancellationTokenSource>();

            AddStep("queue one gated preview and 100 neighbours", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Child = probe = new GatedQueueProbe();
                map = importedSet.Beatmaps.First();
                for (int i = 0; i <= 100; i++)
                {
                    var owner = new CancellationTokenSource();
                    owners.Add(owner);
                    _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = map.Ruleset },
                        owner.Token, calculatePerformance: false);
                    if (i == 0)
                        Assert.That(probe.FirstPreviewEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                }
            });
            AddStep("cancel queued neighbours, request selected full", () =>
            {
                for (int i = 1; i < owners.Count; i++) owners[i].Cancel();
                full = probe.GetDifficultyAsync(map, map.Ruleset);
            });
            AddUntilStep("full admitted ahead of preview backlog", () => full.IsCompleted);
            AddAssert("full completes while preview worker blocked", () => !probe.PreviewReleased && probe.FullStarts == 1);
            AddStep("drain preview", () => probe.ReleasePreview());
            AddUntilStep("all requests ended", () => probe.ActiveRequests == 0);
            AddStep("assert starts and warm hit", () =>
            {
                Assert.That(probe.PreviewStarts, Is.EqualTo(1));
                Assert.That(probe.MaximumPreviewActive, Is.EqualTo(1));
                Assert.That(probe.MaximumFullActive, Is.EqualTo(1));
                Assert.That(probe.GetDifficultyAsync(map, map.Ruleset).IsCompletedSuccessfully, Is.True);
                Assert.That(probe.FullStarts, Is.EqualTo(1));
                TestContext.Progress.WriteLine($"QUEUE burst=100 preview_starts={probe.PreviewStarts} full_starts={probe.FullStarts} max_preview={probe.MaximumPreviewActive} max_full={probe.MaximumFullActive} active_requests={probe.ActiveRequests} selected_waited_for_preview=false");
                foreach (var owner in owners) { owner.Cancel(); owner.Dispose(); }
            });
        }

        [Test]
        public void TestQueuedPreviewRechecksFullBeforeEnteringConversion()
        {
            GatedQueueProbe probe = null;
            BeatmapInfo map = null;
            Task<StarDifficulty?> full = null;
            IBindable<StarDifficulty> preview = null;
            var firstOwner = new CancellationTokenSource();
            var secondOwner = new CancellationTokenSource();
            AddStep("block preview worker", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Child = probe = new GatedQueueProbe();
                map = importedSet.Beatmaps.First(b => b.Ruleset.Equals(Ruleset.Value));
                _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = map.Ruleset }, firstOwner.Token, calculatePerformance: false);
                Assert.That(probe.FirstPreviewEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                preview = probe.GetBindableDifficulty(map, secondOwner.Token, calculatePerformance: false);
            });
            AddUntilStep("second preview admitted to queue", () => probe.ActiveRequests >= 2);
            AddStep("full completes before queued preview starts", () => full = probe.GetDifficultyAsync(map, map.Ruleset));
            AddUntilStep("full done", () => full.IsCompletedSuccessfully);
            AddStep("release preview worker", () => probe.ReleasePreview());
            // Preview retention intentionally strips attribute graphs: the full value's
            // exact stars, plus the conversion counter, proves worker-boundary reuse.
            AddUntilStep("queued preview reuses full result", () => preview.Value.Stars == 6.5 && probe.ActiveRequests == 0);
            AddAssert("only first preview converted", () => probe.PreviewStarts, () => Is.EqualTo(1));
            AddAssert("one full calculation", () => probe.FullStarts, () => Is.EqualTo(1));
            AddStep("release request owners", () =>
            {
                firstOwner.Cancel();
                secondOwner.Cancel();
                firstOwner.Dispose();
                secondOwner.Dispose();
            });
        }

        [TestCase("success")]
        [TestCase("cancel-preview")]
        [TestCase("clear")]
        [TestCase("null-full")]
        [TestCase("fault-full")]
        [TestCase("cancel-full")]
        public void TestQueuedPreviewJoinsPendingFullWithoutBlockingPreviewLane(string action)
        {
            GatedQueueProbe probe = null;
            BeatmapInfo map = null;
            Task<StarDifficulty?> full = null;
            IBindable<StarDifficulty> preview = null;
            var owner = new CancellationTokenSource();
            var fullOwner = new CancellationTokenSource();
            bool fallback = action is "null-full" or "fault-full" or "cancel-full";
            AddStep("block preview and queue target", () =>
            {
                SelectedMods.Value = Array.Empty<Mod>();
                Child = probe = new GatedQueueProbe { GateFull = true, FullOutcome = action };
                map = importedSet.Beatmaps.First(b => b.Ruleset.Equals(Ruleset.Value));
                _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = map.Ruleset }, calculatePerformance: false);
                Assert.That(probe.FirstPreviewEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                preview = probe.GetBindableDifficulty(map, owner.Token, calculatePerformance: false);
            });
            AddUntilStep("target queued", () => probe.ActiveRequests >= 2);
            AddStep("start full gate", () => full = probe.GetDifficultyAsync(map, map.Ruleset, cancellationToken: fullOwner.Token));
            AddUntilStep("full worker entered", () => probe.FullEntered.IsSet);
            AddStep("release preview and request unrelated map", () =>
            {
                probe.ReleasePreview();
                _ = probe.GetBindableDifficulty(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = map.Ruleset }, calculatePerformance: false);
            });
            AddUntilStep("unrelated preview progressed while full pending", () => probe.PreviewStarts == 2);
            AddAssert("full still pending", () => !full.IsCompleted);
            AddStep("finish or invalidate joined work", () =>
            {
                if (action == "cancel-preview") owner.Cancel();
                if (action == "clear") probe.Clear();
                if (action == "cancel-full") fullOwner.Cancel();
                probe.ReleaseFull();
            });
            AddUntilStep("all requests drained", () => full.IsCompleted && probe.ActiveRequests == 0);
            AddAssert("target converts only after full failure", () => probe.PreviewStarts, () => Is.EqualTo(fallback ? 3 : 2));
            AddUntilStep("only valid target result published", () => action == "success" ? preview.Value.Stars == 6.5
                : fallback ? preview.Value.Stars == 5.5 : preview.Value.Stars != 6.5);
            AddStep("dispose owners and observe full failure", () =>
            {
                _ = full.Exception;
                owner.Cancel(); owner.Dispose();
                fullOwner.Cancel(); fullOwner.Dispose();
            });
        }

        private partial class GatedQueueProbe : BeatmapDifficultyCache
        {
            private readonly ManualResetEventSlim releasePreview = new ManualResetEventSlim();
            private readonly ManualResetEventSlim releaseFull = new ManualResetEventSlim();
            public readonly ManualResetEventSlim FirstPreviewEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim FullEntered = new ManualResetEventSlim();
            public bool GateFull;
            public string FullOutcome;
            private int previewStarts;
            private int fullStarts;
            private int previewActive;
            private int fullActive;
            private int maximumPreviewActive;
            private int maximumFullActive;

            public int PreviewStarts => Volatile.Read(ref previewStarts);
            public int FullStarts => Volatile.Read(ref fullStarts);
            public int MaximumPreviewActive => Volatile.Read(ref maximumPreviewActive);
            public int MaximumFullActive => Volatile.Read(ref maximumFullActive);
            public bool PreviewReleased => releasePreview.IsSet;
            public int ActiveRequests
            {
                get
                {
                    object requests = typeof(BeatmapDifficultyCache).BaseType.GetField("activeRequests", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
                    return (int)requests.GetType().GetProperty("Count").GetValue(requests);
                }
            }

            public void ReleasePreview() => releasePreview.Set();
            public void ReleaseFull() => releaseFull.Set();

            protected override StarDifficulty? computeDifficulty(in DifficultyCacheLookup key, CancellationToken cancellationToken = default, bool calculatePerformance = true)
            {
                if (calculatePerformance)
                {
                    Interlocked.Increment(ref fullStarts);
                    int running = Interlocked.Increment(ref fullActive);
                    Interlocked.Exchange(ref maximumFullActive, Math.Max(running, MaximumFullActive));
                    try
                    {
                        FullEntered.Set();
                        if (GateFull && !releaseFull.Wait(TimeSpan.FromSeconds(15), cancellationToken))
                            throw new TimeoutException("full gate timed out");
                        cancellationToken.ThrowIfCancellationRequested();
                        if (FullOutcome == "null-full") return null;
                        if (FullOutcome == "fault-full") throw new InvalidOperationException("expected full failure");
                        return new StarDifficulty(new osu.Game.Rulesets.Difficulty.DifficultyAttributes { StarRating = 6.5, MaxCombo = 100 });
                    }
                    finally { Interlocked.Decrement(ref fullActive); }
                }
                Interlocked.Increment(ref previewStarts);
                int active = Interlocked.Increment(ref previewActive);
                Interlocked.Exchange(ref maximumPreviewActive, Math.Max(active, MaximumPreviewActive));
                FirstPreviewEntered.Set();
                try
                {
                    if (!releasePreview.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("preview gate timed out");
                    return new StarDifficulty(5.5, 100);
                }
                finally { Interlocked.Decrement(ref previewActive); }
            }

            protected override void Dispose(bool isDisposing)
            {
                releasePreview.Set();
                releaseFull.Set();
                base.Dispose(isDisposing);
                releasePreview.Dispose();
                releaseFull.Dispose();
                FirstPreviewEntered.Dispose();
                FullEntered.Dispose();
            }
        }

        private partial class RealDifficultyProbe : BeatmapDifficultyCache
        {
            private readonly TaskCompletionSource release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            private int fullStarts;
            private int previewStarts;
            private long fullElapsedTicks;

            public int FullStarts => Volatile.Read(ref fullStarts);
            public int PreviewStarts => Volatile.Read(ref previewStarts);
            public double FullElapsedMilliseconds => fullElapsedTicks * 1000.0 / Stopwatch.Frequency;

            public void Release() => release.TrySetResult();

            protected override async Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken token = default)
            {
                if (lookup.CalculatePerformance)
                    Interlocked.Increment(ref fullStarts);
                else
                    Interlocked.Increment(ref previewStarts);
                await release.Task.WaitAsync(token).ConfigureAwait(false);
                long start = Stopwatch.GetTimestamp();
                var result = await base.ComputeValueAsync(lookup, token).ConfigureAwait(false);
                if (lookup.CalculatePerformance)
                    Interlocked.Exchange(ref fullElapsedTicks, Stopwatch.GetTimestamp() - start);
                return result;
            }
        }

        private partial class TestBeatmapDifficultyCache : BeatmapDifficultyCache
        {
            public Func<DifficultyCacheLookup, StarDifficulty> ComputeDifficulty { get; set; }
            public bool ComputeAsynchronously { get; set; }

            public void SeedPreview(DifficultyCacheLookup lookup, StarDifficulty value) => OnValueComputed(lookup, value);

            public bool TryPreview(DifficultyCacheLookup lookup, out StarDifficulty value) => TryLightPreview(lookup, out value);

            public bool HasCachedResult(BeatmapInfo beatmap) => CheckExists(new DifficultyCacheLookup(beatmap, beatmap.Ruleset, null), out _);

            public bool HasCachedPreviewResult(BeatmapInfo beatmap) => CheckExists(new DifficultyCacheLookup(beatmap, beatmap.Ruleset, null, false), out _);

            protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken token = default)
            {
                if (ComputeAsynchronously)
                    return Task.Run<StarDifficulty?>(() => ComputeDifficulty?.Invoke(lookup) ?? new StarDifficulty(BASE_STARS, 0), token);

                return Task.FromResult<StarDifficulty?>(ComputeDifficulty?.Invoke(lookup) ?? new StarDifficulty(BASE_STARS, 0));
            }
        }
    }
}
