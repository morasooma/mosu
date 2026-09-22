// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// Shared destination persistence for anything the player can walk into to change rooms.
    /// </summary>
    internal abstract class PassageKind<T> : WorldEntityKind<T>
        where T : WorldPassage
    {
        /// <summary>
        /// Shown for a passage whose <see cref="EntityRecord.DestinationRoomId"/> leads nowhere yet.
        /// </summary>
        private const string UNLINKED_DESTINATION = WorldPassage.UNLINKED_DESTINATION;

        protected override void Read(T entity, EntityRecord record, WorldEntityContext context)
        {
            entity.Destination = record.Destination ?? UNLINKED_DESTINATION;
            entity.DestinationRoomId = record.DestinationRoomId;
            entity.ArrivalEntityId = string.IsNullOrWhiteSpace(record.ArrivalEntityId) ? null : record.ArrivalEntityId.Trim();
            entity.ArrivalPoint = record.ArrivalX is float x && record.ArrivalY is float y
                                  && float.IsFinite(x) && float.IsFinite(y)
                ? new Vector2(x, y)
                : null;
        }

        protected override void Write(T entity, EntityRecord record)
        {
            record.Destination = entity.Destination;
            record.DestinationRoomId = entity.DestinationRoomId;
            record.ArrivalEntityId = entity.ArrivalEntityId;
            record.ArrivalX = entity.ArrivalPoint?.X;
            record.ArrivalY = entity.ArrivalPoint?.Y;
        }

        protected override void Initialise(EntityRecord record)
        {
            record.Destination = UNLINKED_DESTINATION;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            yield return Field("ID комнаты назначения", "куда ведёт переход; пусто — вернуть на вход текущей комнаты",
                passage => passage.DestinationRoomId ?? string.Empty,
                (passage, text) => passage.DestinationRoomId = string.IsNullOrWhiteSpace(text) ? null : text.Trim());

            yield return Field("Подпись перехода", "показывается на экране при входе",
                passage => passage.Destination,
                (passage, text) => passage.Destination = string.IsNullOrWhiteSpace(text) ? UNLINKED_DESTINATION : text.Trim());

            yield return Field("ID объекта прибытия", "пусто — выйти у обратного прохода, а если его нет, на входе комнаты",
                passage => passage.ArrivalEntityId ?? string.Empty,
                (passage, text) => passage.ArrivalEntityId = string.IsNullOrWhiteSpace(text) ? null : text.Trim());

            yield return Field("Выход X", "точная точка выхода; пусто — считать автоматически",
                passage => passage.ArrivalPoint is Vector2 point ? EditorValue.Format(point.X) : string.Empty,
                (passage, text) => passage.ArrivalPoint = readArrival(passage, text, horizontal: true));

            yield return Field("Выход Y", "пусто — считать автоматически",
                passage => passage.ArrivalPoint is Vector2 point ? EditorValue.Format(point.Y) : string.Empty,
                (passage, text) => passage.ArrivalPoint = readArrival(passage, text, horizontal: false));
        }

        /// <summary>
        /// Reads one coordinate of the hand-placed exit. Clearing either one clears the pair, because half a
        /// point is not a point.
        /// </summary>
        private static Vector2? readArrival(T passage, string text, bool horizontal)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            Vector2 current = passage.ArrivalPoint ?? Vector2.Zero;
            float value = EditorValue.Float(text, horizontal ? current.X : current.Y,
                -DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X);

            return horizontal ? new Vector2(value, current.Y) : new Vector2(current.X, value);
        }
    }

    /// <summary>
    /// A gateway rendered as a ring, used for named destinations such as the Dodge gate.
    /// </summary>
    internal sealed class PortalKind : PassageKind<WorldPortal>
    {
        public override string Kind => EntityKinds.PORTAL;

        protected override WorldPortal CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new WorldPortal(context.Colours, context.IsEditing, context.Select);
    }

    /// <summary>
    /// A side exit at the edge of a room, pointing left or right.
    /// </summary>
    internal sealed class SidePassageKind : PassageKind<SidePassage>
    {
        public override string Kind => EntityKinds.PASSAGE;

        protected override bool SupportsRotation => true;

        protected override SidePassage CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new SidePassage(context.Colours, context.IsEditing, context.Select, record.PointsRight ?? true);

        protected override void Write(SidePassage entity, EntityRecord record)
        {
            base.Write(entity, record);
            record.PointsRight = entity.PointsRight;
        }

        protected override void Initialise(EntityRecord record)
        {
            base.Initialise(record);
            record.PointsRight = true;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            foreach (EntityField shared in base.DescribeFields())
                yield return shared;

            // There was no way at all to flip a doorway, which matters: the side it faces is the side
            // somebody arriving through it stands on.
            yield return Switch("Смотрит вправо", "выключено — дверь смотрит влево; игрок выходит с той стороны, куда она смотрит",
                passage => passage.PointsRight, (passage, right) => passage.PointsRight = right);
        }
    }
}
