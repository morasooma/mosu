// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers the texture configuration entities carry: which of them accept an image, and that a
    /// stored one survives a round trip through the editor.
    /// </summary>
    [TestFixture]
    public class WorldEntityTextureTest
    {
        private readonly WorldEntityRegistry registry = WorldEntityRegistry.Default;

        /// <remarks>
        /// The texture store is only reached by Mora, whose image is bundled, so the kinds under test
        /// here never touch it.
        /// </remarks>
        private WorldEntityContext context() => new WorldEntityContext(new OsuColour(), null!, () => false,
            _ => { }, () => "ru", _ => { });

        private static EntityRecord textured(string kind, int? fill = null) => new EntityRecord
        {
            Id = $"{kind}-1",
            Kind = kind,
            DisplayName = "Textured",
            Texture = "grass.png",
            TextureOpacity = 0.5f,
            TextureSmoothing = false,
            TextureFill = fill,
        };

        [TestCase(EntityKinds.SURFACE)]
        [TestCase(EntityKinds.NPC)]
        [TestCase(EntityKinds.MOB_SPAWN)]
        public void TextureSurvivesARoundTrip(string kind)
        {
            IWorldEntityKind entityKind = registry.Find(kind)!;

            EditableWorldEntity entity = entityKind.Create(textured(kind), context());

            Assert.That(entity, Is.InstanceOf<ITexturedEntity>());

            var textured1 = (ITexturedEntity)entity;
            Assert.That(textured1.TexturePath, Is.EqualTo("grass.png"));
            Assert.That(textured1.TextureOpacity, Is.EqualTo(0.5f));
            Assert.That(textured1.TextureSmoothing, Is.False);

            EntityRecord captured = entityKind.Capture(entity);

            Assert.That(captured.Texture, Is.EqualTo("grass.png"));
            Assert.That(captured.TextureOpacity, Is.EqualTo(0.5f));
            Assert.That(captured.TextureSmoothing, Is.False);
        }

        /// <summary>
        /// Tiling is the only fill mode that needs a repeating wrap mode, which decides how the image
        /// is loaded rather than how it is drawn.
        /// </summary>
        [Test]
        public void OnlyATilingSurfaceRepeats()
        {
            IWorldEntityKind kind = registry.Find(EntityKinds.SURFACE)!;

            var tiled = (RoomSurface)kind.Create(textured(EntityKinds.SURFACE, (int)SurfaceFillMode.Tile), context());
            var filled = (RoomSurface)kind.Create(textured(EntityKinds.SURFACE, (int)SurfaceFillMode.Fill), context());

            Assert.That(tiled.TextureRepeats, Is.True);
            Assert.That(tiled.TextureFillModeName, Is.EqualTo("Tile"));
            Assert.That(kind.Capture(tiled).TextureFill, Is.EqualTo(3));

            Assert.That(filled.TextureRepeats, Is.False);
            Assert.That(filled.TextureFillModeName, Is.EqualTo("Fill"));
        }

        /// <summary>
        /// A world round-trips through a server and is hand-editable, so an out-of-range fill mode
        /// has to land on a real one rather than on an undrawable value.
        /// </summary>
        [Test]
        public void UnknownFillModeIsClamped()
        {
            IWorldEntityKind kind = registry.Find(EntityKinds.SURFACE)!;

            var surface = (RoomSurface)kind.Create(textured(EntityKinds.SURFACE, 97), context());

            Assert.That(surface.TextureFillModeIndex, Is.EqualTo((int)SurfaceFillMode.Tile));
        }

        /// <summary>
        /// A mob is a character and nothing else, so it is always fitted whole into its box and there is no
        /// fill mode to offer. A placed NPC does offer one, because it doubles as any object the player can
        /// press E on, and the image of a sign is not the shape of a person.
        /// </summary>
        [Test]
        public void AMobOffersNoFillModeButAPlacedNpcDoes()
        {
            var zone = (ITexturedEntity)registry.Find(EntityKinds.MOB_SPAWN)!.Create(textured(EntityKinds.MOB_SPAWN), context());

            Assert.That(zone.TextureFillModeName, Is.Null);
            Assert.That(zone.TextureRepeats, Is.False);

            var npc = (ITexturedEntity)registry.Find(EntityKinds.NPC)!.Create(textured(EntityKinds.NPC), context());

            Assert.That(npc.TextureFillModeName, Is.EqualTo(nameof(SurfaceFillMode.Fit)),
                "a placed NPC should start fitted, as characters always were");
            Assert.That(npc.TextureRepeats, Is.False);

            npc.CycleTextureFill();
            Assert.That(npc.TextureFillModeName, Is.Not.EqualTo(nameof(SurfaceFillMode.Fit)));
        }

        /// <summary>
        /// A mob's image is sized to fit rather than letterboxed inside a fixed square. Letterboxing
        /// centres the image in the box, so a wide one left the gap underneath it and the mob was drawn
        /// hovering above its own shadow.
        /// </summary>
        [Test]
        public void AWideMobImageIsSizedToFitRatherThanFloatingInItsBox()
        {
            var renderer = new DummyRenderer();
            var box = new Vector2(74, 74);

            Vector2 wide = MobAppearance.FitInto(renderer.CreateTexture(200, 100), box);
            Vector2 tall = MobAppearance.FitInto(renderer.CreateTexture(100, 200), box);
            Vector2 square = MobAppearance.FitInto(renderer.CreateTexture(64, 64), box);

            Assert.That(wide.X, Is.EqualTo(74).Within(0.01), "a wide image should fill the width");
            Assert.That(wide.Y, Is.EqualTo(37).Within(0.01), "and keep its proportions");
            Assert.That(tall.X, Is.EqualTo(37).Within(0.01));
            Assert.That(tall.Y, Is.EqualTo(74).Within(0.01));

            // Grown to the box, as a fitted image always was: a small image is not drawn small.
            Assert.That(square, Is.EqualTo(box));
        }

        /// <summary>
        /// Clearing the path has to drop the image with it, or the old one stays on screen.
        /// </summary>
        [Test]
        public void ClearingThePathClearsTheImage()
        {
            var zone = (MobSpawnZone)registry.Find(EntityKinds.MOB_SPAWN)!.Create(textured(EntityKinds.MOB_SPAWN), context());

            zone.SetTexturePath(null);

            Assert.That(zone.TexturePath, Is.Null);
            Assert.That(zone.MobTexture, Is.Null);
        }
    }
}
