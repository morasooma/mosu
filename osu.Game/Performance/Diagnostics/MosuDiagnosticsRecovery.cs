using System.Linq;
using osu.Framework.Configuration;

namespace osu.Game.Performance.Diagnostics
{
    public static class MosuDiagnosticsRecovery
    {
        public static RendererType GetRestoreRenderer(MosuDiagnosticsSession session)
            => session.Results.Any(result => result.Renderer == session.OriginalRenderer && result.FailureReason == "benchmark_interrupted")
                ? RendererType.OpenGL
                : session.OriginalRenderer;

        // A process that died while starting or measuring a renderer must not be retried
        // implicitly. Keep prior measurements and quarantine its remaining profiles.
        public static bool PauseInterruptedSession(MosuDiagnosticsSession session)
        {
            if (session.Phase is not (MosuDiagnosticsPhase.PendingRestart or MosuDiagnosticsPhase.RunningBenchmark))
                return false;

            bool started = session.Phase == MosuDiagnosticsPhase.RunningBenchmark;
            if (session.ExpectedRenderer is RendererType renderer)
            {
                for (int index = session.ProfileIndex; index < session.Profiles.Count; index++)
                {
                    string reason = index == session.ProfileIndex ? "benchmark_interrupted" : "renderer_skipped_after_interruption";
                    session.Results.Add(new MosuDiagnosticsRendererResult
                    {
                        Renderer = renderer,
                        Profile = session.Profiles[index],
                        Started = started && index == session.ProfileIndex,
                        Completed = false,
                        FailureReason = reason,
                        Quality = MosuDiagnosticsQuality.Failed,
                        QualityReason = reason,
                    });
                }
            }

            session.RendererIndex++;
            session.ProfileIndex = 0;
            session.ExpectedRenderer = session.RendererIndex < session.Renderers.Count ? session.Renderers[session.RendererIndex] : null;
            session.ExpectedProfile = session.ExpectedRenderer != null && session.Profiles.Count > 0 ? session.Profiles[0] : null;
            session.Phase = MosuDiagnosticsPhase.Paused;
            return true;
        }
    }
}
