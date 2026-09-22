// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Configuration;

namespace osu.Game.Performance.Diagnostics
{
    public interface IPerformanceDiagnosticsManager
    {
        bool IsRunning { get; }

        MosuDiagnosticsSession? CurrentSession { get; }

        void ShowSetup();

        void BeginSession(MosuDiagnosticsSetupOptions options);

        void ShowResultsIfPending();

        void ResumeSession();

        void CancelSession();

        void ApplyRecommendedSettings(RendererType renderer);

        void OpenExportFolder();

        /// <summary>
        /// Captures a process dump containing managed heap and native process state, together with
        /// a lightweight JSON sidecar describing the client at capture time.
        /// </summary>
        Task<string> CaptureMemoryDumpAsync(CancellationToken cancellationToken = default);

        void OpenMemoryDumpFolder();

        void DismissResults();
    }
}
