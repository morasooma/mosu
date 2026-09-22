// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Stateless geometry for an unbounded field of compact song clusters.
    /// Each song occupies one island; its difficulties surround the first difficulty in that island.
    /// </summary>
    internal sealed class InfiniteGlassBeatmapLayout
    {
        public const float CardWidth = 228;
        public const float CardHeight = 128;

        private const int maximum_difficulty_columns = 3;
        private const float difficulty_spacing_x = CardWidth + 10;
        private const float difficulty_spacing_y = CardHeight + 10;
        private const float song_cell_width = CardWidth + 64;
        private const float song_cell_height = CardHeight + 54;
        private const float song_cluster_padding = 40;

        public static readonly InfiniteGlassBeatmapLayout Empty = new InfiniteGlassBeatmapLayout(Array.Empty<string>());

        private readonly Vector2[] positions;

        public int Count => positions.Length;

        public InfiniteGlassBeatmapLayout(IReadOnlyList<string> songKeys)
        {
            positions = new Vector2[songKeys.Count];

            var groups = new List<List<int>>();
            var groupIndices = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int index = 0; index < songKeys.Count; index++)
            {
                string key = songKeys[index];

                if (!groupIndices.TryGetValue(key, out int groupIndex))
                {
                    groupIndex = groups.Count;
                    groupIndices.Add(key, groupIndex);
                    groups.Add(new List<int>());
                }

                groups[groupIndex].Add(index);
            }

            var occupiedCells = new HashSet<(int X, int Y)>();

            foreach (List<int> group in groups)
            {
                int columns = Math.Min(maximum_difficulty_columns, group.Count);
                int rows = (group.Count + columns - 1) / columns;
                float contentWidth = CardWidth + (columns - 1) * difficulty_spacing_x;
                float contentHeight = CardHeight + (rows - 1) * difficulty_spacing_y;
                int cellWidth = Math.Max(1, (int)Math.Ceiling((contentWidth + song_cluster_padding) / song_cell_width));
                int cellHeight = Math.Max(1, (int)Math.Ceiling((contentHeight + song_cluster_padding) / song_cell_height));
                (int X, int Y) origin = findFreeBlock(occupiedCells, cellWidth, cellHeight);
                var groupCentre = new Vector2(
                    (origin.X + (cellWidth - 1) / 2f) * song_cell_width,
                    (origin.Y + (cellHeight - 1) / 2f) * song_cell_height);

                for (int difficultyIndex = 0; difficultyIndex < group.Count; difficultyIndex++)
                {
                    int itemIndex = group[difficultyIndex];
                    int row = difficultyIndex / columns;
                    int column = difficultyIndex % columns;
                    int itemsInRow = Math.Min(columns, group.Count - row * columns);

                    positions[itemIndex] = groupCentre + new Vector2(
                        (column - (itemsInRow - 1) / 2f) * difficulty_spacing_x,
                        (row - (rows - 1) / 2f) * difficulty_spacing_y);
                }
            }
        }

        public Vector2 GetCardPosition(int index) => positions[index];

        public Vector2 GetScreenPosition(int index, Vector2 worldPosition, float zoom)
            => worldPosition + positions[index] * zoom;

        /// <summary>
        /// Finds the spatially nearest card in the requested direction.
        /// This keeps keyboard navigation local instead of following the unrelated linear carousel order.
        /// </summary>
        public int? FindClosestInDirection(int currentIndex, Vector2 direction)
        {
            if ((uint)currentIndex >= positions.Length || direction.LengthSquared <= 0 || !float.IsFinite(direction.LengthSquared))
                return null;

            direction /= direction.Length;
            Vector2 origin = positions[currentIndex];
            int bestIndex = -1;
            float bestScore = float.MaxValue;
            float bestDistanceSquared = float.MaxValue;

            for (int index = 0; index < positions.Length; index++)
            {
                if (index == currentIndex)
                    continue;

                Vector2 delta = positions[index] - origin;
                float forwardDistance = Vector2.Dot(delta, direction);

                if (forwardDistance <= 0.001f)
                    continue;

                float distanceSquared = delta.LengthSquared;
                // Distance divided by directional alignment strongly penalises cards which are
                // technically on the correct side but mostly perpendicular to the pressed arrow.
                float score = distanceSquared / forwardDistance;

                if (score < bestScore || (Math.Abs(score - bestScore) < 0.001f && distanceSquared < bestDistanceSquared))
                {
                    bestIndex = index;
                    bestScore = score;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return bestIndex >= 0 ? bestIndex : null;
        }

        public static Vector2 GetWorldPositionAfterZoom(Vector2 worldPosition, Vector2 cursorFromCentre, float oldZoom, float newZoom)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(oldZoom);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newZoom);

            return cursorFromCentre - (cursorFromCentre - worldPosition) * (newZoom / oldZoom);
        }

        /// <summary>
        /// Returns every card intersecting the viewport plus a preload margin.
        /// The complete lightweight position model remains available at every camera position.
        /// </summary>
        public IEnumerable<int> GetVisibleIndices(Vector2 viewportSize, Vector2 worldPosition, float zoom)
        {
            if (viewportSize.X <= 0 || viewportSize.Y <= 0 || zoom <= 0 || !float.IsFinite(zoom))
                yield break;

            Vector2 viewportCentre = -worldPosition / zoom;
            float horizontalRadius = viewportSize.X / (2 * zoom) + CardWidth;
            float verticalRadius = viewportSize.Y / (2 * zoom) + CardHeight;

            for (int index = 0; index < positions.Length; index++)
            {
                Vector2 position = positions[index];

                if (Math.Abs(position.X - viewportCentre.X) <= horizontalRadius
                    && Math.Abs(position.Y - viewportCentre.Y) <= verticalRadius)
                    yield return index;
            }
        }

        /// <summary>
        /// Returns successive cells of a square spiral, beginning at (0, 0).
        /// The same small primitive is used for song islands and for difficulties inside an island.
        /// </summary>
        private static Vector2 getSquareSpiralCell(int index)
        {
            if (index == 0)
                return Vector2.Zero;

            int layer = (int)Math.Ceiling((Math.Sqrt(index + 1) - 1) / 2);
            int legLength = layer * 2;
            int offset = (2 * layer + 1) * (2 * layer + 1) - 1 - index;

            if (offset < legLength)
                return new Vector2(layer - offset, -layer);

            offset -= legLength;

            if (offset < legLength)
                return new Vector2(-layer, -layer + offset);

            offset -= legLength;

            if (offset < legLength)
                return new Vector2(-layer + offset, layer);

            offset -= legLength;
            return new Vector2(layer, layer - offset);
        }

        private static (int X, int Y) findFreeBlock(HashSet<(int X, int Y)> occupiedCells, int width, int height)
        {
            for (int candidateIndex = 0; ; candidateIndex++)
            {
                Vector2 candidate = getSquareSpiralCell(candidateIndex);
                int originX = (int)candidate.X;
                int originY = (int)candidate.Y;
                bool available = true;

                for (int y = 0; y < height && available; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (occupiedCells.Contains((originX + x, originY + y)))
                        {
                            available = false;
                            break;
                        }
                    }
                }

                if (!available)
                    continue;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        occupiedCells.Add((originX + x, originY + y));
                }

                return (originX, originY);
            }
        }
    }

}
