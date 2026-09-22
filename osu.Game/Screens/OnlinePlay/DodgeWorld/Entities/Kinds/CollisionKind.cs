// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// Invisible walls. Only shown while the editor is open.
    /// </summary>
    internal sealed class CollisionKind : WorldEntityKind<CollisionZone>
    {
        private static readonly Vector2 default_size = new Vector2(224, 128);

        public override string Kind => EntityKinds.COLLISION;

        /// <summary>
        /// A wall across a corner needs to be turned. It could not be before, because the collision stayed
        /// square whatever the drawing did; now the box turns with it.
        /// </summary>
        protected override bool SupportsRotation => true;

        protected override CollisionZone CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new CollisionZone(context.Colours, context.IsEditing, context.Select, default_size);

        protected override void Initialise(EntityRecord record)
        {
            record.Width = default_size.X;
            record.Height = default_size.Y;
        }
    }
}
