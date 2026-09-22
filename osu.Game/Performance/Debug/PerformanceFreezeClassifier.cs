// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Performance.Debug
{
    public enum PerformanceFreezeCause
    {
        Unknown,
        GarbageCollection,
        GpuPipeline,
        TextureUpload,
        Presentation,
        UiChurn,
        Input,
        DrawWork,
        UpdateWork,
    }

    public static class PerformanceFreezeClassifier
    {
        public const double STUTTER_THRESHOLD_MS = 20;

        public static PerformanceFreezeCause Classify(PerformanceDebugSnapshot sample)
        {
            double frameMs = sample.WorstFrameMs;
            double gcMs = System.Math.Max(sample.DrawGcMs, System.Math.Max(sample.UpdateGcMs, sample.InputGcMs));

            if (gcMs >= 10 || (gcMs >= 2 && gcMs >= frameMs * 0.4))
                return PerformanceFreezeCause.GarbageCollection;

            bool inputIsWorst = sample.InputFrameMs >= STUTTER_THRESHOLD_MS && sample.InputFrameMs >= sample.DrawFrameMs && sample.InputFrameMs >= sample.UpdateFrameMs;
            bool drawIsWorst = sample.DrawFrameMs >= STUTTER_THRESHOLD_MS && sample.DrawFrameMs >= sample.UpdateFrameMs && sample.DrawFrameMs >= sample.InputFrameMs;
            bool updateIsWorst = sample.UpdateFrameMs >= STUTTER_THRESHOLD_MS && sample.UpdateFrameMs >= sample.DrawFrameMs && sample.UpdateFrameMs >= sample.InputFrameMs;

            if (inputIsWorst)
                return PerformanceFreezeCause.Input;

            if (drawIsWorst)
            {
                if (sample.DrawPipelineCreates > 0)
                    return PerformanceFreezeCause.GpuPipeline;

                if (sample.DrawTextureUploadFlushes > 0 || sample.DrawTextureUploads > 5)
                    return PerformanceFreezeCause.TextureUpload;

                if (sample.DrawSwapBuffersUs >= 15000)
                    return PerformanceFreezeCause.Presentation;

                return PerformanceFreezeCause.DrawWork;
            }

            if (updateIsWorst)
            {
                if (sample.UpdateInvalidations > 1000 || sample.UpdateCcl > 1500)
                    return PerformanceFreezeCause.UiChurn;

                return PerformanceFreezeCause.UpdateWork;
            }

            return PerformanceFreezeCause.Unknown;
        }
    }
}