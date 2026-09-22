// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// The wire representation of a single placed world object.
    /// </summary>
    /// <remarks>
    /// This is deliberately a flat, permissive bag of nullable fields rather than a polymorphic
    /// hierarchy: the shape is a server contract, and published worlds already store documents in
    /// it. A null field means "this kind does not use the field", or "use the kind's default".
    /// <para>
    /// Reading and writing these fields is the job of the matching
    /// <see cref="Entities.IWorldEntityKind"/>, so a kind stays the single place that knows which
    /// subset it owns. Nothing outside <c>Entities</c> should branch on <see cref="Kind"/>.
    /// </para>
    /// </remarks>
    public sealed class EntityRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Discriminator naming the <see cref="Entities.IWorldEntityKind"/> that owns this record.
        /// </summary>
        [JsonProperty("kind")]
        public string Kind { get; set; } = "entity";

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float? Width { get; set; }

        [JsonProperty("height")]
        public float? Height { get; set; }

        [JsonProperty("style")]
        public int? Style { get; set; }

        [JsonProperty("rounded")]
        public bool? Rounded { get; set; }

        [JsonProperty("scale")]
        public float? Scale { get; set; }

        [JsonProperty("corner_radius")]
        public float? CornerRadius { get; set; }

        [JsonProperty("texture")]
        public string? Texture { get; set; }

        [JsonProperty("texture_fill")]
        public int? TextureFill { get; set; }

        [JsonProperty("texture_opacity")]
        public float? TextureOpacity { get; set; }

        [JsonProperty("texture_smoothing")]
        public bool? TextureSmoothing { get; set; }

        [JsonProperty("destination")]
        public string? Destination { get; set; }

        [JsonProperty("destination_room_id")]
        public string? DestinationRoomId { get; set; }

        [JsonProperty("points_right")]
        public bool? PointsRight { get; set; }

        /// <summary>
        /// Which way the object is turned, in degrees clockwise. Only kinds that do not block movement
        /// carry one, because collision is resolved against upright boxes.
        /// </summary>
        [JsonProperty("facing")]
        public float? Facing { get; set; }

        /// <summary>
        /// The object in the destination room the player appears beside, for a passage. Null lets the
        /// passage find the way back by itself, and failing that the room's entrance.
        /// </summary>
        [JsonProperty("arrival_entity_id")]
        public string? ArrivalEntityId { get; set; }

        /// <summary>
        /// Where a passage leaves the player, when the author placed the spot by hand. Overrides both
        /// <see cref="ArrivalEntityId"/> and the way back.
        /// </summary>
        [JsonProperty("arrival_x")]
        public float? ArrivalX { get; set; }

        [JsonProperty("arrival_y")]
        public float? ArrivalY { get; set; }

        /// <summary>
        /// Whether a talkable entity is a living character. False makes it scenery with something to say:
        /// no name over it, and a prompt that reads as an action. Null means alive.
        /// </summary>
        [JsonProperty("alive")]
        public bool? Alive { get; set; }

        /// <summary>Whether the entity is drawn standing on a shadow. Null means it is.</summary>
        [JsonProperty("shadow")]
        public bool? Shadow { get; set; }

        [JsonProperty("spawn_count")]
        public int? SpawnCount { get; set; }

        [JsonProperty("max_health")]
        public int? MaxHealth { get; set; }

        [JsonProperty("contact_damage")]
        public int? ContactDamage { get; set; }

        [JsonProperty("experience_reward")]
        public int? ExperienceReward { get; set; }

        [JsonProperty("coins_reward")]
        public int? CoinsReward { get; set; }

        [JsonProperty("maximum_farm_level")]
        public int? MaximumFarmLevel { get; set; }

        [JsonProperty("attack_detection_radius")]
        public float? AttackDetectionRadius { get; set; }

        [JsonProperty("projectile_count")]
        public int? ProjectileCount { get; set; }

        [JsonProperty("projectile_damage")]
        public int? ProjectileDamage { get; set; }

        [JsonProperty("projectile_speed")]
        public float? ProjectileSpeed { get; set; }

        [JsonProperty("projectile_range")]
        public float? ProjectileRange { get; set; }

        [JsonProperty("attack_cooldown_ms")]
        public int? AttackCooldownMs { get; set; }

        /// <summary>How fast this zone's mobs move, in world units per second. Zero holds them still.</summary>
        [JsonProperty("mob_speed")]
        public float? MobSpeed { get; set; }

        /// <summary>
        /// Whether this zone's mobs walk towards a player they have noticed. Null means they only patrol,
        /// which is how every mob behaved before a zone could say otherwise.
        /// </summary>
        [JsonProperty("mob_chases")]
        public bool? MobChases { get; set; }

        /// <summary>
        /// Which way a repeating hazard points, in degrees. Zero is to the right, as elsewhere.
        /// </summary>
        [JsonProperty("hazard_direction")]
        public float? HazardDirection { get; set; }

        /// <summary>
        /// Degrees added to a hazard's direction on every cycle. Non-zero turns a fan into a spinner and a
        /// beam into a sweep.
        /// </summary>
        [JsonProperty("hazard_turn")]
        public float? HazardTurn { get; set; }

        /// <summary>How long one repetition of a hazard takes, in milliseconds.</summary>
        [JsonProperty("hazard_cycle_ms")]
        public int? HazardCycleMs { get; set; }

        /// <summary>
        /// Offset into the cycle, in milliseconds. What staggers several hazards into a pattern instead of
        /// firing them all at once.
        /// </summary>
        [JsonProperty("hazard_phase_ms")]
        public int? HazardPhaseMs { get; set; }

        /// <summary>The arc an emitter's fan covers, in degrees. 360 makes a ring.</summary>
        [JsonProperty("projectile_spread")]
        public float? ProjectileSpread { get; set; }

        /// <summary>The length of a beam's line, in world units.</summary>
        [JsonProperty("beam_length")]
        public float? BeamLength { get; set; }

        /// <summary>The thickness of a beam's line, in world units.</summary>
        [JsonProperty("beam_width")]
        public float? BeamWidth { get; set; }

        /// <summary>How long a beam is lethal within each cycle, in milliseconds.</summary>
        [JsonProperty("beam_active_ms")]
        public int? BeamActiveMs { get; set; }

        /// <summary>
        /// The story flag this entity records once the player is done with it — finishing an NPC's
        /// conversation, for now. Null for everything that is not a story point.
        /// </summary>
        [JsonProperty("story_flag")]
        public string? StoryFlag { get; set; }

        /// <summary>
        /// What <see cref="StoryFlag"/> is raised to. Raised, never set: reaching the same point twice is
        /// not progress twice, and progress does not go backwards.
        /// </summary>
        [JsonProperty("story_flag_value")]
        public int? StoryFlagValue { get; set; }

        /// <summary>
        /// Experience paid once, the first time this story point is reached. Zero or null pays nothing.
        /// </summary>
        /// <remarks>
        /// Safe to pay because reaching the same point twice is not progress twice: the server raises the
        /// flag to a floor, so the second visit reports nothing applied and pays nothing.
        /// </remarks>
        [JsonProperty("story_experience")]
        public int? StoryExperience { get; set; }

        /// <summary>Coins paid once, on the same terms as <see cref="StoryExperience"/>.</summary>
        [JsonProperty("story_coins")]
        public int? StoryCoins { get; set; }

        /// <summary>
        /// The flag deciding whether this entity exists for a player at all. Null means always.
        /// </summary>
        [JsonProperty("visible_if_flag")]
        public string? VisibleIfFlag { get; set; }

        /// <summary>The lowest value of <see cref="VisibleIfFlag"/> that shows this entity. Defaults to one.</summary>
        [JsonProperty("visible_if_at_least")]
        public long? VisibleIfAtLeast { get; set; }

        /// <summary>
        /// The value of <see cref="VisibleIfFlag"/> at which this entity disappears again, for something
        /// that belongs to one chapter only.
        /// </summary>
        [JsonProperty("visible_if_below")]
        public long? VisibleIfBelow { get; set; }

        /// <summary>
        /// Coins charged once, per player, to open a warp. Zero for the starting locations.
        /// </summary>
        [JsonProperty("warp_unlock_cost")]
        public int? WarpUnlockCost { get; set; }

        /// <summary>
        /// Coins charged every time a player travels to this warp.
        /// </summary>
        [JsonProperty("warp_travel_cost")]
        public int? WarpTravelCost { get; set; }

        /// <summary>
        /// Legacy single-language dialogue. Superseded by <see cref="DialogueLocalized"/>, but still
        /// written by older clients, so it is read as the English page set.
        /// </summary>
        [JsonProperty("dialogue")]
        public string[]? Dialogue { get; set; }

        /// <summary>
        /// The pages of the first branch, per language. This is the whole conversation of a character with
        /// one branch, which is every character published before branches existed, and it is still written
        /// alongside <see cref="DialogueBranches"/> so that an older client shows something.
        /// </summary>
        [JsonProperty("dialogue_localized")]
        public Dictionary<string, string[]>? DialogueLocalized { get; set; }

        /// <summary>
        /// Every branch of the conversation, per language: one entry per branch, each a list of pages. A
        /// branch is read through in order, and the next conversation takes the next branch, coming back
        /// round to the first.
        /// </summary>
        /// <remarks>
        /// Written only by a character that has more than one branch, so a world full of one-branch
        /// characters keeps exactly the shape it had. When it is present it holds the first branch too,
        /// and <see cref="DialogueLocalized"/> is ignored.
        /// </remarks>
        [JsonProperty("dialogue_branches")]
        public Dictionary<string, string[][]>? DialogueBranches { get; set; }
    }
}
