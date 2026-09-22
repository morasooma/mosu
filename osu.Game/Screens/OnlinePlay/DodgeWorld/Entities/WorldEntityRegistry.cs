// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities
{
    /// <summary>
    /// The set of entity kinds this client can place and store.
    /// </summary>
    /// <remarks>
    /// Registering a kind here is the only step needed to support a new world object: construction,
    /// persistence and the property editor all read from this list.
    /// </remarks>
    internal sealed class WorldEntityRegistry
    {
        public static WorldEntityRegistry Default { get; } = new WorldEntityRegistry(
            new SurfaceKind(),
            new CollisionKind(),
            new MoraKind(),
            new NpcKind(),
            new TerminalKind(),
            new PortalKind(),
            new SidePassageKind(),
            new MobSpawnKind(),
            new WarpKind(),
            new EmitterKind(),
            new BeamKind());

        private readonly Dictionary<string, IWorldEntityKind> byKind;

        public IReadOnlyCollection<IWorldEntityKind> Kinds { get; }

        public WorldEntityRegistry(params IWorldEntityKind[] kinds)
        {
            byKind = kinds.ToDictionary(kind => kind.Kind);
            Kinds = kinds;
        }

        public IWorldEntityKind? Find(string kind) => byKind.GetValueOrDefault(kind);

        /// <summary>
        /// Builds the drawable for a stored record.
        /// </summary>
        /// <returns>
        /// The entity, or <c>null</c> if no kind claims <see cref="EntityRecord.Kind"/> — which happens
        /// when a world was published by a client that knows more kinds than this one.
        /// </returns>
        public EditableWorldEntity? Create(EntityRecord record, WorldEntityContext context)
        {
            IWorldEntityKind? kind = Find(record.Kind);

            if (kind == null)
            {
                Logger.Log($"DodgeWorld: skipping entity '{record.Id}' of unsupported kind '{record.Kind}'.",
                    LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }

            return kind.Create(record, context);
        }

        /// <summary>
        /// Builds the stored record for a live entity.
        /// </summary>
        /// <remarks>
        /// An entity whose kind is not registered cannot be persisted, so it is dropped rather than
        /// written as a malformed record. This is unreachable for entities this registry created.
        /// </remarks>
        public EntityRecord? Capture(EditableWorldEntity entity)
        {
            IWorldEntityKind? kind = Find(entity.LayoutKind);

            if (kind == null)
            {
                Logger.Log($"DodgeWorld: cannot persist entity '{entity.EntityId}' of unregistered kind '{entity.LayoutKind}'.",
                    LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }

            return kind.Capture(entity);
        }
    }
}
