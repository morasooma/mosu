// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Graphics.Carousel
{
    /// <summary>
    /// Provides deterministic, input-independent scrolling for automated performance benchmarks.
    /// </summary>
    public interface ICarouselBenchmarkController
    {
        double BenchmarkScrollPosition { get; }

        double BenchmarkScrollableExtent { get; }

        void ScrollToBenchmarkPosition(double position);

        /// <summary>
        /// Executes the same carousel traversal queued by the song-select Right arrow binding.
        /// </summary>
        void ActivateNextSetForBenchmark();
    }
}
