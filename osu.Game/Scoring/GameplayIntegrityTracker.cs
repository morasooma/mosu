// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using osu.Game.Beatmaps;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Session-local bridge between gameplay components and the final privacy-preserving report.
    /// </summary>
    public class GameplayIntegrityTracker
    {
        public Func<GameplayClockIntegrityReport>? ClockReportProvider { get; set; }

        public Func<GameplayAssistanceIntegrityReport>? AssistanceReportProvider { get; set; }

        public Func<bool>? VisualOD11Provider { get; set; }

        private GameplayDifficultyIntegrityReport difficulty = new GameplayDifficultyIntegrityReport();

        public void SetDifficulty(BeatmapDifficulty original, BeatmapDifficulty applied, bool customApproachRateEnabled)
        {
            difficulty = new GameplayDifficultyIntegrityReport
            {
                OriginalApproachRate = original.ApproachRate,
                OriginalOverallDifficulty = original.OverallDifficulty,
                OriginalCircleSize = original.CircleSize,
                OriginalDrainRate = original.DrainRate,
                AppliedApproachRate = applied.ApproachRate,
                AppliedOverallDifficulty = applied.OverallDifficulty,
                AppliedCircleSize = applied.CircleSize,
                AppliedDrainRate = applied.DrainRate,
                CustomApproachRateEnabled = customApproachRateEnabled,
            };
        }

        public GameplayClockIntegrityReport CreateClockReport() => ClockReportProvider?.Invoke() ?? new GameplayClockIntegrityReport();

        public GameplayAssistanceIntegrityReport CreateAssistanceReport() => AssistanceReportProvider?.Invoke() ?? new GameplayAssistanceIntegrityReport();

        public GameplayDifficultyIntegrityReport CreateDifficultyReport() => new GameplayDifficultyIntegrityReport
        {
            OriginalApproachRate = difficulty.OriginalApproachRate,
            OriginalOverallDifficulty = difficulty.OriginalOverallDifficulty,
            OriginalCircleSize = difficulty.OriginalCircleSize,
            OriginalDrainRate = difficulty.OriginalDrainRate,
            AppliedApproachRate = difficulty.AppliedApproachRate,
            AppliedOverallDifficulty = difficulty.AppliedOverallDifficulty,
            AppliedCircleSize = difficulty.AppliedCircleSize,
            AppliedDrainRate = difficulty.AppliedDrainRate,
            CustomApproachRateEnabled = difficulty.CustomApproachRateEnabled,
            VisualOD11Enabled = VisualOD11Provider?.Invoke() == true,
        };
    }
}
