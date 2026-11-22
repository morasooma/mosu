using System;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Stable song-select measurements, converted from its 480-high coordinate space
    /// to the 768-high legacy presentation used by this screen.
    /// </summary>
    internal static class StableCarouselLayout
    {
        public const float Scale = 1.6f;
        public const float RowStep = 48 * Scale;
        public const float BoundaryGap = 10 * Scale;
        public const float ExpandedOffset = 50 * Scale;
        public const float HoverOffset = 45 * Scale;
        public const double ColourDuration = 300;

        // Stable uses GDI Font's default Point unit and normalises DPI to 96.
        // OsuFont uses pixel-sized glyphs, so literal GDI sizes are not interchangeable.
        public static float FontPixels(float points) => points * 96 / 72;

        public static readonly Color4 Inactive = new Color4(235, 73, 153, 240);
        public static readonly Color4 Unplayed = new Color4(233, 104, 0, 240);
        public static readonly Color4 Expanded = new Color4(0, 150, 236, 240);
        public static readonly Color4 Selected = new Color4(255, 255, 255, 220);
        public static readonly Color4 Group = new Color4(35, 50, 143, 255);
        public static readonly Color4 ExpandedGroup = new Color4(163, 240, 44, 255);

        public static float CurveOffset(float panelCentreY, float viewportHeight)
            => viewportHeight <= 0 ? 0 : Math.Min(200, Math.Abs(panelCentreY / viewportHeight - 0.5f) * 75) * Scale;

        public static float Spacing(float topHeight, float bottomHeight, bool boundary)
            => RowStep - (topHeight + bottomHeight) / 2 + (boundary ? BoundaryGap : 0);

        // Stable's update ratio is elapsed time / (1000 / 60), not elapsed milliseconds.
        public static double Approach(double current, double target, double decay, double elapsedMilliseconds)
            => target - (target - current) * Math.Pow(decay, Math.Max(0, elapsedMilliseconds) / (1000.0 / 60));
    }
}
