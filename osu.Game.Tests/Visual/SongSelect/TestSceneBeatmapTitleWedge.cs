// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Tests.Beatmaps;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapTitleWedge : SongSelectComponentsTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private RulesetStore rulesets = null!;

        private BeatmapTitleWedge titleWedge = null!;
        private BeatmapTitleWedge.DifficultyDisplay difficultyDisplay => titleWedge.ChildrenOfType<BeatmapTitleWedge.DifficultyDisplay>().Single();

        [Cached(typeof(IBindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>))]
        private Bindable<Screens.Select.SongSelect.BeatmapSetLookupResult?> onlineLookupResult = new Bindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>();

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            this.rulesets = rulesets;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Shear = OsuGame.SHEAR,
                    Children = new Drawable[]
                    {
                        titleWedge = new BeatmapTitleWedge
                        {
                            State = { Value = Visibility.Visible },
                        },
                    },
                }
            });

            AddSliderStep("change star difficulty", 0, 11.9, 4.18, v =>
            {
                difficultyDisplay.ChildrenOfType<StarRatingDisplay>().Single().Current.Value = new StarDifficulty(v, 0);
            });
        }

        [Test]
        public void TestSelectionTitleCompletedSubtreeCost()
        {
            const int changes = 60;
            var maps = new WorkingBeatmap[changes];
            var assignmentTicks = new long[2];
            var subtreeTicks = new long[2];
            var assignmentBytes = new long[2];
            var subtreeBytes = new long[2];
            var maxSubtreeTicks = new long[2];
            var originalParameters = new LocalisationParameters(null, false);
            LocalisationManager localisation = null!;
            FrameworkConfigManager frameworkConfig = null!;

            AddStep("prepare fixed in-memory titles", () =>
            {
                localisation = Parent!.Dependencies.Get<LocalisationManager>();
                frameworkConfig = Parent!.Dependencies.Get<FrameworkConfigManager>();
                originalParameters = localisation.CurrentParameters.Value;
                for (int i = 0; i < changes; i++)
                {
                    bool overflow = i % 2 == 1;
                    var map = (Beatmap)createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
                    map.BeatmapInfo.ID = Guid.NewGuid();
                    map.BeatmapInfo.Metadata.Title = overflow ? $"Romanised title {i} " + new string('W', 150) : $"Short {i}";
                    map.BeatmapInfo.Metadata.TitleUnicode = overflow ? $"日本語タイトル {i} " + new string('長', 150) : $"短い {i}";
                    map.BeatmapInfo.Metadata.Artist = $"Artist {i}";
                    map.BeatmapInfo.Metadata.ArtistUnicode = $"歌手 {i}";
                    maps[i] = CreateWorkingBeatmap(map);
                }
            });
            AddStep("settle initial wedge", () => { });

            for (int i = 0; i < changes; i++)
            {
                int index = i;
                int group = i % 2;
                AddStep($"select in-memory title {i}", () =>
                {
                    long bytes = GC.GetAllocatedBytesForCurrentThread();
                    long ticks = Stopwatch.GetTimestamp();
                    Beatmap.Value = maps[index];
                    assignmentTicks[group] += Stopwatch.GetTimestamp() - ticks;
                    assignmentBytes[group] += GC.GetAllocatedBytesForCurrentThread() - bytes;

                    // Complete this wedge subtree, including children/autosize, after selection. This is
                    // not the host's full update frame or draw-node generation.
                    bytes = GC.GetAllocatedBytesForCurrentThread();
                    ticks = Stopwatch.GetTimestamp();
                    titleWedge.UpdateSubTree();
                    long elapsed = Stopwatch.GetTimestamp() - ticks;
                    subtreeTicks[group] += elapsed;
                    maxSubtreeTicks[group] = Math.Max(maxSubtreeTicks[group], elapsed);
                    subtreeBytes[group] += GC.GetAllocatedBytesForCurrentThread() - bytes;
                });
                AddUntilStep($"title {i} published after glyph preparation", () =>
                {
                    var labels = titleWedge.ChildrenOfType<MarqueeContainer>().ToArray();
                    string expectedTitle = maps[index].Metadata.Title;
                    string expectedArtist = maps[index].Metadata.Artist;
                    return titleWedge.DisplayedTitle == expectedTitle && titleWedge.DisplayedArtist == expectedArtist
                           && labels[0].ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString() == expectedTitle)
                           && labels[1].ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString() == expectedArtist)
                           && labels[0].ChildrenOfType<OsuSpriteText>().Count() >= (group == 0 ? 1 : 2);
                });
            }

            AddStep("verify original-script labels and theme refresh", () =>
            {
                frameworkConfig.SetValue(FrameworkSetting.ShowUnicode, true);
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                // The theme refresh may keep the same colour; selection refreshes the labels explicitly.
                Beatmap.Value = maps[^2];
                Beatmap.Value = maps[^1];
            });
            AddUntilStep("both original-script labels prepared after theme change", () =>
            {
                var labels = titleWedge.ChildrenOfType<MarqueeContainer>().ToArray();
                return localisation.CurrentParameters.Value.PreferOriginalScript
                       && labels[0].ChildrenOfType<OsuSpriteText>().All(t => localisation.GetLocalisedString(t.Text) == maps[^1].Metadata.TitleUnicode)
                       && labels[1].ChildrenOfType<OsuSpriteText>().All(t => localisation.GetLocalisedString(t.Text) == maps[^1].Metadata.ArtistUnicode);
            });
            AddStep("restore theme and script", () =>
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
                frameworkConfig.SetValue(FrameworkSetting.ShowUnicode, originalParameters.PreferOriginalScript);
            });
            AddStep("report completed subtree measurements", () =>
            {
                for (int group = 0; group < 2; group++)
                    TestContext.Progress.WriteLine($"TITLE_FRAME group={(group == 0 ? "fit" : "overflow")} count={changes / 2} "
                                                   + $"assign_ms={assignmentTicks[group] * 1000.0 / Stopwatch.Frequency:F3} "
                                                   + $"subtree_ms={subtreeTicks[group] * 1000.0 / Stopwatch.Frequency:F3} "
                                                   + $"subtree_max_ms={maxSubtreeTicks[group] * 1000.0 / Stopwatch.Frequency:F3} "
                                                   + $"assign_thread_bytes={assignmentBytes[group]} subtree_thread_bytes={subtreeBytes[group]} "
                                                   + $"process_gc0={GC.CollectionCount(0)}");
            });
        }

        [Test]
        public void TestRulesetChange()
        {
            selectBeatmap(Beatmap.Value.Beatmap);

            AddWaitStep("wait for select", 3);

            foreach (var rulesetInfo in rulesets.AvailableRulesets)
            {
                var testBeatmap = createTestBeatmapFromRuleset(rulesetInfo);

                setRuleset(rulesetInfo);
                selectBeatmap(testBeatmap);
            }
        }

        [Test]
        public void TestModReplacementRestartsOnlyCountAwait()
        {
            var map = createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
            map.BeatmapInfo.ID = Guid.NewGuid();
            int fallbacks = 0;
            AddStep("select map", () =>
            {
                fallbacks = difficultyDisplay.CountFallbacksStarted;
                Beatmap.Value = CreateWorkingBeatmap(map);
            });
            AddStep("replace mods during calculation", () => SelectedMods.Value = new[] { new OsuModDoubleTime() });
            AddStep("replace mods again during calculation", () => SelectedMods.Value = new[] { new OsuModHardRock() });
            AddUntilStep("counts visible for latest request", () => difficultyDisplay.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>().First().Alpha > 0);
            AddAssert("modded result uses one unmodded fallback", () => difficultyDisplay.CountFallbacksStarted - fallbacks, () => Is.EqualTo(1));
            AddWaitStep("allow old request to settle", 2);
            AddAssert("old request cannot repeat fallback", () => difficultyDisplay.CountFallbacksStarted - fallbacks, () => Is.EqualTo(1));
        }

        private readonly List<GatedCountWorkingBeatmap> gatedCountBeatmaps = new List<GatedCountWorkingBeatmap>();
        private Scheduler? isolatedStatisticsScheduler;

        [TearDown]
        public void ReleaseCountFallbackGates()
        {
            // Always unblock workers, including when an assertion/step fails.
            foreach (var map in gatedCountBeatmaps)
                map.Release.TrySetResult(true);
            gatedCountBeatmaps.Clear();
            // Scheduler.UpdateClock is thread-safe. Restore it even when a visual assertion fails.
            isolatedStatisticsScheduler?.UpdateClock(titleWedge.Clock);
            isolatedStatisticsScheduler = null;
        }

        [Test]
        public void TestCountFallbackSurvivesModChangesAndRekeysOnSelection()
        {
            GatedCountWorkingBeatmap first = null!;
            GatedCountWorkingBeatmap second = null!;
            int waits = 0;
            int fallbacks = 0;

            AddStep("select gated modded map", () =>
            {
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                SelectedMods.Value = new Mod[] { new OsuModDoubleTime() };
                first = createGatedCountBeatmap();
                fallbacks = difficultyDisplay.CountFallbacksStarted;
                waits = difficultyDisplay.CountFallbackWaitsStarted;
                Beatmap.Value = first;
            });
            AddUntilStep("fallback entered and initial waiter joined", () => first.Entered.Task.IsCompletedSuccessfully
                && difficultyDisplay.CountFallbackWaitsStarted > waits);
            AddStep("replace mods while fallback blocked", () =>
            {
                waits = difficultyDisplay.CountFallbackWaitsStarted;
                SelectedMods.Value = new Mod[] { new OsuModHardRock() };
            });
            AddUntilStep("replacement joined fallback", () => difficultyDisplay.CountFallbackWaitsStarted > waits);
            AddAssert("one uncancelled conversion still blocked", () =>
                first.Starts == 1 && !first.Release.Task.IsCompleted && !first.Exited.Task.IsCompleted && !first.Token.IsCancellationRequested);
            AddStep("replace mods again while blocked", () =>
            {
                waits = difficultyDisplay.CountFallbackWaitsStarted;
                SelectedMods.Value = new Mod[] { new OsuModDoubleTime() };
            });
            AddUntilStep("second replacement joined fallback", () => difficultyDisplay.CountFallbackWaitsStarted > waits);
            AddAssert("still one conversion", () => first.Starts == 1 && difficultyDisplay.CountFallbacksStarted == fallbacks + 1);
            AddStep("release fallback", () => first.Release.TrySetResult(true));
            AddUntilStep("counts published", () => first.Exited.Task.IsCompleted &&
                difficultyDisplay.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>().First().Statistics.Count > 0);
            AddStep("replace mods after completion", () =>
            {
                waits = difficultyDisplay.CountFallbackWaitsStarted;
                SelectedMods.Value = new Mod[] { new OsuModHardRock() };
            });
            AddUntilStep("completed result reused", () => difficultyDisplay.CountFallbackWaitsStarted > waits);
            AddAssert("completed reuse did not convert again", () => first.Starts == 1 && difficultyDisplay.CountFallbacksStarted == fallbacks + 1);
            AddStep("select another gated map", () =>
            {
                second = createGatedCountBeatmap();
                Beatmap.Value = second;
            });
            AddUntilStep("new selection has its own fallback", () => second.Entered.Task.IsCompletedSuccessfully);
            AddAssert("selection cancelled old owner and rekeyed", () => first.Token.IsCancellationRequested && second.Starts == 1
                && difficultyDisplay.CountFallbacksStarted == fallbacks + 2);
            AddStep("switch back while second fallback blocked", () => Beatmap.Value = first);
            AddUntilStep("abandoned second fallback exited", () => second.Exited.Task.IsCompleted);
            AddUntilStep("return selection recomputed counts", () => first.Starts == 2 && difficultyDisplay.CountFallbacksStarted == fallbacks + 3);
            AddAssert("abandoned work cancelled", () => second.Token.IsCancellationRequested && !second.Release.Task.IsCompleted);
        }

        private GatedCountWorkingBeatmap createGatedCountBeatmap()
        {
            var map = createTestBeatmapFromRuleset(Ruleset.Value);
            map.BeatmapInfo.ID = Guid.NewGuid();
            var working = new GatedCountWorkingBeatmap(map);
            gatedCountBeatmaps.Add(working);
            return working;
        }

        private class GatedCountWorkingBeatmap : TestWorkingBeatmap
        {
            public readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int starts;
            public int Starts => Volatile.Read(ref starts);
            public CancellationToken Token { get; private set; }

            public GatedCountWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            {
                if (mods.Count != 0)
                    return base.GetPlayableBeatmap(ruleset, mods, token);

                Interlocked.Increment(ref starts);
                Token = token;
                Entered.TrySetResult(true);
                try
                {
                    // Runs only on the fallback worker; never block the test's update thread.
                    Release.Task.WaitAsync(TimeSpan.FromSeconds(30), token).GetAwaiter().GetResult();
                    return base.GetPlayableBeatmap(ruleset, mods, token);
                }
                finally
                {
                    Exited.TrySetResult(true);
                }
            }
        }

        [Test]
        public void TestIncompatibleBeatmapDoesNotFaultCountStatistics()
        {
            RulesetInfo osuRuleset = new OsuRuleset().RulesetInfo;
            var incompatibleBeatmap = (Beatmap)createTestBeatmapFromRuleset(osuRuleset);
            incompatibleBeatmap.HitObjects = new List<HitObject>
            {
                new IncompatibleHitObject(),
            };

            setRuleset(osuRuleset);
            selectBeatmap(incompatibleBeatmap);
            AddWaitStep("wait for rejected conversion", 3);
            AddAssert("count statistics hidden", () =>
                difficultyDisplay.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>().First().Alpha == 0);
        }

        [Test]
        public void TestTitleWedgeCornerRadiusClampedWhenShearedAndUnclampedWhenUnsheared()
        {
            AddStep("enable custom theme mode with large panel radius", () =>
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                var theme = new CustomThemeDefinition();
                theme.SetRadius(GeometryRole.Panel, 20f);
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddStep("enable shear", () => OsuGame.DisableShear.Value = false);
            AddAssert("title wedge corner radius clamped to <= 12px when sheared", () => titleWedge.CornerRadius <= 12f);

            AddStep("disable shear", () => OsuGame.DisableShear.Value = true);
            AddAssert("title wedge corner radius takes full 20px when unsheared", () => titleWedge.CornerRadius == 20f);

            AddStep("re-enable shear", () => OsuGame.DisableShear.Value = false);
            AddAssert("title wedge corner radius clamped back to <= 12px", () => titleWedge.CornerRadius <= 12f);

            AddStep("restore default theme and shear", () =>
            {
                OsuGame.DisableShear.Value = false;
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            });
        }

        [Test]
        public void TestNullBeatmap()
        {
            selectBeatmap(null);
            AddAssert("check default title", () => titleWedge.DisplayedTitle == Beatmap.Default.BeatmapInfo.Metadata.Title);
            AddAssert("check default artist", () => titleWedge.DisplayedArtist == Beatmap.Default.BeatmapInfo.Metadata.Artist);
            AddAssert("statistics not visible",
                () => difficultyDisplay.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>()
                                       .All(d => d.Alpha == 0 || d.ChildrenOfType<BeatmapTitleWedge.StatisticDifficulty>().All(s => s.Alpha == 0)));
        }

        [Test]
        public void TestTransientSelectionDoesNotDecodeForLengthBpmAndLatestStatisticsStillPublish()
        {
            var maps = new StatisticsDecodeProbe[8];
            var clock = new ManualClock();
            AddStep("use isolated statistics scheduler clock", () =>
            {
                // Only this wedge's scheduler uses the clock, not its children or the host.
                isolatedStatisticsScheduler = (Scheduler)typeof(Drawable)
                    .GetProperty("Scheduler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(titleWedge)!;
                isolatedStatisticsScheduler.UpdateClock(clock);
            });

            for (int i = 0; i < maps.Length; i++)
            {
                int index = i;
                AddStep($"select transient statistics map {i}", () =>
                {
                    var map = createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
                    map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60 * 1000 / (120 + index) });
                    map.BeatmapInfo.Length = 60000 * (index + 1);
                    maps[index] = new StatisticsDecodeProbe(map);
                    Beatmap.Value = maps[index];
                    clock.CurrentTime += 300;
                    titleWedge.UpdateSubTree();
                });
                AddAssert($"transient map {i} not decoded by statistics", () => maps[index].DecodeCount == 0);
                AddAssert($"transient map {i} metadata length shown without stale drain tooltip", () =>
                    titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(1).Text.ToString() == $"{index + 1:00}:00"
                    && titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(1).TooltipText == default);
            }

            AddStep("admit settled selection", () =>
            {
                clock.CurrentTime += Screens.Select.SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE;
                titleWedge.UpdateSubTree();
            });
            checkDisplayedBPM("127");
            AddAssert("settled drain length tooltip published", () =>
                titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(1).TooltipText.ToString() == BeatmapsetsStrings.ShowStatsTotalLength("00:49").ToString());
            AddAssert("only final statistics map decoded", () => maps[^1].DecodeCount == 1 && maps.Take(maps.Length - 1).All(m => m.DecodeCount == 0));
            AddStep("replace pending statistics and change rate", () =>
            {
                Beatmap.Value = maps[0];
                Beatmap.Value = maps[2];
                SelectedMods.Value = new[] { new OsuModDoubleTime { SpeedChange = { Value = 2 } } };
            });
            AddAssert("old BPM hidden while cold statistics wait", () =>
                titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().Single(l => l.TooltipText == BeatmapsetsStrings.ShowStatsBpm).Text == null);
            AddAssert("modded metadata length shown without old drain tooltip", () =>
                titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(1).Text.ToString() == "01:30"
                && titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(1).TooltipText == default);
            AddStep("admit latest map and rate", () =>
            {
                clock.CurrentTime += Screens.Select.SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE;
                titleWedge.UpdateSubTree();
            });
            checkDisplayedBPM("244");
            AddAssert("replaced pending map never decoded", () => maps[0].DecodeCount == 0);
            AddStep("change rate on decoded selection without advancing admission clock", () =>
                ((OsuModDoubleTime)SelectedMods.Value.Single()).SpeedChange.Value = 1.5);
            checkDisplayedBPM("183");
            AddAssert("decoded statistics cache did not start another decode", () => maps[2].DecodeCount == 1);
            AddStep("restore statistics scheduler clock", () =>
            {
                isolatedStatisticsScheduler!.UpdateClock(titleWedge.Clock);
                isolatedStatisticsScheduler = null;
            });
        }

        private class StatisticsDecodeProbe : WorkingBeatmap
        {
            private readonly IBeatmap beatmap;
            private int decodeCount;
            public int DecodeCount => Volatile.Read(ref decodeCount);

            public StatisticsDecodeProbe(IBeatmap beatmap) : base(beatmap.BeatmapInfo, null)
            {
                this.beatmap = beatmap;
            }

            protected override IBeatmap GetBeatmap()
            {
                Interlocked.Increment(ref decodeCount);
                return beatmap;
            }

            public override Texture? GetBackground() => null;
            public override Stream? GetStream(string storagePath) => null;
            protected override Track? GetBeatmapTrack() => null;
            protected internal override ISkin? GetSkin() => null;
        }

        [Test]
        public void TestLengthBpmJobsOnlyForMapOrEffectiveRateChanges()
        {
            int jobs = 0;
            AddStep("select first map", () => Beatmap.Value = CreateWorkingBeatmap(createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo)));
            AddWaitStep("allow selection bindings to settle", 2);
            AddStep("capture job count", () => jobs = titleWedge.LengthBpmJobsStarted);
            AddStep("refresh unrelated online lookup", () => onlineLookupResult.Value = Screens.Select.SongSelect.BeatmapSetLookupResult.Completed(null));
            AddAssert("online refresh did not restart statistics", () => titleWedge.LengthBpmJobsStarted, () => Is.EqualTo(jobs));
            AddStep("refresh theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddAssert("theme refresh did not restart statistics", () => titleWedge.LengthBpmJobsStarted, () => Is.EqualTo(jobs));
            OsuModDoubleTime doubleTime = null!;
            AddStep("change effective rate", () => SelectedMods.Value = new[] { doubleTime = new OsuModDoubleTime() });
            AddAssert("rate change starts one job", () => titleWedge.LengthBpmJobsStarted == jobs + 1);
            AddStep("change rate setting on same mod", () => doubleTime.SpeedChange.Value = 2);
            AddAssert("rate setting change starts one job", () => titleWedge.LengthBpmJobsStarted == jobs + 2);
            AddStep("select another map while prior job may be running", () => Beatmap.Value = CreateWorkingBeatmap(createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo)));
            AddAssert("new map starts one job", () => titleWedge.LengthBpmJobsStarted == jobs + 3);
            AddStep("restore theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddAssert("theme refresh does not restart new map job", () => titleWedge.LengthBpmJobsStarted == jobs + 3);
        }

        [Test]
        public void TestBPMUpdates()
        {
            const double bpm = 120;
            IBeatmap beatmap = createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60 * 1000 / bpm });

            OsuModDoubleTime doubleTime = null!;

            selectBeatmap(beatmap);
            checkDisplayedBPM($"{bpm}");

            AddStep("select DT", () => SelectedMods.Value = new[] { doubleTime = new OsuModDoubleTime() });
            checkDisplayedBPM($"{bpm * 1.5f}");

            AddStep("change DT rate", () => doubleTime.SpeedChange.Value = 2);
            checkDisplayedBPM($"{bpm * 2}");

            AddStep("select HT", () => SelectedMods.Value = new[] { new OsuModHalfTime() });
            checkDisplayedBPM($"{bpm * 0.75f}");
        }

        [Test]
        public void TestWedgeVisibility()
        {
            AddStep("hide", () => { titleWedge.Hide(); });
            AddWaitStep("wait for hide", 3);
            AddAssert("check visibility", () => titleWedge.Alpha == 0);
            AddStep("show", () => { titleWedge.Show(); });
            AddWaitStep("wait for show", 1);
            AddAssert("check visibility", () => titleWedge.Alpha > 0);
        }

        [Test]
        public void TestOnlineAvailability()
        {
            AddStep("online beatmapset", () => (Beatmap.Value, onlineLookupResult.Value) = createTestBeatmap());

            AddUntilStep("play count is 10000", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("10,000"));
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));
            AddStep("online beatmapset with local diff", () =>
            {
                var (working, lookupResult) = createTestBeatmap();

                working.BeatmapInfo.ResetOnlineInfo();

                Beatmap.Value = working;
                onlineLookupResult.Value = lookupResult;
            });
            AddUntilStep("play count is -", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("-"));
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));
            AddStep("local beatmapset", () =>
            {
                var (working, _) = createTestBeatmap();

                Beatmap.Value = working;
                onlineLookupResult.Value = Screens.Select.SongSelect.BeatmapSetLookupResult.Completed(null);
            });
            AddUntilStep("play count is -", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("-"));
            AddUntilStep("favourites count is -", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("-"));
        }

        [Test]
        public void TestFavouriting()
        {
            var resetEvent = new ManualResetEventSlim(false);

            AddStep("set up request handler", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    switch (request)
                    {
                        case PostBeatmapFavouriteRequest favourite:
                            Task.Run(() =>
                            {
                                resetEvent.Wait(10000);
                                favourite.TriggerSuccess();
                            });
                            return true;

                        default:
                            return false;
                    }
                };
            });

            AddStep("online beatmapset", () => (Beatmap.Value, onlineLookupResult.Value) = createTestBeatmap());

            AddUntilStep("play count is 10000", () => this.ChildrenOfType<BeatmapTitleWedge.Statistic>().ElementAt(0).Text.ToString(), () => Is.EqualTo("10,000"));
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));

            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("favourites count is 2346", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,346"));

            AddStep("reset event", resetEvent.Reset);
            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("favourites count is 2345", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("2,345"));

            AddStep("reset event", resetEvent.Reset);
            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddStep("change to another beatmap", () =>
            {
                var (working, online) = createTestBeatmap();
                online.Result!.FavouriteCount = 9999;
                online.Result!.HasFavourited = true;
                working.BeatmapSetInfo.OnlineID = online.Result!.OnlineID = 99999;

                Beatmap.Value = working;
                onlineLookupResult.Value = online;
            });
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("favourites count is 9999", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().Text.ToString(), () => Is.EqualTo("9,999"));

            AddStep("set up request handler to fail", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    switch (request)
                    {
                        case PostBeatmapFavouriteRequest favourite:
                            Task.Run(() =>
                            {
                                resetEvent.Wait(10000);
                                favourite.TriggerFailure(new APIException("You have too many favourited beatmaps! Please unfavourite some before trying again.", null));
                            });
                            return true;

                        default:
                            return false;
                    }
                };
            });
            AddStep("reset event", resetEvent.Reset);
            AddStep("click favourite button", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single().TriggerClick());
            AddUntilStep("spinner visible", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single()
                                                      .ChildrenOfType<LoadingSpinner>().Single().State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("allow request to complete", resetEvent.Set);
            AddUntilStep("spinner hidden", () => this.ChildrenOfType<BeatmapTitleWedge.FavouriteButton>().Single()
                                                     .ChildrenOfType<LoadingSpinner>().Single().State.Value, () => Is.EqualTo(Visibility.Hidden));
        }

        [TestCase(120, 125, null, "120-125 (mostly 120)")]
        [TestCase(120, 120.6, null, "120-121 (mostly 120)")]
        [TestCase(120, 120.4, null, "120")]
        [TestCase(120, 120.6, "DT", "180-181 (mostly 180)")]
        [TestCase(120, 120.4, "DT", "180-181 (mostly 180)")]
        public void TestVaryingBPM(double commonBpm, double otherBpm, string? mod, string expectedDisplay)
        {
            IBeatmap beatmap = createTestBeatmapFromRuleset(new OsuRuleset().RulesetInfo);
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60 * 1000 / commonBpm });
            beatmap.ControlPointInfo.Add(100, new TimingControlPoint { BeatLength = 60 * 1000 / otherBpm });
            beatmap.ControlPointInfo.Add(200, new TimingControlPoint { BeatLength = 60 * 1000 / commonBpm });

            if (mod != null)
                AddStep($"select {mod}", () => SelectedMods.Value = new[] { Ruleset.Value.CreateInstance().CreateModFromAcronym(mod) });

            selectBeatmap(beatmap);
            checkDisplayedBPM(expectedDisplay);
        }

        [Test]
        [Explicit]
        public void TestPerformanceWithLongBeatmap()
        {
            AddStep("select heavy beatmap", () => Beatmap.Value = new HeavyWorkingBeatmap(Audio));

            foreach (var rulesetInfo in rulesets.AvailableRulesets)
                setRuleset(rulesetInfo);
        }

        private void setRuleset(RulesetInfo rulesetInfo)
        {
            AddStep("set ruleset", () => Ruleset.Value = rulesetInfo);
        }

        private void selectBeatmap(IBeatmap? b)
        {
            AddStep($"select {b?.Metadata.Title ?? "null"} beatmap", () =>
            {
                Beatmap.Value = b == null ? Beatmap.Default : CreateWorkingBeatmap(b);
            });
        }

        private void checkDisplayedBPM(string target)
        {
            AddUntilStep($"displayed bpm is {target}", () =>
            {
                var label = titleWedge.ChildrenOfType<BeatmapTitleWedge.Statistic>().Single(l => l.TooltipText == BeatmapsetsStrings.ShowStatsBpm);
                return label.Text.ToString() == target;
            });
        }

        private (WorkingBeatmap, Screens.Select.SongSelect.BeatmapSetLookupResult) createTestBeatmap()
        {
            var working = CreateWorkingBeatmap(Ruleset.Value);
            var onlineSet = new APIBeatmapSet
            {
                OnlineID = working.BeatmapSetInfo.OnlineID,
                FavouriteCount = 2345,
                Beatmaps = new[]
                {
                    new APIBeatmap
                    {
                        OnlineID = working.BeatmapInfo.OnlineID,
                        PlayCount = 10000,
                        PassCount = 4567,
                        UserPlayCount = 123,
                    },
                }
            };

            working.BeatmapSetInfo.DateSubmitted = DateTimeOffset.Now;
            working.BeatmapSetInfo.DateRanked = DateTimeOffset.Now;
            return (working, Screens.Select.SongSelect.BeatmapSetLookupResult.Completed(onlineSet));
        }

        private static IBeatmap createTestBeatmapFromRuleset(RulesetInfo ruleset)
        {
            List<HitObject> objects = new List<HitObject>();
            for (double i = 0; i < 50000; i += 1000)
                objects.Add(new TestHitObject { StartTime = i });

            return new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Metadata = new BeatmapMetadata
                    {
                        Author = { Username = $"{ruleset.ShortName}Author" },
                        Artist = $"{ruleset.ShortName}Artist",
                        Source = $"{ruleset.ShortName}Source",
                        Title = $"{ruleset.ShortName}Title"
                    },
                    Ruleset = ruleset,
                    StarRating = 6,
                    DifficultyName = $"{ruleset.ShortName}Version",
                    Difficulty = new BeatmapDifficulty()
                },
                HitObjects = objects
            };
        }

        private class TestHitObject : ConvertHitObject;

        private class IncompatibleHitObject : HitObject;

        private class HeavyWorkingBeatmap : WorkingBeatmap
        {
            private static readonly BeatmapInfo beatmap_info = new BeatmapInfo
            {
                Metadata = new BeatmapMetadata
                {
                    Author = { Username = "osuAuthor" },
                    Artist = "osuArtist",
                    Source = "osuSource",
                    Title = "osuTitle"
                },
                Ruleset = new OsuRuleset().RulesetInfo,
                StarRating = 6,
                DifficultyName = "osuVersion",
                Difficulty = new BeatmapDifficulty()
            };

            public HeavyWorkingBeatmap(AudioManager audioManager)
                : base(beatmap_info, audioManager)
            {
            }

            protected override IBeatmap GetBeatmap()
            {
                List<HitObject> objects = new List<HitObject>();

                for (int i = 0; i < 200_000; i++)
                    objects.Add(new TestHitObject { StartTime = i * 1000 });

                return new Beatmap
                {
                    BeatmapInfo = beatmap_info,
                    HitObjects = objects
                };
            }

            public override Texture? GetBackground() => null;
            public override Stream? GetStream(string storagePath) => null;
            protected override Track? GetBeatmapTrack() => null;
            protected internal override ISkin? GetSkin() => null;
        }
    }
}
