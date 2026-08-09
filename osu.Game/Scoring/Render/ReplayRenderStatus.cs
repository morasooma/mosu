// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using Newtonsoft.Json;

namespace osu.Game.Scoring.Render
{
    internal sealed class ReplayRenderStatus
    {
        public ReplayRenderOperationState State { get; init; }

        public int CurrentFrame { get; init; }

        public int TotalFrames { get; init; }

        public int OutputFps { get; init; }

        public string? OutputFileName { get; init; }

        public string? ErrorMessage { get; init; }

        public double EffectiveRenderFps { get; init; }

        public double RealtimeFactor { get; init; }

        public float DownloadProgress { get; init; }

        [JsonIgnore]
        public float Progress => TotalFrames > 0
            ? Math.Clamp((float)CurrentFrame / TotalFrames, 0f, 1f)
            : 0f;

        public static bool TryReadFromFile(string path, out ReplayRenderStatus? status)
        {
            status = null;

            try
            {
                if (!File.Exists(path))
                    return false;

                status = JsonConvert.DeserializeObject<ReplayRenderStatus>(File.ReadAllText(path));
                return status != null;
            }
            catch
            {
                return false;
            }
        }
    }

    internal enum ReplayRenderOperationState
    {
        Queued,
        Active,
        Completed,
        Failed,
        Cancelled,
        DownloadingFFmpeg,
    }
}
