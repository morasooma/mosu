// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Graphics.Rendering.Deferred.Allocation;
using osu.Framework.Graphics.Rendering.Deferred.Events;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Veldrid.Buffers;

namespace osu.Framework.Graphics.Rendering.Deferred
{
    internal class DeferredVertexBatch<TVertex> : IVertexBatch<TVertex>, IDeferredVertexBatch
        where TVertex : unmanaged, IEquatable<TVertex>, IVertex
    {
        private static readonly bool capture_draw_node_events = !string.IsNullOrEmpty(FrameworkEnvironment.DeferredRendererEventsOutputPath);

        // Keep each coalesced upload comfortably below VertexManager's 2MB VBO chunks.
        private const int max_pending_upload_size = 512 * 1024;

        private static readonly TVertex[] current_primitive = new TVertex[4];

        // ReSharper disable once StaticMemberInGenericType
        private static int currentPrimitiveSize;

        public Action<TVertex> AddAction { get; }

        private readonly DeferredRenderer renderer;
        private readonly PrimitiveTopology topology;
        private readonly VeldridIndexLayout indexLayout;
        private readonly int primitiveSize;

        private int currentDrawCount;
        private MemoryReference pendingVertexMemory;
        private bool hasPendingVertexMemory;
        private ResourceReference frameReference;
        private bool hasFrameReference;

        public DeferredVertexBatch(DeferredRenderer renderer, PrimitiveTopology topology, VeldridIndexLayout indexLayout)
        {
            this.renderer = renderer;

            this.topology = topology;
            this.indexLayout = indexLayout;

            if (this.indexLayout == VeldridIndexLayout.Linear)
            {
                switch (this.topology)
                {
                    case PrimitiveTopology.Points:
                        primitiveSize = 1;
                        break;

                    case PrimitiveTopology.Lines:
                        primitiveSize = 2;
                        break;

                    case PrimitiveTopology.Triangles:
                        primitiveSize = 3;
                        break;

                    case PrimitiveTopology.LineStrip:
                    case PrimitiveTopology.TriangleStrip:
                        throw new NotImplementedException($"Topology '{topology}' is not yet implemented for this renderer.");

                    default:
                        throw new ArgumentOutOfRangeException(nameof(topology));
                }
            }
            else
                primitiveSize = 4;

            AddAction = ((IVertexBatch<TVertex>)this).Add;
        }

        public void Write(in MemoryReference primitive)
            => renderer.Context.VertexManager.Write<TVertex>(primitive);

        public void Draw(int count)
            => renderer.Context.VertexManager.Draw<TVertex>(count, topology, indexLayout, primitiveSize);

        public ResourceReference FrameReference
        {
            get
            {
                if (!hasFrameReference)
                {
                    frameReference = renderer.Context.Reference(this);
                    hasFrameReference = true;
                }

                return frameReference;
            }
        }

        int IVertexBatch.Size
            => int.MaxValue;

        int IVertexBatch.Draw()
        {
            int count = currentDrawCount;
            currentDrawCount = 0;

            if (count == 0)
                return 0;

            enqueuePendingVertexMemory();
            renderer.DrawVertices(topology, 0, count);
            renderer.Context.EnqueueEvent(FlushEvent.Create(this, count));
            return count;
        }

        void IVertexBatch.ResetCounters()
        {
            // FinishFrame() flushes every active batch before the context is reset.
            // No pending memory may survive into a new allocator frame.
            Debug.Assert(!hasPendingVertexMemory);

            currentPrimitiveSize = 0;
            currentDrawCount = 0;
            hasFrameReference = false;
        }

        void IVertexBatch<TVertex>.Add(TVertex vertex)
        {
            renderer.SetActiveBatch(this);

            current_primitive[currentPrimitiveSize] = vertex;

            if (++currentPrimitiveSize == primitiveSize)
            {
                ReadOnlySpan<TVertex> primitive = current_primitive.AsSpan(0, primitiveSize);

                if (renderer.Context.DirectVertexBufferWrites)
                {
                    // The legacy path first copies every primitive into ResourceAllocator memory,
                    // records an event, then copies that memory into the mapped VBO in FinishFrame.
                    // Writing the completed primitive directly keeps identical ordering while
                    // removing one full vertex copy and almost the entire upload event stream.
                    renderer.Context.VertexManager.Write(primitive);
                    currentPrimitiveSize = 0;
                    currentDrawCount++;
                    return;
                }

                MemoryReference memory = renderer.Context.AllocateRegion(primitive);

                if (capture_draw_node_events || !DeferredRendererPerformanceSettings.CoalesceVertexUploadEvents)
                {
                    // Preserve the one-event-per-primitive ordering expected by the optional
                    // draw-node event dump. This is also the conservative production path when
                    // experimental event coalescing is disabled.
                    enqueuePendingVertexMemory();
                    renderer.Context.EnqueueUploadEvent(AddPrimitiveToBatchEvent.Create(this, memory));
                }
                else if (hasPendingVertexMemory
                    && pendingVertexMemory.BufferId == memory.BufferId
                    && pendingVertexMemory.Offset + pendingVertexMemory.Length == memory.Offset
                    && pendingVertexMemory.Length + memory.Length <= max_pending_upload_size)
                {
                    pendingVertexMemory = pendingVertexMemory with { Length = pendingVertexMemory.Length + memory.Length };
                }
                else
                {
                    enqueuePendingVertexMemory();
                    pendingVertexMemory = memory;
                    hasPendingVertexMemory = true;
                }

                currentPrimitiveSize = 0;
            }

            currentDrawCount++;
        }

        private void enqueuePendingVertexMemory()
        {
            if (!hasPendingVertexMemory)
                return;

            renderer.Context.EnqueueUploadEvent(AddPrimitiveToBatchEvent.Create(this, pendingVertexMemory));
            hasPendingVertexMemory = false;
        }

        public void Dispose()
        {
        }
    }
}
