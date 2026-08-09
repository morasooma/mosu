// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Configuration;
using osu.Game.Configuration;

namespace osu.Game.Performance.Diagnostics
{
    public static class MosuDiagnosticsDefaults
    {
        public const int SchemaVersion = 6;
        public const int BeatmapOnlineId = 4999492;
        public const string ScoreHash = "13561e3b9afdcc17c19e7eb1404af90fb4ea4c044ae69e16dd671f12069309e1";
        public const string ReplayFilename = "mosu-performance-benchmark.osr";
        public const int MeasuredRuns = 2;
        public const int WarmupRuns = 1;
        public const int EstimatedStepDurationSeconds = 110;
        public const string BenchmarkMode = "diagnostics";
        public const double StutterThresholdMs = 20;
        public const double ReliableVariationPercent = 5;
        public const double UnstableVariationPercent = 10;
    }

    public enum MosuDiagnosticsMode
    {
        Quick,
        Deep,
        RendererComparison,
        Extended,
    }

    public enum MosuDiagnosticsProfile
    {
        Current,
        Recommended,
        CleanBaseline,
        ClassicSkinBaseline,
        WindowsUltraPerformanceMode,
        Use8kPollingRate,
        LargeTextureAtlas,
        DeferredVertexUploadBatching,
        DeferredDirectVertexUpload,
        DeferredDirectUniformUpload,
        VeldridPipelineLookupCache,
        StaticChildLifetimeCache,
        AllowTearing,
        UpdateThreadSpinWait,
        SkinPerformanceMode,
        CleanBaselineVerification,
    }

    public enum MosuDiagnosticsQuality
    {
        Reliable,
        SingleRun,
        Variable,
        Unstable,
        Failed,
    }

    public enum MosuDiagnosticsSkipReason
    {
        RequiresWindows,
        RequiresDeferredRenderer,
        RequiresNonDeferredRenderer,
    }

    public enum MosuDiagnosticsPhase
    {
        PendingRestart,
        RunningBenchmark,
        PendingResultsRestart,
        ShowResults,
        Paused,
    }

    public sealed class MosuDiagnosticsSetupOptions
    {
        public MosuDiagnosticsMode Mode { get; set; } = MosuDiagnosticsMode.Quick;
        public bool UseDefaultSkin { get; set; } = true;
        public bool ExportZip { get; set; } = true;
    }

    public sealed class MosuDiagnosticsConfigurationSnapshot
    {
        public bool WindowsUltraPerformanceMode { get; set; }
        public bool LargeTextureAtlas { get; set; }
        public bool SkinPerformanceMode { get; set; }
        public bool SkinPerformanceFreezeAnimations { get; set; }
        public bool SkinPerformanceSimplifyEffects { get; set; }
        public bool SkinPerformanceOptimiseTextures { get; set; }
        public bool SkinPerformanceSimplifyHud { get; set; }
        public bool SkinPerformanceSimplifyCounters { get; set; }
        public bool SkinPerformanceDisableKiaiFlashing { get; set; }
        public bool SkinPerformanceBlackBackground { get; set; }
        public bool ArgonFollowRing { get; set; }
        public bool DeferredVertexUploadBatching { get; set; }
        public bool DeferredDirectVertexUpload { get; set; }
        public bool DeferredDirectUniformUpload { get; set; }
        public bool VeldridPipelineLookupCache { get; set; }
        public bool StaticChildLifetimeCache { get; set; }
        public bool Use8kPollingRate { get; set; }
        public bool AllowTearing { get; set; }
        public bool UpdateThreadSpinWait { get; set; }

        public bool Get(OsuSetting setting) => setting switch
        {
            OsuSetting.ForkWindowsUltraPerformanceMode => WindowsUltraPerformanceMode,
            OsuSetting.ForkLargeTextureAtlas => LargeTextureAtlas,
            OsuSetting.ForkSkinPerformanceMode => SkinPerformanceMode,
            OsuSetting.ForkSkinPerformanceFreezeAnimations => SkinPerformanceFreezeAnimations,
            OsuSetting.ForkSkinPerformanceSimplifyEffects => SkinPerformanceSimplifyEffects,
            OsuSetting.ForkSkinPerformanceOptimiseTextures => SkinPerformanceOptimiseTextures,
            OsuSetting.ForkSkinPerformanceSimplifyHud => SkinPerformanceSimplifyHud,
            OsuSetting.ForkSkinPerformanceSimplifyCounters => SkinPerformanceSimplifyCounters,
            OsuSetting.ForkSkinPerformanceDisableKiaiFlashing => SkinPerformanceDisableKiaiFlashing,
            OsuSetting.ForkSkinPerformanceBlackBackground => SkinPerformanceBlackBackground,
            OsuSetting.ForkArgonFollowRing => ArgonFollowRing,
            OsuSetting.ForkDeferredVertexUploadBatching => DeferredVertexUploadBatching,
            OsuSetting.ForkDeferredDirectVertexUpload => DeferredDirectVertexUpload,
            OsuSetting.ForkDeferredDirectUniformUpload => DeferredDirectUniformUpload,
            OsuSetting.ForkVeldridPipelineLookupCache => VeldridPipelineLookupCache,
            OsuSetting.ForkStaticChildLifetimeCache => StaticChildLifetimeCache,
            OsuSetting.ForkUse8kPollingRate => Use8kPollingRate,
            OsuSetting.ForkAllowTearing => AllowTearing,
            OsuSetting.ForkUpdateThreadSpinWait => UpdateThreadSpinWait,
            _ => false,
        };

        public void Set(OsuSetting setting, bool value)
        {
            switch (setting)
            {
                case OsuSetting.ForkWindowsUltraPerformanceMode:
                    WindowsUltraPerformanceMode = value;
                    break;

                case OsuSetting.ForkLargeTextureAtlas:
                    LargeTextureAtlas = value;
                    break;

                case OsuSetting.ForkSkinPerformanceMode:
                    SkinPerformanceMode = value;
                    break;

                case OsuSetting.ForkSkinPerformanceFreezeAnimations:
                    SkinPerformanceFreezeAnimations = value;
                    break;

                case OsuSetting.ForkSkinPerformanceSimplifyEffects:
                    SkinPerformanceSimplifyEffects = value;
                    break;

                case OsuSetting.ForkSkinPerformanceOptimiseTextures:
                    SkinPerformanceOptimiseTextures = value;
                    break;

                case OsuSetting.ForkSkinPerformanceSimplifyHud:
                    SkinPerformanceSimplifyHud = value;
                    break;

                case OsuSetting.ForkSkinPerformanceSimplifyCounters:
                    SkinPerformanceSimplifyCounters = value;
                    break;

                case OsuSetting.ForkSkinPerformanceDisableKiaiFlashing:
                    SkinPerformanceDisableKiaiFlashing = value;
                    break;

                case OsuSetting.ForkSkinPerformanceBlackBackground:
                    SkinPerformanceBlackBackground = value;
                    break;

                case OsuSetting.ForkArgonFollowRing:
                    ArgonFollowRing = value;
                    break;

                case OsuSetting.ForkDeferredVertexUploadBatching:
                    DeferredVertexUploadBatching = value;
                    break;

                case OsuSetting.ForkDeferredDirectVertexUpload:
                    DeferredDirectVertexUpload = value;
                    break;

                case OsuSetting.ForkDeferredDirectUniformUpload:
                    DeferredDirectUniformUpload = value;
                    break;

                case OsuSetting.ForkVeldridPipelineLookupCache:
                    VeldridPipelineLookupCache = value;
                    break;

                case OsuSetting.ForkStaticChildLifetimeCache:
                    StaticChildLifetimeCache = value;
                    break;

                case OsuSetting.ForkUse8kPollingRate:
                    Use8kPollingRate = value;
                    break;

                case OsuSetting.ForkAllowTearing:
                    AllowTearing = value;
                    break;

                case OsuSetting.ForkUpdateThreadSpinWait:
                    UpdateThreadSpinWait = value;
                    break;
            }
        }
    }

    public static class MosuDiagnosticsProfiles
    {
        public static readonly IReadOnlyList<MosuDiagnosticsProfile> SettingProfiles = new[]
        {
            MosuDiagnosticsProfile.WindowsUltraPerformanceMode,
            MosuDiagnosticsProfile.Use8kPollingRate,
            MosuDiagnosticsProfile.LargeTextureAtlas,
            MosuDiagnosticsProfile.DeferredVertexUploadBatching,
            MosuDiagnosticsProfile.DeferredDirectVertexUpload,
            MosuDiagnosticsProfile.DeferredDirectUniformUpload,
            MosuDiagnosticsProfile.VeldridPipelineLookupCache,
            MosuDiagnosticsProfile.StaticChildLifetimeCache,
            MosuDiagnosticsProfile.AllowTearing,
            MosuDiagnosticsProfile.UpdateThreadSpinWait,
            MosuDiagnosticsProfile.SkinPerformanceMode,
        };

        public static readonly IReadOnlyList<MosuDiagnosticsProfile> DeepProfiles = createFullProfileList();

        // Kept for loading already selected legacy setup modes. New diagnostics use the same complete profile plan.
        public static readonly IReadOnlyList<MosuDiagnosticsProfile> ExtendedProfiles = DeepProfiles;

        private static IReadOnlyList<MosuDiagnosticsProfile> createFullProfileList()
        {
            var profiles = new List<MosuDiagnosticsProfile>
            {
                MosuDiagnosticsProfile.CleanBaseline,
            };

            profiles.AddRange(SettingProfiles);
            profiles.Add(MosuDiagnosticsProfile.Recommended);
            profiles.Add(MosuDiagnosticsProfile.CleanBaselineVerification);
            return profiles;
        }

        public static MosuDiagnosticsConfigurationSnapshot Resolve(MosuDiagnosticsProfile profile, MosuDiagnosticsConfigurationSnapshot current)
        {
            if (profile == MosuDiagnosticsProfile.Current)
                return current;

            var resolved = new MosuDiagnosticsConfigurationSnapshot();

            if (profile == MosuDiagnosticsProfile.Recommended)
            {
                foreach ((OsuSetting setting, bool value) in MosuRecommendedPerformancePreset.Settings)
                    resolved.Set(setting, value);

                return resolved;
            }

            if (profile == MosuDiagnosticsProfile.SkinPerformanceMode)
            {
                foreach ((OsuSetting setting, _) in MosuRecommendedPerformancePreset.Settings)
                {
                    if (isSkinPerformanceSetting(setting))
                        resolved.Set(setting, true);
                }

                return resolved;
            }

            if (!TryGetSetting(profile, out OsuSetting testedSetting))
                return resolved;

            resolved.Set(testedSetting, true);

            return resolved;
        }

        public static bool TryGetSetting(MosuDiagnosticsProfile profile, out OsuSetting setting)
        {
            setting = profile switch
            {
                MosuDiagnosticsProfile.WindowsUltraPerformanceMode => OsuSetting.ForkWindowsUltraPerformanceMode,
                MosuDiagnosticsProfile.Use8kPollingRate => OsuSetting.ForkUse8kPollingRate,
                MosuDiagnosticsProfile.LargeTextureAtlas => OsuSetting.ForkLargeTextureAtlas,
                MosuDiagnosticsProfile.DeferredVertexUploadBatching => OsuSetting.ForkDeferredVertexUploadBatching,
                MosuDiagnosticsProfile.DeferredDirectVertexUpload => OsuSetting.ForkDeferredDirectVertexUpload,
                MosuDiagnosticsProfile.DeferredDirectUniformUpload => OsuSetting.ForkDeferredDirectUniformUpload,
                MosuDiagnosticsProfile.VeldridPipelineLookupCache => OsuSetting.ForkVeldridPipelineLookupCache,
                MosuDiagnosticsProfile.StaticChildLifetimeCache => OsuSetting.ForkStaticChildLifetimeCache,
                MosuDiagnosticsProfile.AllowTearing => OsuSetting.ForkAllowTearing,
                MosuDiagnosticsProfile.UpdateThreadSpinWait => OsuSetting.ForkUpdateThreadSpinWait,
                MosuDiagnosticsProfile.SkinPerformanceMode => OsuSetting.ForkSkinPerformanceMode,
                _ => default,
            };

            return profile is >= MosuDiagnosticsProfile.WindowsUltraPerformanceMode and <= MosuDiagnosticsProfile.SkinPerformanceMode;
        }

        public static bool IsReferenceProfile(MosuDiagnosticsProfile profile) =>
            profile is MosuDiagnosticsProfile.CleanBaseline
                or MosuDiagnosticsProfile.CleanBaselineVerification
                or MosuDiagnosticsProfile.ClassicSkinBaseline;

        public static MosuDiagnosticsSkipReason? GetSkipReason(MosuDiagnosticsProfile profile, RendererType renderer, bool isWindows)
        {
            bool deferredRenderer = renderer.ToString().StartsWith("Deferred_", StringComparison.Ordinal);

            return profile switch
            {
                MosuDiagnosticsProfile.WindowsUltraPerformanceMode when !isWindows => MosuDiagnosticsSkipReason.RequiresWindows,
                MosuDiagnosticsProfile.AllowTearing when !isWindows => MosuDiagnosticsSkipReason.RequiresWindows,
                MosuDiagnosticsProfile.DeferredVertexUploadBatching
                    or MosuDiagnosticsProfile.DeferredDirectVertexUpload
                    or MosuDiagnosticsProfile.DeferredDirectUniformUpload when !deferredRenderer => MosuDiagnosticsSkipReason.RequiresDeferredRenderer,
                MosuDiagnosticsProfile.VeldridPipelineLookupCache when deferredRenderer => MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer,
                _ => null,
            };
        }

        public static (List<MosuDiagnosticsProfile> Profiles, List<MosuDiagnosticsSkippedProfile> SkippedProfiles) CreateApplicablePlan(
            IReadOnlyList<MosuDiagnosticsProfile> source,
            RendererType renderer,
            bool isWindows)
        {
            var profiles = new List<MosuDiagnosticsProfile>();
            var skippedProfiles = new List<MosuDiagnosticsSkippedProfile>();

            foreach (MosuDiagnosticsProfile profile in source)
            {
                MosuDiagnosticsSkipReason? reason = GetSkipReason(profile, renderer, isWindows);

                if (reason == null)
                {
                    profiles.Add(profile);
                    continue;
                }

                skippedProfiles.Add(new MosuDiagnosticsSkippedProfile
                {
                    Renderer = renderer,
                    Profile = profile,
                    Reason = reason.Value,
                });
            }

            return (profiles, skippedProfiles);
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

    public static class MosuDiagnosticsAnalysis
    {
        public static MosuDiagnosticsRendererResult? CreateComparisonBaseline(
            MosuDiagnosticsSession session,
            MosuDiagnosticsRendererResult result)
        {
            if (MosuDiagnosticsProfiles.IsReferenceProfile(result.Profile))
                return null;

            const MosuDiagnosticsProfile baselineProfile = MosuDiagnosticsProfile.CleanBaseline;
            const MosuDiagnosticsProfile verificationProfile = MosuDiagnosticsProfile.CleanBaselineVerification;

            MosuDiagnosticsRendererResult? start = findUsableResult(session, result.Renderer, baselineProfile);
            if (start == null)
                return null;

            MosuDiagnosticsRendererResult? end = findUsableResult(session, result.Renderer, verificationProfile);
            return createTimeAdjustedBaseline(session, start, end, result.Profile, baselineProfile, verificationProfile);
        }

        public static MosuDiagnosticsRendererResult CreateTimeAdjustedBaseline(
            MosuDiagnosticsSession session,
            MosuDiagnosticsRendererResult start,
            MosuDiagnosticsRendererResult? end,
            MosuDiagnosticsProfile profile)
            => createTimeAdjustedBaseline(
                session,
                start,
                end,
                profile,
                MosuDiagnosticsProfile.CleanBaseline,
                MosuDiagnosticsProfile.CleanBaselineVerification);

        private static MosuDiagnosticsRendererResult createTimeAdjustedBaseline(
            MosuDiagnosticsSession session,
            MosuDiagnosticsRendererResult start,
            MosuDiagnosticsRendererResult? end,
            MosuDiagnosticsProfile profile,
            MosuDiagnosticsProfile baselineProfile,
            MosuDiagnosticsProfile verificationProfile)
        {
            if (end == null)
                return start;

            int startIndex = session.Profiles.IndexOf(baselineProfile);
            int endIndex = session.Profiles.IndexOf(verificationProfile);
            int profileIndex = session.Profiles.IndexOf(profile);

            if (startIndex < 0 || endIndex <= startIndex || profileIndex < 0)
                return start;

            double amount = Math.Clamp((double)(profileIndex - startIndex) / (endIndex - startIndex), 0, 1);
            double? interpolatedMinFps = interpolate(start.MinFps, end.MinFps, amount);

            return new MosuDiagnosticsRendererResult
            {
                Renderer = start.Renderer,
                Profile = baselineProfile,
                ResolvedRenderer = start.ResolvedRenderer,
                Started = true,
                Completed = true,
                AvgFps = interpolate(start.AvgFps, end.AvgFps, amount),
                P5Fps = interpolate(start.P5Fps, end.P5Fps, amount),
                P1Fps = interpolate(start.P1Fps, end.P1Fps, amount),
                MinFps = interpolatedMinFps != null ? (int)Math.Round(interpolatedMinFps.Value) : null,
                DrawP99Ms = interpolate(start.DrawP99Ms, end.DrawP99Ms, amount),
                UpdateP99Ms = interpolate(start.UpdateP99Ms, end.UpdateP99Ms, amount),
                DrawWorkP99Ms = interpolate(start.DrawWorkP99Ms, end.DrawWorkP99Ms, amount),
                UpdateWorkP99Ms = interpolate(start.UpdateWorkP99Ms, end.UpdateWorkP99Ms, amount),
                InputP99Ms = interpolate(start.InputP99Ms, end.InputP99Ms, amount),
                StutterCount = (int)Math.Round(start.StutterCount + (end.StutterCount - start.StutterCount) * amount),
                Quality = (MosuDiagnosticsQuality)Math.Max((int)start.Quality, (int)end.Quality),
            };
        }

        private static MosuDiagnosticsRendererResult? findUsableResult(
            MosuDiagnosticsSession session,
            RendererType renderer,
            MosuDiagnosticsProfile profile)
            => session.Results.Find(result =>
                result.Renderer == renderer
                && result.Profile == profile
                && result.Completed
                && result.Quality != MosuDiagnosticsQuality.Failed);

        private static double? interpolate(double? start, double? end, double amount)
            => start != null && end != null ? start.Value + (end.Value - start.Value) * amount : start ?? end;
    }

    public sealed class MosuDiagnosticsStutterEvent
    {
        public string LocalTime { get; set; } = string.Empty;
        public double ElapsedMs { get; set; }
        public string Thread { get; set; } = string.Empty;
        public double FrameMs { get; set; }
        public double GcMs { get; set; }
        public long Ccl { get; set; }
        public long Invalidations { get; set; }
        public string RendererSetting { get; set; } = string.Empty;
        public string ResolvedRenderer { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public sealed class MosuDiagnosticsRendererResult
    {
        public RendererType Renderer { get; set; }
        public MosuDiagnosticsProfile Profile { get; set; }
        public string ResolvedRenderer { get; set; } = string.Empty;
        public bool Started { get; set; }
        public bool Completed { get; set; }
        public string? FailureReason { get; set; }
        public double? AvgFps { get; set; }
        public double? P5Fps { get; set; }
        public double? P1Fps { get; set; }
        public int? MinFps { get; set; }
        public double? DrawP99Ms { get; set; }
        public double? UpdateP99Ms { get; set; }
        public double? DrawWorkP99Ms { get; set; }
        public double? UpdateWorkP99Ms { get; set; }
        public double? InputP99Ms { get; set; }
        public double? CclP99 { get; set; }
        public double? InvalP99 { get; set; }
        public int MeasuredRunCount { get; set; }
        public double? AvgFpsVariationPercent { get; set; }
        public double? P1FpsVariationPercent { get; set; }
        public MosuDiagnosticsQuality Quality { get; set; }
        public string? QualityReason { get; set; }
        public int StutterCount { get; set; }
        public List<MosuDiagnosticsStutterEvent> Stutters { get; set; } = new List<MosuDiagnosticsStutterEvent>();
    }

    public sealed class MosuDiagnosticsSkippedProfile
    {
        public RendererType Renderer { get; set; }
        public MosuDiagnosticsProfile Profile { get; set; }
        public MosuDiagnosticsSkipReason Reason { get; set; }
    }

    public sealed class MosuDiagnosticsMachineProfile
    {
        public string MachineId { get; set; } = string.Empty;
        public string OsDescription { get; set; } = string.Empty;
        public string CpuDescription { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSetMb { get; set; }
        public string? GpuDescription { get; set; }
    }

    public sealed class MosuDiagnosticsSession
    {
        public int SchemaVersion { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public MosuDiagnosticsPhase Phase { get; set; }
        public MosuDiagnosticsSetupOptions Options { get; set; } = new MosuDiagnosticsSetupOptions();
        public string ClientVersion { get; set; } = string.Empty;
        public string ClientVersionHash { get; set; } = string.Empty;
        public string RuntimeDescription { get; set; } = string.Empty;
        public MosuDiagnosticsMachineProfile Machine { get; set; } = new MosuDiagnosticsMachineProfile();
        public MosuDiagnosticsConfigurationSnapshot CurrentConfiguration { get; set; } = new MosuDiagnosticsConfigurationSnapshot();
        public RendererType OriginalRenderer { get; set; }
        public string OriginalSkin { get; set; } = string.Empty;
        public List<RendererType> Renderers { get; set; } = new List<RendererType>();
        public List<MosuDiagnosticsProfile> Profiles { get; set; } = new List<MosuDiagnosticsProfile>();
        public int RendererIndex { get; set; }
        public int ProfileIndex { get; set; }
        public RendererType? ExpectedRenderer { get; set; }
        public MosuDiagnosticsProfile? ExpectedProfile { get; set; }
        public string? BenchmarkId { get; set; }
        public string? ExportZipPath { get; set; }
        public bool ExportFolderPresented { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public List<MosuDiagnosticsRendererResult> Results { get; set; } = new List<MosuDiagnosticsRendererResult>();
        public List<MosuDiagnosticsSkippedProfile> SkippedProfiles { get; set; } = new List<MosuDiagnosticsSkippedProfile>();
        public List<MosuDiagnosticsStutterEvent> PendingStutters { get; set; } = new List<MosuDiagnosticsStutterEvent>();

        public int TotalSteps => Renderers.Count * Profiles.Count;

        public int CompletedSteps => Results.Count;
    }
}
