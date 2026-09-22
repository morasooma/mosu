// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers the world document format, including the migration of documents written by older clients.
    /// </summary>
    [TestFixture]
    public class DodgeWorldSerializerTest
    {
        [Test]
        public void CurrentFormatIsRead()
        {
            DodgeWorldDocument? document = DodgeWorldSerializer.Deserialize("""
                {"version":1,"initial_room_id":"east","rooms":[
                  {"id":"east","name":"East","width":1200,"height":900,"spawn_x":5,"spawn_y":7,
                   "entities":[{"id":"a","kind":"surface","x":10,"y":20,"corner_radius":12}]}]}
                """);

            Assert.That(document, Is.Not.Null);
            Assert.That(document!.IsSupported, Is.True);
            Assert.That(document.InitialRoomId, Is.EqualTo("east"));
            Assert.That(document.Rooms, Has.Count.EqualTo(1));
            Assert.That(document.Rooms[0].Size, Is.EqualTo(new osuTK.Vector2(1200, 900)));
            Assert.That(document.Rooms[0].Entities[0].CornerRadius, Is.EqualTo(12));
        }

        /// <summary>
        /// Legacy documents stored C# property names. Migration is derived from the model by
        /// reflection, so this covers all three nesting levels at once.
        /// </summary>
        [Test]
        public void LegacyPascalCaseNamesAreMigratedAtEveryLevel()
        {
            DodgeWorldDocument? document = DodgeWorldSerializer.Deserialize("""
                {"Version":1,"DefaultWeaponSkinId":"rose","Rooms":[
                  {"Id":"east","Name":"East","Width":1200,"Height":900,"SpawnX":5,"SpawnY":7,
                   "Entities":[{"Id":"a","Kind":"mob-spawn","X":10,"Y":20,
                                "MaxHealth":9,"AttackCooldownMs":1500,"ProjectileSpeed":250}]}]}
                """);

            Assert.That(document, Is.Not.Null);
            Assert.That(document!.Version, Is.EqualTo(1));
            Assert.That(document.DefaultWeaponSkinId, Is.EqualTo("rose"));

            RoomDefinition room = document.Rooms[0];
            Assert.That(room.Id, Is.EqualTo("east"));
            Assert.That(room.Spawn, Is.EqualTo(new osuTK.Vector2(5, 7)));

            EntityRecord entity = room.Entities[0];
            Assert.That(entity.Kind, Is.EqualTo("mob-spawn"));
            Assert.That(entity.X, Is.EqualTo(10));
            Assert.That(entity.MaxHealth, Is.EqualTo(9));
            Assert.That(entity.AttackCooldownMs, Is.EqualTo(1500));
            Assert.That(entity.ProjectileSpeed, Is.EqualTo(250));
        }

        [Test]
        public void CurrentNameWinsWhenBothArePresent()
        {
            DodgeWorldDocument? document = DodgeWorldSerializer.Deserialize("""
                {"version":1,"rooms":[{"id":"a","name":"A","Name":"legacy","entities":[]}]}
                """);

            Assert.That(document!.Rooms[0].Name, Is.EqualTo("A"));
        }

        /// <summary>
        /// A semantic rename rather than a casing change, so it is handled by the model rather than
        /// by the reflective migration.
        /// </summary>
        [Test]
        public void LegacyCurrentRoomIdBecomesInitialRoomId()
        {
            DodgeWorldDocument? document = DodgeWorldSerializer.Deserialize("""
                {"version":1,"CurrentRoomId":"west","rooms":[{"id":"west","name":"West","entities":[]}]}
                """);

            Assert.That(document!.InitialRoomId, Is.EqualTo("west"));
        }

        [Test]
        public void RoundTripPreservesEntityFields()
        {
            var original = new DodgeWorldDocument
            {
                InitialRoomId = "east",
                DefaultWeaponSkinId = "rose",
                WeaponSkins = new List<WeaponSkin> { new WeaponSkin { Id = "rose", Texture = "sword.png", DisplayWidth = 176 } },
                Rooms =
                {
                    new RoomDefinition
                    {
                        Id = "east",
                        Name = "East",
                        Entities =
                        {
                            new EntityRecord
                            {
                                Id = "zone", Kind = EntityKinds.MOB_SPAWN, X = 1, Y = 2,
                                SpawnCount = 4, MaxHealth = 7, ProjectileRange = 333,
                                MaximumFarmLevel = 12, TextureSmoothing = false,
                            },
                        },
                    },
                },
            };

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            Assert.That(restored, Is.Not.Null);

            EntityRecord entity = restored!.Rooms[0].Entities[0];
            Assert.That(entity.SpawnCount, Is.EqualTo(4));
            Assert.That(entity.MaxHealth, Is.EqualTo(7));
            Assert.That(entity.ProjectileRange, Is.EqualTo(333));
            Assert.That(entity.MaximumFarmLevel, Is.EqualTo(12));
            Assert.That(entity.TextureSmoothing, Is.False);
            Assert.That(restored.WeaponSkins![0].DisplayWidth, Is.EqualTo(176));
        }

        /// <summary>
        /// A hand-placed room keeps its place on the world map across a save, and a room that was never
        /// placed stays unplaced rather than being pinned to the origin.
        /// </summary>
        [Test]
        public void RoundTripPreservesMapPlacement()
        {
            var original = new DodgeWorldDocument
            {
                Rooms =
                {
                    new RoomDefinition { Id = "plaza", Name = "Plaza" },
                    new RoomDefinition { Id = "east", Name = "East", MapX = -320.5f, MapY = 190 },
                },
            };

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Rooms[0].MapPosition, Is.Null);
            Assert.That(restored.Rooms[1].MapPosition, Is.EqualTo(new Vector2(-320.5f, 190)));
        }

        /// <summary>
        /// A course is authored content, so its timing has to survive the round trip through the document
        /// and through the server, which drops any field it does not know.
        /// </summary>
        [Test]
        public void RoundTripPreservesACourse()
        {
            var original = new DodgeWorldDocument
            {
                Rooms =
                {
                    new RoomDefinition
                    {
                        Id = "course",
                        Name = "Course",
                        Entities =
                        {
                            new EntityRecord
                            {
                                Id = "spinner", Kind = EntityKinds.EMITTER, X = 40, Y = -60,
                                HazardDirection = 90, HazardTurn = 25, HazardCycleMs = 700, HazardPhaseMs = 350,
                                ProjectileCount = 7, ProjectileSpread = 360, ProjectileDamage = 6,
                                ProjectileSpeed = 260, ProjectileRange = 900,
                            },
                            new EntityRecord
                            {
                                Id = "sweeper", Kind = EntityKinds.BEAM, X = -200, Y = 120,
                                HazardDirection = 180, HazardTurn = -15, HazardCycleMs = 2400, HazardPhaseMs = 1200,
                                BeamLength = 1100, BeamWidth = 64, BeamActiveMs = 800, ContactDamage = 21,
                            },
                        },
                    },
                },
            };

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            Assert.That(restored, Is.Not.Null);

            EntityRecord emitter = restored!.Rooms[0].Entities[0];
            Assert.That(emitter.HazardDirection, Is.EqualTo(90));
            Assert.That(emitter.HazardTurn, Is.EqualTo(25));
            Assert.That(emitter.HazardCycleMs, Is.EqualTo(700));
            Assert.That(emitter.HazardPhaseMs, Is.EqualTo(350));
            Assert.That(emitter.ProjectileSpread, Is.EqualTo(360));

            EntityRecord beam = restored.Rooms[0].Entities[1];
            Assert.That(beam.BeamLength, Is.EqualTo(1100));
            Assert.That(beam.BeamWidth, Is.EqualTo(64));
            Assert.That(beam.BeamActiveMs, Is.EqualTo(800));
            Assert.That(beam.ContactDamage, Is.EqualTo(21));
        }

        /// <summary>
        /// A turned passage and the object it leaves the player beside are part of the room's shape, so a
        /// world that survives a save with one of them missing is a world that quietly loses its corridors.
        /// </summary>
        [Test]
        public void RoundTripPreservesHowAPassageIsTurnedAndWhereItArrives()
        {
            var original = new DodgeWorldDocument
            {
                Rooms =
                {
                    new RoomDefinition
                    {
                        Id = "hall",
                        Name = "Hall",
                        Entities =
                        {
                            new EntityRecord
                            {
                                Id = "door-north", Kind = EntityKinds.PASSAGE, X = 0, Y = -300,
                                DestinationRoomId = "yard", Destination = "Yard",
                                Facing = 270, ArrivalEntityId = "door-south",
                            },
                        },
                    },
                },
            };

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            EntityRecord door = restored!.Rooms[0].Entities[0];
            Assert.That(door.Facing, Is.EqualTo(270));
            Assert.That(door.ArrivalEntityId, Is.EqualTo("door-south"));
            Assert.That(door.DestinationRoomId, Is.EqualTo("yard"));
        }

        /// <summary>
        /// Every branch of a conversation has to survive a save, and the old flat page list has to keep
        /// reading as the single branch it always was — that is what a world published before branches is.
        /// </summary>
        [Test]
        public void RoundTripPreservesEveryBranchAndReadsAnOldConversationAsOne()
        {
            var original = new DodgeWorldDocument
            {
                Rooms =
                {
                    new RoomDefinition
                    {
                        Id = "hall",
                        Name = "Hall",
                        Entities =
                        {
                            new EntityRecord
                            {
                                Id = "villager", Kind = EntityKinds.NPC, X = 0, Y = 0,
                                DialogueLocalized = new Dictionary<string, string[]>
                                {
                                    ["ru"] = new[] { "одна" },
                                    ["en"] = new[] { "one" },
                                },
                                DialogueBranches = new Dictionary<string, string[][]>
                                {
                                    ["ru"] = new[] { new[] { "одна" }, new[] { "две", "и ещё" } },
                                    ["en"] = new[] { new[] { "one" }, new[] { "two", "and more" } },
                                },
                            },
                            new EntityRecord
                            {
                                Id = "guard", Kind = EntityKinds.NPC, X = 80, Y = 0,
                                DialogueLocalized = new Dictionary<string, string[]>
                                {
                                    ["ru"] = new[] { "Стой." },
                                    ["en"] = new[] { "Halt." },
                                },
                            },
                        },
                    },
                },
            };

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            EntityRecord villager = restored!.Rooms[0].Entities[0];
            Assert.That(villager.DialogueBranches!["ru"].Length, Is.EqualTo(2));
            Assert.That(villager.DialogueBranches["ru"][1], Is.EqualTo(new[] { "две", "и ещё" }));
            Assert.That(villager.DialogueBranches["en"][1], Is.EqualTo(new[] { "two", "and more" }));

            // A character from before branches carries none, and its pages are still where they were.
            EntityRecord guard = restored.Rooms[0].Entities[1];
            Assert.That(guard.DialogueBranches, Is.Null);
            Assert.That(guard.DialogueLocalized!["ru"], Is.EqualTo(new[] { "Стой." }));
        }

        /// <summary>
        /// The numbers a fight is balanced with have to survive a save, or they cannot be tuned without a
        /// new client. A world that says nothing about them keeps what it always did.
        /// </summary>
        [Test]
        public void RoundTripPreservesTheSwordAndHowMobsMove()
        {
            var original = new DodgeWorldDocument
            {
                DefaultWeaponSkinId = "default",
                WeaponSkins = new List<WeaponSkin> { new WeaponSkin { Id = "default", Damage = 9 } },
                Rooms =
                {
                    new RoomDefinition
                    {
                        Id = "hall",
                        Name = "Hall",
                        Entities =
                        {
                            new EntityRecord { Id = "pack", Kind = EntityKinds.MOB_SPAWN, X = 0, Y = 0, MobSpeed = 155, MobChases = true },
                            new EntityRecord { Id = "old", Kind = EntityKinds.MOB_SPAWN, X = 200, Y = 0 },
                        },
                    },
                },
            };

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            Assert.That(restored!.SwordDamage, Is.EqualTo(9));

            MobSpawnDefinition chaser = MobSpawnKind.Describe(restored.Rooms[0].Entities[0]);
            Assert.That(chaser.Speed, Is.EqualTo(155));
            Assert.That(chaser.Chases, Is.True);

            MobSpawnDefinition unchanged = MobSpawnKind.Describe(restored.Rooms[0].Entities[1]);
            Assert.That(unchanged.Speed, Is.EqualTo(MobSpawnRules.DEFAULT_SPEED));
            Assert.That(unchanged.Chases, Is.False, "a zone that says nothing should not start chasing");
        }

        /// <summary>
        /// A copy must share nothing with what it was copied from: editing a duplicated character's lines
        /// used to be editing the original's.
        /// </summary>
        [Test]
        public void ACopiedObjectSharesNothingWithTheOriginal()
        {
            var original = new EntityRecord
            {
                Id = "mora",
                Kind = EntityKinds.NPC,
                X = 10,
                Y = 20,
                Dialogue = new[] { "first", "second" },
                DialogueLocalized = new Dictionary<string, string[]> { ["ru"] = new[] { "первая" } },
            };

            EntityRecord copy = DodgeWorldSerializer.Clone(original);
            copy.Id = "mora-copy";
            copy.Dialogue![0] = "changed";
            copy.DialogueLocalized!["ru"][0] = "изменено";

            Assert.That(original.Id, Is.EqualTo("mora"));
            Assert.That(original.Dialogue![0], Is.EqualTo("first"));
            Assert.That(original.DialogueLocalized!["ru"][0], Is.EqualTo("первая"));
            Assert.That(copy.X, Is.EqualTo(10), "a copy should keep everything it did not deliberately change");
        }

        [Test]
        public void UnsupportedVersionIsNotAccepted()
        {
            DodgeWorldDocument? document = DodgeWorldSerializer.Deserialize("""
                {"version":2,"rooms":[{"id":"a","name":"A","entities":[]}]}
                """);

            Assert.That(document!.IsSupported, Is.False);
        }

        [Test]
        public void DocumentWithoutRoomsIsNotAccepted()
        {
            DodgeWorldDocument? document = DodgeWorldSerializer.Deserialize("""{"version":1,"rooms":[]}""");

            Assert.That(document!.IsSupported, Is.False);
        }

        [Test]
        public void DefaultWorldIsSupportedAndRoundTrips()
        {
            DodgeWorldDocument original = DefaultWorld.CreateDocument();

            Assert.That(original.IsSupported, Is.True);
            Assert.That(original.ResolveInitialRoom(), Is.Not.Null);

            DodgeWorldDocument? restored = DodgeWorldSerializer.Deserialize(DodgeWorldSerializer.Serialize(original));

            Assert.That(restored!.Rooms[0].Entities, Has.Count.EqualTo(original.Rooms[0].Entities.Count));
        }
    }
}
