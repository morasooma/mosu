// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// The root of a Dodge World save. Shared between the server document and the local preview file.
    /// </summary>
    public sealed class DodgeWorldDocument
    {
        [JsonProperty(DodgeWorldDefaults.DOCUMENT_VERSION_FIELD)]
        public int Version { get; set; } = DodgeWorldDefaults.SUPPORTED_DOCUMENT_VERSION;

        [JsonProperty("initial_room_id")]
        public string InitialRoomId { get; set; } = DodgeWorldDefaults.ROOT_ROOM_ID;

        [JsonProperty("default_weapon_skin_id")]
        public string? DefaultWeaponSkinId { get; set; }

        [JsonProperty("weapon_skins")]
        public List<WeaponSkin>? WeaponSkins { get; set; } = new List<WeaponSkin>();

        [JsonProperty("rooms")]
        public List<RoomDefinition> Rooms { get; set; } = new List<RoomDefinition>();

        /// <summary>
        /// Accepts the pre-rename field from documents written before <see cref="InitialRoomId"/> existed.
        /// </summary>
        /// <remarks>
        /// This is a semantic rename rather than a casing change, so
        /// <see cref="DodgeWorldSerializer"/> cannot derive it and it has to live here. It matters for
        /// the server path, which deserialises the response payload directly.
        /// </remarks>
        [JsonProperty("CurrentRoomId")]
        private string? LegacyCurrentRoomId
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                    InitialRoomId = value;
            }
        }

        /// <summary>
        /// Whether this document is something this client can actually open.
        /// </summary>
        [JsonIgnore]
        public bool IsSupported => Version == DodgeWorldDefaults.SUPPORTED_DOCUMENT_VERSION && Rooms.Count > 0;

        /// <summary>
        /// What one swing takes off a mob in this world, being the damage of the sword everybody holds.
        /// </summary>
        /// <remarks>
        /// Read here rather than at either end, so the client and the server cannot disagree. A world
        /// naming no sword keeps the built-in single point of damage.
        /// </remarks>
        [JsonIgnore]
        public int SwordDamage =>
            WeaponSkins?.FirstOrDefault(skin => skin.Id == DefaultWeaponSkinId)?.EffectiveDamage
            ?? WeaponSkin.DEFAULT_DAMAGE;

        /// <summary>
        /// The room to enter on load, falling back to any room present when the named one is missing.
        /// </summary>
        public RoomDefinition? ResolveInitialRoom() =>
            Rooms.FirstOrDefault(room => room.Id == InitialRoomId) ?? Rooms.FirstOrDefault();
    }
}
