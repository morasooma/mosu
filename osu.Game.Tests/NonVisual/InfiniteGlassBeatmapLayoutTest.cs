// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class InfiniteGlassBeatmapLayoutTest
    {
        private const int item_count = 2264;
        private static readonly Vector2 viewport_size = new Vector2(1920, 1080);

        [Test]
        public void TestDenseSongIsNotTruncated()
        {
            var layout = new InfiniteGlassBeatmapLayout(Enumerable.Repeat("same song", item_count).ToArray());
            const int farDifficulty = item_count - 1;
            Vector2 cameraPosition = -layout.GetCardPosition(farDifficulty) * 0.35f;
            int[] visible = layout.GetVisibleIndices(viewport_size, cameraPosition, 0.35f).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(layout.Count, Is.EqualTo(item_count));
                Assert.That(visible, Does.Contain(farDifficulty));
            });
        }

        [Test]
        public void TestEveryCardCanBeReachedByPanning()
        {
            string[] songKeys = Enumerable.Range(0, item_count).Select(index => $"song {index / 4}").ToArray();
            var layout = new InfiniteGlassBeatmapLayout(songKeys);
            var reached = new HashSet<int>();

            for (int index = 0; index < item_count; index++)
            {
                Vector2 cameraPosition = -layout.GetCardPosition(index);
                reached.UnionWith(layout.GetVisibleIndices(viewport_size, cameraPosition, 1));
            }

            Assert.That(reached, Has.Count.EqualTo(item_count));
        }

        [Test]
        public void TestSelectedCardCanBeCentred()
        {
            const int selectedIndex = 1234;
            string[] songKeys = Enumerable.Range(0, item_count).Select(index => $"song {index / 5}").ToArray();
            var layout = new InfiniteGlassBeatmapLayout(songKeys);
            const float zoom = 0.63f;
            Vector2 cameraPosition = -layout.GetCardPosition(selectedIndex) * zoom;

            Assert.That(layout.GetScreenPosition(selectedIndex, cameraPosition, zoom), Is.EqualTo(Vector2.Zero));
        }

        [TestCase(1, 1, 0)]
        [TestCase(3, 0, 1)]
        [TestCase(5, -1, 0)]
        [TestCase(7, 0, -1)]
        public void TestDirectionalNavigationSelectsNearbyCard(int expectedIndex, float directionX, float directionY)
        {
            string[] songKeys = Enumerable.Range(0, 9).Select(index => $"song {index}").ToArray();
            var layout = new InfiniteGlassBeatmapLayout(songKeys);

            Assert.That(layout.FindClosestInDirection(0, new Vector2(directionX, directionY)), Is.EqualTo(expectedIndex));
        }

        [Test]
        public void TestDirectionalNavigationStopsAtFieldEdge()
        {
            var layout = new InfiniteGlassBeatmapLayout(new[] { "only song" });

            Assert.That(layout.FindClosestInDirection(0, Vector2.UnitY), Is.Null);
        }

        [Test]
        public void TestDifficultiesOfSameSongAreCloserTogether()
        {
            string[] songKeys = { "song a", "song a", "song b" };
            var layout = new InfiniteGlassBeatmapLayout(songKeys);
            float sameSongDistance = (layout.GetCardPosition(1) - layout.GetCardPosition(0)).Length;
            float differentSongDistance = (layout.GetCardPosition(2) - layout.GetCardPosition(0)).Length;

            Assert.That(sameSongDistance, Is.LessThan(differentSongDistance));
        }

        [Test]
        public void TestCardsNeverOverlap()
        {
            string[] songKeys = Enumerable.Range(0, item_count)
                                          .Select(index => $"song {index / (index % 13 + 1)}")
                                          .ToArray();
            var layout = new InfiniteGlassBeatmapLayout(songKeys);

            for (int first = 0; first < item_count; first++)
            {
                Vector2 firstPosition = layout.GetCardPosition(first);

                for (int second = first + 1; second < item_count; second++)
                {
                    Vector2 delta = layout.GetCardPosition(second) - firstPosition;
                    bool overlaps = System.Math.Abs(delta.X) < InfiniteGlassBeatmapLayout.CardWidth
                                    && System.Math.Abs(delta.Y) < InfiniteGlassBeatmapLayout.CardHeight;

                    if (overlaps)
                        Assert.Fail($"Cards {first} and {second} overlap.");
                }
            }
        }

        [TestCase(0.35f)]
        [TestCase(1f)]
        [TestCase(1.8f)]
        public void TestDragSensitivityDoesNotDependOnZoom(float zoom)
        {
            var layout = new InfiniteGlassBeatmapLayout(new[] { "song" });
            var worldPosition = new Vector2(-400, 270);
            var dragDelta = new Vector2(73, -41);
            Vector2 before = layout.GetScreenPosition(0, worldPosition, zoom);
            Vector2 after = layout.GetScreenPosition(0, worldPosition + dragDelta, zoom);

            Assert.That(after - before, Is.EqualTo(dragDelta));
        }

        [Test]
        public void TestZoomKeepsWorldPointUnderCursor()
        {
            const float oldZoom = 1;
            const float newZoom = 0.35f;
            var oldWorldPosition = new Vector2(-700, -4200);
            var cursorFromCentre = new Vector2(430, -170);

            Vector2 newWorldPosition = InfiniteGlassBeatmapLayout.GetWorldPositionAfterZoom(
                oldWorldPosition, cursorFromCentre, oldZoom, newZoom);

            Vector2 worldPointUnderCursor = (cursorFromCentre - oldWorldPosition) / oldZoom;
            Vector2 cursorAfterZoom = newWorldPosition + worldPointUnderCursor * newZoom;

            Assert.That(cursorAfterZoom.X, Is.EqualTo(cursorFromCentre.X).Within(0.001f));
            Assert.That(cursorAfterZoom.Y, Is.EqualTo(cursorFromCentre.Y).Within(0.001f));
        }
    }
}
