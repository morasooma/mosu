// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Newtonsoft.Json;

namespace osu.Game.Scoring.Render
{
    internal sealed class ReplayRenderStatusReporter
    {
        private readonly string path;
        private readonly string tempPath;
        private readonly int outputFps;

        private DateTime lastProgressWrite = DateTime.MinValue;
        private DateTime? renderStartedAt;

        public ReplayRenderStatusReporter(string path, int outputFps)
        {
            this.path = path;
            tempPath = path + ".tmp";
            this.outputFps = outputFps;

            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        public void ReportQueued(string outputFileName) => writeStatus(new ReplayRenderStatus
        {
            State = ReplayRenderOperationState.Queued,
            OutputFileName = outputFileName,
        });

        public void ReportProgress(int currentFrame, int totalFrames, string outputFileName)
        {
            DateTime now = DateTime.UtcNow;
            renderStartedAt ??= now;

            if (currentFrame != 0 && currentFrame != totalFrames && now - lastProgressWrite < TimeSpan.FromMilliseconds(100))
                return;

            lastProgressWrite = now;

            double effectiveRenderFps = 0;
            double realtimeFactor = 0;

            if (renderStartedAt is DateTime startedAt)
            {
                double elapsedSeconds = Math.Max(0.001, (now - startedAt).TotalSeconds);
                effectiveRenderFps = currentFrame / elapsedSeconds;
                realtimeFactor = outputFps > 0 ? effectiveRenderFps / outputFps : 0;
            }

            writeStatus(new ReplayRenderStatus
            {
                State = ReplayRenderOperationState.Active,
                CurrentFrame = currentFrame,
                TotalFrames = totalFrames,
                OutputFps = outputFps,
                OutputFileName = outputFileName,
                EffectiveRenderFps = effectiveRenderFps,
                RealtimeFactor = realtimeFactor,
            });
        }

        public void ReportCompleted(string outputFileName, int totalFrames) => writeStatus(new ReplayRenderStatus
        {
            State = ReplayRenderOperationState.Completed,
            CurrentFrame = totalFrames,
            TotalFrames = totalFrames,
            OutputFps = outputFps,
            OutputFileName = outputFileName,
        });

        public void ReportFailed(string message) => writeStatus(new ReplayRenderStatus
        {
            State = ReplayRenderOperationState.Failed,
            ErrorMessage = message,
        });

        public void ReportCancelled(string? outputFileName = null, int currentFrame = 0, int totalFrames = 0) => writeStatus(new ReplayRenderStatus
        {
            State = ReplayRenderOperationState.Cancelled,
            CurrentFrame = currentFrame,
            TotalFrames = totalFrames,
            OutputFps = outputFps,
            OutputFileName = outputFileName,
        });

        public void ReportDownloadingFFmpeg(int percent, long downloadedMB, long totalMB) => writeStatus(new ReplayRenderStatus
        {
            State = ReplayRenderOperationState.DownloadingFFmpeg,
            DownloadProgress = percent < 0 ? 1f : totalMB > 0 ? Math.Clamp(percent / 100f, 0f, 1f) : 0f,
            OutputFileName = percent < 0
                ? "Extracting FFmpeg..."
                : totalMB > 0
                    ? $"Downloading FFmpeg... {percent}% ({downloadedMB} MB / {totalMB} MB)"
                    : $"Downloading FFmpeg... {downloadedMB} MB",
        });

        private void writeStatus(ReplayRenderStatus status)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.WriteAllText(tempPath, JsonConvert.SerializeObject(status));
                    File.Move(tempPath, path, overwrite: true);
                    return;
                }
                catch (IOException) when (i < 4)
                {
                    System.Threading.Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (i < 4)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }
    }
}
