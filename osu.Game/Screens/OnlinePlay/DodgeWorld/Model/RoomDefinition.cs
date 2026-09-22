// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// One traversable area of the world, and everything placed in it.
    /// </summary>
    public sealed class RoomDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("width")]
        public float? Width { get; set; } = DodgeWorldDefaults.MAP_SIZE.X;

        [JsonProperty("height")]
        public float? Height { get; set; } = DodgeWorldDefaults.MAP_SIZE.Y;

        [JsonProperty("spawn_x")]
        public float SpawnX { get; set; }

        [JsonProperty("spawn_y")]
        public float SpawnY { get; set; } = DodgeWorldDefaults.PLAYER_SPAWN.Y;

        /// <summary>
        /// Where this room sits on the world map, or null to let the map place it automatically.
        /// </summary>
        /// <remarks>
        /// Not a world coordinate: this is the room's own position on the map of rooms, in map units.
        /// Absent from every world published before the map existed, which is why it is optional —
        /// an unplaced room is laid out from its links instead.
        /// </remarks>
        [JsonProperty("map_x")]
        public float? MapX { get; set; }

        [JsonProperty("map_y")]
        public float? MapY { get; set; }

        [JsonProperty("entities")]
        public List<EntityRecord> Entities { get; set; } = new List<EntityRecord>();

        /// <summary>
        /// Room bounds, falling back to the default map size when the document omits them.
        /// </summary>
        [JsonIgnore]
        public Vector2 Size => new Vector2(Width ?? DodgeWorldDefaults.MAP_SIZE.X, Height ?? DodgeWorldDefaults.MAP_SIZE.Y);

        [JsonIgnore]
        public Vector2 Spawn => new Vector2(SpawnX, SpawnY);

        /// <summary>
        /// The hand-placed map position, if this room has one and it is usable.
        /// </summary>
        [JsonIgnore]
        public Vector2? MapPosition => MapX.HasValue && MapY.HasValue
                                       && float.IsFinite(MapX.Value) && float.IsFinite(MapY.Value)
            ? new Vector2(MapX.Value, MapY.Value)
            : null;
    }
}
