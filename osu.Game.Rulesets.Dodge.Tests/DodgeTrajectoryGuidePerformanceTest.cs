// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeTrajectoryGuidePerformanceTest
    {
        [Test]
        public void TestLinearFullPathFrameUpdatesDoNotAllocateOrUseBufferedPath()
        {
            const int update_count = 100_000;
            var guide = new DodgeTrajectoryGuide();

            for (int i = 0; i < 1000; i++)
                updateGuide(guide, i, 1000);

            var stopwatch = Stopwatch.StartNew();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < update_count; i++)
                updateGuide(guide, i, update_count);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            stopwatch.Stop();
            TestContext.Progress.WriteLine($"Linear FullPath: {update_count:N0} updates in {stopwatch.Elapsed.TotalMilliseconds:N2} ms, {allocated:N0} allocated bytes.");

            Assert.Multiple(() =>
            {
                Assert.That(guide.UsesBufferedPath, Is.False);
                Assert.That(guide.BufferedGeometryRebuildCount, Is.Zero);
                Assert.That(allocated, Is.LessThanOrEqualTo(1024), $"Linear FullPath allocated {allocated:N0} bytes for {update_count:N0} frame updates.");
            });
        }

        [Test]
        public void TestWaveGuideUsesUnbufferedPathWithoutFrameAllocations()
        {
            const int update_count = 100_000;
            var guide = new DodgeTrajectoryGuide();
            var start = new Vector2(64, 192);
            var end = new Vector2(448, 192);

            guide.SetGeometry(start, end, 0, 3, DodgeMovementType.Sine, 48, 2, 30);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < update_count; i++)
            {
                float minimumProgress = (float)i / update_count * 2;
                guide.SetGeometry(start, end, minimumProgress, 3, DodgeMovementType.Sine, 48, 2, 30);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            TestContext.Progress.WriteLine($"Wave FullPath: {update_count:N0} updates, {allocated:N0} allocated bytes.");

            Assert.Multiple(() =>
            {
                Assert.That(guide.UsesBufferedPath, Is.False);
                Assert.That(guide.UsesUnbufferedPath, Is.True);
                Assert.That(guide.BufferedGeometryRebuildCount, Is.EqualTo(update_count + 1));
                Assert.That(allocated, Is.LessThanOrEqualTo(1024), $"Wave FullPath allocated {allocated:N0} bytes for {update_count:N0} frame updates.");
                Assert.That(guide.EndPosition, Is.EqualTo(DodgeTrajectory.PositionAtProgress(start, end, 3, DodgeMovementType.Sine, 48, 2, 30)));
            });

            guide.SetGeometry(start, end, 1, 2, DodgeMovementType.Linear, 48, 2, 30);

            Assert.Multiple(() =>
            {
                Assert.That(guide.UsesBufferedPath, Is.False);
                Assert.That(guide.UsesUnbufferedPath, Is.False);
            });
        }

        private static void updateGuide(DodgeTrajectoryGuide guide, int frame, int frameCount)
        {
            float progress = (float)frame / frameCount * 2;
            guide.SetGeometry(
                new Vector2(64, 192),
                new Vector2(448, 192),
                progress,
                3,
                DodgeMovementType.Linear,
                DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
                DodgeHitObject.DEFAULT_WAVE_CYCLES,
                0);
        }
    }
}
