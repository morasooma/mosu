// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Framework.IO
{
    internal class AsyncBufferStream : Stream
    {
        private const int block_size = 32768;
        // 64 blocks keep sequential playback and prescan buffered without retaining whole tracks.
        private const int cache_blocks = 64;
        private const int default_read_ahead_blocks = 8;
        // Bound idle retention across tracks as well as the live per-cache window. Unlike
        // ArrayPool.Shared this pool has no per-thread buckets (prefetch uses short-lived workers).
        private static readonly ArrayPool<byte> blockPool = ArrayPool<byte>.Create(block_size, cache_blocks * 2);

        private sealed class BlockCache
        {
            public readonly object Sync = new object();
            public readonly Dictionary<long, byte[]> Blocks = new Dictionary<long, byte[]>();
            public readonly LinkedList<long> Order = new LinkedList<long>();
            public readonly Dictionary<long, int> Requested = new Dictionary<long, int>();
            public readonly Stack<byte[]> Free = new Stack<byte[]>();
            public int Allocated;
            public int References;
        }

        private readonly Stream underlyingStream;
        private readonly BlockCache cache;
        private readonly long length;
        private readonly bool seekable;
        private readonly int blocksToReadAhead;
        private readonly byte[] nonSeekableData;
        private long position;
        private long positionVersion;
        private volatile bool disposed;
        private bool cacheOwnershipReleased;
        private ExceptionDispatchInfo loadException;
        private long requestedBlock = -1;

        /// <summary>
        /// Buffers seekable streams in a bounded 2 MiB block cache. Non-seekable streams retain the
        /// entire input: discarded bytes cannot be re-read when this stream is subsequently seeked.
        /// </summary>
        /// <param name="stream">The underlying stream to read from; owned by this instance.</param>
        /// <param name="blocksToReadAhead">The number of blocks to prefetch (-1 uses the bounded cache window).</param>
        /// <param name="shared">A stream over identical content whose cache can be shared, independently of its lifetime.</param>
        public AsyncBufferStream(Stream stream, int blocksToReadAhead, AsyncBufferStream shared = null)
        {
            underlyingStream = stream ?? throw new ArgumentNullException(nameof(stream));
            length = stream.Length;
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(stream));

            seekable = stream.CanSeek;
            this.blocksToReadAhead = blocksToReadAhead;
            if (seekable && shared != null && shared.seekable && shared.length == length)
            {
                lock (shared.cache.Sync)
                {
                    // A released cache can still be referenced by disposed streams/read waiters.
                    // Do not attach a new reader to their obsolete request bookkeeping.
                    if (shared.cache.References > 0)
                    {
                        cache = shared.cache;
                        cache.References += 2;
                    }
                }
            }
            cache ??= new BlockCache { References = seekable ? 2 : 0 };

            try
            {
                if (seekable)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }
                else
                {
                    // This path preserves seeking semantics for sources which cannot re-read evicted blocks.
                    nonSeekableData = new byte[checked((int)length)];
                }

                Task.Factory.StartNew(prefetch, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch
            {
                if (seekable)
                {
                    lock (cache.Sync)
                    {
                        releaseCacheReference();
                        releaseCacheReference();
                    }
                }
                throw;
            }
        }

        // Both the stream and its producer own a reference. Disposal must not recycle
        // bytes still used by a blocked producer, a surviving shared reader or its worker.
        // Called under cache.Sync, only after the corresponding owner has stopped accessing bytes.
        private void releaseCacheReference()
        {
            if (--cache.References != 0) return;

            foreach (byte[] bytes in cache.Blocks.Values)
                blockPool.Return(bytes, clearArray: true);
            foreach (byte[] bytes in cache.Free)
                blockPool.Return(bytes, clearArray: true);
            cache.Blocks.Clear();
            cache.Order.Clear();
            cache.Free.Clear();
            cache.Requested.Clear();
            cache.Allocated = 0;
        }

        private void prefetch()
        {
            try
            {
                if (!seekable)
                {
                    int filled = 0;
                    while (filled < nonSeekableData.Length)
                    {
                        lock (cache.Sync)
                        {
                            if (disposed) return;
                        }

                        int count = underlyingStream.Read(nonSeekableData, filled, Math.Min(block_size, nonSeekableData.Length - filled));
                        if (count == 0) throw new EndOfStreamException();
                        filled += count;
                        lock (cache.Sync)
                        {
                            nonSeekableFilled = filled;
                            Monitor.PulseAll(cache.Sync);
                        }
                    }
                    return;
                }

                long next = 0;
                while (true)
                {
                    lock (cache.Sync)
                    {
                        if (disposed) return;

                        long start = requestedBlock >= 0 ? requestedBlock : position / block_size;
                        long end = Math.Min(length / block_size + (length % block_size == 0 ? 0 : 1),
                                            start + Math.Min(cache_blocks, blocksToReadAhead < 0 ? default_read_ahead_blocks : (long)blocksToReadAhead + 1));
                        // A native callback can seek backwards within the prefetch window while
                        // later blocks are already cached. Service its missing block before the
                        // cursor scans forward (and sleeps at the end of the window).
                        if (requestedBlock >= 0 && !cache.Blocks.ContainsKey(requestedBlock))
                            next = requestedBlock;
                        else if (next < start || next >= end)
                            next = start;
                        while (next < end && cache.Blocks.ContainsKey(next)) next++;

                        if (next >= end)
                        {
                            Monitor.Wait(cache.Sync);
                            continue;
                        }

                        byte[] bytes = reserveBlock();
                        if (bytes == null)
                        {
                            Monitor.Wait(cache.Sync);
                            continue;
                        }

                        long block = next++;
                        // Only this worker touches its own source cursor. Never hold the shared
                        // cache lock across source I/O (which can block or be independently disposed).
                        Monitor.Exit(cache.Sync);
                        try
                        {
                            loadBlock(block, bytes);
                        }
                        catch
                        {
                            Monitor.Enter(cache.Sync);
                            cache.Free.Push(bytes);
                            Monitor.PulseAll(cache.Sync);
                            throw;
                        }
                        Monitor.Enter(cache.Sync);

                        if (!disposed && !cache.Blocks.ContainsKey(block))
                        {
                            cache.Blocks.Add(block, bytes);
                            cache.Order.AddLast(block);
                        }
                        else
                            cache.Free.Push(bytes);
                        Monitor.PulseAll(cache.Sync);
                    }
                }
            }
            catch (Exception e)
            {
                lock (cache.Sync)
                {
                    if (!disposed)
                        loadException = ExceptionDispatchInfo.Capture(e);
                    Monitor.PulseAll(cache.Sync);
                }
            }
            finally
            {
                if (seekable)
                {
                    lock (cache.Sync)
                        releaseCacheReference();
                }
            }
        }

        // Called under cache.Sync. A requested block stays cached until its consumer has
        // copied it, even when another stream is prefetching a distant part of the file.
        private byte[] reserveBlock()
        {
            if (cache.Free.Count > 0) return cache.Free.Pop();
            if (cache.Allocated < cache_blocks)
            {
                cache.Allocated++;
                return blockPool.Rent(block_size);
            }

            for (var node = cache.Order.First; node != null; node = node.Next)
            {
                if (cache.Requested.ContainsKey(node.Value)) continue;
                byte[] bytes = cache.Blocks[node.Value];
                cache.Blocks.Remove(node.Value);
                cache.Order.Remove(node);
                return bytes;
            }
            return null;
        }

        private void loadBlock(long block, byte[] bytes)
        {
            int size = (int)Math.Min(block_size, length - block * block_size);
            underlyingStream.Seek(block * block_size, SeekOrigin.Begin);
            int filled = 0;
            while (filled < size)
            {
                int read = underlyingStream.Read(bytes, filled, size - filled);
                if (read == 0) throw new EndOfStreamException();
                filled += read;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            lock (cache.Sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                int remaining = (int)Math.Min(buffer.Length, length - position);
                if (remaining == 0) return 0;

                if (!seekable)
                {
                    // Wait for the producer without holding the lock while it publishes new bytes.
                    while (nonSeekableFilled < position + remaining && loadException == null && !disposed)
                        Monitor.Wait(cache.Sync);
                    ObjectDisposedException.ThrowIf(disposed, this);
                    loadException?.Throw();
                    nonSeekableData.AsSpan((int)position, remaining).CopyTo(buffer);
                    position += remaining;
                    Monitor.PulseAll(cache.Sync);
                    return remaining;
                }

                int copied = 0;
                long readVersion = positionVersion;
                while (buffer.Length > 0 && position < length)
                {
                    long block = position / block_size;
                    int offset = (int)(position % block_size);
                    // Pin the requested block across waits; other producers may not recycle it
                    // until the bytes are copied. The background worker alone does source I/O.
                    cache.Requested.TryGetValue(block, out int waiting);
                    cache.Requested[block] = waiting + 1;
                    try
                    {
                        requestedBlock = block;
                        Monitor.PulseAll(cache.Sync);
                        while (!cache.Blocks.ContainsKey(block) && loadException == null && !disposed && readVersion == positionVersion)
                            Monitor.Wait(cache.Sync);
                        ObjectDisposedException.ThrowIf(disposed, this);
                        // Seek may run while Wait releases the lock. Do not copy stale bytes or
                        // combine bytes from before and after a seek into a single Read.
                        if (readVersion != positionVersion)
                        {
                            if (copied > 0) return copied;
                            readVersion = positionVersion;
                            continue;
                        }
                        if (!cache.Blocks.TryGetValue(block, out byte[] bytes))
                            loadException?.Throw();
                        int count = Math.Min(buffer.Length, (int)Math.Min(block_size, length - block * block_size) - offset);
                        bytes.AsSpan(offset, count).CopyTo(buffer);
                        buffer = buffer.Slice(count);
                        position += count;
                        copied += count;
                    }
                    finally
                    {
                        requestedBlock = -1;
                        if (waiting == 0) cache.Requested.Remove(block);
                        else cache.Requested[block] = waiting;
                        Monitor.PulseAll(cache.Sync);
                    }
                }

                Monitor.PulseAll(cache.Sync);
                return copied;
            }
        }

        private long nonSeekableFilled;

        public override bool CanRead => !disposed;
        public override bool CanSeek => !disposed;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get { lock (cache.Sync) return position; }
            set
            {
                lock (cache.Sync)
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    position = Math.Clamp(value, 0, length);
                    positionVersion++;
                    Monitor.PulseAll(cache.Sync);
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (cache.Sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                long start = origin switch
                {
                    SeekOrigin.Begin => 0,
                    SeekOrigin.Current => position,
                    SeekOrigin.End => length,
                    _ => throw new ArgumentOutOfRangeException(nameof(origin)),
                };
                position = Math.Clamp(offset > 0 && start > long.MaxValue - offset ? long.MaxValue :
                                      offset < 0 && start < long.MinValue - offset ? long.MinValue : start + offset, 0, length);
                positionVersion++;
                Monitor.PulseAll(cache.Sync);
                return position;
            }
        }

        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                disposed = true;
                // A blocked source read must be released before acquiring the producer's cache lock.
                try
                {
                    underlyingStream.Dispose();
                }
                finally
                {
                    lock (cache.Sync)
                    {
                        if (seekable && !cacheOwnershipReleased)
                        {
                            cacheOwnershipReleased = true;
                            releaseCacheReference();
                        }
                        Monitor.PulseAll(cache.Sync);
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
