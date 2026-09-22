// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers the map of rooms: which rooms are linked, and where they end up when nobody has placed
    /// them by hand.
    /// </summary>
    [TestFixture]
    public class WorldGraphTest
    {
        private static RoomDefinition room(string id, params EntityRecord[] entities) => new RoomDefinition
        {
            Id = id,
            Name = id.ToUpperInvariant(),
            Entities = entities.ToList(),
        };

        private static EntityRecord exit(string destinationRoomId, float x = 0, string kind = EntityKinds.PASSAGE) => new EntityRecord
        {
            Id = $"exit-{destinationRoomId}",
            Kind = kind,
            DestinationRoomId = destinationRoomId,
            X = x,
        };

        private static Vector2 positionOf(WorldGraph graph, string roomId) =>
            graph.Nodes.Single(node => node.RoomId == roomId).Position;

        [Test]
        public void APassageAndThePortalBackAreOneLink()
        {
            WorldGraph graph = WorldGraph.Describe(new[]
            {
                room("plaza", exit("east")),
                room("east", exit("plaza", kind: EntityKinds.PORTAL)),
            }, "plaza");

            Assert.That(graph.Links, Has.Count.EqualTo(1));
            Assert.That(graph.Links[0].FromRoomId, Is.EqualTo("east"));
            Assert.That(graph.Links[0].ToRoomId, Is.EqualTo("plaza"));
        }

        [Test]
        public void ExitsLeadingNowhereAreNotLinks()
        {
            WorldGraph graph = WorldGraph.Describe(new[]
            {
                room("plaza", exit(string.Empty), exit("missing"), exit("plaza")),
            }, "plaza");

            Assert.That(graph.Links, Is.Empty);
            Assert.That(graph.Nodes, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Distance from the root on the map should mean distance from the root in the world, and the
        /// room reached through the west exit should be the one drawn to the west.
        /// </summary>
        [Test]
        public void RoomsAreLaidOutByDistanceFromTheRoot()
        {
            WorldGraph graph = WorldGraph.Describe(new[]
            {
                room("plaza", exit("west", x: -1000), exit("east", x: 1000)),
                room("west"),
                room("east", exit("far", x: 1000)),
                room("far"),
            }, "plaza");

            Assert.That(positionOf(graph, "plaza"), Is.EqualTo(Vector2.Zero));
            Assert.That(positionOf(graph, "west").Y, Is.EqualTo(WorldGraph.SPACING.Y));
            Assert.That(positionOf(graph, "east").Y, Is.EqualTo(WorldGraph.SPACING.Y));
            Assert.That(positionOf(graph, "west").X, Is.LessThan(positionOf(graph, "east").X));

            // Two steps from the plaza, so two rows down.
            Assert.That(positionOf(graph, "far").Y, Is.EqualTo(WorldGraph.SPACING.Y * 2));
        }

        [Test]
        public void AHandPlacedRoomKeepsItsPlace()
        {
            RoomDefinition east = room("east");
            east.MapX = 640;
            east.MapY = -80;

            WorldGraph graph = WorldGraph.Describe(new[] { room("plaza", exit("east")), east }, "plaza");

            Assert.That(positionOf(graph, "east"), Is.EqualTo(new Vector2(640, -80)));
            Assert.That(graph.Nodes.Single(node => node.RoomId == "east").Placed, Is.True);
            Assert.That(graph.Nodes.Single(node => node.RoomId == "plaza").Placed, Is.False);
        }

        /// <summary>
        /// Corrupt coordinates fall back to the automatic layout rather than throwing the room off the
        /// map, the same way a corrupt entity position falls back to the room's origin.
        /// </summary>
        [Test]
        public void UnusableCoordinatesAreIgnored()
        {
            RoomDefinition east = room("east");
            east.MapX = float.NaN;
            east.MapY = 40;

            WorldGraph graph = WorldGraph.Describe(new[] { room("plaza", exit("east")), east }, "plaza");

            Assert.That(graph.Nodes.Single(node => node.RoomId == "east").Placed, Is.False);
            Assert.That(positionOf(graph, "east").Y, Is.EqualTo(WorldGraph.SPACING.Y));
        }

        /// <summary>
        /// A room nothing leads to is a mistake worth seeing, so it is drawn stranded below the rest
        /// rather than left off the map.
        /// </summary>
        [Test]
        public void UnreachableRoomsStillAppear()
        {
            WorldGraph graph = WorldGraph.Describe(new[]
            {
                room("plaza", exit("east")),
                room("east"),
                room("orphan"),
            }, "plaza");

            Assert.That(graph.Nodes.Select(node => node.RoomId), Is.EquivalentTo(new[] { "plaza", "east", "orphan" }));
            Assert.That(positionOf(graph, "orphan").Y, Is.EqualTo(WorldGraph.SPACING.Y * 2));
        }

        [Test]
        public void WarpsAreCountedPerRoom()
        {
            WorldGraph graph = WorldGraph.Describe(new[]
            {
                room("plaza",
                    new EntityRecord { Id = "w1", Kind = EntityKinds.WARP },
                    new EntityRecord { Id = "w2", Kind = EntityKinds.WARP },
                    new EntityRecord { Id = "npc", Kind = EntityKinds.NPC }),
            }, "plaza");

            Assert.That(graph.Nodes.Single().Warps, Is.EqualTo(2));
        }

        /// <summary>
        /// A world whose root room id is missing still draws: the map is the one place an author would
        /// go to work out what went wrong.
        /// </summary>
        [Test]
        public void AMissingRootStillProducesAMap()
        {
            WorldGraph graph = WorldGraph.Describe(new[] { room("a", exit("b")), room("b") }, "nonexistent");

            Assert.That(graph.Nodes, Has.Count.EqualTo(2));
            Assert.That(graph.Links, Has.Count.EqualTo(1));
            Assert.That(positionOf(graph, "a"), Is.EqualTo(Vector2.Zero));
        }
    }
}
