using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Game.Performance.Diagnostics;

namespace osu.Game.Tests.Performance
{
    [TestFixture]
    public class MosuDiagnosticsRecoveryTest
    {
        [TestCase(MosuDiagnosticsPhase.PendingRestart, false)]
        [TestCase(MosuDiagnosticsPhase.RunningBenchmark, true)]
        public void TestInterruptionSkipsRemainingProfilesAndPreservesResults(MosuDiagnosticsPhase phase, bool started)
        {
            var session = createSession(phase);
            var previous = new MosuDiagnosticsRendererResult { Completed = true };
            session.Results.Add(previous);

            Assert.That(MosuDiagnosticsRecovery.PauseInterruptedSession(session), Is.True);
            Assert.That(session.Results[0], Is.SameAs(previous));
            Assert.That(session.Results, Has.Count.EqualTo(3));
            Assert.That(session.Results[1].FailureReason, Is.EqualTo("benchmark_interrupted"));
            Assert.That(session.Results[1].Started, Is.EqualTo(started));
            Assert.That(session.Results[2].FailureReason, Is.EqualTo("renderer_skipped_after_interruption"));
            Assert.That(session.Results[2].Started, Is.False);
            Assert.That(session.Results[1].Quality, Is.EqualTo(MosuDiagnosticsQuality.Failed));
            Assert.That(session.Phase, Is.EqualTo(MosuDiagnosticsPhase.Paused));
            Assert.That(session.ExpectedRenderer, Is.EqualTo(RendererType.OpenGL));
            Assert.That(session.ExpectedProfile, Is.EqualTo(MosuDiagnosticsProfile.Current));
            Assert.That(session.ProfileIndex, Is.Zero);
            Assert.That(MosuDiagnosticsRecovery.PauseInterruptedSession(session), Is.False);
            Assert.That(session.Results, Has.Count.EqualTo(3));
        }

        [Test]
        public void TestLastRendererCanProceedToResultsWithoutRetry()
        {
            var session = createSession(MosuDiagnosticsPhase.RunningBenchmark);
            session.Renderers.RemoveAt(1);
            MosuDiagnosticsRecovery.PauseInterruptedSession(session);
            Assert.That(session.ExpectedRenderer, Is.Null);
            Assert.That(session.ExpectedProfile, Is.Null);
            Assert.That(MosuDiagnosticsRecovery.GetRestoreRenderer(session), Is.EqualTo(RendererType.OpenGL));
        }

        [Test]
        public void TestHealthyOriginalRendererIsRestored()
        {
            var session = createSession(MosuDiagnosticsPhase.RunningBenchmark);
            session.OriginalRenderer = RendererType.Deferred_Direct3D11;
            MosuDiagnosticsRecovery.PauseInterruptedSession(session);
            Assert.That(MosuDiagnosticsRecovery.GetRestoreRenderer(session), Is.EqualTo(RendererType.Deferred_Direct3D11));
        }

        [TestCase(MosuDiagnosticsPhase.PendingResultsRestart)]
        [TestCase(MosuDiagnosticsPhase.ShowResults)]
        [TestCase(MosuDiagnosticsPhase.Paused)]
        public void TestNonRunningSessionsAreUnchanged(MosuDiagnosticsPhase phase)
        {
            var session = createSession(phase);
            Assert.That(MosuDiagnosticsRecovery.PauseInterruptedSession(session), Is.False);
            Assert.That(session.Phase, Is.EqualTo(phase));
            Assert.That(session.Results, Is.Empty);
        }

        private static MosuDiagnosticsSession createSession(MosuDiagnosticsPhase phase) => new MosuDiagnosticsSession
        {
            Phase = phase,
            OriginalRenderer = RendererType.Mora_Vulkan,
            ExpectedRenderer = RendererType.Mora_Vulkan,
            ExpectedProfile = MosuDiagnosticsProfile.Recommended,
            Renderers = { RendererType.Mora_Vulkan, RendererType.OpenGL },
            Profiles = { MosuDiagnosticsProfile.Current, MosuDiagnosticsProfile.Recommended, MosuDiagnosticsProfile.CleanBaseline },
            ProfileIndex = 1,
        };
    }
}
