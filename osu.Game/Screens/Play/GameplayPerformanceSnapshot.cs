// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        public static void Reset()
        {
            Combo = 0;
            MaxCombo = 0;
            MissCount = 0;
            LastJudgement = string.Empty;
            LastJudgementTime = 0;
            Health = 0;
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

    }
}
