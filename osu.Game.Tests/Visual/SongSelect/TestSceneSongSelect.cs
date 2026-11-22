// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Performance.Debug;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning.Select;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;
using BeatmapCarousel = osu.Game.Screens.Select.BeatmapCarousel;
using FooterButtonMods = osu.Game.Screens.Select.FooterButtonMods;
using FooterButtonOptions = osu.Game.Screens.Select.FooterButtonOptions;
using FooterButtonRandom = osu.Game.Screens.Select.FooterButtonRandom;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneSongSelect : SongSelectTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private bool measureNavigationFrames;
        private long lastNavigationFrame;
        private readonly double[] navigationFrameTimes = new double[100000];
        private int navigationFrameCount;
        private int navigationSlowFrames;
        private double navigationMaxFrameMs;
        private double navigationMaxWorkMs;

        protected override void Update()
        {
            base.Update();
            if (!measureNavigationFrames || navigationFrameCount >= navigationFrameTimes.Length)
                return;
            long now = Stopwatch.GetTimestamp();
            if (lastNavigationFrame != 0)
            {
                double elapsed = Stopwatch.GetElapsedTime(lastNavigationFrame, now).TotalMilliseconds;
                navigationFrameTimes[navigationFrameCount++] = elapsed;
                navigationMaxFrameMs = Math.Max(navigationMaxFrameMs, elapsed);
                if (elapsed > 50) navigationSlowFrames++;
                navigationMaxWorkMs = Math.Max(navigationMaxWorkMs, Dependencies.Get<GameHost>().UpdateThread.PerformanceSnapshot.WorkMs);
            }
            lastNavigationFrame = now;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestRealMapScoreNavigationThroughSongSelectPanels(bool virtualAudio)
        {
            BeatmapSetInfo imported = null!;
            int selected = 0;
            int scores = 0;
            long allocated = 0;
            long start = 0;
            AddStep("import real multi-difficulty .osz", () =>
            {
                Beatmaps.Import(TestResources.GetTestBeatmapForImport(virtualTrack: virtualAudio)).WaitSafely();
                imported = Beatmaps.GetAllUsableBeatmapSets().Single();
                Assert.That(imported.Beatmaps.Count, Is.GreaterThan(1));
                foreach (var beatmap in imported.Beatmaps)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var score = TestResources.CreateTestScoreInfo(beatmap);
                        score.Hash = Guid.NewGuid().ToString();
                        score.User = API.LocalUser.Value;
                        score.TotalScore += i;
                        ScoreManager.Import(score);
                        scores++;
                    }
                }
            });
            LoadSongSelect();
            AddUntilStep("imported set visible in real carousel", () => Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single().SetItems.Count >= 1);
            AddStep("select imported ruleset", () => Ruleset.Value = imported.Beatmaps[0].Ruleset);
            AddStep("start measured navigation", () =>
            {
                PerformancePhaseLogger.Enabled = true;
                Bindable<WorkingBeatmap>.TraceSlowValueChanges = true;
                Dependencies.Get<GameHost>().SetUpdateTreeDiagnostics(true);
                navigationFrameCount = navigationSlowFrames = 0;
                navigationMaxFrameMs = navigationMaxWorkMs = 0;
                allocated = GC.GetTotalAllocatedBytes(false);
                start = Stopwatch.GetTimestamp();
                lastNavigationFrame = start;
                measureNavigationFrames = true;
            });
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (int index in Enumerable.Range(0, 12))
                {
                    int captured = index;
                    AddStep($"select real map {index}, pass {pass}", () =>
                    {
                        var target = imported.Beatmaps[(captured + 1) % imported.Beatmaps.Count];
                        Ruleset.Value = target.Ruleset;
                        Assert.That(SongSelect.IsCurrentScreen(), Is.True, "selection screen must be current");
                        Assert.That(target.ID, Is.Not.EqualTo(Beatmap.Value.BeatmapInfo.ID), "navigation must change selection");
                        var requestSelection = (Action<GroupedBeatmap>)typeof(BeatmapCarousel)
                            .GetProperty(nameof(BeatmapCarousel.RequestSelection))!.GetGetMethod(true)!.Invoke(Carousel, null)!;
                        requestSelection(new GroupedBeatmap(null, target));
                        selected++;
                    });
                    AddUntilStep("selection follows real map", () => Carousel.CurrentBeatmap == imported.Beatmaps[(captured + 1) % imported.Beatmaps.Count]);
                    AddUntilStep("global beatmap selection committed", () => Beatmap.Value.BeatmapInfo.ID == imported.Beatmaps[(captured + 1) % imported.Beatmaps.Count].ID);
                    AddWaitStep("allow preview/full calculation and rank snapshot", 90);
                }
            }
            AddStep("record measured navigation", () =>
            {
                measureNavigationFrames = false;
                PerformancePhaseLogger.Enabled = false;
                Bindable<WorkingBeatmap>.TraceSlowValueChanges = false;
                Dependencies.Get<GameHost>().SetUpdateTreeDiagnostics(false);
                Array.Sort(navigationFrameTimes, 0, navigationFrameCount);
                TestContext.Progress.WriteLine($"SONG_SELECT_NAV virtual_audio={virtualAudio} real_maps={imported.Beatmaps.Count} scores={scores} selected={selected} "
                                               + $"frames={navigationFrameCount} frame_p95_ms={navigationFrameTimes[(int)(navigationFrameCount * .95)]:F1} "
                                               + $"frame_max_ms={navigationMaxFrameMs:F1} frame_stalls_gt50={navigationSlowFrames} update_work_max_ms={navigationMaxWorkMs:F1} "
                                               + $"wall_ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F1} "
                                               + $"process_alloc_mib={(GC.GetTotalAllocatedBytes(false) - allocated) / 1048576.0:F2} "
                                               + $"managed_retained_mib={GC.GetTotalMemory(false) / 1048576.0:F2} "
                                               + $"rss_mib={Process.GetCurrentProcess().WorkingSet64 / 1048576.0:F2}");
                Assert.That(selected, Is.EqualTo(36));
            });
            AddStep("return from song select", () => SongSelect.Exit());
            AddUntilStep("song select exited", () => !SongSelect.IsCurrentScreen());
        }

        [TestCase("none")]
        [TestCase("DT")]
        [TestCase("HR")]
        [TestCase("RX")]
        public void TestLongMapModdedNavigation(string mod)
        {
            string? fixtureDirectory = Environment.GetEnvironmentVariable("MOSU_LONG_MAP_FIXTURES");
            if (string.IsNullOrEmpty(fixtureDirectory))
                Assert.Ignore("Set MOSU_LONG_MAP_FIXTURES to disposable virtual-audio long-map archives.");

            BeatmapInfo[] maps = null!;
            long allocated = 0;
            long pauseTicks = 0;
            long start = 0;
            double longestPlayableTime = 0;
            AddStep("import isolated long maps", () =>
            {
                foreach (string source in Directory.GetFiles(fixtureDirectory!, "*.osz"))
                {
                    // Path imports delete their input. Import a disposable copy, never the fixture itself.
                    string copy = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.osz");
                    File.Copy(source, copy);
                    Beatmaps.Import(copy).WaitSafely();
                }
                maps = Beatmaps.GetAllUsableBeatmapSets().SelectMany(s => s.Beatmaps).OrderBy(b => b.Metadata.Title).ToArray();
                Assert.That(maps.Length, Is.GreaterThanOrEqualTo(3));
                foreach (var map in maps)
                {
                    // Import metadata is not guaranteed to contain Length yet.
                    var decoded = Beatmaps.GetWorkingBeatmap(map).Beatmap;
                    longestPlayableTime = Math.Max(longestPlayableTime, decoded.HitObjects.Max(h => h.StartTime));
                }
                Assert.That(longestPlayableTime, Is.GreaterThan(600_000), "fixture must contain actual ten-minute hitobject data");
                Ruleset.Value = maps[0].Ruleset;
            });
            LoadSongSelect();
            AddUntilStep("all sets available", () => Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single().SetItems.Count >= maps.Length);
            AddStep("apply measured mods", () => SelectedMods.Value = mod switch
            {
                "DT" => new Mod[] { new OsuModDoubleTime() },
                "HR" => new Mod[] { new OsuModHardRock() },
                "RX" => new Mod[] { new OsuModRelax() },
                _ => Array.Empty<Mod>()
            });
            AddStep("start frame measurement", () =>
            {
                navigationFrameCount = navigationSlowFrames = 0;
                navigationMaxFrameMs = navigationMaxWorkMs = 0;
                allocated = GC.GetTotalAllocatedBytes(false);
                pauseTicks = GC.GetTotalPauseDuration().Ticks;
                start = lastNavigationFrame = Stopwatch.GetTimestamp();
                measureNavigationFrames = true;
            });
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < 6; i++)
                {
                    int index = i;
                    AddStep($"select long fixture {i}, pass {pass}", () =>
                    {
                        var request = (Action<GroupedBeatmap>)typeof(BeatmapCarousel)
                            .GetProperty(nameof(BeatmapCarousel.RequestSelection))!.GetGetMethod(true)!.Invoke(Carousel, null)!;
                        request(new GroupedBeatmap(null, maps[index % maps.Length]));
                    });
                    AddUntilStep("global selection committed", () => Beatmap.Value.BeatmapInfo.ID == maps[index % maps.Length].ID);
                    AddWaitStep("exercise selected and preview calculations", 60);
                }
            }
            AddStep("report long map frames", () =>
            {
                measureNavigationFrames = false;
                Array.Sort(navigationFrameTimes, 0, navigationFrameCount);
                TestContext.Progress.WriteLine($"LONG_MAP_NAV mod={mod} maps={maps.Length} max_object_time_ms={longestPlayableTime} "
                                               + $"frames={navigationFrameCount} p95_ms={navigationFrameTimes[(int)(navigationFrameCount * .95)]:F3} "
                                               + $"max_frame_ms={navigationMaxFrameMs:F3} max_update_work_ms={navigationMaxWorkMs:F3} stalls_gt50={navigationSlowFrames} "
                                               + $"alloc_mib={(GC.GetTotalAllocatedBytes(false) - allocated) / 1048576.0:F2} "
                                               + $"gc_pause_ms={TimeSpan.FromTicks(GC.GetTotalPauseDuration().Ticks - pauseTicks).TotalMilliseconds:F3} "
                                               + $"wall_ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F3}");
            });
            AddStep("return from selection", () => SongSelect.Exit());
            AddUntilStep("selection exited", () => !SongSelect.IsCurrentScreen());
        }

        [Test]
        public void TestExitedSongSelectReleasedAfterDisposal()
        {
            var exitedScreens = new List<WeakReference<Screens.Select.SongSelect>>();
            for (int pass = 0; pass < 3; pass++)
            {
                LoadSongSelect();
                AddStep("exit small song select", () =>
                {
                    exitedScreens.Add(new WeakReference<Screens.Select.SongSelect>(SongSelect));
                    SongSelect.Exit();
                });
                AddUntilStep("exited screen removed from stack", () =>
                    exitedScreens[^1].TryGetTarget(out var screen) && !Stack.ChildrenOfType<Screens.Select.SongSelect>().Contains(screen));
                AddUntilStep("exited screen disposed", () =>
                    exitedScreens[^1].TryGetTarget(out var screen) && (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!);
                AddStep("release scene screen reference", ReleaseExitedSongSelect);
            }
            AddStep("collect exited screens", () =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                TestContext.Progress.WriteLine($"SMALL_EXIT pid={Environment.ProcessId} alive={string.Join(',', exitedScreens.Select(screen => screen.TryGetTarget(out _)))} managed_mib={GC.GetTotalMemory(false) / 1048576.0:F1}");
                Assert.That(exitedScreens.Count(screen => screen.TryGetTarget(out _)), Is.Zero);
            });
        }

        [Test]
        [Explicit("Local diagnostic requires replay-steps.tsv and actual-maps.osz from the private /tmp/mosu-actual-replay-h7TlOPKs fixture.")]
        public void TestHistoricalSecondLaunchActualMapNavigation()
        {
            const string fixture = "/tmp/mosu-actual-replay-h7TlOPKs";
            var sequence = File.ReadAllLines(Path.Combine(fixture, "replay-steps.tsv"))
                               .Select(line => line.Split('\t'))
                               .Select(fields => (id: int.Parse(fields[0], CultureInfo.InvariantCulture), delay: int.Parse(fields[1], CultureInfo.InvariantCulture)))
                               .ToArray();
            int slotCount = Environment.GetEnvironmentVariable("MOSU_ACTUAL_REPLAY_SLOTS") is { } limit ? int.Parse(limit, CultureInfo.InvariantCulture) : sequence.Length;
            Assert.That(slotCount, Is.InRange(1, sequence.Length));
            Dictionary<int, BeatmapInfo> maps = null!;
            BeatmapDifficultyCache resultCache = null!;
            AddStep("import original-content virtual-audio maps", () =>
            {
                resultCache = difficultyCache;
                // Path-based imports delete source archives on success; preserve this disposable fixture across runs.
                Beatmaps.Import(new[] { new ImportTask(File.OpenRead(Path.Combine(fixture, "actual-maps.osz")), "actual-maps.osz") }).WaitSafely();
                maps = Beatmaps.GetAllUsableBeatmapSets().SelectMany(set => set.Beatmaps).ToDictionary(map => map.OnlineID);
                Assert.That(maps.Count, Is.EqualTo(166));
                Assert.That(maps.Values.Select(map => map.Hash).Distinct().Count(), Is.EqualTo(166));
                Assert.That(sequence.Length, Is.EqualTo(180));
                Assert.That(sequence.Count(entry => entry.id == -1), Is.EqualTo(2));
            });
            string archiveHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture, "actual-maps.osz"))));
            string stepsHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture, "replay-steps.tsv"))));
            TestContext.Progress.WriteLine($"ACTUAL_INPUT archive_sha256={archiveHash} steps_sha256={stepsHash} source_sha256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs"))))} binary_sha256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(BeatmapDifficultyCache).Assembly.Location)))}");
            int passes = Environment.GetEnvironmentVariable("MOSU_ACTUAL_REPLAY_REPEAT") is { } repeat ? int.Parse(repeat, CultureInfo.InvariantCulture) : 1;
            Assert.That(passes, Is.InRange(1, 4));
            WeakReference<Screens.Select.SongSelect>? previousScreen = null;
            int disposedScreens = 0;
            var exitedScreens = new List<WeakReference<Screens.Select.SongSelect>>();
            for (int pass = 1; pass <= passes; pass++)
            {
            int replayPass = pass;
            int committed = 0;
            long allocated = 0;
            long started = 0;
            long cumulativeDelay = 0;
            int lateSlots = 0;
            double maximumLatenessMs = 0;
            LoadSongSelect();
            AddUntilStep("actual maps visible", () => Carousel.MatchedBeatmapsCount >= 166);
            AddStep("begin historical replay", () =>
            {
                navigationFrameCount = navigationSlowFrames = 0;
                navigationMaxFrameMs = navigationMaxWorkMs = 0;
                lastNavigationFrame = started = Stopwatch.GetTimestamp();
                allocated = GC.GetTotalAllocatedBytes(false);
                measureNavigationFrames = true;
            });
            long plannedDelay = 0;
            for (int index = 0; index < slotCount; index++)
            {
                int slot = index;
                var (id, delay) = sequence[index];
                double expectedCommitMs = plannedDelay;
                if (id > 0)
                {
                    AddStep($"request historical selection {index}", () =>
                    {
                        var target = maps[id];
                        Ruleset.Value = target.Ruleset;
                        var requestSelection = (Action<GroupedBeatmap>)typeof(BeatmapCarousel)
                            .GetProperty(nameof(BeatmapCarousel.RequestSelection))!.GetGetMethod(true)!.Invoke(Carousel, null)!;
                        requestSelection(new GroupedBeatmap(null, target));
                    });
                    AddUntilStep("carousel selected original map", () => Carousel.CurrentBeatmap?.OnlineID == id);
                    AddUntilStep("global selection committed original map", () => Beatmap.Value.BeatmapInfo.OnlineID == id);
                    AddStep($"record global commit {slot}", () =>
                    {
                        committed++;
                        double actualMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                        TestContext.Progress.WriteLine($"ACTUAL_COMMIT slot={slot} id={id} elapsed_ms={actualMs:F1} expected_ms={expectedCommitMs:F1} delta_ms={actualMs - expectedCommitMs:F1}");
                    });
                }
                // Compare each commit against the original cumulative timestamp; do not add an entire frame wait per historical 10 ms.
                AddStep($"record pacing lateness {slot}", () =>
                {
                    double lateness = Stopwatch.GetElapsedTime(started).TotalMilliseconds - expectedCommitMs;
                    if (lateness <= 0) return;
                    lateSlots++;
                    maximumLatenessMs = Math.Max(maximumLatenessMs, lateness);
                    TestContext.Progress.WriteLine($"ACTUAL_LATE slot={slot} id={id} late_ms={lateness:F1}");
                });
                // The two -1 commits cannot be mapped to an original file. Keep their time slots, not substitute maps.
                plannedDelay += delay;
                long deadlineMs = plannedDelay;
                AddUntilStep($"historical timestamp after {slot}", () => Stopwatch.GetElapsedTime(started).TotalMilliseconds >= deadlineMs);
            }
            long passDelay = plannedDelay;
            AddStep("record historical duration", () => cumulativeDelay = passDelay);
            AddWaitStep("drain pending calculations", 120);
            AddStep("record historical replay", () =>
            {
                measureNavigationFrames = false;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                TestContext.Progress.WriteLine($"ACTUAL_REPLAY pass={replayPass} slots={slotCount} commits={committed} historical_ms={cumulativeDelay} late_slots={lateSlots} max_late_ms={maximumLatenessMs:F1} elapsed_ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} "
                                               + $"process_alloc_mib={(GC.GetTotalAllocatedBytes(false) - allocated) / 1048576.0:F1} "
                                               + $"live_managed_mib={GC.GetTotalMemory(false) / 1048576.0:F1} "
                                               + $"rss_mib={Process.GetCurrentProcess().WorkingSet64 / 1048576.0:F1} "
                                               + $"frame_max_ms={navigationMaxFrameMs:F1} frame_stalls_gt50={navigationSlowFrames} update_work_max_ms={navigationMaxWorkMs:F1}");
                Assert.That(committed, Is.EqualTo(sequence.Take(slotCount).Count(entry => entry.id > 0)));
            });
            AddWaitStep("idle after replay", 200);
            AddStep("record idle heap", () =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                TestContext.Progress.WriteLine($"ACTUAL_REPLAY_IDLE pass={replayPass} heavy={resultCache.HeavyCount} light={resultCache.PreviewCount} alloc_mib={(GC.GetTotalAllocatedBytes(false) - allocated) / 1048576.0:F1} "
                                               + $"live_managed_mib={GC.GetTotalMemory(false) / 1048576.0:F1} "
                                               + $"rss_mib={Process.GetCurrentProcess().WorkingSet64 / 1048576.0:F1}");
            });
            WeakReference<Screens.Select.SongSelect>? exitedScreen = null;
            AddStep("exit historical song select", () =>
            {
                exitedScreen = new WeakReference<Screens.Select.SongSelect>(SongSelect);
                SongSelect.Exit();
            });
            AddUntilStep("historical screen removed", () =>
                exitedScreen!.TryGetTarget(out var screen) && !Stack.ChildrenOfType<Screens.Select.SongSelect>().Contains(screen));
            AddUntilStep("historical screen disposed", () =>
                exitedScreen!.TryGetTarget(out var screen) && (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!);
            AddStep("release exited historical screen", () =>
            {
                disposedScreens++;
                exitedScreens.Add(exitedScreen!);
                ReleaseExitedSongSelect();
            });
            AddStep("record exit heap", () =>
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                bool currentAlive = exitedScreen!.TryGetTarget(out _);
                bool previousAlive = previousScreen?.TryGetTarget(out _) ?? false;
                var cacheType = typeof(BeatmapDifficultyCache);
                int pendingFull = ((System.Collections.IDictionary)cacheType.GetField("pendingFull", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resultCache)!).Count;
                object requests = cacheType.BaseType!.GetField("activeRequests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resultCache)!;
                int activeRequests = (int)requests.GetType().GetProperty("Count")!.GetValue(requests)!;
                object coordinator = RankSnapshotCoordinator.For(Realm);
                object rankPanels = typeof(RankSnapshotCoordinator).GetField("panels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!;
                int rankSubscribers = (int)rankPanels.GetType().GetProperty("Count")!.GetValue(rankPanels)!;
                TestContext.Progress.WriteLine($"ACTUAL_REPLAY_EXIT pass={replayPass} heavy={resultCache.HeavyCount} light={resultCache.PreviewCount} "
                    + $"removed=true disposed={disposedScreens} active_requests={activeRequests} pending_full={pendingFull} rank_subscribers={rankSubscribers} "
                    + $"screen_alive={currentAlive} previous_screen_alive={previousAlive} all_screen_alive={exitedScreens.Count(screen => screen.TryGetTarget(out _))} "
                    + $"live_managed_mib={GC.GetTotalMemory(false) / 1048576.0:F1} rss_mib={Process.GetCurrentProcess().WorkingSet64 / 1048576.0:F1}");
                Assert.That(exitedScreens.Count(screen => screen.TryGetTarget(out _)), Is.Zero);
                previousScreen = exitedScreen;
            });
            }
        }

        [Test]
        public void TestResultsScreenWhenClickingLeaderboardScore()
        {
            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddAssert("beatmap imported", () => Beatmaps.GetAllUsableBeatmapSets().Any(), () => Is.True);

            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("import score", () =>
            {
                var beatmapInfo = Beatmaps.GetAllUsableBeatmapSets().Single().Beatmaps.First();
                ScoreManager.Import(new ScoreInfo
                {
                    Hash = Guid.NewGuid().ToString(),
                    BeatmapHash = beatmapInfo.Hash,
                    BeatmapInfo = beatmapInfo,
                    Ruleset = new OsuRuleset().RulesetInfo,
                    User = new GuestUser(),
                });
            });

            AddStep("select ranking tab", () =>
            {
                InputManager.MoveMouseTo(SongSelect.ChildrenOfType<BeatmapDetailsArea.WedgeSelector<BeatmapDetailsArea.Header.Selection>>().Last());
                InputManager.Click(MouseButton.Left);
            });

            // probably should be done via dropdown menu instead of forcing this way?
            AddStep("set local scope", () =>
            {
                var current = LeaderboardManager.CurrentCriteria!;
                LeaderboardManager.FetchWithCriteria(current with
                {
                    Scope = BeatmapLeaderboardScope.Local,
                });
            });

            AddUntilStep("wait for score panel", () => SongSelect.ChildrenOfType<BeatmapLeaderboardScore>().Any());
            AddStep("click score panel", () =>
            {
                InputManager.MoveMouseTo(SongSelect.ChildrenOfType<BeatmapLeaderboardScore>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("wait for results screen", () => Stack.CurrentScreen is ResultsScreen);
        }

        [Test]
        public void TestInitiallyPreloadedDetailsWedge()
        {
            BeatmapDetailTab originalTab = default;
            AddStep("start on details tab", () =>
            {
                originalTab = Config.Get<BeatmapDetailTab>(OsuSetting.BeatmapDetailTab);
                Config.SetValue(OsuSetting.BeatmapDetailTab, BeatmapDetailTab.Details);
            });
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            AddUntilStep("details wedge visible", () => SongSelect.ChildrenOfType<BeatmapMetadataWedge>().Any(w => w.IsPresent));
            AddStep("switch to ranking tab", () =>
                SongSelect.ChildrenOfType<BeatmapDetailsArea.WedgeSelector<BeatmapDetailsArea.Header.Selection>>().Single().SelectItem(BeatmapDetailsArea.Header.Selection.Ranking));
            AddUntilStep("ranking wedge visible", () => SongSelect.ChildrenOfType<BeatmapLeaderboardWedge>().Any(w => w.IsPresent));
            AddStep("restore selected tab", () => Config.SetValue(OsuSetting.BeatmapDetailTab, originalTab));
        }

        [Test]
        public void TestSingleFilterWhenEntering()
        {
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            AddAssert("single filter", () => Carousel.FilterCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestCarouselRestoredAfterLeavingInfiniteGlass()
        {
            ImportBeatmapForRuleset(0);
            AddStep("start in InfiniteGlass", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass));
            LoadSongSelect();

            AddUntilStep("regular carousel hidden", () => Carousel.Alpha, () => Is.Zero.Within(0.001f));
            AddAssert("hidden carousel kept updating", () => Carousel.AlwaysPresent, () => Is.True);

            BeatmapInfo? infiniteGlassBeatmap = null;
            AddStep("store InfiniteGlass selection", () => infiniteGlassBeatmap = Beatmap.Value.BeatmapInfo);
            AddStep("select next map in InfiniteGlass", () => InputManager.Key(Key.Down));
            AddUntilStep("InfiniteGlass selection changed", () => Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(infiniteGlassBeatmap!.ID));

            addTransitionAndNavigationSteps(ForkSongSelectStyle.Modern, Key.Up);

            AddStep("return to InfiniteGlass", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass));
            AddUntilStep("regular carousel hidden again", () => Carousel.Alpha, () => Is.Zero.Within(0.001f));
            AddAssert("hidden carousel still updating", () => Carousel.AlwaysPresent, () => Is.True);

            BeatmapInfo? returnedInfiniteGlassBeatmap = null;
            AddStep("store returned InfiniteGlass selection", () => returnedInfiniteGlassBeatmap = Beatmap.Value.BeatmapInfo);
            AddStep("select next map after returning to InfiniteGlass", () => InputManager.Key(Key.Down));
            AddUntilStep("returned InfiniteGlass selection changed", () => Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(returnedInfiniteGlassBeatmap!.ID));

            addTransitionAndNavigationSteps(ForkSongSelectStyle.LegacySkinned, Key.Up);

            void addTransitionAndNavigationSteps(ForkSongSelectStyle targetStyle, Key navigationKey)
            {
                AddStep($"switch to {targetStyle}", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, targetStyle));
                AddUntilStep($"{targetStyle} carousel visible", () => Carousel.Alpha, () => Is.EqualTo(1).Within(0.001f));
                AddAssert($"{targetStyle} carousel present", () => Carousel.IsPresent, () => Is.True);
                AddAssert($"{targetStyle} forced presence released", () => Carousel.AlwaysPresent, () => Is.False);

                BeatmapInfo? previousBeatmap = null;
                AddStep($"store {targetStyle} selection", () => previousBeatmap = Beatmap.Value.BeatmapInfo);
                AddStep($"select adjacent map in {targetStyle}", () => InputManager.Key(navigationKey));
                AddUntilStep($"{targetStyle} selection changed", () => Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(previousBeatmap!.ID));
                AddAssert($"{targetStyle} selected track loaded", () => Beatmap.Value.TrackLoaded, () => Is.True);
            }
        }

        [TestCase(1024)]
        [TestCase(1366)]
        public void TestStableChromeKeepsSkinScale(float viewportWidth)
        {
            ScalingContainer scalingContainer = null!;
            MarginPadding originalPadding = default;
            float originalScale = 1;
            ForkSongSelectStyle originalStyle = default;

            AddStep("set stable viewport", () =>
            {
                scalingContainer = this.ChildrenOfType<ScalingContainer>().First();
                originalPadding = Stack.Padding;
                originalScale = Config.Get<float>(OsuSetting.UIScale);
                originalStyle = Config.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);
                scalingContainer.RelativeSizeAxes = Axes.None;
                scalingContainer.Size = new Vector2(viewportWidth, 768);
                Stack.Padding = default;
                Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
            });
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            foreach (float scale in new[] { 1f, 1.25f, 0.8f })
            {
                AddStep($"set UI scale to {scale}", () => Config.SetValue(OsuSetting.UIScale, scale));
                AddUntilStep("screen applies UI scale", () => SongSelect.DrawHeight, () => Is.EqualTo(768 / scale).Within(0.01f));
                AddUntilStep("top keeps skin coordinates", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().DrawSize,
                    () => Is.EqualTo(new Vector2(viewportWidth, 768)).Using<Vector2>((actual, expected) => (actual - expected).Length < 0.01f));
                AddUntilStep("footer matches top coordinates", () => this.ChildrenOfType<LegacyFooter>().SingleOrDefault()?.DrawSize,
                    () => Is.EqualTo(new Vector2(viewportWidth, 768)).Using<Vector2>((actual, expected) => (actual - expected).Length < 0.01f));
                AddAssert("footer bottom is not shifted", () =>
                    Math.Abs(this.ChildrenOfType<LegacyFooter>().Single().ScreenSpaceDrawQuad.BottomLeft.Y
                             - SongSelect.ScreenSpaceDrawQuad.BottomLeft.Y) < 0.01f);
                AddAssert("carousel uses stable baseline", () =>
                {
                    var carouselTopLeft = SongSelect.ToLocalSpace(Carousel.ScreenSpaceDrawQuad.TopLeft);
                    return Math.Abs(carouselTopLeft.X - (viewportWidth - 544) / scale) < 0.01f
                           && Math.Abs(Carousel.DrawWidth - 544 / scale) < 0.01f;
                });
            }

            AddStep("restore viewport and settings", () =>
            {
                scalingContainer.RelativeSizeAxes = Axes.Both;
                scalingContainer.Size = Vector2.One;
                Stack.Padding = originalPadding;
                Config.SetValue(OsuSetting.UIScale, originalScale);
                Config.SetValue(OsuSetting.ForkSongSelectStyle, originalStyle);
            });
        }

        [Test]
        public void TestStableSelectionUsesDifficultyRows()
        {
            AddStep("use stable style", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            ImportBeatmapForRuleset(0);
            LoadSongSelect();

            AddUntilStep("difficulty rows loaded", () => Carousel.ChildrenOfType<PanelBeatmapStandalone>().Any(panel => panel.Item?.IsVisible == true));
            AddAssert("expanded set row is hidden", () => Carousel.ChildrenOfType<PanelBeatmapSet>().Any(panel => panel.Item?.IsVisible == true && panel.Expanded.Value), () => Is.False);
            AddAssert("selected difficulty is a visible full row", () => Carousel.ChildrenOfType<PanelBeatmapStandalone>().Any(panel => panel.Item?.IsVisible == true && panel.Selected.Value && panel.Item.Model is GroupedBeatmap grouped && grouped.Beatmap.Equals(Carousel.CurrentBeatmap)), () => Is.True);
            AddAssert("skin layer is above carousel", () => SongSelect.SkinLayerIsAboveCarousel, () => Is.True);

            BeatmapInfo? previousDifficulty = null;
            AddStep("store selected difficulty", () => previousDifficulty = Carousel.CurrentBeatmap);
            AddStep("select next difficulty", () => InputManager.Key(Key.Down));
            AddUntilStep("difficulty selection changes", () => Carousel.CurrentBeatmap, () => Is.Not.EqualTo(previousDifficulty));
            AddAssert("new selected difficulty stays visible", () => Carousel.ChildrenOfType<PanelBeatmapStandalone>().Any(panel => panel.Item?.IsVisible == true && panel.Selected.Value && panel.Item.Model is GroupedBeatmap grouped && grouped.Beatmap.Equals(Carousel.CurrentBeatmap)), () => Is.True);
            PanelBeatmapStandalone hoveredRow = null!;
            AddStep("hover selected stable row", () =>
            {
                hoveredRow = Carousel.ChildrenOfType<PanelBeatmapStandalone>().Single(panel => panel.Item?.IsVisible == true && panel.Selected.Value);
                InputManager.MoveMouseTo(hoveredRow.TopLevelContent);
            });
            AddUntilStep("hovered difficulty protrudes", () =>
            {
                var rows = Carousel.ChildrenOfType<PanelBeatmapStandalone>().Where(panel => panel.Item?.IsVisible == true).ToArray();
                return hoveredRow.IsHovered && rows.Where(panel => panel != hoveredRow).All(panel => hoveredRow.DrawPosition.X < panel.DrawPosition.X);
            });
            AddUntilStep("hover extension matches stable 45px in 480-space", () =>
            {
                float centreY = (float)(hoveredRow.DrawYPosition - Carousel.ChildrenOfType<UserTrackingScrollContainer>().Single().Current + hoveredRow.DrawHeight / 2);
                float expectedX = (StableCarouselLayout.CurveOffset(centreY, Carousel.DrawHeight)
                                   - StableCarouselLayout.ExpandedOffset - StableCarouselLayout.HoverOffset) * Carousel.StableLayoutScale;
                return Math.Abs(hoveredRow.X - expectedX) < 0.1f;
            });
            AddUntilStep("neighbours spread around hovered row", () =>
                Carousel.ChildrenOfType<PanelBeatmapStandalone>().Where(panel => panel.Item?.IsVisible == true && panel != hoveredRow)
                    .All(panel => Math.Abs(panel.DrawYPosition - panel.Item!.CarouselYPosition) > StableCarouselLayout.BoundaryGap - 1));
        }

        [Test]
        public void TestModernMouseSelectionAfterLeavingInfiniteGlass()
        {
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            AddStep("start in Modern", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            LoadSongSelect();

            int initialFilterCount = 0;
            AddStep("store initial filter count", () => initialFilterCount = Carousel.FilterCount);
            AddStep("switch to InfiniteGlass", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.InfiniteGlass));
            AddUntilStep("Modern carousel hidden", () => Carousel.Alpha, () => Is.Zero.Within(0.001f));
            AddAssert("InfiniteGlass switch did not refilter", () => Carousel.FilterCount, () => Is.EqualTo(initialFilterCount));

            AddStep("switch back to Modern", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddUntilStep("returned Modern carousel visible", () => Carousel.Alpha, () => Is.EqualTo(1).Within(0.001f));
            AddAssert("Modern switch did not refilter", () => Carousel.FilterCount, () => Is.EqualTo(initialFilterCount));

            Panel? targetPanel = null;
            BeatmapInfo? targetBeatmap = null;

            AddUntilStep("find another visible Modern panel", () =>
            {
                targetPanel = Carousel.ChildrenOfType<Panel>().FirstOrDefault(panel =>
                    panel.IsPresent
                    && panel.Item?.IsVisible == true
                    && getBeatmap(panel.Item.Model) is BeatmapInfo panelBeatmap
                    && panelBeatmap.ID != Beatmap.Value.BeatmapInfo.ID);

                return targetPanel != null;
            });
            AddStep("click another Modern panel", () =>
            {
                targetBeatmap = getBeatmap(targetPanel!.Item!.Model);
                InputManager.MoveMouseTo(targetPanel.TopLevelContent);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("clicked Modern beatmap selected", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(targetBeatmap!.ID));

            static BeatmapInfo? getBeatmap(object model) => model switch
            {
                GroupedBeatmap groupedBeatmap => groupedBeatmap.Beatmap,
                GroupedBeatmapSet groupedBeatmapSet => groupedBeatmapSet.BeatmapSet.Beatmaps.FirstOrDefault(),
                _ => null,
            };
        }

        [Test]
        public void TestModernMouseSelectionAfterReenter()
        {
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            ImportBeatmapForRuleset(_ => { }, 1, 0);
            AddStep("use Modern", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            LoadSongSelect();

            Screens.Select.SongSelect? firstSongSelect = null;
            AddStep("store first song select", () => firstSongSelect = SongSelect);
            AddStep("leave song select", () => SongSelect.Exit());
            AddUntilStep("first song select exited", () => !firstSongSelect!.ValidForPush);

            LoadSongSelect();
            AddAssert("new song select instance", () => SongSelect, () => Is.Not.SameAs(firstSongSelect));

            Panel? targetPanel = null;
            BeatmapInfo? targetBeatmap = null;
            WorkingBeatmap? previousWorkingBeatmap = null;
            ITrack? previousTrack = null;

            AddUntilStep("find another visible panel after re-entering", () =>
            {
                targetPanel = Carousel.ChildrenOfType<Panel>().FirstOrDefault(panel =>
                    panel.IsPresent
                    && panel.Item?.IsVisible == true
                    && getBeatmap(panel.Item.Model) is BeatmapInfo panelBeatmap
                    && panelBeatmap.ID != Beatmap.Value.BeatmapInfo.ID);

                return targetPanel != null;
            });
            AddStep("click another panel after re-entering", () =>
            {
                previousWorkingBeatmap = Beatmap.Value;
                previousTrack = previousWorkingBeatmap.Track;
                targetBeatmap = getBeatmap(targetPanel!.Item!.Model);
                InputManager.MoveMouseTo(targetPanel.TopLevelContent);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("mouse selection works after re-entering", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(targetBeatmap!.ID));
            AddUntilStep("selected track loaded", () => Beatmap.Value.TrackLoaded);
            AddAssert("selected track replaced", () => Beatmap.Value.Track, () => Is.Not.SameAs(previousTrack));
            AddUntilStep("selected track playing", () => Beatmap.Value.Track.IsRunning);
            AddUntilStep("previous track stopped", () => previousTrack!.IsRunning, () => Is.False);

            static BeatmapInfo? getBeatmap(object model) => model switch
            {
                GroupedBeatmap groupedBeatmap => groupedBeatmap.Beatmap,
                GroupedBeatmapSet groupedBeatmapSet => groupedBeatmapSet.BeatmapSet.Beatmaps.FirstOrDefault(),
                _ => null,
            };
        }

        [Test]
        public void TestCookieDoesNothingIfNothingSelected()
        {
            var screensPushed = new List<IScreen>();

            LoadSongSelect();
            AddStep("subscribe to screen pushed", () => Stack.ScreenPushed += onScreenPushed);
            AddStep("click osu! cookie", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<OsuLogo>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("no screens pushed", () => screensPushed, () => Is.Empty);
            AddStep("unsubscribe from screen pushed", () => Stack.ScreenPushed -= onScreenPushed);

            void onScreenPushed(IScreen lastScreen, IScreen newScreen) => screensPushed.Add(lastScreen);
        }

        [Test]
        public void TestInvalidRulesetDoesNotEnterGameplay()
        {
            var screensPushed = new List<IScreen>();

            ImportBeatmapForRuleset(0);
            ImportBeatmapForRuleset(1);

            LoadSongSelect();
            AddStep("subscribe to screen pushed", () => Stack.ScreenPushed += onScreenPushed);

            AddStep("change ruleset to taiko", () => Ruleset.Value = Rulesets.AvailableRulesets.Single(r => r.OnlineID == 1));

            AddStep("disable converts", () => Config.SetValue(OsuSetting.ShowConvertedBeatmaps, false));

            AddUntilStep("wait for taiko beatmap selected", () => Beatmap.Value.BeatmapInfo.Ruleset.OnlineID, () => Is.EqualTo(1));

            AddStep("change ruleset back and start gameplay immediately", () =>
            {
                Ruleset.Value = Rulesets.AvailableRulesets.Single(r => r.OnlineID == 0);

                InputManager.MoveMouseTo(this.ChildrenOfType<OsuLogo>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("no screens pushed", () => screensPushed, () => Is.Empty);
            AddStep("unsubscribe from screen pushed", () => Stack.ScreenPushed -= onScreenPushed);

            AddUntilStep("wait for osu beatmap selected", () => Beatmap.Value.BeatmapInfo.Ruleset.OnlineID, () => Is.EqualTo(0));

            void onScreenPushed(IScreen lastScreen, IScreen newScreen) => screensPushed.Add(lastScreen);
        }

        #region Hotkeys

        [Test]
        public void TestDeleteHotkey()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);

            AddAssert("beatmap imported", () => Beatmaps.GetAllUsableBeatmapSets().Any(), () => Is.True);
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("press shift-delete", () =>
            {
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Key(Key.Delete);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });

            AddUntilStep("delete dialog shown", () => DialogOverlay.CurrentDialog, Is.InstanceOf<BeatmapDeleteDialog>);
            AddStep("confirm deletion", () => DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());

            AddAssert("beatmap set deleted", () => Beatmaps.GetAllUsableBeatmapSets().Any(), () => Is.False);
        }

        [Test]
        public void TestClearModsViaModButtonRightClick()
        {
            LoadSongSelect();

            AddStep("select NC", () => SelectedMods.Value = new[] { new OsuModNightcore() });
            AddAssert("mods selected", () => SelectedMods.Value, () => Has.Count.EqualTo(1));
            AddStep("right click mod button", () =>
            {
                InputManager.MoveMouseTo(ScreenFooter.ChildrenOfType<FooterButtonMods>().Single());
                InputManager.Click(MouseButton.Right);
            });
            AddAssert("not mods selected", () => SelectedMods.Value, () => Has.Count.EqualTo(0));
        }

        [Test]
        public void TestSpeedChange()
        {
            LoadSongSelect();
            AddStep("clear mods", () => SelectedMods.Value = Array.Empty<Mod>());

            decreaseModSpeed();
            AddAssert("half time activated at 0.95x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            decreaseModSpeed();
            AddAssert("half time speed changed to 0.9x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.9).Within(0.005));

            increaseModSpeed();
            AddAssert("half time speed changed to 0.95x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            increaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            increaseModSpeed();
            AddAssert("double time activated at 1.05x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));

            increaseModSpeed();
            AddAssert("double time speed changed to 1.1x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.1).Within(0.005));

            decreaseModSpeed();
            AddAssert("double time speed changed to 1.05x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));

            OsuModNightcore nc = new OsuModNightcore
            {
                SpeedChange = { Value = 1.05 }
            };
            AddStep("select NC", () => SelectedMods.Value = new[] { nc });

            increaseModSpeed();
            AddAssert("nightcore speed changed to 1.1x", () => SelectedMods.Value.OfType<ModNightcore>().Single().SpeedChange.Value, () => Is.EqualTo(1.1).Within(0.005));

            decreaseModSpeed();
            AddAssert("nightcore speed changed to 1.05x", () => SelectedMods.Value.OfType<ModNightcore>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));

            decreaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            decreaseModSpeed();
            AddAssert("daycore activated at 0.95x", () => SelectedMods.Value.OfType<ModDaycore>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            decreaseModSpeed();
            AddAssert("daycore activated at 0.95x", () => SelectedMods.Value.OfType<ModDaycore>().Single().SpeedChange.Value, () => Is.EqualTo(0.9).Within(0.005));

            increaseModSpeed();
            AddAssert("daycore activated at 0.95x", () => SelectedMods.Value.OfType<ModDaycore>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));

            OsuModDoubleTime dt = new OsuModDoubleTime
            {
                SpeedChange = { Value = 1.02 },
                AdjustPitch = { Value = true },
            };
            AddStep("select DT", () => SelectedMods.Value = new[] { dt });

            decreaseModSpeed();
            AddAssert("half time activated at 0.97x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.97).Within(0.005));
            AddAssert("adjust pitch preserved", () => SelectedMods.Value.OfType<ModHalfTime>().Single().AdjustPitch.Value, () => Is.True);

            OsuModHalfTime ht = new OsuModHalfTime
            {
                SpeedChange = { Value = 0.97 },
                AdjustPitch = { Value = true },
            };
            Mod[] modlist = { ht, new OsuModHardRock(), new OsuModHidden() };
            AddStep("select HT+HD", () => SelectedMods.Value = modlist);

            increaseModSpeed();
            AddAssert("double time activated at 1.02x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.02).Within(0.005));
            AddAssert("double time activated at 1.02x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().AdjustPitch.Value, () => Is.True);
            AddAssert("HD still enabled", () => SelectedMods.Value.OfType<ModHidden>().SingleOrDefault(), () => Is.Not.Null);
            AddAssert("HR still enabled", () => SelectedMods.Value.OfType<ModHardRock>().SingleOrDefault(), () => Is.Not.Null);

            AddStep("select WU", () => SelectedMods.Value = new[] { new ModWindUp() });
            increaseModSpeed();
            AddAssert("windup still active", () => SelectedMods.Value.First() is ModWindUp);

            AddStep("select AS", () => SelectedMods.Value = new[] { new ModAdaptiveSpeed() });
            increaseModSpeed();
            AddAssert("adaptive speed still active", () => SelectedMods.Value.First() is ModAdaptiveSpeed);

            OsuModDoubleTime dtWithAdjustPitch = new OsuModDoubleTime
            {
                SpeedChange = { Value = 1.05 },
                AdjustPitch = { Value = true },
            };
            AddStep("select DT x1.05", () => SelectedMods.Value = new[] { dtWithAdjustPitch });

            decreaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            decreaseModSpeed();
            AddAssert("half time activated at 0.95x", () => SelectedMods.Value.OfType<ModHalfTime>().Single().SpeedChange.Value, () => Is.EqualTo(0.95).Within(0.005));
            AddAssert("half time has adjust pitch active", () => SelectedMods.Value.OfType<ModHalfTime>().Single().AdjustPitch.Value, () => Is.True);

            AddStep("turn off adjust pitch", () => SelectedMods.Value.OfType<ModHalfTime>().Single().AdjustPitch.Value = false);

            increaseModSpeed();
            AddAssert("no mods selected", () => SelectedMods.Value.Count == 0);

            increaseModSpeed();
            AddAssert("double time activated at 1.05x", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().SpeedChange.Value, () => Is.EqualTo(1.05).Within(0.005));
            AddAssert("double time has adjust pitch inactive", () => SelectedMods.Value.OfType<ModDoubleTime>().Single().AdjustPitch.Value, () => Is.False);

            void increaseModSpeed() => AddStep("increase mod speed", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Up);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            void decreaseModSpeed() => AddStep("decrease mod speed", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Down);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
        }

        /// <summary>
        /// Last played and rank achieved may have changed, so we want to make sure filtering runs on resume to song select.
        /// </summary>
        [Test]
        public void TestFilteringRunsAfterReturningFromGameplay()
        {
            AddStep("import actual beatmap", () => Beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely());

            LoadSongSelect();

            AddUntilStep("wait for filtered", () => SongSelect.ChildrenOfType<BeatmapCarousel>().Single().FilterCount, () => Is.EqualTo(1));

            AddStep("enter gameplay", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () => Stack.CurrentScreen is Player);
            AddUntilStep("wait for fail", () => ((Player)Stack.CurrentScreen).GameplayState.HasFailed);

            AddStep("exit gameplay", () => Stack.CurrentScreen.Exit());

            AddUntilStep("wait for song select", () => Stack.CurrentScreen is Screens.Select.SongSelect);
            AddUntilStep("wait for filtered", () => SongSelect.ChildrenOfType<BeatmapCarousel>().Single().FilterCount, () => Is.EqualTo(2));
        }

        [Test]
        public void TestAutoplayShortcut()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);

            AddAssert("autoplay selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);

            AddUntilStep("wait for return to ss", () => SongSelect.IsCurrentScreen());

            AddAssert("no mods selected", () => SongSelect.Mods.Value.Count == 0);
        }

        [Test]
        public void TestAutoplayShortcutKeepsAutoplayIfSelectedAlready()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            ChangeMods(new OsuModAutoplay());

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);

            AddAssert("autoplay selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);

            AddUntilStep("wait for return to ss", () => SongSelect.IsCurrentScreen());

            AddAssert("autoplay still selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);
        }

        [Test]
        public void TestAutoplayShortcutReturnsInitialModsOnExit()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            ChangeMods(new OsuModRelax());

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);

            AddAssert("only autoplay selected", () => SongSelect.Mods.Value.Single() is ModAutoplay);

            AddUntilStep("wait for return to ss", () => SongSelect.IsCurrentScreen());

            AddAssert("relax returned", () => SongSelect.Mods.Value.Single() is ModRelax);
        }

        [Test]
        public void TestModSelectCannotBeOpenedAfterConfirmingSelection()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddStep("press right", () => InputManager.Key(Key.Right)); // press right to select in carousel, also remove.
            AddAssert("beatmap selected", () => !Beatmap.IsDefault);

            ChangeMods(new OsuModAutoplay());

            AddStep("press ctrl+enter", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Enter);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddStep("press F1", () => InputManager.PressKey(Key.F1));
            AddAssert("mod select not visible", () => this.ChildrenOfType<ModSelectOverlay>().Single().State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddUntilStep("wait for player", () => Stack.CurrentScreen is PlayerLoader);
            AddAssert("osu! cookie visible", () => this.ChildrenOfType<OsuLogo>().Single().Alpha, () => Is.Not.Zero);
        }

        [Test]
        public void TestDropdownKeyboardNavigation()
        {
            ImportBeatmapForRuleset(0);

            LoadSongSelect();

            BeatmapInfo? firstBeatmap = null;
            AddStep("store first difficulty", () => firstBeatmap = Beatmap.Value.BeatmapInfo);

            AddStep("click sort dropdown", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<ShearedDropdown<SortMode>>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddStep("press up arrow", () => InputManager.Key(Key.Up));
            AddStep("press up arrow", () => InputManager.Key(Key.Up));

            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddAssert("sort mode is length", () => this.ChildrenOfType<ShearedDropdown<SortMode>>().Single().Current.Value, () => Is.EqualTo(SortMode.Length));
            AddAssert("beatmap not changed", () => Beatmap.Value.BeatmapInfo, () => Is.EqualTo(firstBeatmap));
        }

        #endregion

        #region Footer

        [Test]
        public void TestFooterMods()
        {
            LoadSongSelect();

            AddStep("one mod", () => SelectedMods.Value = new List<Mod> { new OsuModHidden() });
            AddStep("two mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock() });
            AddStep("three mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime() });
            AddStep("four mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime(), new OsuModClassic() });
            AddStep("five mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime(), new OsuModClassic(), new OsuModDifficultyAdjust() });

            AddStep("modified", () => SelectedMods.Value = new List<Mod> { new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + one", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + two", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + three",
                () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModClassic(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("modified + four",
                () => SelectedMods.Value = new List<Mod>
                    { new OsuModHidden(), new OsuModHardRock(), new OsuModClassic(), new OsuModDifficultyAdjust(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });

            AddStep("clear mods", () => SelectedMods.Value = Array.Empty<Mod>());
            AddWaitStep("wait", 3);
            AddStep("one mod", () => SelectedMods.Value = new List<Mod> { new OsuModHidden() });

            AddStep("clear mods", () => SelectedMods.Value = Array.Empty<Mod>());
            AddWaitStep("wait", 3);
            AddStep("five mods", () => SelectedMods.Value = new List<Mod> { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime(), new OsuModClassic(), new OsuModDifficultyAdjust() });
        }

        [Test]
        public void TestFooterModOverlay()
        {
            LoadSongSelect();

            AddStep("Press F1", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<FooterButtonMods>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("Overlay visible", () => this.ChildrenOfType<ModSelectOverlay>().Single().State.Value == Visibility.Visible);
            AddStep("Hide", () => this.ChildrenOfType<ModSelectOverlay>().Single().Hide());
        }

        [Test]
        public void TestFooterRandom()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("press F2", () => InputManager.Key(Key.F2));
            AddAssert("next random invoked", () => nextRandomCalled && !previousRandomCalled);
        }

        [Test]
        public void TestFooterRandomViaMouse()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("click button", () =>
            {
                InputManager.MoveMouseTo(randomButton);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("next random invoked", () => nextRandomCalled && !previousRandomCalled);
        }

        [Test]
        public void TestFooterRewind()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("press Shift+F2", () =>
            {
                InputManager.PressKey(Key.LShift);
                InputManager.PressKey(Key.F2);
                InputManager.ReleaseKey(Key.F2);
                InputManager.ReleaseKey(Key.LShift);
            });

            AddAssert("previous random invoked", () => previousRandomCalled && !nextRandomCalled);
        }

        [Test]
        public void TestFooterRewindViaShiftMouseLeft()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("shift + click button", () =>
            {
                InputManager.PressKey(Key.LShift);
                InputManager.MoveMouseTo(randomButton);
                InputManager.Click(MouseButton.Left);
                InputManager.ReleaseKey(Key.LShift);
            });
            AddAssert("previous random invoked", () => previousRandomCalled && !nextRandomCalled);
        }

        [Test]
        public void TestFooterRewindViaMouseRight()
        {
            LoadSongSelect();

            bool nextRandomCalled = false;
            bool previousRandomCalled = false;
            AddStep("hook events", () =>
            {
                randomButton.NextRandom = () => nextRandomCalled = true;
                randomButton.PreviousRandom = () => previousRandomCalled = true;
            });

            AddStep("right click button", () =>
            {
                InputManager.MoveMouseTo(randomButton);
                InputManager.Click(MouseButton.Right);
            });
            AddAssert("previous random invoked", () => previousRandomCalled && !nextRandomCalled);
        }

        private FooterButtonRandom randomButton => ScreenFooter.ChildrenOfType<FooterButtonRandom>().Single();

        [Test]
        public void TestFooterOptions()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);
            AddUntilStep("options enabled", () => this.ChildrenOfType<FooterButtonOptions>().Single().Enabled.Value);

            AddStep("click", () => this.ChildrenOfType<FooterButtonOptions>().Single().TriggerClick());
            AddUntilStep("popover displayed", () => this.ChildrenOfType<FooterButtonOptions.Popover>().Any(p => p.IsPresent));
        }

        [Test]
        public void TestSelectionChangedFromProtectedToNone()
        {
            ImportBeatmapForRuleset(0);
            AddStep("set protected on import", () => Realm.Write(r => r.All<BeatmapSetInfo>().First(s => !s.DeletePending).Protected = true));

            AddStep("selected protected", () => Beatmap.Value = Beatmaps.GetWorkingBeatmap(Beatmaps.GetAllUsableBeatmapSets().First(s => s.Protected).Beatmaps.First()));

            LoadSongSelect();

            AddUntilStep("beatmap deselected", () => Beatmap.IsDefault);
        }

        [Test]
        public void TestSelectionChangedFromProtectedToSomething()
        {
            ImportBeatmapForRuleset(0);
            AddStep("set protected on import", () => Realm.Write(r => r.All<BeatmapSetInfo>().First(s => !s.DeletePending).Protected = true));

            AddStep("selected protected", () => Beatmap.Value = Beatmaps.GetWorkingBeatmap(Beatmaps.GetAllUsableBeatmapSets().First(s => s.Protected).Beatmaps.First()));

            ImportBeatmapForRuleset(0);

            LoadSongSelect();

            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
            AddUntilStep("selection not protected", () => !Beatmap.Value.BeatmapSetInfo.Protected);
        }

        [Test]
        public void TestSelectAfterDeletion()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("delete all beatmaps", () => Beatmaps.Delete());
            AddUntilStep("beatmap not selected", () => Beatmap.IsDefault);

            AddStep("restore deleted", () => Beatmaps.UndeleteAll());
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
        }

        [Test]
        public void TestFooterOptionsState()
        {
            LoadSongSelect();

            ImportBeatmapForRuleset(0);

            AddUntilStep("options enabled", () => this.ChildrenOfType<FooterButtonOptions>().Single().Enabled.Value);
            AddStep("delete all beatmaps", () => Beatmaps.Delete());

            AddAssert("beatmap selected", () => !Beatmap.IsDefault);
            AddStep("select no beatmap", () => Beatmap.SetDefault());

            AddUntilStep("wait for no beatmap", () => Beatmap.IsDefault);
            AddAssert("options disabled", () => !this.ChildrenOfType<FooterButtonOptions>().Single().Enabled.Value);
        }

        /// <summary>
        /// tests that clicking the osu! logo immediately after selecting a different difficulty
        /// (before the selection debounce completes) starts the correct beatmap.
        /// this tests the fix for https://github.com/ppy/osu/issues/36074
        /// </summary>
        [Test]
        public void TestPlayCorrectBeatmapWhenSelectionNotFullyLoaded()
        {
            // import a beatmap set with multiple difficulties
            ImportBeatmapForRuleset(0);

            LoadSongSelect();

            // wait for initial beatmap to be selected
            AddUntilStep("wait for first beatmap selected", () => !Beatmap.IsDefault);

            BeatmapInfo? firstBeatmap = null;
            AddStep("store first difficulty", () => firstBeatmap = Beatmap.Value.BeatmapInfo);

            // start loading the first difficulty
            AddStep("click logo to start loading", () => this.ChildrenOfType<OsuLogo>().Single().TriggerClick());
            AddUntilStep("wait for player loader", () => Stack.CurrentScreen is PlayerLoader);

            // return to song select
            AddStep("press escape to return", () => InputManager.Key(Key.Escape));
            AddUntilStep("wait for return to song select", () => SongSelect.IsCurrentScreen());

            // press down and schedule logo click to happen shortly after (but before 150ms debounce)
            // this reproduces the race condition where Beatmap.Value hasn't updated yet
            AddStep("select next difficulty and click logo immediately", () =>
            {
                InputManager.Key(Key.Down);
                Schedule(() => this.ChildrenOfType<OsuLogo>().Single().TriggerClick());
            });

            AddUntilStep("wait for player loader", () => Stack.CurrentScreen is PlayerLoader);

            // verify we're loading the second difficulty, not the first
            // without the fix, this would fail because Beatmap.Value still has the old value
            AddAssert("player is loading second difficulty", () =>
                Beatmap.Value.BeatmapInfo.ID != firstBeatmap!.ID);

            AddUntilStep("wait for return to song select", () => SongSelect.IsCurrentScreen());

            Panel? targetPanel = null;
            BeatmapInfo? targetBeatmap = null;

            AddUntilStep("find another panel after returning from loader", () =>
            {
                targetPanel = Carousel.ChildrenOfType<Panel>().FirstOrDefault(panel =>
                    panel.IsPresent
                    && panel.Item?.IsVisible == true
                    && panel.Item.Model is GroupedBeatmap groupedBeatmap
                    && groupedBeatmap.Beatmap.ID != Beatmap.Value.BeatmapInfo.ID);

                return targetPanel != null;
            });
            AddStep("click another panel after returning from loader", () =>
            {
                targetBeatmap = ((GroupedBeatmap)targetPanel!.Item!.Model).Beatmap;
                InputManager.MoveMouseTo(targetPanel.TopLevelContent);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("selection works after returning from loader", () => Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(targetBeatmap!.ID));
        }

        #endregion
    }
}
