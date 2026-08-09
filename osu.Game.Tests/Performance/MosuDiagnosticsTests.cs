// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Performance;
using osu.Game.Performance.Diagnostics;
using osu.Game.Skinning;

namespace osu.Game.Tests.Performance
{
    [TestFixture]
    public class MosuDiagnosticsTests
    {
        [Test]
        public void TestSessionStoreRoundTrip()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-test-{Guid.NewGuid():N}");
            var storage = new NativeStorage(tempDirectory);

            var session = new MosuDiagnosticsSession
            {
                SchemaVersion = MosuDiagnosticsDefaults.SchemaVersion,
                SessionId = "diagnostics-test",
                Phase = MosuDiagnosticsPhase.RunningBenchmark,
                OriginalSkin = "skin-test-id",
                Options = new MosuDiagnosticsSetupOptions
                {
                    Mode = MosuDiagnosticsMode.Quick,
                    UseDefaultSkin = true,
                    ExportZip = true,
                },
                Renderers = { osu.Framework.Configuration.RendererType.Deferred_Direct3D11 },
                Profiles =
                {
                    MosuDiagnosticsProfile.Current,
                    MosuDiagnosticsProfile.Recommended,
                },
                SkippedProfiles =
                {
                    new MosuDiagnosticsSkippedProfile
                    {
                        Renderer = osu.Framework.Configuration.RendererType.Deferred_Direct3D11,
                        Profile = MosuDiagnosticsProfile.VeldridPipelineLookupCache,
                        Reason = MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer,
                    },
                },
                RendererIndex = 0,
                ProfileIndex = 1,
                ExpectedRenderer = osu.Framework.Configuration.RendererType.Deferred_Direct3D11,
                ExpectedProfile = MosuDiagnosticsProfile.Recommended,
            };

            MosuDiagnosticsSessionStore.Save(storage, session);

            var loaded = MosuDiagnosticsSessionStore.TryLoad(storage);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.SessionId, Is.EqualTo(session.SessionId));
            Assert.That(loaded.Phase, Is.EqualTo(session.Phase));
            Assert.That(loaded.OriginalSkin, Is.EqualTo(session.OriginalSkin));
            Assert.That(loaded.Options.UseDefaultSkin, Is.True);
            Assert.That(loaded.Renderers, Has.Count.EqualTo(1));
            Assert.That(loaded.Profiles, Has.Count.EqualTo(2));
            Assert.That(loaded.SkippedProfiles, Has.Count.EqualTo(1));
            Assert.That(loaded.SkippedProfiles[0].Profile, Is.EqualTo(MosuDiagnosticsProfile.VeldridPipelineLookupCache));
            Assert.That(loaded.SkippedProfiles[0].Reason, Is.EqualTo(MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer));
            Assert.That(loaded.ExpectedProfile, Is.EqualTo(MosuDiagnosticsProfile.Recommended));
            Assert.That(loaded.TotalSteps, Is.EqualTo(2));

            Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestResultParserReadsMeasuredSegmentsOnly()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-parser-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            string segmentsPath = Path.Combine(tempDirectory, "mosu-performance-test.segments.csv");
            File.WriteAllText(segmentsPath, string.Join('\n', new[]
            {
                "segment,benchmark_id,benchmark_mode,benchmark_run,benchmark_warmup,active_ratio,draw_fps_avg,draw_fps_p5,draw_fps_p1,draw_fps_min,draw_ms_max_p99,update_ms_max_p99,draw_work_ms_max_p99,update_work_ms_max_p99,input_ms_max_p99,update_ccl_max_p99,update_invalidations_max_p99",
                "1,diagnostics-test-Deferred_Direct3D11,diagnostics,1,True,1,900,890,880,800,4.0,5.0,1.8,2.8,2.0,2000,4000",
                "2,diagnostics-test-Deferred_Direct3D11,diagnostics,2,False,1,1120,1110,1095,1010,2.4,3.0,1.2,2.0,1.1,1490,2750",
                "3,diagnostics-test-Deferred_Direct3D11,diagnostics,3,False,1,1100,1090,1075,990,2.6,3.2,1.4,2.2,1.3,1510,2850",
                "4,diagnostics-test-Deferred_Direct3D11,replay,1,False,1,700,690,680,600,6.0,7.0,4.0,5.0,3.0,3000,5000",
            }));

            var metrics = MosuDiagnosticsResultParser.TryParseSegments(tempDirectory, "diagnostics-test-Deferred_Direct3D11");

            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics!.AvgFps, Is.EqualTo(1110).Within(0.01));
            Assert.That(metrics.P5Fps, Is.EqualTo(1100).Within(0.01));
            Assert.That(metrics.DrawWorkP99Ms, Is.EqualTo(1.3).Within(0.01));
            Assert.That(metrics.UpdateWorkP99Ms, Is.EqualTo(2.1).Within(0.01));
            Assert.That(metrics.CclP99, Is.EqualTo(1500).Within(0.01));
            Assert.That(metrics.MeasuredRunCount, Is.EqualTo(MosuDiagnosticsDefaults.MeasuredRuns));
            Assert.That(metrics.Quality, Is.EqualTo(MosuDiagnosticsQuality.Reliable));
            Assert.That(metrics.QualityReason, Is.Null);

            Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestResultParserRejectsWarmupOnlyMeasurements()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-incomplete-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            string segmentsPath = Path.Combine(tempDirectory, "mosu-performance-test.segments.csv");
            File.WriteAllText(segmentsPath, string.Join('\n', new[]
            {
                "segment,benchmark_id,benchmark_mode,benchmark_run,benchmark_warmup,draw_fps_avg,draw_fps_p1",
                "1,diagnostics-test,diagnostics,1,True,1100,1080",
            }));

            var metrics = MosuDiagnosticsResultParser.TryParseSegments(tempDirectory, "diagnostics-test");

            Assert.That(metrics, Is.Null);

            Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestResultParserRejectsInactiveMeasurements()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-inactive-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            string segmentsPath = Path.Combine(tempDirectory, "mosu-performance-test.segments.csv");
            File.WriteAllText(segmentsPath, string.Join('\n', new[]
            {
                "segment,benchmark_id,benchmark_mode,benchmark_run,benchmark_warmup,active_ratio,draw_fps_avg,draw_fps_p1",
                "1,diagnostics-test,diagnostics,1,True,1,1100,1080",
                "2,diagnostics-test,diagnostics,2,False,0,1120,1095",
                "3,diagnostics-test,diagnostics,3,False,0.998,1110,1085",
            }));

            var metrics = MosuDiagnosticsResultParser.TryParseSegments(tempDirectory, "diagnostics-test");

            Assert.That(metrics, Is.Null);

            Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestDeepProfilesCoverEveryTopLevelPerformanceSetting()
        {
            var mappedSettings = MosuDiagnosticsProfiles.DeepProfiles
                                                       .Where(profile => MosuDiagnosticsProfiles.TryGetSetting(profile, out _))
                                                       .Select(profile =>
                                                       {
                                                           Assert.That(MosuDiagnosticsProfiles.TryGetSetting(profile, out OsuSetting setting), Is.True);
                                                           return setting;
                                                       })
                                                       .ToList();
            OsuSetting[] topLevelSettings =
            {
                OsuSetting.ForkWindowsUltraPerformanceMode,
                OsuSetting.ForkUse8kPollingRate,
                OsuSetting.ForkLargeTextureAtlas,
                OsuSetting.ForkDeferredVertexUploadBatching,
                OsuSetting.ForkDeferredDirectVertexUpload,
                OsuSetting.ForkDeferredDirectUniformUpload,
                OsuSetting.ForkVeldridPipelineLookupCache,
                OsuSetting.ForkStaticChildLifetimeCache,
                OsuSetting.ForkAllowTearing,
                OsuSetting.ForkUpdateThreadSpinWait,
                OsuSetting.ForkSkinPerformanceMode,
            };

            Assert.That(MosuDiagnosticsDefaults.WarmupRuns, Is.EqualTo(1));
            Assert.That(MosuDiagnosticsDefaults.MeasuredRuns, Is.EqualTo(2));
            Assert.That(
                MosuDiagnosticsProfiles.DeepProfiles.Count * MosuDiagnosticsDefaults.EstimatedStepDurationSeconds,
                Is.InRange(25 * 60, 27 * 60));
            Assert.That(MosuDiagnosticsProfiles.DeepProfiles, Has.Count.EqualTo(14));
            Assert.That(MosuDiagnosticsProfiles.DeepProfiles[0], Is.EqualTo(MosuDiagnosticsProfile.CleanBaseline));
            Assert.That(MosuDiagnosticsProfiles.DeepProfiles, Does.Not.Contain(MosuDiagnosticsProfile.ClassicSkinBaseline));
            Assert.That(
                MosuDiagnosticsProfiles.DeepProfiles.Where(profile => profile.ToString().StartsWith("SkinPerformance", StringComparison.Ordinal)),
                Is.EqualTo(new[] { MosuDiagnosticsProfile.SkinPerformanceMode }));
            Assert.That(MosuDiagnosticsProfiles.DeepProfiles[^2], Is.EqualTo(MosuDiagnosticsProfile.Recommended));
            Assert.That(MosuDiagnosticsProfiles.DeepProfiles[^1], Is.EqualTo(MosuDiagnosticsProfile.CleanBaselineVerification));
            Assert.That(mappedSettings, Is.Unique);
            Assert.That(mappedSettings, Is.EquivalentTo(topLevelSettings));
            Assert.That(MosuDiagnosticsProfiles.SettingProfiles, Is.EqualTo(MosuDiagnosticsProfiles.DeepProfiles.Skip(1).Take(topLevelSettings.Length)));
        }

        [Test]
        public void TestLegacyExtendedModeUsesTheSameCompleteProfileList()
        {
            Assert.That(MosuDiagnosticsProfiles.ExtendedProfiles, Is.EqualTo(MosuDiagnosticsProfiles.DeepProfiles));
        }

        [Test]
        public void TestSettingProfilesEnableOnlyTheTestedOptimisation()
        {
            var current = new MosuDiagnosticsConfigurationSnapshot();

            foreach (MosuDiagnosticsProfile profile in MosuDiagnosticsProfiles.SettingProfiles)
            {
                if (!MosuDiagnosticsProfiles.TryGetSetting(profile, out OsuSetting testedSetting))
                    continue;

                MosuDiagnosticsConfigurationSnapshot resolved = MosuDiagnosticsProfiles.Resolve(profile, current);

                foreach ((OsuSetting setting, _) in MosuRecommendedPerformancePreset.Settings)
                {
                    bool expected = testedSetting == OsuSetting.ForkSkinPerformanceMode
                        ? isSkinPerformanceSetting(setting)
                        : setting == testedSetting;

                    Assert.That(
                        resolved.Get(setting),
                        Is.EqualTo(expected),
                        $"{profile} unexpectedly resolved {setting} to {resolved.Get(setting)}");
                }
            }
        }

        [Test]
        public void TestProfileApplicabilityIsExplicit()
        {
            const osu.Framework.Configuration.RendererType deferred = osu.Framework.Configuration.RendererType.Deferred_Direct3D11;
            const osu.Framework.Configuration.RendererType nonDeferred = osu.Framework.Configuration.RendererType.Vulkan;

            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.VeldridPipelineLookupCache, deferred, true),
                Is.EqualTo(MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer));
            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.DeferredDirectVertexUpload, deferred, true),
                Is.Null);
            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.DeferredDirectVertexUpload, nonDeferred, true),
                Is.EqualTo(MosuDiagnosticsSkipReason.RequiresDeferredRenderer));
            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.VeldridPipelineLookupCache, nonDeferred, true),
                Is.Null);
            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.WindowsUltraPerformanceMode, deferred, false),
                Is.EqualTo(MosuDiagnosticsSkipReason.RequiresWindows));
            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.AllowTearing, deferred, false),
                Is.EqualTo(MosuDiagnosticsSkipReason.RequiresWindows));
            Assert.That(
                MosuDiagnosticsProfiles.GetSkipReason(MosuDiagnosticsProfile.AllowTearing, deferred, true),
                Is.Null);
        }

        [Test]
        public void TestDeferredWindowsPlanRunsEveryApplicableSetting()
        {
            const osu.Framework.Configuration.RendererType renderer = osu.Framework.Configuration.RendererType.Deferred_Direct3D11;

            (var runnableProfiles, var skippedProfiles) =
                MosuDiagnosticsProfiles.CreateApplicablePlan(MosuDiagnosticsProfiles.DeepProfiles, renderer, true);

            Assert.That(runnableProfiles, Has.Count.EqualTo(13));
            Assert.That(
                runnableProfiles.Count(profile => MosuDiagnosticsProfiles.SettingProfiles.Contains(profile)),
                Is.EqualTo(10));
            Assert.That(runnableProfiles, Does.Contain(MosuDiagnosticsProfile.Use8kPollingRate));
            Assert.That(runnableProfiles, Does.Contain(MosuDiagnosticsProfile.AllowTearing));
            Assert.That(runnableProfiles, Does.Contain(MosuDiagnosticsProfile.SkinPerformanceMode));
            Assert.That(skippedProfiles, Has.Count.EqualTo(1));
            Assert.That(skippedProfiles[0].Renderer, Is.EqualTo(renderer));
            Assert.That(skippedProfiles[0].Profile, Is.EqualTo(MosuDiagnosticsProfile.VeldridPipelineLookupCache));
            Assert.That(skippedProfiles[0].Reason, Is.EqualTo(MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer));
        }

        [Test]
        public void TestSkinPerformanceProfileEnablesCompleteSkinPackageOnly()
        {
            var current = new MosuDiagnosticsConfigurationSnapshot();

            foreach ((OsuSetting setting, _) in MosuRecommendedPerformancePreset.Settings)
                current.Set(setting, true);

            MosuDiagnosticsConfigurationSnapshot resolved = MosuDiagnosticsProfiles.Resolve(
                MosuDiagnosticsProfile.SkinPerformanceMode,
                current);

            foreach ((OsuSetting setting, _) in MosuRecommendedPerformancePreset.Settings)
            {
                Assert.That(
                    resolved.Get(setting),
                    Is.EqualTo(isSkinPerformanceSetting(setting)),
                    $"{setting} resolved to an unexpected value");
            }
        }

        [Test]
        public void TestBaselineAndRecommendedProfileResolution()
        {
            var current = new MosuDiagnosticsConfigurationSnapshot
            {
                WindowsUltraPerformanceMode = true,
                LargeTextureAtlas = true,
            };

            Assert.That(MosuDiagnosticsProfiles.Resolve(MosuDiagnosticsProfile.Current, current), Is.SameAs(current));

            MosuDiagnosticsConfigurationSnapshot baseline = MosuDiagnosticsProfiles.Resolve(MosuDiagnosticsProfile.CleanBaseline, current);
            Assert.That(MosuRecommendedPerformancePreset.Settings.All(pair => !baseline.Get(pair.Setting)), Is.True);
            MosuDiagnosticsConfigurationSnapshot verificationBaseline = MosuDiagnosticsProfiles.Resolve(MosuDiagnosticsProfile.CleanBaselineVerification, current);
            Assert.That(MosuRecommendedPerformancePreset.Settings.All(pair => !verificationBaseline.Get(pair.Setting)), Is.True);
            MosuDiagnosticsConfigurationSnapshot recommended = MosuDiagnosticsProfiles.Resolve(MosuDiagnosticsProfile.Recommended, current);

            foreach ((OsuSetting setting, bool value) in MosuRecommendedPerformancePreset.Settings)
                Assert.That(recommended.Get(setting), Is.EqualTo(value), setting.ToString());
        }

        [Test]
        public void TestTimeAdjustedBaselineInterpolatesByProfilePosition()
        {
            var session = new MosuDiagnosticsSession
            {
                Profiles =
                {
                    MosuDiagnosticsProfile.CleanBaseline,
                    MosuDiagnosticsProfile.WindowsUltraPerformanceMode,
                    MosuDiagnosticsProfile.Use8kPollingRate,
                    MosuDiagnosticsProfile.CleanBaselineVerification,
                },
            };
            var start = new MosuDiagnosticsRendererResult
            {
                AvgFps = 1000,
                P1Fps = 800,
                DrawP99Ms = 4,
                DrawWorkP99Ms = 2,
                StutterCount = 2,
                Quality = MosuDiagnosticsQuality.Reliable,
            };
            var end = new MosuDiagnosticsRendererResult
            {
                AvgFps = 700,
                P1Fps = 500,
                DrawP99Ms = 7,
                DrawWorkP99Ms = 5,
                StutterCount = 5,
                Quality = MosuDiagnosticsQuality.Variable,
            };

            MosuDiagnosticsRendererResult adjusted = MosuDiagnosticsAnalysis.CreateTimeAdjustedBaseline(
                session,
                start,
                end,
                MosuDiagnosticsProfile.Use8kPollingRate);

            Assert.That(adjusted.AvgFps, Is.EqualTo(800).Within(0.001));
            Assert.That(adjusted.P1Fps, Is.EqualTo(600).Within(0.001));
            Assert.That(adjusted.DrawP99Ms, Is.EqualTo(6).Within(0.001));
            Assert.That(adjusted.DrawWorkP99Ms, Is.EqualTo(4).Within(0.001));
            Assert.That(adjusted.StutterCount, Is.EqualTo(4));
            Assert.That(adjusted.Quality, Is.EqualTo(MosuDiagnosticsQuality.Variable));
        }

        [Test]
        public void TestStuttersAreLimitedToMeasuredGameplay()
        {
            MosuDiagnosticsStutterCollector.Reset();

            try
            {
                MosuDiagnosticsStutterCollector.BeginCollection(
                    osu.Framework.Configuration.RendererType.Deferred_Direct3D11,
                    osu.Framework.Configuration.RendererType.Deferred_Direct3D11);

                MosuDiagnosticsStutterCollector.TryRecord(new MosuDiagnosticsStutterEvent { FrameMs = 30, Note = "startup" });
                MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(false);
                MosuDiagnosticsStutterCollector.TryRecord(new MosuDiagnosticsStutterEvent { FrameMs = 31, Note = "warmup" });
                MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(true);
                MosuDiagnosticsStutterCollector.TryRecord(new MosuDiagnosticsStutterEvent { FrameMs = 32, Note = "measured" });
                MosuDiagnosticsStutterCollector.SetMeasuredGameplayActive(false);
                MosuDiagnosticsStutterCollector.TryRecord(new MosuDiagnosticsStutterEvent { FrameMs = 33, Note = "results" });

                var events = MosuDiagnosticsStutterCollector.TakeEvents();
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].Note, Is.EqualTo("measured"));
            }
            finally
            {
                MosuDiagnosticsStutterCollector.Reset();
            }
        }

        [Test]
        [NonParallelizable]
        public void TestBenchmarkOverrideWinsWithoutChangingPersistedBackingState()
        {
            bool previousEnabled = SkinPerformanceMode.Enabled;
            bool previousFreezeAnimations = SkinPerformanceMode.FreezeAnimations;

            try
            {
                SkinPerformanceMode.ClearBenchmarkOverride();
                SkinPerformanceMode.Enabled = true;
                SkinPerformanceMode.FreezeAnimations = false;

                SkinPerformanceMode.ApplyBenchmarkOverride(new SkinPerformanceModeOverride(
                    false,
                    true,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null));

                Assert.That(SkinPerformanceMode.HasBenchmarkOverride, Is.True);
                Assert.That(SkinPerformanceMode.Enabled, Is.False);
                Assert.That(SkinPerformanceMode.FreezeAnimations, Is.True);

                // Config callbacks continue updating the backing state while the benchmark remains isolated.
                SkinPerformanceMode.Enabled = true;
                SkinPerformanceMode.FreezeAnimations = false;
                Assert.That(SkinPerformanceMode.Enabled, Is.False);
                Assert.That(SkinPerformanceMode.FreezeAnimations, Is.True);

                SkinPerformanceMode.ClearBenchmarkOverride();
                Assert.That(SkinPerformanceMode.Enabled, Is.True);
                Assert.That(SkinPerformanceMode.FreezeAnimations, Is.False);
            }
            finally
            {
                SkinPerformanceMode.ClearBenchmarkOverride();
                SkinPerformanceMode.Enabled = previousEnabled;
                SkinPerformanceMode.FreezeAnimations = previousFreezeAnimations;
            }
        }

        [Test]
        public void TestInterruptedSessionRemainsAvailable()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-abandon-{Guid.NewGuid():N}");
            var storage = new NativeStorage(tempDirectory);

            MosuDiagnosticsSessionStore.Save(storage, new MosuDiagnosticsSession
            {
                SchemaVersion = MosuDiagnosticsDefaults.SchemaVersion,
                SessionId = "diagnostics-test",
                Phase = MosuDiagnosticsPhase.PendingRestart,
                StartedAt = DateTimeOffset.Now,
            });

            Assert.That(MosuDiagnosticsSessionStore.HasActiveSession(storage), Is.True);
            Assert.That(MosuDiagnosticsSessionStore.TryLoad(storage)?.SessionId, Is.EqualTo("diagnostics-test"));

            Directory.Delete(tempDirectory, true);
        }

        [TestCase(MosuDiagnosticsPhase.PendingResultsRestart)]
        [TestCase(MosuDiagnosticsPhase.ShowResults)]
        public void TestCompletedResultsSurviveNormalLaunch(MosuDiagnosticsPhase phase)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-results-{Guid.NewGuid():N}");
            var storage = new NativeStorage(tempDirectory);

            MosuDiagnosticsSessionStore.Save(storage, new MosuDiagnosticsSession
            {
                SchemaVersion = MosuDiagnosticsDefaults.SchemaVersion,
                SessionId = "diagnostics-test",
                Phase = phase,
                StartedAt = DateTimeOffset.Now,
                CompletedAt = DateTimeOffset.Now,
            });

            Assert.That(MosuDiagnosticsSessionStore.TryLoad(storage), Is.Not.Null);

            Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestOldDiagnosticsSessionIsPreserved()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-expired-{Guid.NewGuid():N}");
            var storage = new NativeStorage(tempDirectory);

            MosuDiagnosticsSessionStore.Save(storage, new MosuDiagnosticsSession
            {
                SchemaVersion = MosuDiagnosticsDefaults.SchemaVersion,
                SessionId = "diagnostics-test",
                Phase = MosuDiagnosticsPhase.RunningBenchmark,
                StartedAt = DateTimeOffset.Now - TimeSpan.FromDays(30),
            });

            Assert.That(MosuDiagnosticsSessionStore.TryLoad(storage)?.SessionId, Is.EqualTo("diagnostics-test"));

            Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestOutdatedSessionIsDiscarded()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"mosu-diagnostics-schema-{Guid.NewGuid():N}");
            var storage = new NativeStorage(tempDirectory);

            MosuDiagnosticsSessionStore.Save(storage, new MosuDiagnosticsSession
            {
                SchemaVersion = MosuDiagnosticsDefaults.SchemaVersion - 1,
                SessionId = "diagnostics-test",
                Phase = MosuDiagnosticsPhase.ShowResults,
                StartedAt = DateTimeOffset.Now,
            });

            Assert.That(MosuDiagnosticsSessionStore.TryLoad(storage), Is.Null);
            Assert.That(File.Exists(MosuDiagnosticsSessionStore.GetSessionPath(storage)), Is.False);

            Directory.Delete(tempDirectory, true);
        }

        private static bool isSkinPerformanceSetting(OsuSetting setting) =>
            setting == OsuSetting.ForkSkinPerformanceMode || isSkinSubSetting(setting);

        private static bool isSkinSubSetting(OsuSetting setting) =>
            setting is OsuSetting.ForkSkinPerformanceFreezeAnimations
                or OsuSetting.ForkSkinPerformanceSimplifyEffects
                or OsuSetting.ForkSkinPerformanceOptimiseTextures
                or OsuSetting.ForkSkinPerformanceSimplifyHud
                or OsuSetting.ForkSkinPerformanceSimplifyCounters
                or OsuSetting.ForkSkinPerformanceDisableKiaiFlashing
                or OsuSetting.ForkSkinPerformanceBlackBackground
                or OsuSetting.ForkArgonFollowRing;
    }
}
