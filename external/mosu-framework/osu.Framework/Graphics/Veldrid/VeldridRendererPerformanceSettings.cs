// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;

namespace osu.Framework.Graphics.Veldrid
{
    /// <summary>
    /// Runtime switches for experimental Veldrid renderer optimisations.
    /// </summary>
    public static class VeldridRendererPerformanceSettings
    {
        private static int cachePipelineLookups;

        /// <summary>
        /// Whether the pipeline resolved for an unchanged graphics-pipeline description should be
        /// reused without hashing and probing the full pipeline cache again on every draw call.
        /// </summary>
        public static bool CachePipelineLookups
        {
            get => Volatile.Read(ref cachePipelineLookups) != 0;
            set => Volatile.Write(ref cachePipelineLookups, value ? 1 : 0);
        }
    }
}
