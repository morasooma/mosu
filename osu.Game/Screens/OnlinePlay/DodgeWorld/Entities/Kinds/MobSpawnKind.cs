// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// An area that keeps a number of mobs alive, and defines how they fight and what they drop.
    /// </summary>
    internal sealed class MobSpawnKind : WorldEntityKind<MobSpawnZone>
    {
        private static readonly Vector2 default_size = new Vector2(320, 192);

        // Ranges accepted from a stored document. A world is author-editable and round-trips through
        // the server, so values are clamped on read rather than trusted.
        public const int MIN_SPAWN_COUNT = 1;
        public const int MAX_SPAWN_COUNT = 24;
        public const int MIN_HEALTH = 1;
        public const int MAX_HEALTH = 1000;
        public const int MIN_CONTACT_DAMAGE = 1;
        public const int MAX_CONTACT_DAMAGE = 100;
        public const int MAX_REWARD = 1_000_000;
        public const int MAX_FARM_LEVEL = 1000;
        public const float MIN_DETECTION_RADIUS = 64;
        public const float MAX_DETECTION_RADIUS = 2000;
        public const int MAX_PROJECTILE_COUNT = 32;
        public const int MIN_PROJECTILE_DAMAGE = 1;
        public const int MAX_PROJECTILE_DAMAGE = 100;
        public const float MIN_PROJECTILE_SPEED = 20;
        public const float MAX_PROJECTILE_SPEED = 1000;
        public const float MIN_PROJECTILE_RANGE = 40;
        public const float MAX_PROJECTILE_RANGE = 2000;
        public const int MIN_ATTACK_COOLDOWN = 300;
        public const int MAX_ATTACK_COOLDOWN = 30_000;

        public override string Kind => EntityKinds.MOB_SPAWN;

        protected override MobSpawnZone CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new MobSpawnZone(context.Colours, context.IsEditing, context.Select, default_size);

        /// <summary>
        /// Reads a zone's fighting parameters out of a stored record, with the defaults and the clamps
        /// applied.
        /// </summary>
        /// <remarks>
        /// Shared with the server, which builds the same zones from the same document in order to run the
        /// mobs. Two implementations of these defaults would mean two different fights.
        /// </remarks>
        public static MobSpawnDefinition Describe(EntityRecord record) => new MobSpawnDefinition(
            record.Id,
            float.IsFinite(record.X) && float.IsFinite(record.Y) ? new Vector2(record.X, record.Y) : Vector2.Zero,
            new Vector2(record.Width ?? default_size.X, record.Height ?? default_size.Y),
            Count: Math.Clamp(record.SpawnCount ?? 3, MIN_SPAWN_COUNT, MAX_SPAWN_COUNT),
            MaxHealth: Math.Clamp(record.MaxHealth ?? 3, MIN_HEALTH, MAX_HEALTH),
            ContactDamage: Math.Clamp(record.ContactDamage ?? 10, MIN_CONTACT_DAMAGE, MAX_CONTACT_DAMAGE),
            DetectionRadius: Math.Clamp(record.AttackDetectionRadius ?? 420, MIN_DETECTION_RADIUS, MAX_DETECTION_RADIUS),
            ProjectileCount: Math.Clamp(record.ProjectileCount ?? 8, 0, MAX_PROJECTILE_COUNT),
            ProjectileDamage: Math.Clamp(record.ProjectileDamage ?? 8, MIN_PROJECTILE_DAMAGE, MAX_PROJECTILE_DAMAGE),
            ProjectileSpeed: Math.Clamp(record.ProjectileSpeed ?? 110, MIN_PROJECTILE_SPEED, MAX_PROJECTILE_SPEED),
            ProjectileRange: Math.Clamp(record.ProjectileRange ?? 240, MIN_PROJECTILE_RANGE, MAX_PROJECTILE_RANGE),
            AttackCooldown: Math.Clamp(record.AttackCooldownMs ?? 2200, MIN_ATTACK_COOLDOWN, MAX_ATTACK_COOLDOWN),
            Speed: Math.Clamp(record.MobSpeed ?? MobSpawnRules.DEFAULT_SPEED, MobSpawnRules.MIN_SPEED, MobSpawnRules.MAX_SPEED),
            Chases: record.MobChases ?? false);

        protected override void Read(MobSpawnZone entity, EntityRecord record, WorldEntityContext context)
        {
            MobSpawnDefinition definition = Describe(record);

            entity.SpawnCount = definition.Count;
            entity.MobMaxHealth = definition.MaxHealth;
            entity.ContactDamage = definition.ContactDamage;
            entity.DetectionRadius = definition.DetectionRadius;
            entity.ProjectileCount = definition.ProjectileCount;
            entity.ProjectileDamage = definition.ProjectileDamage;
            entity.ProjectileSpeed = definition.ProjectileSpeed;
            entity.ProjectileRange = definition.ProjectileRange;
            entity.AttackCooldown = definition.AttackCooldown;
            entity.MobSpeed = definition.Speed;
            entity.Chases = definition.Chases;

            // Rewards are not part of the fight, so they are not in the definition the simulation uses:
            // they are settled by the API when a kill is claimed.
            entity.ExperienceReward = Math.Clamp(record.ExperienceReward ?? 10, 0, MAX_REWARD);
            entity.CoinsReward = Math.Clamp(record.CoinsReward ?? 1, 0, MAX_REWARD);
            entity.MaximumFarmLevel = record.MaximumFarmLevel is null
                ? null
                : Math.Clamp(record.MaximumFarmLevel.Value, 1, MAX_FARM_LEVEL);

            // A zone's texture is the appearance of the mobs it spawns, not of the zone rectangle,
            // which the player never sees.
            entity.ReadTexture(record.Texture, record.TextureOpacity, record.TextureSmoothing);
            context.RequestTexture(entity);
        }

        protected override void Write(MobSpawnZone entity, EntityRecord record)
        {
            record.SpawnCount = entity.SpawnCount;
            record.MaxHealth = entity.MobMaxHealth;
            record.ContactDamage = entity.ContactDamage;
            record.ExperienceReward = entity.ExperienceReward;
            record.CoinsReward = entity.CoinsReward;
            record.MaximumFarmLevel = entity.MaximumFarmLevel;
            record.AttackDetectionRadius = entity.DetectionRadius;
            record.ProjectileCount = entity.ProjectileCount;
            record.ProjectileDamage = entity.ProjectileDamage;
            record.ProjectileSpeed = entity.ProjectileSpeed;
            record.ProjectileRange = entity.ProjectileRange;
            record.AttackCooldownMs = entity.AttackCooldown;
            record.MobSpeed = entity.MobSpeed;
            record.MobChases = entity.Chases;
            record.Texture = entity.TexturePath;
            record.TextureOpacity = entity.TextureOpacity;
            record.TextureSmoothing = entity.TextureSmoothing;
        }

        protected override void Initialise(EntityRecord record)
        {
            record.Width = default_size.X;
            record.Height = default_size.Y;
            record.SpawnCount = 3;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            yield return Field("Здоровье моба", "сколько ударов мечом держит",
                zone => EditorValue.Format(zone.MobMaxHealth),
                (zone, text) => zone.MobMaxHealth = EditorValue.Int(text, zone.MobMaxHealth, MIN_HEALTH, MAX_HEALTH));

            yield return Field("Контактный урон", "снимается при касании, раз в 0,7 с",
                zone => EditorValue.Format(zone.ContactDamage),
                (zone, text) => zone.ContactDamage = EditorValue.Int(text, zone.ContactDamage, MIN_CONTACT_DAMAGE, MAX_CONTACT_DAMAGE));

            yield return Field("Опыт за убийство", null,
                zone => EditorValue.Format(zone.ExperienceReward),
                (zone, text) => zone.ExperienceReward = EditorValue.Int(text, zone.ExperienceReward, 0, MAX_REWARD));

            yield return Field("Монеты за убийство", null,
                zone => EditorValue.Format(zone.CoinsReward),
                (zone, text) => zone.CoinsReward = EditorValue.Int(text, zone.CoinsReward, 0, MAX_REWARD));

            yield return Field("Макс. уровень для награды", "выше этого уровня зона не даёт награду; пусто — без ограничения",
                zone => EditorValue.Format(zone.MaximumFarmLevel),
                (zone, text) => zone.MaximumFarmLevel = EditorValue.OptionalInt(text, zone.MaximumFarmLevel, 1, MAX_FARM_LEVEL));

            yield return Field("Скорость моба", "единиц в секунду; 0 — стоит на месте",
                zone => EditorValue.Format(zone.MobSpeed),
                (zone, text) => zone.MobSpeed = EditorValue.Float(text, zone.MobSpeed, MobSpawnRules.MIN_SPEED, MobSpawnRules.MAX_SPEED));

            yield return Switch("Преследует игрока", "выключено — ходит по своей кривой внутри зоны и не реагирует на игрока",
                zone => zone.Chases, (zone, chases) => zone.Chases = chases);

            yield return Field("Радиус обнаружения", "с какого расстояния моб замечает игрока: начинает стрелять и, если преследует, идёт к нему",
                zone => EditorValue.Format(zone.DetectionRadius),
                (zone, text) => zone.DetectionRadius = EditorValue.Float(text, zone.DetectionRadius, MIN_DETECTION_RADIUS, MAX_DETECTION_RADIUS));

            yield return Field("Пуль в залпе", "0 — не стреляет. Первая летит в игрока, остальные добивают круг",
                zone => EditorValue.Format(zone.ProjectileCount),
                (zone, text) => zone.ProjectileCount = EditorValue.Int(text, zone.ProjectileCount, 0, MAX_PROJECTILE_COUNT));

            yield return Field("Урон одной пули", null,
                zone => EditorValue.Format(zone.ProjectileDamage),
                (zone, text) => zone.ProjectileDamage = EditorValue.Int(text, zone.ProjectileDamage, MIN_PROJECTILE_DAMAGE, MAX_PROJECTILE_DAMAGE));

            yield return Field("Скорость пули", "единиц в секунду",
                zone => EditorValue.Format(zone.ProjectileSpeed),
                (zone, text) => zone.ProjectileSpeed = EditorValue.Float(text, zone.ProjectileSpeed, MIN_PROJECTILE_SPEED, MAX_PROJECTILE_SPEED));

            yield return Field("Дальность пули", "единиц; дальше пуля исчезает",
                zone => EditorValue.Format(zone.ProjectileRange),
                (zone, text) => zone.ProjectileRange = EditorValue.Float(text, zone.ProjectileRange, MIN_PROJECTILE_RANGE, MAX_PROJECTILE_RANGE));

            yield return Field("Пауза между залпами", "миллисекунд; из них 650 мс — предупреждение",
                zone => EditorValue.Format(zone.AttackCooldown),
                (zone, text) => zone.AttackCooldown = EditorValue.Int(text, zone.AttackCooldown, MIN_ATTACK_COOLDOWN, MAX_ATTACK_COOLDOWN));
        }
    }
}
