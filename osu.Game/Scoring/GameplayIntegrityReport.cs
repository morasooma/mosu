// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Privacy-preserving input timing statistics collected during a play session.
    /// </summary>
    public class GameplayIntegrityReport
    {
        public const int CURRENT_VERSION = 4;

        [JsonProperty("version")]
        public int Version { get; set; } = CURRENT_VERSION;

        [JsonProperty("replay_frame_rate")]
        public int ReplayFrameRate { get; set; }

        [JsonProperty("clock_rate")]
        public double ClockRate { get; set; }

        [JsonProperty("replay_frame_count")]
        public int ReplayFrameCount { get; set; }

        [JsonProperty("replay_action_press_count")]
        public long ReplayActionPressCount { get; set; }

        [JsonProperty("replay_action_release_count")]
        public long ReplayActionReleaseCount { get; set; }

        [JsonProperty("input_sources")]
        public GameplayInputTimingReport[] InputSources { get; set; } = Array.Empty<GameplayInputTimingReport>();

        [JsonProperty("clock")]
        public GameplayClockIntegrityReport Clock { get; set; } = new GameplayClockIntegrityReport();

        [JsonProperty("difficulty")]
        public GameplayDifficultyIntegrityReport Difficulty { get; set; } = new GameplayDifficultyIntegrityReport();

        [JsonProperty("assistance")]
        public GameplayAssistanceIntegrityReport Assistance { get; set; } = new GameplayAssistanceIntegrityReport();

        [JsonProperty("debug_scenario", NullValueHandling = NullValueHandling.Ignore)]
        public string? DebugScenario { get; set; }
    }

    public class GameplayInputTimingReport
    {
        [JsonProperty("handler")]
        public string Handler { get; set; } = string.Empty;

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("event_count")]
        public long EventCount { get; set; }

        [JsonProperty("interval_count")]
        public long IntervalCount { get; set; }

        [JsonProperty("button_press_count")]
        public long ButtonPressCount { get; set; }

        [JsonProperty("button_release_count")]
        public long ButtonReleaseCount { get; set; }

        [JsonProperty("active_duration_ms")]
        public double ActiveDurationMilliseconds { get; set; }

        [JsonProperty("expected_interval_ms")]
        public double ExpectedIntervalMilliseconds { get; set; }

        [JsonProperty("mean_interval_ms")]
        public double MeanIntervalMilliseconds { get; set; }

        [JsonProperty("interval_std_dev_ms")]
        public double IntervalStandardDeviationMilliseconds { get; set; }

        [JsonProperty("matching_interval_ratio")]
        public double MatchingIntervalRatio { get; set; }
    }

    public class GameplayClockIntegrityReport
    {
        [JsonProperty("validation_enabled")]
        public bool ValidationEnabled { get; set; }

        [JsonProperty("playback_rate_valid")]
        public bool PlaybackRateValid { get; set; } = true;

        [JsonProperty("discrepancy_count")]
        public int DiscrepancyCount { get; set; }

        [JsonProperty("max_drift_ms")]
        public double MaxDriftMilliseconds { get; set; }

        /// <summary>
        /// Accumulated gameplay-clock time with explicit seek displacement removed.
        /// </summary>
        [JsonProperty("gameplay_elapsed_ms")]
        public double GameplayElapsedMilliseconds { get; set; }

        /// <summary>
        /// Accumulated gameplay-clock time before removing explicit seek displacement.
        /// </summary>
        [JsonProperty("raw_gameplay_elapsed_ms")]
        public double RawGameplayElapsedMilliseconds { get; set; }

        /// <summary>
        /// Number of explicit seeks performed while the gameplay clock was running.
        /// </summary>
        [JsonProperty("seek_count")]
        public int SeekCount { get; set; }

        /// <summary>
        /// Signed gameplay-clock displacement caused by explicit seeks.
        /// Forward skips are positive and rewinds are negative.
        /// </summary>
        [JsonProperty("seek_delta_ms")]
        public double SeekDeltaMilliseconds { get; set; }

        [JsonProperty("wall_elapsed_ms")]
        public double WallElapsedMilliseconds { get; set; }

        [JsonProperty("special_rate_mode")]
        public bool SpecialRateMode { get; set; }

        /// <summary>
        /// Explicitly authorised gameplay-clock skips. These are matched against the aggregate
        /// seek count and displacement by the score submission service.
        /// </summary>
        [JsonProperty("authorised_skips")]
        public GameplaySkipIntegrityEvent[] AuthorisedSkips { get; set; } = Array.Empty<GameplaySkipIntegrityEvent>();
    }

    public class GameplaySkipIntegrityEvent
    {
        public const string INTRO = "intro";
        public const string BREAK = "break";

        [JsonProperty("sequence")]
        public int Sequence { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonProperty("from_ms")]
        public double FromMilliseconds { get; set; }

        [JsonProperty("to_ms")]
        public double ToMilliseconds { get; set; }

        [JsonProperty("period_start_ms")]
        public double PeriodStartMilliseconds { get; set; }

        [JsonProperty("period_end_ms")]
        public double PeriodEndMilliseconds { get; set; }

        [JsonProperty("break_index", NullValueHandling = NullValueHandling.Ignore)]
        public int? BreakIndex { get; set; }

        [JsonProperty("multiplayer_server_authorised")]
        public bool MultiplayerServerAuthorised { get; set; }
    }

    public class GameplayDifficultyIntegrityReport
    {
        [JsonProperty("original_ar")]
        public float OriginalApproachRate { get; set; }

        [JsonProperty("original_od")]
        public float OriginalOverallDifficulty { get; set; }

        [JsonProperty("original_cs")]
        public float OriginalCircleSize { get; set; }

        [JsonProperty("original_hp")]
        public float OriginalDrainRate { get; set; }

        [JsonProperty("applied_ar")]
        public float AppliedApproachRate { get; set; }

        [JsonProperty("applied_od")]
        public float AppliedOverallDifficulty { get; set; }

        [JsonProperty("applied_cs")]
        public float AppliedCircleSize { get; set; }

        [JsonProperty("applied_hp")]
        public float AppliedDrainRate { get; set; }

        [JsonProperty("custom_ar_enabled")]
        public bool CustomApproachRateEnabled { get; set; }

        [JsonProperty("visual_od11_enabled")]
        public bool VisualOD11Enabled { get; set; }
    }

    public class GameplayAssistanceIntegrityReport
    {
        [JsonProperty("aim_assist_enabled")]
        public bool AimAssistEnabled { get; set; }

        [JsonProperty("aim_assist_declared_mod")]
        public bool AimAssistDeclaredMod { get; set; }

        [JsonProperty("aim_assist_adjusted_frame_count")]
        public long AimAssistAdjustedFrameCount { get; set; }

        [JsonProperty("aim_assist_max_adjustment")]
        public float AimAssistMaxAdjustment { get; set; }

        [JsonProperty("relax_enabled")]
        public bool RelaxEnabled { get; set; }

        [JsonProperty("relax_declared_mod")]
        public bool RelaxDeclaredMod { get; set; }

        [JsonProperty("relax_generated_press_count")]
        public long RelaxGeneratedPressCount { get; set; }

        [JsonProperty("relax_generated_release_count")]
        public long RelaxGeneratedReleaseCount { get; set; }
    }

#if DEBUG
    public enum GameplayIntegrityDebugScenario
    {
        None,
        ReplayBotLike,
        Timewarp,
        CustomApproachRate,
        UndeclaredAimAssist,
        UndeclaredRelax,
        AllSignals,
    }

    /// <summary>
    /// Deterministic report fault injection used to test the local anti-cheat pipeline.
    /// This type is not compiled into Release builds.
    /// </summary>
    public static class GameplayIntegrityDebugInjector
    {
        public static void Apply(GameplayIntegrityReport report, GameplayIntegrityDebugScenario scenario)
        {
            if (scenario == GameplayIntegrityDebugScenario.None)
                return;

            report.DebugScenario = scenario.ToString();

            switch (scenario)
            {
                case GameplayIntegrityDebugScenario.ReplayBotLike:
                    injectReplayBotLike(report);
                    break;

                case GameplayIntegrityDebugScenario.Timewarp:
                    injectTimewarp(report);
                    break;

                case GameplayIntegrityDebugScenario.CustomApproachRate:
                    injectCustomApproachRate(report);
                    break;

                case GameplayIntegrityDebugScenario.UndeclaredAimAssist:
                    injectUndeclaredAimAssist(report);
                    break;

                case GameplayIntegrityDebugScenario.UndeclaredRelax:
                    injectUndeclaredRelax(report);
                    break;

                case GameplayIntegrityDebugScenario.AllSignals:
                    injectReplayBotLike(report);
                    injectTimewarp(report);
                    injectCustomApproachRate(report);
                    injectUndeclaredAimAssist(report);
                    injectUndeclaredRelax(report);
                    break;
            }
        }

        private static void injectReplayBotLike(GameplayIntegrityReport report)
        {
            report.ReplayFrameCount = Math.Max(report.ReplayFrameCount, 3600);
            report.ReplayActionPressCount = Math.Max(report.ReplayActionPressCount, 120);
            report.ReplayActionReleaseCount = Math.Max(report.ReplayActionReleaseCount, 120);

            foreach (GameplayInputTimingReport source in report.InputSources)
            {
                source.EventCount = 0;
                source.IntervalCount = 0;
                source.ButtonPressCount = 0;
                source.ButtonReleaseCount = 0;
                source.ActiveDurationMilliseconds = 0;
                source.MeanIntervalMilliseconds = 0;
                source.IntervalStandardDeviationMilliseconds = 0;
                source.MatchingIntervalRatio = 0;
            }
        }

        private static void injectTimewarp(GameplayIntegrityReport report)
        {
            report.Clock.ValidationEnabled = true;
            report.Clock.PlaybackRateValid = false;
            report.Clock.DiscrepancyCount = Math.Max(report.Clock.DiscrepancyCount, 7);
            report.Clock.MaxDriftMilliseconds = Math.Max(report.Clock.MaxDriftMilliseconds, 1200);
            report.Clock.WallElapsedMilliseconds = Math.Max(report.Clock.WallElapsedMilliseconds, 60_000);
            report.Clock.GameplayElapsedMilliseconds = report.Clock.WallElapsedMilliseconds * 1.25;
            report.Clock.RawGameplayElapsedMilliseconds =
                report.Clock.GameplayElapsedMilliseconds + report.Clock.SeekDeltaMilliseconds;
            report.Clock.SpecialRateMode = false;
        }

        private static void injectCustomApproachRate(GameplayIntegrityReport report)
        {
            report.Difficulty.CustomApproachRateEnabled = true;
            report.Difficulty.AppliedApproachRate = Math.Max(report.Difficulty.AppliedApproachRate, 11);
        }

        private static void injectUndeclaredAimAssist(GameplayIntegrityReport report)
        {
            report.Assistance.AimAssistEnabled = true;
            report.Assistance.AimAssistDeclaredMod = false;
            report.Assistance.AimAssistAdjustedFrameCount = Math.Max(report.Assistance.AimAssistAdjustedFrameCount, 240);
            report.Assistance.AimAssistMaxAdjustment = Math.Max(report.Assistance.AimAssistMaxAdjustment, 18);
        }

        private static void injectUndeclaredRelax(GameplayIntegrityReport report)
        {
            report.Assistance.RelaxEnabled = true;
            report.Assistance.RelaxDeclaredMod = false;
            report.Assistance.RelaxGeneratedPressCount = Math.Max(report.Assistance.RelaxGeneratedPressCount, 120);
            report.Assistance.RelaxGeneratedReleaseCount = Math.Max(report.Assistance.RelaxGeneratedReleaseCount, 120);
        }
    }
#endif
}
