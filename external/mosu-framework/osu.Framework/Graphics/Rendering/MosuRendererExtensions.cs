// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.OpenGL;
using osu.Framework.Graphics.OpenGL.Batches;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Statistics;

namespace osu.Framework.Graphics.Rendering
{
    /// <summary>
    /// Mosu: public bridge to renderer internals needed by game-side custom-mesh draw nodes.
    /// </summary>
    /// <remarks>
    /// Game assemblies cannot reach <see cref="IRenderer.DefaultQuadBatch"/> or <see cref="FrameStatistics"/>
    /// (both internal), but creating a private vertex batch instead (the <c>CursorTrail</c> approach) forces a
    /// flush of the shared batch around every use — exactly the draw-call regression custom meshes exist to
    /// avoid. This bridge lets a draw node emit hand-built vertices into the shared quad batch, staying inside
    /// the current texture/shader/flush state.
    /// </remarks>
    public static class MosuRendererExtensions
    {
        /// <summary>
        /// Appends one quad (four vertices in cyclic order) to the renderer's shared quad batch.
        /// Callers must have bound the intended texture and shader, as for any batched draw.
        /// </summary>
        public static void AddDefaultBatchQuad(this IRenderer renderer, in TexturedVertex2D v1, in TexturedVertex2D v2, in TexturedVertex2D v3, in TexturedVertex2D v4)
        {
            var action = renderer.DefaultQuadBatch.AddAction;

            action(v1);
            action(v2);
            action(v3);
            action(v4);
        }

        /// <summary>
        /// Creates a private quad batch for geometry rebuilt each draw. Desktop GL streams to
        /// unused vertex ranges, replacing the store on wrap rather than overwriting pending draws.
        /// Other backends use their ordinary quad batch.
        /// </summary>
        public static IVertexBatch<T> CreateStreamingQuadBatch<T>(this IRenderer renderer, int size)
            where T : unmanaged, IEquatable<T>, IVertex
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(size, IRenderer.MAX_QUADS);

            return renderer is GLRenderer { IsEmbedded: false } gl
                ? new GLQuadBatch<T>(gl, size, 1, streamWholeDraw: true)
                : renderer.CreateQuadBatch<T>(size, 1);
        }

        /// <summary>
        /// Accounts custom-mesh geometry to the frame's pixel statistics, mirroring what
        /// <see cref="RendererExtensions.DrawQuad"/> does for plain quads.
        /// </summary>
        public static void RecordCustomMeshPixels(long area)
        {
            if (area <= 0)
                return;

            FrameStatistics.Add(StatisticsCounterType.Pixels, area);

            if (PixelSourceStatistics.Active)
                PixelSourceStatistics.Add(area);
        }
    }
}
