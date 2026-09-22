// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// Decorative floor panels. Purely visual — surfaces never block movement.
    /// </summary>
    internal sealed class SurfaceKind : WorldEntityKind<RoomSurface>
    {
        /// <summary>
        /// Id prefix marking a surface the user added, and may therefore delete. The three surfaces
        /// of the default room are part of its structure and are deliberately not removable.
        /// </summary>
        public const string CUSTOM_ID_PREFIX = "surface-custom";

        private static readonly Vector2 fallback_size = new Vector2(320, 176);

        public override string Kind => EntityKinds.SURFACE;

        // Decoration only, so turning one changes nothing about where the player can walk.
        protected override bool SupportsRotation => true;

        protected override RoomSurface CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new RoomSurface(
                context.Colours,
                context.IsEditing,
                context.Select,
                // The stored size doubles as the size "restore defaults" returns to, so that resetting
                // a room recovers the published layout rather than an arbitrary construction default.
                size: new Vector2(record.Width ?? fallback_size.X, record.Height ?? fallback_size.Y),
                style: record.Style ?? 1,
                rounded: record.Rounded ?? true);

        protected override void Read(RoomSurface entity, EntityRecord record, WorldEntityContext context)
        {
            entity.ApplyMaterialState(record.CornerRadius, record.Texture, record.TextureFill, record.TextureOpacity, record.TextureSmoothing);
            context.RequestTexture(entity);
        }

        protected override void Write(RoomSurface entity, EntityRecord record)
        {
            record.CornerRadius = entity.CornerRadiusValue;
            record.Texture = entity.TexturePath;
            record.TextureFill = entity.TextureFillModeIndex;
            record.TextureOpacity = entity.TextureOpacity;
            record.TextureSmoothing = entity.TextureSmoothing;
        }

        protected override void Initialise(EntityRecord record)
        {
            record.Width = fallback_size.X;
            record.Height = fallback_size.Y;
            record.Style = 1;
            record.Rounded = true;
        }
    }
}
