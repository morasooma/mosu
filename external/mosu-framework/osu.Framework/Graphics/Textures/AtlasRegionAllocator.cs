// Copyright (c) ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Primitives;

namespace osu.Framework.Graphics.Textures
{
    /// <summary>
    /// A free-list region allocator for a <see cref="TextureAtlas"/>.
    /// <para>
    /// Unlike the default shelf-packing cursor used by <see cref="TextureAtlas"/>, this allocator
    /// supports freeing previously allocated regions and reusing that space for new allocations.
    /// Freed regions are merged with adjacent free regions to reduce fragmentation.
    /// </para>
    /// <para>
    /// Allocation strategy: best-fit search over the free list, falling back to extending the
    /// shelf cursor into untouched atlas space. This keeps the common "many small textures of
    /// similar size" case fast while still reclaiming space from purged textures.
    /// </para>
    /// </summary>
    internal class AtlasRegionAllocator
    {
        private readonly object lockObject = new object();

        /// <summary>
        /// The usable area starts after the reserved white pixel row/column.
        /// All allocations are offset by this amount.
        /// </summary>
        private readonly RectangleI usableBounds;

        /// <summary>
        /// Free rectangles, maintained as a list. Searched best-fit on allocation.
        /// </summary>
        private readonly List<RectangleI> freeRects = new List<RectangleI>();

        /// <summary>
        /// The current shelf cursor position within <see cref="usableBounds"/>.
        /// Used when no free rectangle can satisfy an allocation.
        /// </summary>
        private int cursorX;

        private int cursorY;

        private int currentRowHeight;

        public AtlasRegionAllocator(RectangleI usableBounds)
        {
            this.usableBounds = usableBounds;
            cursorX = usableBounds.X;
            cursorY = usableBounds.Y;
            currentRowHeight = 0;

            freeRects.Add(usableBounds);
        }

        /// <summary>
        /// Total number of successful allocations since creation or the last <see cref="Reset"/>.
        /// </summary>
        public int AllocateCount { get; private set; }

        /// <summary>
        /// Total number of frees since creation or the last <see cref="Reset"/>.
        /// </summary>
        public int FreeCount { get; private set; }

        /// <summary>
        /// Total number of adjacent merges performed on free.
        /// </summary>
        public int MergeCount { get; private set; }

        /// <summary>
        /// The number of free rectangles currently tracked.
        /// </summary>
        public int FreeRectCount
        {
            get
            {
                lock (lockObject)
                    return freeRects.Count;
            }
        }

        /// <summary>
        /// Ratio of free space that is split across multiple rectangles (fragmented),
        /// relative to the total free area. 0 means all free space is contiguous.
        /// </summary>
        public float FragmentationRatio
        {
            get
            {
                lock (lockObject)
                {
                    if (freeRects.Count == 0)
                        return 0;

                    long totalFreeArea = 0;
                    long largestFreeArea = 0;

                    foreach (var rect in freeRects)
                    {
                        long area = (long)rect.Width * rect.Height;
                        totalFreeArea += area;

                        if (area > largestFreeArea)
                            largestFreeArea = area;
                    }

                    if (totalFreeArea == 0)
                        return 0;

                    return 1f - (float)largestFreeArea / totalFreeArea;
                }
            }
        }

        /// <summary>
        /// Attempts to allocate a region of the given size.
        /// </summary>
        /// <param name="rectWidth">The width of the region to allocate (excluding padding).</param>
        /// <param name="rectHeight">The height of the region to allocate (excluding padding).</param>
        /// <param name="padding">Padding required on the right and bottom edges of the region.</param>
        /// <returns>The allocated bounds in atlas coordinates, or null if the region does not fit.</returns>
        public RectangleI? Allocate(int rectWidth, int rectHeight, int padding)
        {
            lock (lockObject)
            {
                int requiredWidth = rectWidth + padding;
                int requiredHeight = rectHeight + padding;

                if (requiredWidth > usableBounds.Width || requiredHeight > usableBounds.Height)
                    return null;

                // Best-fit search over free rectangles.
                int bestIndex = -1;
                long bestWaste = long.MaxValue;

                for (int i = 0; i < freeRects.Count; i++)
                {
                    var free = freeRects[i];

                    if (free.Width < requiredWidth || free.Height < requiredHeight)
                        continue;

                    // Prefer the rectangle that wastes the least area.
                    long waste = (long)free.Width * free.Height - (long)requiredWidth * requiredHeight;

                    if (waste < bestWaste)
                    {
                        bestWaste = waste;
                        bestIndex = i;
                    }
                }

                RectangleI result;

                if (bestIndex >= 0)
                {
                    var free = freeRects[bestIndex];
                    freeRects.RemoveAt(bestIndex);

                    result = new RectangleI(free.X, free.Y, rectWidth, rectHeight);

                    // Split the remainder of the free rectangle into up to two pieces:
                    // the strip to the right of the allocation and the strip below it.
                    int rightWidth = free.Width - requiredWidth;
                    int bottomHeight = free.Height - requiredHeight;

                    if (rightWidth > 0)
                        freeRects.Add(new RectangleI(free.X + requiredWidth, free.Y, rightWidth, requiredHeight));

                    if (bottomHeight > 0)
                        freeRects.Add(new RectangleI(free.X, free.Y + requiredHeight, free.Width, bottomHeight));
                }
                else
                {
                    // Fall back to the shelf cursor for untouched space.
                    if (cursorX + requiredWidth > usableBounds.Right || cursorY + requiredHeight > usableBounds.Bottom)
                    {
                        // Wrap to the next row.
                        cursorX = usableBounds.X;
                        cursorY += currentRowHeight;
                        currentRowHeight = 0;

                        if (cursorX + requiredWidth > usableBounds.Right || cursorY + requiredHeight > usableBounds.Bottom)
                            return null;
                    }

                    result = new RectangleI(cursorX, cursorY, rectWidth, rectHeight);

                    cursorX += requiredWidth;
                    currentRowHeight = Math.Max(currentRowHeight, requiredHeight);
                }

                AllocateCount++;
                return result;
            }
        }

        /// <summary>
        /// Frees a previously allocated region, making it available for reuse.
        /// Adjacent free rectangles are merged.
        /// </summary>
        /// <param name="bounds">The bounds (in atlas coordinates) returned by a previous <see cref="Allocate"/> call.</param>
        /// <param name="padding">The padding that was applied on the right and bottom edges at allocation time.</param>
        public void Free(RectangleI bounds, int padding)
        {
            lock (lockObject)
            {
                var freed = new RectangleI(bounds.X, bounds.Y, bounds.Width + padding, bounds.Height + padding);

                // Clamp to usable bounds in case the region touched the atlas edge.
                freed = RectangleI.Intersect(freed, usableBounds);

                if (freed.Width <= 0 || freed.Height <= 0)
                    return;

                freeRects.Add(freed);
                FreeCount++;

                mergeFreeRects();
            }
        }

        /// <summary>
        /// Resets the allocator to its initial state, as if no allocations were made.
        /// </summary>
        public void Reset()
        {
            lock (lockObject)
            {
                freeRects.Clear();
                freeRects.Add(usableBounds);

                cursorX = usableBounds.X;
                cursorY = usableBounds.Y;
                currentRowHeight = 0;

                AllocateCount = 0;
                FreeCount = 0;
                MergeCount = 0;
            }
        }

        /// <summary>
        /// Merges adjacent free rectangles. Runs a simple O(n^2) pass; the free list is
        /// typically small (bounded by the number of live atlas textures).
        /// Must be called under <see cref="lockObject"/>.
        /// </summary>
        private void mergeFreeRects()
        {
            bool merged = true;

            while (merged)
            {
                merged = false;

                for (int i = 0; i < freeRects.Count; i++)
                {
                    for (int j = i + 1; j < freeRects.Count; j++)
                    {
                        var a = freeRects[i];
                        var b = freeRects[j];

                        // Horizontal merge: same Y and height, touching on X.
                        if (a.Y == b.Y && a.Height == b.Height)
                        {
                            if (a.X + a.Width == b.X)
                            {
                                freeRects[i] = new RectangleI(a.X, a.Y, a.Width + b.Width, a.Height);
                                freeRects.RemoveAt(j);
                                MergeCount++;
                                merged = true;
                                j--;
                                continue;
                            }

                            if (b.X + b.Width == a.X)
                            {
                                freeRects[i] = new RectangleI(b.X, b.Y, a.Width + b.Width, a.Height);
                                freeRects.RemoveAt(j);
                                MergeCount++;
                                merged = true;
                                j--;
                                continue;
                            }
                        }

                        // Vertical merge: same X and width, touching on Y.
                        if (a.X == b.X && a.Width == b.Width)
                        {
                            if (a.Y + a.Height == b.Y)
                            {
                                freeRects[i] = new RectangleI(a.X, a.Y, a.Width, a.Height + b.Height);
                                freeRects.RemoveAt(j);
                                MergeCount++;
                                merged = true;
                                j--;
                                continue;
                            }

                            if (b.Y + b.Height == a.Y)
                            {
                                freeRects[i] = new RectangleI(b.X, b.Y, a.Width, a.Height + b.Height);
                                freeRects.RemoveAt(j);
                                MergeCount++;
                                merged = true;
                                j--;
                            }
                        }
                    }
                }
            }
        }
    }
}
