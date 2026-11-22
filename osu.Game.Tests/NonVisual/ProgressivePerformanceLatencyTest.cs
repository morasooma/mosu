using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Opt-in headless queue measurement. Not a rendering/FPS benchmark: a paced producer
    /// drives the real worker, with optional simultaneous all-prefix difficulty work.
    /// Run by exact NUnit method name with DOTNET_TieredCompilation=0 for comparisons.
    /// </summary>
    [TestFixture]
    public class ProgressivePerformanceLatencyTest
    {
        [TestCase(false)]
        [TestCase(true)]
        [Explicit("Manual headless latency/CPU measurement, not a timing assertion")]
        public void MeasurePacedWorkerWithConcurrentDifficulty(bool concurrentDifficulty)
        {
            var ruleset = new OsuRuleset();
            Mod[] mods = { new OsuModHidden(), new OsuModFlashlight() };
            var raw = new Beatmap<OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 4506).Select(i => (OsuHitObject)new HitCircle
                {
                    StartTime = i * 120,
                    Position = new osuTK.Vector2(60 + i % 4 * 130, 60 + i % 3 * 120)
                }).ToList()
            };
            var working = new FlatWorkingBeatmap(raw);
            var times = new OsuDifficultyCalculator(ruleset.RulesetInfo, working).CalculateTimed(mods).Select(a => a.Time).ToArray();
            var calculator = new OsuDifficultyCalculator(ruleset.RulesetInfo, working);
            using (var warm = calculator.CreateProgressiveCalculation(mods))
                warm!.GetAttributesAtTime(times[0]);
            var performance = new OsuPerformanceCalculator();
            var latency = new List<double>();
            var producerDelay = new List<double>();
            var queueCost = new List<double>();
            long finalStamp = 0;
            long latestCompleted = 0;
            Exception? error = null;
            using var finished = new ManualResetEventSlim();
            using var competingStop = new CancellationTokenSource();
            using var competingStarted = new ManualResetEventSlim();
            Thread? competing = null;
            int fullPasses = 0;
            if (concurrentDifficulty)
            {
                competing = new Thread(() =>
                {
                    competingStarted.Set();
                    try
                    {
                        while (!competingStop.IsCancellationRequested)
                        {
                            new OsuDifficultyCalculator(ruleset.RulesetInfo, working).CalculateTimed(mods, competingStop.Token);
                            Interlocked.Increment(ref fullPasses);
                        }
                    }
                    catch (OperationCanceledException) { }
                }) { IsBackground = true };
                competing.Start();
                Assert.That(competingStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
            }
            try
            {
                using var worker = new ProgressivePerformanceCalculation(
                    token => new OsuDifficultyCalculator(ruleset.RulesetInfo, working).CreateProgressiveCalculation(mods, token)!,
                    new TimestampPerformance(performance), (result, _) =>
                    {
                        lock (latency)
                        {
                            latestCompleted = (long)result.Total;
                            latency.Add((Stopwatch.GetTimestamp() - latestCompleted) * 1000.0 / Stopwatch.Frequency);
                            if (latestCompleted == Volatile.Read(ref finalStamp))
                                finished.Set();
                        }
                    }, e => { error = e; finished.Set(); });
                var source = new ScoreInfo(raw.BeatmapInfo, ruleset.RulesetInfo) { Mods = mods, Accuracy = .98, MaxCombo = 1000 };
                source.Statistics[HitResult.Great] = 1000;
                var snapshots = new LivePerformanceScoreSnapshot(source);
                using var process = Process.GetCurrentProcess();
                var cpu = process.TotalProcessorTime;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 600; i++)
                {
                    double wait = i * 5 - (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                    if (wait > 0)
                        Thread.Sleep(TimeSpan.FromMilliseconds(wait));
                    producerDelay.Add(Math.Max(0, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency - i * 5));
                    long stamp = Stopwatch.GetTimestamp();
                    source.TotalScore = stamp;
                    if (i == 599)
                        Volatile.Write(ref finalStamp, stamp);
                    worker.Queue(snapshots.Create(source), times[i * 7], false);
                    queueCost.Add((Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency);
                }
                Assert.That(finished.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(error, Is.Null);
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                double wall = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                double cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                lock (latency)
                {
                    Assert.That(latestCompleted, Is.EqualTo(finalStamp));
                    latency.Sort();
                    producerDelay.Sort();
                    queueCost.Sort();
                    TestContext.Out.WriteLine($"concurrent={concurrentDifficulty}; requested=600; published={latency.Count}; " +
                                              $"latency median/p95/max={latency[latency.Count / 2]:F3}/{latency[(int)(latency.Count * .95)]:F3}/{latency[^1]:F3}ms; " +
                                              $"producer lateness p95/max={producerDelay[570]:F3}/{producerDelay[^1]:F3}ms; " +
                                              $"snapshot+queue p95/max={queueCost[570]:F3}/{queueCost[^1]:F3}ms; " +
                                              $"producer bytes={bytes}; processCPU={cpuMs:F1}ms; wall={wall:F1}ms; simultaneous full passes={fullPasses}");
                }
            }
            finally
            {
                competingStop.Cancel();
                Assert.That(competing == null || competing.Join(TimeSpan.FromSeconds(5)), Is.True);
            }
        }

        private class TimestampPerformance : PerformanceCalculator
        {
            private readonly PerformanceCalculator inner;
            public TimestampPerformance(PerformanceCalculator inner) : base(null!) => this.inner = inner;
            protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
            {
                inner.Calculate(score, attributes);
                return new PerformanceAttributes { Total = score.TotalScore };
            }
        }
    }
}
