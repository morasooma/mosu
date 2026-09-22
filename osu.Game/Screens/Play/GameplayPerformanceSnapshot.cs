// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Screens.Play
{
    public static class GameplayPerformanceSnapshot
    {
        public static int Combo { get; set; }

        public static int MaxCombo { get; set; }

        public static int MissCount { get; set; }

        public static string LastJudgement { get; set; } = string.Empty;

        public static double LastJudgementTime { get; set; }

        public static double Health { get; set; }

        public static string BenchmarkId { get; set; } = string.Empty;

        public static string BenchmarkMode { get; set; } = string.Empty;

        public static int BenchmarkRun { get; set; }

        public static bool BenchmarkWarmup { get; set; }

        public static string BenchmarkReplayHash { get; set; } = string.Empty;

        public static bool BenchmarkGameplayActive { get; set; }

        /// <summary>
        /// Whether an automated replay benchmark is currently orchestrating gameplay.
        /// Used to keep the window on top and execution active even if focus is lost.
        /// </summary>
        public static bool BenchmarkRunnerActive { get; set; }

        public static bool ObservedHitObjectGraphActive { get; set; }

        public static int ObservedHitObjectCount { get; set; }

        public static float ObservedHitObjectShiftPixels { get; set; }

        public static string ObservedHitObjectState { get; set; } = string.Empty;

        public static double ObservedHitObjectScore { get; set; }

        public static double ObservedHitObjectMonitoringTime { get; set; }

        public static double ObservedHitObjectSuspiciousTime { get; set; }

        public static double ObservedHitObjectDivergentTime { get; set; }

        public static double ObservedHitObjectAverageScore { get; set; }

        public static double ObservedHitObjectPeakScore { get; set; }

        public static int ObservedHitObjectPressEvidenceCount { get; set; }

        public static int ObservedHitObjectShiftedTargetCount { get; set; }

        public static void Reset()
        {
            Combo = 0;
            MaxCombo = 0;
            MissCount = 0;
            LastJudgement = string.Empty;
            LastJudgementTime = 0;
            Health = 0;
            ObservedHitObjectGraphActive = false;
            ObservedHitObjectCount = 0;
            ObservedHitObjectShiftPixels = 0;
            ObservedHitObjectState = string.Empty;
            ObservedHitObjectScore = 0;
            ObservedHitObjectMonitoringTime = 0;
            ObservedHitObjectSuspiciousTime = 0;
            ObservedHitObjectDivergentTime = 0;
            ObservedHitObjectAverageScore = 0;
            ObservedHitObjectPeakScore = 0;
            ObservedHitObjectPressEvidenceCount = 0;
            ObservedHitObjectShiftedTargetCount = 0;
        }

        public static void SetBenchmark(string id, string mode, int run, bool warmup, string replayHash)
        {
            BenchmarkId = id;
            BenchmarkMode = mode;
            BenchmarkRun = run;
            BenchmarkWarmup = warmup;
            BenchmarkReplayHash = replayHash;
            BenchmarkGameplayActive = false;
        }

        public static void SetBenchmarkGameplayActive(bool active) => BenchmarkGameplayActive = active;

        public static void ClearBenchmark()
        {
            BenchmarkId = string.Empty;
            BenchmarkMode = string.Empty;
            BenchmarkRun = 0;
            BenchmarkWarmup = false;
            BenchmarkReplayHash = string.Empty;
            BenchmarkGameplayActive = false;
        }

        public static CheatLikenessReport CreateCheatLikenessReport()
        {
            if (ObservedHitObjectMonitoringTime <= 0 || ObservedHitObjectShiftedTargetCount <= 0)
            {
                return new CheatLikenessReport
                {
                    IsAvailable = false,
                    Summary = "The observed-object anti-cheat was inactive for this play. This is expected while offline or when online record sending is disabled."
                };
            }

            double suspiciousRatio = ObservedHitObjectSuspiciousTime / ObservedHitObjectMonitoringTime;
            double divergentRatio = ObservedHitObjectDivergentTime / ObservedHitObjectMonitoringTime;
            double pressRatio = ObservedHitObjectShiftedTargetCount > 0
                ? (double)ObservedHitObjectPressEvidenceCount / ObservedHitObjectShiftedTargetCount
                : 0;

            double percent =
                Math.Clamp(ObservedHitObjectPeakScore / 4.0, 0, 1) * 38 +
                Math.Clamp(ObservedHitObjectAverageScore / 2.25, 0, 1) * 18 +
                Math.Clamp(suspiciousRatio / 0.10, 0, 1) * 18 +
                Math.Clamp(divergentRatio / 0.03, 0, 1) * 18 +
                Math.Clamp(pressRatio / 0.35, 0, 1) * 8;

            double confidence =
                Math.Clamp(ObservedHitObjectMonitoringTime / 12.0, 0, 1) * 0.7 +
                Math.Clamp(ObservedHitObjectShiftedTargetCount / 10.0, 0, 1) * 0.3;

            return new CheatLikenessReport
            {
                IsAvailable = true,
                Percent = Math.Clamp(percent, 0, 100),
                RiskLabel = getRiskLabel(percent),
                ConfidenceLabel = getConfidenceLabel(confidence),
                ConfidencePercent = confidence * 100,
                Summary = getSummary(percent),
                MonitoringTime = ObservedHitObjectMonitoringTime,
                SuspiciousTime = ObservedHitObjectSuspiciousTime,
                DivergentTime = ObservedHitObjectDivergentTime,
                AverageScore = ObservedHitObjectAverageScore,
                PeakScore = ObservedHitObjectPeakScore,
                PressEvidenceCount = ObservedHitObjectPressEvidenceCount,
                ShiftedTargetCount = ObservedHitObjectShiftedTargetCount,
                SuspiciousRatio = suspiciousRatio,
                DivergentRatio = divergentRatio,
                LastRuntimeState = ObservedHitObjectState,
            };
        }

        private static string getRiskLabel(double percent)
        {
            if (percent >= 85)
                return "Very high suspicion";

            if (percent >= 65)
                return "High suspicion";

            if (percent >= 40)
                return "Moderate suspicion";

            if (percent >= 20)
                return "Low suspicion";

            return "Mostly clean";
        }

        private static string getConfidenceLabel(double confidence)
        {
            if (confidence >= 0.75)
                return "High confidence";

            if (confidence >= 0.4)
                return "Medium confidence";

            return "Low confidence";
        }

        private static string getSummary(double percent)
        {
            if (percent >= 75)
                return "Input repeatedly aligned with shifted bait targets that a normal player should not prefer over the real hit objects.";

            if (percent >= 45)
                return "Several gameplay windows stayed closer to bait targets than to the real targets, which is suspicious but not yet absolute proof.";

            if (percent >= 20)
                return "A few bait-follow traces appeared, but the signal is still mixed and needs more samples.";

            return "Input stayed closer to the real hit objects than to the shifted bait targets for most of the monitored play.";
        }

        public sealed class CheatLikenessReport
        {
            public bool IsAvailable { get; init; }

            public double Percent { get; init; }

            public string RiskLabel { get; init; } = string.Empty;

            public string ConfidenceLabel { get; init; } = string.Empty;

            public double ConfidencePercent { get; init; }

            public string Summary { get; init; } = string.Empty;

            public double MonitoringTime { get; init; }

            public double SuspiciousTime { get; init; }

            public double DivergentTime { get; init; }

            public double AverageScore { get; init; }

            public double PeakScore { get; init; }

            public int PressEvidenceCount { get; init; }

            public int ShiftedTargetCount { get; init; }

            public double SuspiciousRatio { get; init; }

            public double DivergentRatio { get; init; }

            public string LastRuntimeState { get; init; } = string.Empty;
        }
    }
}
