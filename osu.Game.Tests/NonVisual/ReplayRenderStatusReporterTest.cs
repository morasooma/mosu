// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayRenderStatusReporterTest
    {
        [Test]
        public void TestMissingStatusFile()
        {
            Assert.That(ReplayRenderStatus.TryReadFromFile(Path.GetTempFileName() + ".missing", out ReplayRenderStatus? status), Is.False);
            Assert.That(status, Is.Null);
        }

        [Test]
        public void TestQueuedProgressAndCompletionRoundTrip()
        {
            string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);

            try
            {
                string statusPath = Path.Combine(directory, "render-status.json");
                var reporter = new ReplayRenderStatusReporter(statusPath, 60);

                reporter.ReportQueued("video.mp4");
                Assert.That(ReplayRenderStatus.TryReadFromFile(statusPath, out ReplayRenderStatus? queued), Is.True);
                Assert.That(queued, Is.Not.Null);
                Assert.That(queued!.State, Is.EqualTo(ReplayRenderOperationState.Queued));
                Assert.That(queued.OutputFileName, Is.EqualTo("video.mp4"));

                reporter.ReportProgress(25, 100, "video.mp4");
                Assert.That(ReplayRenderStatus.TryReadFromFile(statusPath, out ReplayRenderStatus? active), Is.True);
                Assert.That(active, Is.Not.Null);
                Assert.That(active!.State, Is.EqualTo(ReplayRenderOperationState.Active));
                Assert.That(active.Progress, Is.EqualTo(0.25f).Within(0.001f));

                reporter.ReportCompleted("video.mp4", 100);
                Assert.That(ReplayRenderStatus.TryReadFromFile(statusPath, out ReplayRenderStatus? completed), Is.True);
                Assert.That(completed, Is.Not.Null);
                Assert.That(completed!.State, Is.EqualTo(ReplayRenderOperationState.Completed));
                Assert.That(completed.Progress, Is.EqualTo(1f));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
