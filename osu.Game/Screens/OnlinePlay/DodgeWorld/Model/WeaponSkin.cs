// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// A sword appearance, including the geometry needed to animate a swing with it.
    /// </summary>
    public sealed class WeaponSkin
    {
        public const string FALLBACK_ID = "builtin-default";

        /// <summary>What a swing takes off a mob when the world does not say.</summary>
        public const int DEFAULT_DAMAGE = 1;

        public const int MIN_DAMAGE = 1;

        /// <summary>Enough to end any authorable mob in one swing; past that the number is decoration.</summary>
        public const int MAX_DAMAGE = 1000;

        [JsonProperty("id")]
        public string Id { get; set; } = "default";

        [JsonProperty("display_name")]
        public string? DisplayName { get; set; }

        [JsonProperty("texture")]
        public string? Texture { get; set; }

        [JsonProperty("display_width")]
        public float DisplayWidth { get; set; } = 104;

        /// <summary>
        /// Horizontal grip point within the texture, as a fraction of its width.
        /// </summary>
        [JsonProperty("pivot_x")]
        public float PivotX { get; set; } = 0.78f;

        /// <summary>
        /// Vertical grip point within the texture, as a fraction of its height.
        /// </summary>
        [JsonProperty("pivot_y")]
        public float PivotY { get; set; } = 0.5f;

        /// <summary>
        /// Rotation applied to the blade so that it points along the swing direction.
        /// </summary>
        [JsonProperty("blade_direction_degrees")]
        public float BladeDirectionDegrees { get; set; } = 180;

        /// <summary>
        /// What one swing with this sword takes off a mob.
        /// </summary>
        /// <remarks>
        /// On the sword rather than in the rules, so that swords can differ. The world's default one is
        /// what everybody currently holds.
        /// </remarks>
        [JsonProperty("damage")]
        public int Damage { get; set; } = DEFAULT_DAMAGE;

        /// <summary><see cref="Damage"/> clamped on read, as every other authored number is.</summary>
        [JsonIgnore]
        public int EffectiveDamage => Math.Clamp(Damage, MIN_DAMAGE, MAX_DAMAGE);

        public static WeaponSkin CreateFallback() => new WeaponSkin
        {
            Id = FALLBACK_ID,
            DisplayName = "Built-in temporary sword",
        };
    }
}
