// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities
{
    /// <summary>
    /// One kind of placeable world object: how to build it, and which parts of the stored record it owns.
    /// </summary>
    /// <remarks>
    /// A kind is the single place that knows about its own fields. Previously this knowledge was spread
    /// across a construction switch, a save expression, and the property editor, so adding a field
    /// meant editing several unrelated methods and forgetting one produced a silent data loss.
    /// </remarks>
    internal interface IWorldEntityKind
    {
        /// <summary>
        /// The <see cref="EntityRecord.Kind"/> value this kind claims.
        /// </summary>
        string Kind { get; }

        /// <summary>
        /// Builds a drawable from a stored record.
        /// </summary>
        EditableWorldEntity Create(EntityRecord record, WorldEntityContext context);

        /// <summary>
        /// Builds a record for an entity currently in the room.
        /// </summary>
        EntityRecord Capture(EditableWorldEntity entity);

        /// <summary>
        /// Creates a record for a brand new entity of this kind, placed at <paramref name="entityId"/>.
        /// </summary>
        EntityRecord CreateRecord(string entityId, string displayName);

        /// <summary>
        /// The properties the editor should offer for this kind, in display order.
        /// </summary>
        /// <remarks>
        /// Only these appear when an entity of this kind is selected, so a mob zone no longer shows a
        /// destination room and a portal no longer shows projectile settings.
        /// </remarks>
        IReadOnlyList<EntityField> Fields { get; }
    }
}
