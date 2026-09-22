// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using System;
using osu.Game.Performance.Debug;

namespace osu.Game.Tests.NonVisual.Performance
{
    [TestFixture]
    public class PerformanceFreezeClassifierTest
    {
        [Test]
        public void TestGcPauseWinsWhenItExplainsFrame()
        {
            var sample = new PerformanceDebugSnapshot
            {
                UpdateFrameMs = 42,
                UpdateGcMs = 31,
                DrawFrameMs = 20,
                DrawPipelineCreates = 2,
            };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(PerformanceFreezeCause.GarbageCollection));
        }

        [TestCase(45, 0, 1, 0, 0, PerformanceFreezeCause.GpuPipeline)]
        [TestCase(45, 0, 0, 1, 0, PerformanceFreezeCause.TextureUpload)]
        [TestCase(45, 0, 0, 0, 18000, PerformanceFreezeCause.Presentation)]
        public void TestDrawCause(double drawMs, double gcMs, long pipelines, long uploadFlushes, long swapUs, PerformanceFreezeCause expected)
        {
            var sample = new PerformanceDebugSnapshot
            {
                DrawFrameMs = drawMs,
                DrawGcMs = gcMs,
                DrawPipelineCreates = pipelines,
                DrawTextureUploadFlushes = uploadFlushes,
                DrawSwapBuffersUs = swapUs,
            };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(expected));
        }

        [Test]
        public void TestUiChurn()
        {
            var sample = new PerformanceDebugSnapshot
            {
                UpdateFrameMs = 35,
                UpdateInvalidations = 1501,
            };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(PerformanceFreezeCause.UiChurn));
        }

        [Test]
        public void TestInputStall()
        {
            var sample = new PerformanceDebugSnapshot
            {
                DrawFrameMs = 22,
                UpdateFrameMs = 25,
                InputFrameMs = 40,
            };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(PerformanceFreezeCause.Input));
        }

        [Test]
        public void TestUpdateStallIgnoresUnrelatedDrawSignals()
        {
            var sample = new PerformanceDebugSnapshot
            {
                UpdateFrameMs = 100,
                DrawFrameMs = 16,
                DrawSwapBuffersUs = 16000,
                DrawTextureUploads = 6,
            };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(PerformanceFreezeCause.UpdateWork));
        }

        [Test]
        public void TestDrawStallIgnoresUnrelatedUpdateSignals()
        {
            var sample = new PerformanceDebugSnapshot
            {
                DrawFrameMs = 45,
                UpdateFrameMs = 12,
                UpdateInvalidations = 1501,
            };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(PerformanceFreezeCause.DrawWork));
        }

        [Test]
        public void TestDoesNotInventSpecificCause()
        {
            var sample = new PerformanceDebugSnapshot { UpdateFrameMs = 30 };

            Assert.That(PerformanceFreezeClassifier.Classify(sample), Is.EqualTo(PerformanceFreezeCause.UpdateWork));
        }

        [Test]
        public void TestReportContainsEvidenceAndEscapesCsv()
        {
            var freeze = new PerformanceFreezeEvent(1, DateTimeOffset.Parse("2026-09-15T12:00:00+03:00"), new PerformanceDebugSnapshot
            {
                UpdateFrameMs = 42.5,
                UpdateGcMs = 31.25,
                WorkingSetMb = 1200,
                GcHeapMb = 350,
            }, PerformanceFreezeCause.GarbageCollection);

            string report = PerformanceDebugReportFormatter.FormatCsv(new[] { freeze });

            Assert.That(report, Does.Contain("sequence,local_time,cause"));
            Assert.That(report, Does.Contain("GarbageCollection"));
            Assert.That(report, Does.Contain("42.5"));
            Assert.That(report, Does.Contain("31.25"));
            Assert.That(report, Does.Contain("1200"));
        }
    }
}