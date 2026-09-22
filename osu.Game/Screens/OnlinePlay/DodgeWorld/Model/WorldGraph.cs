// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// One room on the world map.
    /// </summary>
    /// <param name="RoomId">The room this node stands for.</param>
    /// <param name="Name">Its name, or its id when it has none.</param>
    /// <param name="Position">Where to draw it, in map units.</param>
    /// <param name="Placed">
    /// Whether <paramref name="Position"/> was authored. False means it was worked out from the links,
    /// and dragging the node is what turns it into an authored position.
    /// </param>
    /// <param name="Warps">How many warps stand in the room.</param>
    internal readonly record struct WorldGraphNode(string RoomId, string Name, Vector2 Position, bool Placed, int Warps);

    /// <summary>
    /// A way between two rooms. Undirected: a passage and the portal leading back are one link.
    /// </summary>
    internal readonly record struct WorldGraphLink(string FromRoomId, string ToRoomId);

    /// <summary>
    /// The rooms of a world and the ways between them.
    /// </summary>
    /// <remarks>
    /// The links come out of the document as it already stands: a portal or side passage carries the id
    /// of the room it leads to. Positions are the part the document need not have — a room may carry a
    /// hand-placed <see cref="RoomDefinition.MapPosition"/>, and anything without one is laid out from
    /// the links, so a world published before the map existed still draws.
    /// </remarks>
    internal sealed class WorldGraph
    {
        /// <summary>
        /// The gap the automatic layout leaves between neighbouring rooms, in map units.
        /// </summary>
        public static readonly Vector2 SPACING = new Vector2(260, 190);

        public IReadOnlyList<WorldGraphNode> Nodes { get; }

        public IReadOnlyList<WorldGraphLink> Links { get; }

        private WorldGraph(IReadOnlyList<WorldGraphNode> nodes, IReadOnlyList<WorldGraphLink> links)
        {
            Nodes = nodes;
            Links = links;
        }

        public static WorldGraph Describe(IEnumerable<RoomDefinition> rooms, string rootRoomId)
        {
            RoomDefinition[] all = rooms.ToArray();
            var byId = new Dictionary<string, RoomDefinition>();

            foreach (RoomDefinition room in all)
                byId.TryAdd(room.Id, room);

            var links = new List<WorldGraphLink>();
            var seen = new HashSet<(string, string)>();

            foreach (RoomDefinition room in all.OrderBy(room => room.Id, StringComparer.Ordinal))
            {
                foreach (string destination in exitsOf(room))
                {
                    if (destination == room.Id || !byId.ContainsKey(destination))
                        continue;

                    // Ordered so that a passage and the portal leading back collapse into one link.
                    (string, string) key = string.CompareOrdinal(room.Id, destination) <= 0
                        ? (room.Id, destination)
                        : (destination, room.Id);

                    if (seen.Add(key))
                        links.Add(new WorldGraphLink(key.Item1, key.Item2));
                }
            }

            return new WorldGraph(layOut(all, byId, rootRoomId), links);
        }

        /// <summary>
        /// The ids of the rooms this one leads to, in the left-to-right order of the exits themselves,
        /// so that the room reached through the west passage is laid out to the west.
        /// </summary>
        private static IEnumerable<string> exitsOf(RoomDefinition room) => room.Entities
            .Where(entity => entity.Kind is EntityKinds.PORTAL or EntityKinds.PASSAGE)
            .Where(entity => !string.IsNullOrWhiteSpace(entity.DestinationRoomId))
            .OrderBy(entity => float.IsFinite(entity.X) ? entity.X : 0)
            .Select(entity => entity.DestinationRoomId!);

        private static IReadOnlyList<WorldGraphNode> layOut(RoomDefinition[] all, Dictionary<string, RoomDefinition> byId, string rootRoomId)
        {
            var placed = new Dictionary<string, Vector2>();
            var pending = new List<List<string>>();
            var visited = new HashSet<string>();

            string start = byId.ContainsKey(rootRoomId) ? rootRoomId : all.FirstOrDefault()?.Id ?? string.Empty;

            // Breadth first from the root, one row per step away from it, so that distance on the map
            // means distance in the world. Ties break on id, which keeps the layout stable between runs.
            var frontier = new List<string>();

            if (byId.ContainsKey(start))
            {
                frontier.Add(start);
                visited.Add(start);
            }

            while (frontier.Count > 0)
            {
                pending.Add(frontier);
                var next = new List<string>();

                foreach (string roomId in frontier)
                {
                    foreach (string destination in exitsOf(byId[roomId]))
                    {
                        if (byId.ContainsKey(destination) && visited.Add(destination))
                            next.Add(destination);
                    }
                }

                frontier = next;
            }

            // Rooms with no path from the root still have to appear somewhere, or a mistake in the
            // links would make a room invisible rather than obviously stranded.
            string[] stranded = all.Select(room => room.Id)
                                   .Where(id => !visited.Contains(id))
                                   .OrderBy(id => id, StringComparer.Ordinal)
                                   .ToArray();

            if (stranded.Length > 0)
                pending.Add(stranded.ToList());

            for (int row = 0; row < pending.Count; row++)
            {
                List<string> occupants = pending[row];

                for (int column = 0; column < occupants.Count; column++)
                {
                    float offset = column - (occupants.Count - 1) / 2f;
                    placed[occupants[column]] = new Vector2(offset * SPACING.X, row * SPACING.Y);
                }
            }

            return all.Select(room => new WorldGraphNode(
                          room.Id,
                          string.IsNullOrWhiteSpace(room.Name) ? room.Id : room.Name,
                          room.MapPosition ?? placed.GetValueOrDefault(room.Id),
                          room.MapPosition.HasValue,
                          room.Entities.Count(entity => entity.Kind == EntityKinds.WARP)))
                      .ToArray();
        }
    }
}
