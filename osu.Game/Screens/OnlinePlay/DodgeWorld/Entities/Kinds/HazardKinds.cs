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
    /// The bounds every repeating hazard is read within, shared by the emitter and the beam.
    /// </summary>
    /// <remarks>
    /// A world round-trips through the server and is edited by hand, so stored values are clamped on read
    /// rather than trusted. These are also the numbers the server reads the same document with — one
    /// implementation, so a course cannot run differently on the two sides.
    /// </remarks>
    internal static class HazardValues
    {
        public const int MIN_CYCLE = CombatRules.HAZARD_MIN_CYCLE;
        public const int MAX_CYCLE = 60_000;
        public const int MAX_PHASE = 60_000;
        public const float MAX_TURN = 360;
        public const int MIN_DAMAGE = 1;
        public const int MAX_DAMAGE = 100;

        public static float Direction(float? value) => Wrap(value ?? 0);

        /// <summary>Keeps an angle inside one turn, so a stored 3600 is not a different number from 0.</summary>
        public static float Wrap(float value)
        {
            if (!float.IsFinite(value))
                return 0;

            value %= 360;
            return value < 0 ? value + 360 : value;
        }

        public static float Turn(float? value) =>
            float.IsFinite(value ?? 0) ? Math.Clamp(value ?? 0, -MAX_TURN, MAX_TURN) : 0;

        public static int Cycle(int? value) => Math.Clamp(value ?? 2000, MIN_CYCLE, MAX_CYCLE);

        public static int Phase(int? value) => Math.Clamp(value ?? 0, 0, MAX_PHASE);

        /// <summary>
        /// Where a stored device fires from: the middle of its housing, not the point it is anchored at.
        /// </summary>
        public static Vector2 Muzzle(EntityRecord record)
        {
            Vector2 position = float.IsFinite(record.X) && float.IsFinite(record.Y)
                ? new Vector2(record.X, record.Y)
                : Vector2.Zero;

            return CombatRules.HazardMuzzle(position, record.Scale ?? 1);
        }
    }

    /// <summary>
    /// A device that fires a fan of projectiles on a loop.
    /// </summary>
    internal sealed class EmitterKind : WorldEntityKind<WorldEmitter>
    {
        public const int MAX_PROJECTILE_COUNT = 32;
        public const float MAX_SPREAD = 360;
        public const float MIN_SPEED = 20;
        public const float MAX_SPEED = 1000;
        public const float MIN_RANGE = 40;
        public const float MAX_RANGE = 4000;

        public override string Kind => EntityKinds.EMITTER;

        protected override WorldEmitter CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new WorldEmitter(context.Colours, context.IsEditing, context.Select);

        /// <summary>
        /// Reads an emitter's behaviour out of a stored record. Shared with the server, which runs the
        /// same course from the same document.
        /// </summary>
        public static HazardDefinition Describe(EntityRecord record) => new HazardDefinition(
            record.Id,
            HazardKind.Emitter,
            HazardValues.Muzzle(record),
            DirectionDegrees: HazardValues.Direction(record.HazardDirection),
            TurnDegrees: HazardValues.Turn(record.HazardTurn),
            CycleMilliseconds: HazardValues.Cycle(record.HazardCycleMs),
            PhaseMilliseconds: HazardValues.Phase(record.HazardPhaseMs),
            ProjectileCount: Math.Clamp(record.ProjectileCount ?? 5, 0, MAX_PROJECTILE_COUNT),
            SpreadDegrees: Math.Clamp(record.ProjectileSpread ?? 60, 0, MAX_SPREAD),
            Damage: Math.Clamp(record.ProjectileDamage ?? 8, HazardValues.MIN_DAMAGE, HazardValues.MAX_DAMAGE),
            ProjectileSpeed: Math.Clamp(record.ProjectileSpeed ?? 150, MIN_SPEED, MAX_SPEED),
            Range: Math.Clamp(record.ProjectileRange ?? 600, MIN_RANGE, MAX_RANGE),
            Width: 0,
            ActiveMilliseconds: 0);

        protected override void Read(WorldEmitter entity, EntityRecord record, WorldEntityContext context)
        {
            HazardDefinition definition = Describe(record);

            entity.Direction = definition.DirectionDegrees;
            entity.Turn = definition.TurnDegrees;
            entity.CycleMilliseconds = definition.CycleMilliseconds;
            entity.PhaseMilliseconds = definition.PhaseMilliseconds;
            entity.ProjectileCount = definition.ProjectileCount;
            entity.Spread = definition.SpreadDegrees;
            entity.ProjectileDamage = definition.Damage;
            entity.ProjectileSpeed = definition.ProjectileSpeed;
            entity.ProjectileRange = definition.Range;
        }

        protected override void Write(WorldEmitter entity, EntityRecord record)
        {
            record.HazardDirection = entity.Direction;
            record.HazardTurn = entity.Turn;
            record.HazardCycleMs = entity.CycleMilliseconds;
            record.HazardPhaseMs = entity.PhaseMilliseconds;
            record.ProjectileCount = entity.ProjectileCount;
            record.ProjectileSpread = entity.Spread;
            record.ProjectileDamage = entity.ProjectileDamage;
            record.ProjectileSpeed = entity.ProjectileSpeed;
            record.ProjectileRange = entity.ProjectileRange;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            yield return Field("Направление", "градусы; 0 — вправо",
                emitter => EditorValue.Format(emitter.Direction),
                (emitter, text) => emitter.Direction = HazardValues.Wrap(EditorValue.Float(text, emitter.Direction, -360, 360)));

            yield return Field("Поворот за цикл", "градусы; не ноль — получается вертушка",
                emitter => EditorValue.Format(emitter.Turn),
                (emitter, text) => emitter.Turn = EditorValue.Float(text, emitter.Turn, -HazardValues.MAX_TURN, HazardValues.MAX_TURN));

            yield return Field("Цикл", "миллисекунд между залпами",
                emitter => EditorValue.Format(emitter.CycleMilliseconds),
                (emitter, text) => emitter.CycleMilliseconds = EditorValue.Int(text, emitter.CycleMilliseconds, HazardValues.MIN_CYCLE, HazardValues.MAX_CYCLE));

            yield return Field("Сдвиг фазы", "миллисекунд; этим разводят соседние устройства",
                emitter => EditorValue.Format(emitter.PhaseMilliseconds),
                (emitter, text) => emitter.PhaseMilliseconds = EditorValue.Int(text, emitter.PhaseMilliseconds, 0, HazardValues.MAX_PHASE));

            yield return Field("Пуль в залпе", "0 — не стреляет",
                emitter => EditorValue.Format(emitter.ProjectileCount),
                (emitter, text) => emitter.ProjectileCount = EditorValue.Int(text, emitter.ProjectileCount, 0, MAX_PROJECTILE_COUNT));

            yield return Field("Разброс", "градусы; 360 — кольцо",
                emitter => EditorValue.Format(emitter.Spread),
                (emitter, text) => emitter.Spread = EditorValue.Float(text, emitter.Spread, 0, MAX_SPREAD));

            yield return Field("Урон одной пули", null,
                emitter => EditorValue.Format(emitter.ProjectileDamage),
                (emitter, text) => emitter.ProjectileDamage = EditorValue.Int(text, emitter.ProjectileDamage, HazardValues.MIN_DAMAGE, HazardValues.MAX_DAMAGE));

            yield return Field("Скорость пули", "единиц в секунду",
                emitter => EditorValue.Format(emitter.ProjectileSpeed),
                (emitter, text) => emitter.ProjectileSpeed = EditorValue.Float(text, emitter.ProjectileSpeed, MIN_SPEED, MAX_SPEED));

            yield return Field("Дальность пули", "единиц; дальше пуля исчезает",
                emitter => EditorValue.Format(emitter.ProjectileRange),
                (emitter, text) => emitter.ProjectileRange = EditorValue.Float(text, emitter.ProjectileRange, MIN_RANGE, MAX_RANGE));
        }
    }

    /// <summary>
    /// A device that holds a lethal line on and off on a loop.
    /// </summary>
    internal sealed class BeamKind : WorldEntityKind<WorldBeamDevice>
    {
        public const float MIN_LENGTH = 60;
        public const float MAX_LENGTH = 6000;
        public const float MIN_WIDTH = 8;
        public const float MAX_WIDTH = 400;
        public const int MIN_ACTIVE = 100;

        public override string Kind => EntityKinds.BEAM;

        protected override WorldBeamDevice CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new WorldBeamDevice(context.Colours, context.IsEditing, context.Select);

        /// <summary>
        /// Reads a beam's behaviour out of a stored record. Shared with the server for the same reason the
        /// emitter's is.
        /// </summary>
        public static HazardDefinition Describe(EntityRecord record)
        {
            int cycle = HazardValues.Cycle(record.HazardCycleMs);

            return new HazardDefinition(
                record.Id,
                HazardKind.Beam,
                HazardValues.Muzzle(record),
                DirectionDegrees: HazardValues.Direction(record.HazardDirection),
                TurnDegrees: HazardValues.Turn(record.HazardTurn),
                CycleMilliseconds: cycle,
                PhaseMilliseconds: HazardValues.Phase(record.HazardPhaseMs),
                ProjectileCount: 0,
                SpreadDegrees: 0,
                Damage: Math.Clamp(record.ContactDamage ?? 12, HazardValues.MIN_DAMAGE, HazardValues.MAX_DAMAGE),
                ProjectileSpeed: 0,
                Range: Math.Clamp(record.BeamLength ?? 700, MIN_LENGTH, MAX_LENGTH),
                Width: Math.Clamp(record.BeamWidth ?? 46, MIN_WIDTH, MAX_WIDTH),
                // A beam on for its whole cycle is a wall, and there would be no moment to cross it.
                ActiveMilliseconds: Math.Clamp(record.BeamActiveMs ?? 900, MIN_ACTIVE, cycle - CombatRules.HAZARD_MIN_CYCLE / 2));
        }

        protected override void Read(WorldBeamDevice entity, EntityRecord record, WorldEntityContext context)
        {
            HazardDefinition definition = Describe(record);

            entity.Direction = definition.DirectionDegrees;
            entity.Turn = definition.TurnDegrees;
            entity.CycleMilliseconds = definition.CycleMilliseconds;
            entity.PhaseMilliseconds = definition.PhaseMilliseconds;
            entity.Length = definition.Range;
            entity.Width = definition.Width;
            entity.ActiveMilliseconds = definition.ActiveMilliseconds;
            entity.Damage = definition.Damage;
        }

        protected override void Write(WorldBeamDevice entity, EntityRecord record)
        {
            record.HazardDirection = entity.Direction;
            record.HazardTurn = entity.Turn;
            record.HazardCycleMs = entity.CycleMilliseconds;
            record.HazardPhaseMs = entity.PhaseMilliseconds;
            record.BeamLength = entity.Length;
            record.BeamWidth = entity.Width;
            record.BeamActiveMs = entity.ActiveMilliseconds;
            record.ContactDamage = entity.Damage;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            yield return Field("Направление", "градусы; 0 — вправо",
                beam => EditorValue.Format(beam.Direction),
                (beam, text) => beam.Direction = HazardValues.Wrap(EditorValue.Float(text, beam.Direction, -360, 360)));

            yield return Field("Поворот за цикл", "градусы; не ноль — луч подметает",
                beam => EditorValue.Format(beam.Turn),
                (beam, text) => beam.Turn = EditorValue.Float(text, beam.Turn, -HazardValues.MAX_TURN, HazardValues.MAX_TURN));

            yield return Field("Цикл", "миллисекунд на одно включение и выключение",
                beam => EditorValue.Format(beam.CycleMilliseconds),
                (beam, text) => beam.CycleMilliseconds = EditorValue.Int(text, beam.CycleMilliseconds, HazardValues.MIN_CYCLE, HazardValues.MAX_CYCLE));

            yield return Field("Сдвиг фазы", "миллисекунд; этим разводят соседние устройства",
                beam => EditorValue.Format(beam.PhaseMilliseconds),
                (beam, text) => beam.PhaseMilliseconds = EditorValue.Int(text, beam.PhaseMilliseconds, 0, HazardValues.MAX_PHASE));

            yield return Field("Сколько горит", "миллисекунд из цикла; последние 600 мс перед включением — предупреждение",
                beam => EditorValue.Format(beam.ActiveMilliseconds),
                (beam, text) => beam.ActiveMilliseconds = EditorValue.Int(text, beam.ActiveMilliseconds, MIN_ACTIVE, HazardValues.MAX_CYCLE));

            yield return Field("Длина", "единиц",
                beam => EditorValue.Format(beam.Length),
                (beam, text) => beam.Length = EditorValue.Float(text, beam.Length, MIN_LENGTH, MAX_LENGTH));

            yield return Field("Толщина", "единиц",
                beam => EditorValue.Format(beam.Width),
                (beam, text) => beam.Width = EditorValue.Float(text, beam.Width, MIN_WIDTH, MAX_WIDTH));

            yield return Field("Урон", "снимается не чаще раза в 0,7 с",
                beam => EditorValue.Format(beam.Damage),
                (beam, text) => beam.Damage = EditorValue.Int(text, beam.Damage, HazardValues.MIN_DAMAGE, HazardValues.MAX_DAMAGE));
        }
    }
}
