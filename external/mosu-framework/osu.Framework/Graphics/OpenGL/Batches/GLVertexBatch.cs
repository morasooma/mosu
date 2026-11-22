// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.OpenGL.Buffers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Statistics;

namespace osu.Framework.Graphics.OpenGL.Batches
{
    internal abstract class GLVertexBatch<T> : IVertexBatch<T>
        where T : unmanaged, IEquatable<T>, IVertex
    {
        public List<GLVertexBuffer<T>> VertexBuffers = new List<GLVertexBuffer<T>>();

        /// <summary>
        /// The number of vertices in each VertexBuffer.
        /// </summary>
        public int Size { get; }

        private int changeBeginIndex = -1;
        private int changeEndIndex = -1;

        private int currentBufferIndex;
        private int currentVertexIndex;
        private int currentDrawIndex;

        private readonly GLRenderer renderer;
        private readonly int maxBuffers;
        private readonly bool appendAfterFlush;
        protected readonly bool StreamWholeDraw;

        private GLVertexBuffer<T> currentVertexBuffer => VertexBuffers[currentBufferIndex];

        protected GLVertexBatch(GLRenderer renderer, int bufferSize, int maxBuffers, bool appendAfterFlush = false, bool streamWholeDraw = false)
        {
            Size = bufferSize;
            this.renderer = renderer;
            this.maxBuffers = maxBuffers;
            this.appendAfterFlush = appendAfterFlush && !streamWholeDraw;
            StreamWholeDraw = streamWholeDraw;

            AddAction = Add;
        }

        #region Disposal

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (GLVertexBuffer<T> vbo in VertexBuffers)
                    vbo.Dispose();
            }
        }

        #endregion

        void IVertexBatch.ResetCounters()
        {
            changeBeginIndex = -1;
            changeEndIndex = -1;
            currentBufferIndex = 0;
            currentVertexIndex = 0;
            currentDrawIndex = 0;
        }

        protected abstract GLVertexBuffer<T> CreateVertexBuffer(GLRenderer renderer);

        /// <summary>
        /// Adds a vertex to this <see cref="GLVertexBatch{T}"/>.
        /// </summary>
        /// <param name="v">The vertex to add.</param>
        public void Add(T v)
        {
            renderer.SetActiveBatch(this);

            if (currentBufferIndex < VertexBuffers.Count && currentVertexIndex >= currentVertexBuffer.Size)
            {
                Draw();
                FrameStatistics.Increment(StatisticsCounterType.VBufOverflow);
            }

            // currentIndex will change after Draw() above, so this cannot be in an else-condition
            while (currentBufferIndex >= VertexBuffers.Count)
                VertexBuffers.Add(CreateVertexBuffer(renderer));

            if (currentVertexBuffer.SetVertex(currentVertexIndex, v))
            {
                if (changeBeginIndex == -1)
                    changeBeginIndex = currentVertexIndex;

                changeEndIndex = currentVertexIndex + 1;
            }

            ++currentVertexIndex;
        }

        /// <summary>
        /// Adds a vertex to this <see cref="GLVertexBatch{T}"/>.
        /// This is a cached delegate of <see cref="Add"/> that should be used in memory-critical locations such as <see cref="DrawNode"/>s.
        /// </summary>
        public Action<T> AddAction { get; private set; }

        public int Draw()
        {
            int countToDraw = currentVertexIndex - currentDrawIndex;

            if (countToDraw == 0)
                return 0;

            GLVertexBuffer<T> vertexBuffer = currentVertexBuffer;
            int drawBeginIndex = currentDrawIndex;
            int drawEndIndex = currentVertexIndex;

            // Drawing can bind a uniform buffer, which flushes this batch again before
            // the binding is recorded. Consume the pending range before calling into
            // the renderer so that the reentrant flush is empty, as it was before
            // append-after-flush was introduced.
            currentDrawIndex = drawEndIndex;

            if (StreamWholeDraw)
            {
                // A streaming flush always starts at zero. The GPU store can cover less
                // than the CPU mirror, so an unchanged suffix can still need uploading.
                vertexBuffer.UploadStreamedDraw(drawEndIndex, changeBeginIndex);
            }
            else if (GLRenderer.AppendVertexStreamingEnabled && !renderer.IsEmbedded)
                vertexBuffer.UpdateForDraw(drawEndIndex, changeBeginIndex);
            else if (changeBeginIndex >= 0)
                vertexBuffer.UpdateRange(changeBeginIndex, changeEndIndex);

            vertexBuffer.DrawRange(drawBeginIndex, drawEndIndex);

            // Quad batches can append to unused capacity: their flushes and buffer size
            // are aligned to independent four-vertex primitives. This avoids consuming a
            // whole buffer for a small flush without overwriting submitted vertices until
            // the ring wraps (or the next frame resets it).
            // Linear batches retain the original behaviour: arbitrary sizes and strip
            // topologies cannot safely split their next primitive at a shortened boundary.
            if (!appendAfterFlush || currentVertexIndex == vertexBuffer.Size || currentVertexIndex % IRenderer.VERTICES_PER_QUAD != 0)
            {
                currentBufferIndex = (currentBufferIndex + 1) % maxBuffers;
                currentVertexIndex = 0;
                currentDrawIndex = 0;
            }

            changeBeginIndex = -1;
            changeEndIndex = -1;

            return countToDraw;
        }
    }
}
