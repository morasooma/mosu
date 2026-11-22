using System;

namespace osu.Game.Screens.Play.Break
{
    /// <summary>
    /// Stable's section display timing (local stable decompile: Player section scheduling).
    /// Times are in the original beatmap break, not BreakTracker's shortened active period.
    /// </summary>
    public static class LegacyBreakPresentation
    {
        /// <summary>Scale for skin texture pixels, after the skin loader handles @2x.</summary>
        public static float GetTextureScale(float screenHeight) => screenHeight / 768;

        public static double? GetStartTime(double start, double end)
        {
            double duration = end - start;
            return duration > 2880 ? Math.Min(start + (int)(duration / 2), end - 2880) : null;
        }

        public static float GetAlpha(double elapsed, bool passed)
        {
            if (elapsed < (passed ? 20 : 130) || elapsed >= 1480)
                return 0;

            if (passed && elapsed >= 100 && elapsed < 160)
                return 0;

            if (elapsed >= 230 && elapsed < 280)
                return 0;

            return elapsed < 1280 ? 1 : (float)((1480 - elapsed) / 200);
        }
    }
}
