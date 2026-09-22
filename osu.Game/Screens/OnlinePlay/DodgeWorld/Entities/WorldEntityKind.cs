// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities
{
    /// <summary>
    /// Handles the fields every entity shares, leaving a kind to describe only what is specific to it.
    /// </summary>
    /// <typeparam name="T">The drawable this kind produces.</typeparam>
    internal abstract class WorldEntityKind<T> : IWorldEntityKind
        where T : EditableWorldEntity
    {
        private IReadOnlyList<EntityField>? fields;

        public abstract string Kind { get; }

        public IReadOnlyList<EntityField> Fields =>
            fields ??= DescribeFields().Concat(rotationField()).Concat(storyFields()).ToArray();

        /// <summary>
        /// Whether this kind can be turned by the author. Declared on the kind rather than read from a
        /// drawable, because the field list is built before any of them exists.
        /// </summary>
        protected virtual bool SupportsRotation => false;

        private IEnumerable<EntityField> rotationField()
        {
            if (!SupportsRotation)
                yield break;

            yield return new EntityField("Поворот", "градусы по часовой; 0 — как нарисовано",
                entity => EditorValue.Format(entity.FacingDegrees),
                (entity, text) => entity.FacingDegrees = EditorValue.Float(text, entity.FacingDegrees, -360, 360));
        }

        /// <summary>
        /// The story properties every entity has, so that anything can be gated or can record progress
        /// without each kind repeating the same four fields.
        /// </summary>
        private static IEnumerable<EntityField> storyFields()
        {
            yield return new EntityField("Ставит флаг", "имя вроде mora.met; пусто — объект не часть сюжета",
                entity => entity.StoryFlag ?? string.Empty,
                (entity, text) => entity.StoryFlag = string.IsNullOrWhiteSpace(text) ? null : text.Trim());

            yield return new EntityField("Значение флага", "флаг поднимается до этого числа, а не прибавляется",
                entity => EditorValue.Format(entity.StoryFlagValue),
                (entity, text) => entity.StoryFlagValue = EditorValue.Int(text, entity.StoryFlagValue, 1, 1_000_000));

            yield return new EntityField("Награда: опыт", "выдаётся один раз, когда флаг поднялся впервые",
                entity => EditorValue.Format(entity.StoryExperience),
                (entity, text) => entity.StoryExperience = EditorValue.Int(text, entity.StoryExperience, 0, 1_000_000));

            yield return new EntityField("Награда: монеты", "тоже один раз; повторный разговор не платит",
                entity => EditorValue.Format(entity.StoryCoins),
                (entity, text) => entity.StoryCoins = EditorValue.Int(text, entity.StoryCoins, 0, 1_000_000));

            yield return new EntityField("Виден, если флаг", "пусто — виден всегда",
                entity => entity.Visibility.Flag ?? string.Empty,
                (entity, text) => entity.Visibility = Model.StoryCondition.Read(text, entity.Visibility.AtLeast,
                    entity.Visibility.Below));

            yield return new EntityField("не меньше", "значение флага, с которого объект появляется",
                entity => EditorValue.Format((int)entity.Visibility.AtLeast),
                (entity, text) => entity.Visibility = entity.Visibility with
                {
                    AtLeast = EditorValue.Int(text, (int)entity.Visibility.AtLeast, 0, 1_000_000),
                });

            yield return new EntityField("и меньше", "значение, с которого объект снова исчезает; пусто — не исчезает",
                entity => entity.Visibility.Below is long below ? EditorValue.Format((int)below) : string.Empty,
                (entity, text) => entity.Visibility = entity.Visibility with
                {
                    Below = EditorValue.OptionalInt(text, (int?)entity.Visibility.Below, 0, 1_000_000),
                });
        }

        protected abstract T CreateEntity(EntityRecord record, WorldEntityContext context);

        /// <summary>
        /// Declares the editable properties of this kind, in the order they should be shown.
        /// </summary>
        protected virtual IEnumerable<EntityField> DescribeFields() => Enumerable.Empty<EntityField>();

        /// <summary>
        /// Declares one property, saving each kind from having to cast the entity itself.
        /// </summary>
        protected static EntityField Field(string label, string? hint, Func<T, string> read, Action<T, string> write) =>
            new EntityField(label, hint, entity => read((T)entity), (entity, value) => write((T)entity, value));

        /// <summary>
        /// Declares a yes-or-no property, which the panel shows as a switch rather than as a text box
        /// holding the word «да».
        /// </summary>
        protected static EntityField Switch(string label, string? hint, Func<T, bool> read, Action<T, bool> write) =>
            new EntityField(label, hint,
                entity => EditorValue.Format(read((T)entity)),
                (entity, value) => write((T)entity, EditorValue.Bool(value, read((T)entity))),
                Toggle: true);

        /// <summary>
        /// Reads the fields this kind owns out of <paramref name="record"/>.
        /// </summary>
        protected virtual void Read(T entity, EntityRecord record, WorldEntityContext context)
        {
        }

        /// <summary>
        /// Writes the fields this kind owns into <paramref name="record"/>.
        /// </summary>
        protected virtual void Write(T entity, EntityRecord record)
        {
        }

        /// <summary>
        /// Defaults for a newly created entity of this kind, beyond the shared fields.
        /// </summary>
        protected virtual void Initialise(EntityRecord record)
        {
        }

        public EditableWorldEntity Create(EntityRecord record, WorldEntityContext context)
        {
            T entity = CreateEntity(record, context);

            entity.EntityId = record.Id;
            entity.SetDisplayName(record.DisplayName ?? record.Kind);
            entity.StoryFlag = string.IsNullOrWhiteSpace(record.StoryFlag) ? null : record.StoryFlag.Trim();
            entity.StoryFlagValue = Math.Clamp(record.StoryFlagValue ?? 1, 1, 1_000_000);
            entity.StoryExperience = Math.Clamp(record.StoryExperience ?? 0, 0, 1_000_000);
            entity.StoryCoins = Math.Clamp(record.StoryCoins ?? 0, 0, 1_000_000);
            entity.Visibility = Model.StoryCondition.Read(record.VisibleIfFlag, record.VisibleIfAtLeast,
                record.VisibleIfBelow);

            if (entity.CanRotate && record.Facing is float facing)
                entity.FacingDegrees = facing;

            Read(entity, record, context);

            // Corrupt coordinates leave the entity at the origin rather than propagating NaN through
            // collision and camera maths. Everything else about it is still applied.
            if (float.IsFinite(record.X) && float.IsFinite(record.Y))
                entity.Position = new Vector2(record.X, record.Y);

            entity.ApplySavedEditorState(record.Width, record.Height, record.Style, record.Rounded, record.Scale);

            return entity;
        }

        public EntityRecord Capture(EditableWorldEntity entity)
        {
            var typed = (T)entity;

            var record = new EntityRecord
            {
                Id = entity.EntityId,
                Kind = Kind,
                DisplayName = entity.DisplayName,
                X = entity.Position.X,
                Y = entity.Position.Y,
                Width = entity.CanResize ? entity.Size.X : null,
                Height = entity.CanResize ? entity.Size.Y : null,
                Style = entity.CanStyle ? entity.StyleIndex : null,
                Rounded = entity.CanRound ? entity.Rounded : null,
                Scale = entity.CanScale ? entity.Scale.X : null,
                Facing = entity.CanRotate && entity.FacingDegrees != 0 ? entity.FacingDegrees : null,
                StoryFlag = entity.StoryFlag,
                StoryFlagValue = entity.StoryFlag == null ? null : entity.StoryFlagValue,
                // A reward with no flag on it could never be paid once, so it is not stored at all.
                StoryExperience = entity.StoryFlag == null || entity.StoryExperience == 0 ? null : entity.StoryExperience,
                StoryCoins = entity.StoryFlag == null || entity.StoryCoins == 0 ? null : entity.StoryCoins,
                VisibleIfFlag = entity.Visibility.Flag,
                VisibleIfAtLeast = entity.Visibility.IsSet ? entity.Visibility.AtLeast : null,
                VisibleIfBelow = entity.Visibility.Below,
            };

            Write(typed, record);

            return record;
        }

        public EntityRecord CreateRecord(string entityId, string displayName)
        {
            var record = new EntityRecord
            {
                Id = entityId,
                Kind = Kind,
                DisplayName = displayName,
            };

            Initialise(record);

            return record;
        }
    }
}
