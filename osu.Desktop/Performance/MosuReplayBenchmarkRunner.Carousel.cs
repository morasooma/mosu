// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Desktop.Performance
{
    internal partial class MosuReplayBenchmarkRunner
    {
        private SoloSongSelect? carouselScreen;
        private BeatmapCarousel? benchmarkCarousel;
        private ICarouselBenchmarkController? carouselController;
        private BeatmapInfo? carouselTarget;
        private DateTime carouselPreparationDeadline;
        private bool carouselRunActive;
        private bool carouselScreenshotTaken;
        private double carouselRunElapsed;
        private double nextCarouselKeyPress;
        private int carouselKeyPressCount;

        [Resolved]
        private ScreenshotManager screenshotManager { get; set; } = null!;

        private bool isCarouselBenchmark => string.Equals(options?.BenchmarkMode, "carousel", StringComparison.OrdinalIgnoreCase);

        protected override void Update()
        {
            base.Update();
            updateCarouselBenchmark();
        }

        private void tryStartCarouselBenchmark()
        {
            if (options == null)
                return;

            if (carouselScreen == null)
            {
                carouselPreparationDeadline = DateTime.UtcNow.AddMilliseconds(options.RunTimeoutMs);
                carouselScreen = new SoloSongSelect();
                game.ScreenStack.Push(carouselScreen);
                Scheduler.AddDelayed(tryStartCarouselBenchmark, 100);
                return;
            }

            if (DateTime.UtcNow >= carouselPreparationDeadline)
            {
                failBenchmark("carousel_preparation_timeout");
                return;
            }

            if (!ReferenceEquals(game.ScreenStack.CurrentScreen, carouselScreen) || !carouselScreen.IsLoaded)
            {
                Scheduler.AddDelayed(tryStartCarouselBenchmark, 100);
                return;
            }

            benchmarkCarousel ??= carouselScreen.ChildrenOfType<BeatmapCarousel>().SingleOrDefault();

            if (benchmarkCarousel == null || benchmarkCarousel.IsFiltering || benchmarkCarousel.GetCarouselItems() == null)
            {
                Scheduler.AddDelayed(tryStartCarouselBenchmark, 100);
                return;
            }

            carouselController = benchmarkCarousel;

            var carouselModels = benchmarkCarousel.GetCarouselItems()!
                                                      .Select(item => item.Model)
                                                      .ToList();
            var beatmapSets = carouselModels.OfType<GroupedBeatmapSet>().ToList();

            if (beatmapSets.Count == 0)
            {
                failBenchmark("carousel_contains_no_beatmaps");
                return;
            }

            BeatmapInfo? target = null;

            if (options.CarouselBeatmapOnlineId > 0)
            {
                target = carouselModels.OfType<GroupedBeatmap>()
                                       .LastOrDefault(grouped => grouped.Beatmap.OnlineID == options.CarouselBeatmapOnlineId)
                                       ?.Beatmap;

                target ??= beatmapSets.Select(set => set.BeatmapSet)
                                      .SelectMany(set => set.Beatmaps)
                                      .LastOrDefault(beatmap => beatmap.OnlineID == options.CarouselBeatmapOnlineId);

                target ??= beatmapSets.LastOrDefault(set => set.BeatmapSet.OnlineID == options.CarouselBeatmapOnlineId)
                                     ?.BeatmapSet.Beatmaps.LastOrDefault();
            }
            else
            {
                // Pick a beatmap from the last set rather than the last expanded difficulty.
                // Expanded difficulties depend on the selection persisted by the previous launch,
                // while the last set is stable and corresponds to the actual bottom of the carousel.
                target = beatmapSets[^1].BeatmapSet.Beatmaps.LastOrDefault();
            }

            if (target == null)
            {
                failBenchmark($"carousel_target_missing:{options.CarouselBeatmapOnlineId}");
                return;
            }

            carouselTarget = target;
            replayHash = $"carousel-beatmap-{carouselTarget.OnlineID}";
            benchmarkCarousel.CurrentBeatmap = carouselTarget;
            benchmarkCarousel.ScrollToSelection(immediate: true);

            Logger.Log(
                $"mosu carousel benchmark starting: id={benchmarkId}, target={carouselTarget}, online_id={carouselTarget.OnlineID},"
                + $" items={benchmarkCarousel.DisplayableItems}, duration={options.CarouselDurationMs:0}ms,"
                + $" runs={options.MeasuredRuns}, warmups={options.WarmupRuns}, config={describeConfig()}",
                LoggingTarget.Runtime,
                LogLevel.Important);

            writeManifest(
                "start",
                0,
                false,
                $"target_online_id={carouselTarget.OnlineID};target={carouselTarget};items={benchmarkCarousel.DisplayableItems};"
                + $"duration_ms={options.CarouselDurationMs:0};action=ActivateNextSet;repeat_ms={options.CarouselKeyRepeatMs:0};"
                + $"config={describeConfig()};renderer={describeRenderer()}");
            logRendererPath();

            Scheduler.AddDelayed(startNextCarouselRun, options.BetweenRunsDelayMs);
        }

        private void startNextCarouselRun()
        {
            if (options == null || benchmarkCarousel == null || carouselController == null || carouselTarget == null)
                return;

            if (!ensureBenchmarkWindowActive(startNextCarouselRun))
                return;

            currentRun++;

            if (currentRun > options.TotalRuns)
            {
                finishBenchmark();
                return;
            }

            currentRunCompleted = false;
            carouselRunActive = false;
            carouselRunElapsed = 0;
            nextCarouselKeyPress = 0;
            carouselKeyPressCount = 0;
            carouselScreenshotTaken = false;

            benchmarkCarousel.CurrentBeatmap = carouselTarget;
            benchmarkCarousel.ScrollToSelection(immediate: true);
            carouselController.ScrollToBenchmarkPosition(carouselController.BenchmarkScrollableExtent);

            bool warmup = currentRun <= options.WarmupRuns;
            GameplayPerformanceSnapshot.SetBenchmark(benchmarkId, "carousel", currentRun, warmup, replayHash);
            writeManifest("run_prepare", currentRun, warmup, $"scroll_extent={carouselController.BenchmarkScrollableExtent:0.###}");

            watchdogDelegate?.Cancel();
            watchdogDelegate = Scheduler.AddDelayed(() => failBenchmark($"carousel_run_timeout:{currentRun}"), options.RunTimeoutMs);
            Scheduler.AddDelayed(beginCarouselScroll, options.CarouselSettleMs);
        }

        private void beginCarouselScroll()
        {
            if (options == null || carouselController == null)
                return;

            carouselController.ScrollToBenchmarkPosition(carouselController.BenchmarkScrollableExtent);
            carouselRunElapsed = 0;
            nextCarouselKeyPress = 0;
            carouselKeyPressCount = 0;
            carouselRunActive = true;

            bool warmup = currentRun <= options.WarmupRuns;
            GameplayPerformanceSnapshot.SetBenchmarkGameplayActive(true);
            writeManifest("carousel_scroll_start", currentRun, warmup, $"scroll_extent={carouselController.BenchmarkScrollableExtent:0.###}");
            Logger.Log($"mosu carousel benchmark run {currentRun}/{options.TotalRuns} scrolling (warmup={warmup}).", LoggingTarget.Runtime, LogLevel.Important);
        }

        private void updateCarouselBenchmark()
        {
            if (!carouselRunActive || options == null || carouselController == null)
                return;

            carouselRunElapsed += Time.Elapsed;

            double progress = Math.Clamp(carouselRunElapsed / options.CarouselDurationMs, 0, 1);

            if (carouselRunElapsed < options.CarouselDurationMs && carouselRunElapsed >= nextCarouselKeyPress)
            {
                carouselController.ActivateNextSetForBenchmark();
                nextCarouselKeyPress += options.CarouselKeyRepeatMs;
                carouselKeyPressCount++;
            }

            // Screenshot readback is intentionally limited to the warmup so measured runs remain clean.
            if (!carouselScreenshotTaken && currentRun <= options.WarmupRuns && progress >= 0.25)
            {
                carouselScreenshotTaken = true;
                _ = screenshotManager.TakeScreenshotAsync();
                writeManifest("carousel_screenshot", currentRun, true, $"progress={progress:0.###};action=ActivateNextSet");
            }

            if (progress >= 1)
                completeCarouselRun();
        }

        private void completeCarouselRun()
        {
            if (options == null || currentRunCompleted)
                return;

            carouselRunActive = false;
            currentRunCompleted = true;
            GameplayPerformanceSnapshot.SetBenchmarkGameplayActive(false);
            watchdogDelegate?.Cancel();
            watchdogDelegate = null;

            bool warmup = currentRun <= options.WarmupRuns;
            writeManifest("run_complete", currentRun, warmup, $"carousel_key_traversal_complete;action=ActivateNextSet;presses={carouselKeyPressCount}");
            Logger.Log($"mosu carousel benchmark run {currentRun}/{options.TotalRuns} completed (warmup={warmup}).", LoggingTarget.Runtime, LogLevel.Important);

            Scheduler.AddDelayed(startNextCarouselRun, options.BetweenRunsDelayMs);
        }

        private void cleanupCarouselBenchmark()
        {
            carouselRunActive = false;
            benchmarkCarousel = null;
            carouselController = null;
            carouselTarget = null;
            carouselScreen = null;
        }
    }
}
