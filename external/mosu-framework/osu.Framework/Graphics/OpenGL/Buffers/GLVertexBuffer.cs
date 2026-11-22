// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using osuTK.Graphics.ES30;
using osu.Framework.Statistics;
using osu.Framework.Development;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;

namespace osu.Framework.Graphics.OpenGL.Buffers
{
    internal abstract class GLVertexBuffer<T> : IVertexBuffer, IDisposable
        where T : unmanaged, IEquatable<T>, IVertex
    {
        protected static readonly int STRIDE = GLVertexUtils<T>.STRIDE;

        protected readonly GLRenderer Renderer;
        private readonly BufferUsageHint usage;
        private readonly bool streamWholeDraw;

        // GL copies this staging data during BufferData/BufferSubData. No ref-counted
        // image owner is needed: the draw thread owns it until Free, as before.
        private byte[]? vertexData;

        private bool isInitialised;
        private int vaoId;
        private int vboId;
        private int uploadedPrefix;
        private int streamOffset;
        private int nextStreamOffset;

        protected int StreamVertexCapacity => Math.Min(Size * 4, IRenderer.MAX_QUADS * IRenderer.VERTICES_PER_QUAD);

        protected GLVertexBuffer(GLRenderer renderer, int amountVertices, BufferUsageHint usage, bool streamWholeDraw = false)
        {
            Renderer = renderer;
            this.usage = usage;
            this.streamWholeDraw = streamWholeDraw;

            Size = amountVertices;
        }

        /// <summary>
        /// Sets the vertex at a specific index of this <see cref="GLVertexBuffer{T}"/>.
        /// </summary>
        /// <param name="vertexIndex">The index of the vertex.</param>
        /// <param name="vertex">The vertex.</param>
        /// <returns>Whether the vertex changed.</returns>
        public bool SetVertex(int vertexIndex, T vertex)
        {
            ref var currentVertex = ref getMemory()[vertexIndex];
            bool isNewVertex = !currentVertex.Equals(vertex);

            currentVertex = vertex;

            return isNewVertex;
        }

        /// <summary>
        /// Gets the number of vertices in this <see cref="GLVertexBuffer{T}"/>.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Initialises this <see cref="GLVertexBuffer{T}"/>. Guaranteed to be run on the draw thread.
        /// </summary>
        protected virtual void Initialise()
        {
            ThreadSafety.EnsureDrawThread();

            int size = Size * STRIDE;

            vboId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);
            if (!streamWholeDraw)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)size, ref getMemory()[0], usage);
                uploadedPrefix = Size;
            }

            GLVertexUtils<T>.SetAttributes();
        }

        ~GLVertexBuffer()
        {
            Renderer.ScheduleDisposal(v => v.Dispose(false), this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected bool IsDisposed { get; private set; }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            ((IVertexBuffer)this).Free();

            IsDisposed = true;
        }

        public void Bind(bool forRendering)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (!isInitialised)
            {
                Renderer.BindVertexArray(vaoId = GL.GenVertexArray());
                Initialise();
                isInitialised = true;
            }
            else
                Renderer.BindVertexArray(vaoId);
        }

        public virtual void Unbind()
        {
        }

        protected virtual int ToElements(int vertices) => vertices;

        protected virtual int ToElementIndex(int vertexIndex) => vertexIndex;

        protected abstract PrimitiveTopology Topology { get; }

        public void Draw()
        {
            DrawRange(0, Size);
        }

        public virtual void DrawRange(int startIndex, int endIndex)
        {
            Bind(true);
            if (!streamWholeDraw && GLRenderer.AppendVertexStreamingEnabled && !Renderer.IsEmbedded && uploadedPrefix < endIndex)
                UpdateForDraw(endIndex, -1);
            Renderer.DrawVertices(Topology, ToElementIndex(startIndex + streamOffset), ToElements(endIndex - startIndex));
        }

        public void Update()
        {
            UpdateRange(0, Size);
        }

        public virtual void UpdateRange(int startIndex, int endIndex)
        {
            using var operation = StutterTraceEventSource.Log.Measure("GL.VertexUpload", (endIndex - startIndex) * STRIDE);
            Bind(false);

            int countVertices = endIndex - startIndex;

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);

            // Streaming retains full store capacity even when only its prefix is
            // populated. A partial update must not publish unrelated CPU changes.
            if (startIndex == 0 && endIndex == Size)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Size * STRIDE), ref getMemory()[0], usage);
                uploadedPrefix = Size;
            }
            else
            {
                GL.BufferSubData(BufferTarget.ArrayBuffer, startIndex * STRIDE, (IntPtr)(countVertices * STRIDE), ref getMemory()[startIndex]);
                if (startIndex <= uploadedPrefix)
                    uploadedPrefix = Math.Max(uploadedPrefix, endIndex);
            }

            FrameStatistics.Add(StatisticsCounterType.VerticesUpl, countVertices);
        }

        internal virtual void UploadStreamedDraw(int endIndex, int changeBeginIndex)
        {
            if (uploadedPrefix >= endIndex && changeBeginIndex < 0)
                return;

            int bytes = endIndex * STRIDE;
            using var operation = StutterTraceEventSource.Log.Measure("GL.VertexStream", bytes);
            Bind(false);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);

            if (nextStreamOffset == 0 || nextStreamOffset + endIndex > StreamVertexCapacity)
            {
                // Earlier draws retain the old store. Keep its allocation size constant
                // so the driver can recycle it once those draws have completed.
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(StreamVertexCapacity * STRIDE), IntPtr.Zero, usage);
                nextStreamOffset = 0;
            }

            streamOffset = nextStreamOffset;
            GL.BufferSubData(BufferTarget.ArrayBuffer, streamOffset * STRIDE, (IntPtr)bytes, ref getMemory()[0]);
            nextStreamOffset += endIndex;
            uploadedPrefix = endIndex;
            FrameStatistics.Add(StatisticsCounterType.VerticesUpl, endIndex);
        }

        internal virtual unsafe void UpdateForDraw(int endIndex, int changeBeginIndex)
        {
            if (changeBeginIndex < 0 && isInitialised && uploadedPrefix >= endIndex)
                return;

            Bind(false);
            if (changeBeginIndex < 0 && uploadedPrefix >= endIndex)
                return;

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboId);
            if (changeBeginIndex >= 0 && changeBeginIndex < uploadedPrefix)
            {
                // Earlier draws can still use the old store. Start a new store
                // before any unsynchronised write overlaps its populated prefix.
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Size * STRIDE), IntPtr.Zero, usage);
                uploadedPrefix = 0;
            }

            int start = uploadedPrefix;
            int count = endIndex - start;
            // Value describes the requested mapped range, not fallback upload bytes.
            using var operation = StutterTraceEventSource.Log.Measure("GL.VertexMap", count * STRIDE);
            IntPtr mapped = GL.MapBufferRange(BufferTarget.ArrayBuffer, (IntPtr)(start * STRIDE), (IntPtr)(count * STRIDE),
                BufferAccessMask.MapWriteBit | BufferAccessMask.MapUnsynchronizedBit);
            if (mapped == IntPtr.Zero)
            {
                // Mapping is optional. Replacing the complete CPU mirror retains
                // correctness without trusting a failed or unsupported map.
                restoreCompleteStore();
                return;
            }

            bool intact;
            try
            {
                MemoryMarshal.AsBytes(getMemory().Slice(start, count)).CopyTo(new Span<byte>((void*)mapped, count * STRIDE));
            }
            finally
            {
                intact = GL.UnmapBuffer(BufferTarget.ArrayBuffer);
            }

            if (!intact)
            {
                restoreCompleteStore();
                return;
            }

            uploadedPrefix = endIndex;
            FrameStatistics.Add(StatisticsCounterType.VerticesUpl, count);
        }

        private void restoreCompleteStore()
        {
            using var operation = StutterTraceEventSource.Log.Measure("GL.VertexUpload", Size * STRIDE);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(Size * STRIDE), ref getMemory()[0], usage);
            uploadedPrefix = Size;
            FrameStatistics.Add(StatisticsCounterType.VerticesUpl, Size);
        }

        private Span<T> getMemory()
        {
            ThreadSafety.EnsureDrawThread();

            int length = checked(Size * Unsafe.SizeOf<T>());
            if (!InUse)
            {
                vertexData = ArrayPool<byte>.Shared.Rent(length);
                // Stale pooled vertices must not suppress the first upload via Equals.
                vertexData.AsSpan(0, length).Clear();
                Renderer.RegisterVertexBufferUse(this);
            }

            LastUseFrameIndex = Renderer.FrameIndex;

            return MemoryMarshal.Cast<byte, T>(vertexData.AsSpan(0, length));
        }

        public ulong LastUseFrameIndex { get; private set; }

        public bool InUse => LastUseFrameIndex > 0;

        void IVertexBuffer.Free()
        {
            if (isInitialised)
            {
                GL.DeleteBuffer(vboId);
                Renderer.DeleteVertexArray(vaoId);
            }

            if (vertexData != null)
            {
                ArrayPool<byte>.Shared.Return(vertexData);
                vertexData = null;
            }

            LastUseFrameIndex = 0;

            isInitialised = false;
            uploadedPrefix = 0;
            streamOffset = nextStreamOffset = 0;
        }
    }
}
