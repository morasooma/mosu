// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers how the travel menu finds warps, and how a warp's prices survive the editor.
    /// </summary>
    /// <remarks>
    /// The menu lists warps in rooms that are not loaded, so it reads the stored document rather than
    /// the live entities. That makes the whole listing testable without a game.
    /// </remarks>
    [TestFixture]
    public class WarpCatalogueTest
    {
        private static RoomDefinition room(string id, string name, params EntityRecord[] entities) => new RoomDefinition
        {
            Id = id,
            Name = name,
            Entities = entities.ToList(),
        };

        private static EntityRecord warp(string id, string? name, float x = 0, float y = 0, int? unlock = null, int? travel = null) =>
            new EntityRecord
            {
                Id = id,
                Kind = EntityKinds.WARP,
                DisplayName = name,
                X = x,
                Y = y,
                WarpUnlockCost = unlock,
                WarpTravelCost = travel,
            };

        [Test]
        public void EveryRoomsWarpsAreListed()
        {
            IReadOnlyList<WarpPoint> points = WarpCatalogue.Describe(new[]
            {
                room("plaza", "Mora Plaza", warp("plaza-warp", "Plaza", travel: 0),
                    new EntityRecord { Id = "mora", Kind = EntityKinds.MORA }),
                room("deep", "Deep Woods", warp("deep-warp", "Woods", x: 40, y: -20, unlock: 25, travel: 5)),
            });

            Assert.That(points.Select(point => point.EntityId), Is.EqualTo(new[] { "plaza-warp", "deep-warp" }));

            WarpPoint deep = points[1];
            Assert.That(deep.RoomId, Is.EqualTo("deep"));
            Assert.That(deep.RoomName, Is.EqualTo("Deep Woods"));
            Assert.That(deep.Name, Is.EqualTo("Woods"));
            Assert.That(deep.Position, Is.EqualTo(new Vector2(40, -20)));
            Assert.That(deep.UnlockCost, Is.EqualTo(25));
            Assert.That(deep.TravelCost, Is.EqualTo(5));
        }

        /// <summary>
        /// A world is hand-editable and round-trips through a server, so the menu must survive values
        /// no editor would produce.
        /// </summary>
        [Test]
        public void BrokenWarpsStayUsable()
        {
            WarpPoint unnamed = WarpCatalogue.Describe(new[] { room("plaza", "Plaza", warp("plaza-warp", null)) }).Single();
            Assert.That(unnamed.Name, Is.EqualTo("plaza-warp"), "an unnamed warp falls back to its id");

            WarpPoint corrupt = WarpCatalogue.Describe(new[]
            {
                room("plaza", "Plaza", warp("plaza-warp", "Plaza", x: float.NaN, y: 10, unlock: -5, travel: int.MaxValue)),
            }).Single();

            Assert.That(corrupt.Position, Is.EqualTo(Vector2.Zero), "non-finite coordinates land in the middle of the room");
            Assert.That(corrupt.UnlockCost, Is.Zero);
            Assert.That(corrupt.TravelCost, Is.EqualTo(WarpCatalogue.MAXIMUM_COST));
        }

        /// <summary>
        /// Missing prices mean free, which is how a starting location is expressed.
        /// </summary>
        [Test]
        public void AbsentPricesAreFree()
        {
            WarpPoint point = WarpCatalogue.Describe(new[] { room("plaza", "Plaza", warp("plaza-warp", "Plaza")) }).Single();

            Assert.That(point.UnlockCost, Is.Zero);
            Assert.That(point.TravelCost, Is.Zero);
        }

        [Test]
        public void PricesSurviveTheEditor()
        {
            IWorldEntityKind kind = WorldEntityRegistry.Default.Find(EntityKinds.WARP)!;
            var context = new WorldEntityContext(new OsuColour(), null!, () => true, _ => { }, () => "ru", _ => { });

            var entity = (WorldWarp)kind.Create(warp("deep-warp", "Woods", unlock: 25, travel: 5), context);

            Assert.That(entity.UnlockCost, Is.EqualTo(25));
            Assert.That(entity.TravelCost, Is.EqualTo(5));
            Assert.That(entity.Unlocked, Is.False, "a warp is closed until the server says otherwise");

            EntityRecord captured = kind.Capture(entity);

            Assert.That(captured.WarpUnlockCost, Is.EqualTo(25));
            Assert.That(captured.WarpTravelCost, Is.EqualTo(5));
        }

        /// <summary>
        /// Prices are typed into the property editor, so out-of-range text must not stick.
        /// </summary>
        [Test]
        public void EditorClampsPrices()
        {
            IWorldEntityKind kind = WorldEntityRegistry.Default.Find(EntityKinds.WARP)!;
            var context = new WorldEntityContext(new OsuColour(), null!, () => true, _ => { }, () => "ru", _ => { });
            var entity = (WorldWarp)kind.Create(warp("deep-warp", "Woods", unlock: 25), context);

            EntityField unlockCost = kind.Fields.Single(field => field.Label == "Цена открытия");

            unlockCost.Write(entity, "-40");
            Assert.That(entity.UnlockCost, Is.Zero);

            unlockCost.Write(entity, "не число");
            Assert.That(entity.UnlockCost, Is.Zero, "unparseable text leaves the price alone");

            unlockCost.Write(entity, "999999999");
            Assert.That(entity.UnlockCost, Is.EqualTo(WarpCatalogue.MAXIMUM_COST));
        }
    }
}
