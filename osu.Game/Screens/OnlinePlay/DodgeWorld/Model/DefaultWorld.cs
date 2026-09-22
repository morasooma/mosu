// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// The world a client starts from when there is nothing stored yet, and the template for rooms
    /// added through the editor.
    /// </summary>
    /// <remarks>
    /// This is deliberately expressed as a document rather than as constructed drawables. The layout
    /// used to exist only as a sequence of constructor calls, which meant the built-in room and a
    /// loaded room went through different code paths and could not be compared, saved or tested alike.
    /// </remarks>
    public static class DefaultWorld
    {
        public const string ROOT_ROOM_NAME = "Mora Plaza";

        /// <summary>
        /// A fresh single-room world containing Mora, the Dodge gate and the two side passages.
        /// </summary>
        public static DodgeWorldDocument CreateDocument() => new DodgeWorldDocument
        {
            InitialRoomId = DodgeWorldDefaults.ROOT_ROOM_ID,
            Rooms = new List<RoomDefinition> { CreateRootRoom() },
        };

        public static RoomDefinition CreateRootRoom() => new RoomDefinition
        {
            Id = DodgeWorldDefaults.ROOT_ROOM_ID,
            Name = ROOT_ROOM_NAME,
            Width = DodgeWorldDefaults.MAP_SIZE.X,
            Height = DodgeWorldDefaults.MAP_SIZE.Y,
            SpawnX = DodgeWorldDefaults.PLAYER_SPAWN.X,
            SpawnY = DodgeWorldDefaults.PLAYER_SPAWN.Y,
            Entities = new List<EntityRecord>
            {
                // corner_radius is intentionally omitted: a surface with rounded set derives its radius
                // from its own size, so leaving it unset keeps the built-in look in one place.
                corridor("surface-horizontal", "Horizontal corridor", 2080, 176),
                corridor("surface-vertical", "Vertical corridor", 190, 980),
                new EntityRecord
                {
                    Id = "surface-plaza", Kind = EntityKinds.SURFACE, DisplayName = "Central plaza",
                    Width = 1250, Height = 720, Style = 0, Rounded = true,
                },
                new EntityRecord { Id = "mora", Kind = EntityKinds.MORA, DisplayName = "Mora", X = 0, Y = 70 },
                new EntityRecord
                {
                    Id = "dodge-gate", Kind = EntityKinds.PORTAL, DisplayName = "Dodge Portal",
                    X = 0, Y = -330, Destination = "Dodge Gate",
                },
                // The starting warp is free both ways: a hub the player can always return to is what
                // makes the paid ones worth opening.
                new EntityRecord
                {
                    Id = "plaza-warp", Kind = EntityKinds.WARP, DisplayName = "Plaza",
                    X = 250, Y = 300, WarpUnlockCost = 0, WarpTravelCost = 0,
                },
                new EntityRecord { Id = "accessory-shop", Kind = EntityKinds.TERMINAL, DisplayName = "Accessory Shop", X = -520, Y = 40 },
                new EntityRecord { Id = "quest-board", Kind = EntityKinds.TERMINAL, DisplayName = "Quest Board", X = 520, Y = 40 },
                sidePassage("west-passage", "West Passage", "Future West District", x: -1010, pointsRight: false),
                sidePassage("east-passage", "East Passage", "Future East District", x: 1010, pointsRight: true),
            },
        };

        /// <summary>
        /// A blank room reachable from an existing one, with a portal leading back.
        /// </summary>
        public static RoomDefinition CreateConnectedRoom(string id, string name, string returnRoomId, string returnRoomName) => new RoomDefinition
        {
            Id = id,
            Name = name,
            Width = DodgeWorldDefaults.MAP_SIZE.X,
            Height = DodgeWorldDefaults.MAP_SIZE.Y,
            SpawnX = DodgeWorldDefaults.PLAYER_SPAWN.X,
            SpawnY = DodgeWorldDefaults.PLAYER_SPAWN.Y,
            Entities = new List<EntityRecord>
            {
                corridor("surface-horizontal", "Horizontal corridor", 2080, 176),
                corridor("surface-vertical", "Vertical corridor", 190, 980),
                new EntityRecord
                {
                    Id = "surface-plaza", Kind = EntityKinds.SURFACE, DisplayName = "Central plaza",
                    Width = 1250, Height = 720, Style = 1, Rounded = true, CornerRadius = 76,
                },
                new EntityRecord
                {
                    Id = $"return-{returnRoomId}", Kind = EntityKinds.PORTAL, DisplayName = $"Return to {returnRoomName}",
                    X = 0, Y = -330, Destination = returnRoomName, DestinationRoomId = returnRoomId,
                },
                // A new room comes with a warp, unpriced, so the author only has to set what it costs.
                new EntityRecord
                {
                    Id = $"warp-{id}", Kind = EntityKinds.WARP, DisplayName = name,
                    X = 250, Y = 300, WarpUnlockCost = 0, WarpTravelCost = 0,
                },
                sidePassage("west-passage", "West passage", null, x: -1010, pointsRight: false),
                sidePassage("east-passage", "East passage", null, x: 1010, pointsRight: true),
            },
        };

        private static EntityRecord corridor(string id, string displayName, float width, float height) => new EntityRecord
        {
            Id = id,
            Kind = EntityKinds.SURFACE,
            DisplayName = displayName,
            Width = width,
            Height = height,
            Style = 0,
            Rounded = false,
        };

        private static EntityRecord sidePassage(string id, string displayName, string? destination, float x, bool pointsRight) => new EntityRecord
        {
            Id = id,
            Kind = EntityKinds.PASSAGE,
            DisplayName = displayName,
            X = x,
            Y = 20,
            Destination = destination,
            PointsRight = pointsRight,
        };
    }
}
