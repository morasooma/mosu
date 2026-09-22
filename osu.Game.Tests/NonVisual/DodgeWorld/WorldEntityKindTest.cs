// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers what each kind of object reads out of a stored record and writes back into one — the seam
    /// where an authored world becomes something on screen.
    /// </summary>
    [TestFixture]
    public class WorldEntityKindTest
    {
        private readonly WorldEntityRegistry registry = WorldEntityRegistry.Default;

        /// <remarks>
        /// The texture store is only reached by Mora, whose image is bundled, so the kinds under test here
        /// never touch it.
        /// </remarks>
        private WorldEntityContext context() => new WorldEntityContext(new OsuColour(), null!, () => false,
            _ => { }, () => "ru", _ => { });

        private T create<T>(EntityRecord record) where T : EditableWorldEntity =>
            (T)registry.Find(record.Kind)!.Create(record, context());

        private EntityRecord capture(EditableWorldEntity entity) => registry.Capture(entity)!;

        /// <summary>
        /// Which kiosk it is comes from the style now. It used to be guessed from the entity's id, which
        /// meant "add a kiosk" could only ever produce a quest board: the id it was given was not one the
        /// code recognised.
        /// </summary>
        [Test]
        public void AKioskIsWhicheverKindItsStyleSays()
        {
            var shop = create<WorldTerminal>(new EntityRecord
            {
                Id = "shop-1", Kind = EntityKinds.TERMINAL, Style = 1,
            });

            var board = create<WorldTerminal>(new EntityRecord
            {
                Id = "shop-1", Kind = EntityKinds.TERMINAL, Style = 0,
            });

            Assert.That(shop.StyleIndex, Is.EqualTo(1));
            Assert.That(board.StyleIndex, Is.EqualTo(0));
            Assert.That(shop.SubtitleForTesting, Is.Not.EqualTo(board.SubtitleForTesting),
                "two kinds of kiosk should not read the same");

            // The choice survives a save, which is the whole point of moving it off the id.
            Assert.That(capture(shop).Style, Is.EqualTo(1));

            shop.CycleStyle();
            Assert.That(capture(shop).Style, Is.Not.EqualTo(1));
        }

        /// <summary>
        /// Every published world's accessory shop was recognised by its id and carries no style, so the old
        /// rule has to keep answering for those records.
        /// </summary>
        [Test]
        public void AKioskFromBeforeStylesKeepsItsLook()
        {
            var shop = create<WorldTerminal>(new EntityRecord { Id = "accessory-shop", Kind = EntityKinds.TERMINAL });
            var other = create<WorldTerminal>(new EntityRecord { Id = "quest-board", Kind = EntityKinds.TERMINAL });

            Assert.That(shop.StyleIndex, Is.EqualTo(1));
            Assert.That(other.StyleIndex, Is.EqualTo(0));
        }

        /// <summary>
        /// An NPC doubles as any object the player can press E on, so what makes it a character rather than
        /// scenery has to be stored: published worlds are full of characters, and none of them said so.
        /// </summary>
        [Test]
        public void AnNpcRemembersWhetherItIsAliveAndWhatSizeItIs()
        {
            var npc = create<GenericNpc>(new EntityRecord
            {
                Id = "sign-1", Kind = EntityKinds.NPC, Alive = false, Shadow = false,
                Width = 240, Height = 96, TextureFill = (int)SurfaceFillMode.Tile,
            });

            Assert.That(npc.Alive, Is.False);
            Assert.That(npc.CastsShadow, Is.False);
            Assert.That(npc.Size, Is.EqualTo(new Vector2(240, 96)));
            Assert.That(npc.TextureRepeats, Is.True);

            EntityRecord saved = capture(npc);

            Assert.That(saved.Alive, Is.False);
            Assert.That(saved.Shadow, Is.False);
            Assert.That(saved.Width, Is.EqualTo(240));
            Assert.That(saved.TextureFill, Is.EqualTo((int)SurfaceFillMode.Tile));

            // A record from before any of this existed is a living character, fitted, with a shadow.
            var old = create<GenericNpc>(new EntityRecord { Id = "npc-1", Kind = EntityKinds.NPC });

            Assert.That(old.Alive, Is.True);
            Assert.That(old.CastsShadow, Is.True);
            Assert.That(old.TextureRepeats, Is.False);
        }

        /// <summary>
        /// A hand-placed exit is a pair of coordinates, and half a point is not a point: clearing either
        /// coordinate has to clear the pair rather than leaving the passage aimed at an axis.
        /// </summary>
        [Test]
        public void APassageRemembersItsHandPlacedExit()
        {
            var door = create<SidePassage>(new EntityRecord
            {
                Id = "door-1", Kind = EntityKinds.PASSAGE, DestinationRoomId = "yard",
                ArrivalX = -120, ArrivalY = 340, Facing = 90,
            });

            Assert.That(door.ArrivalPoint, Is.EqualTo(new Vector2(-120, 340)));
            Assert.That(door.FacingDegrees, Is.EqualTo(90));

            EntityRecord saved = capture(door);
            Assert.That(saved.ArrivalX, Is.EqualTo(-120));
            Assert.That(saved.ArrivalY, Is.EqualTo(340));
            Assert.That(saved.Facing, Is.EqualTo(90));

            // Only one coordinate stored is not a point, so it is not read as one either.
            var half = create<SidePassage>(new EntityRecord
            {
                Id = "door-2", Kind = EntityKinds.PASSAGE, ArrivalX = 40,
            });

            Assert.That(half.ArrivalPoint, Is.Null);
        }

        /// <summary>
        /// Which way a door faces decides which side of it somebody arriving stands on. A door pointing
        /// east leaves them to its west, whatever corner of the room it is in.
        /// </summary>
        [Test]
        public void ADoorKnowsWhichWayItFaces()
        {
            var east = create<SidePassage>(new EntityRecord
            {
                Id = "east", Kind = EntityKinds.PASSAGE, PointsRight = true,
            });

            var west = create<SidePassage>(new EntityRecord
            {
                Id = "west", Kind = EntityKinds.PASSAGE, PointsRight = false,
            });

            Assert.That(east.ExitDirection!.Value.X, Is.EqualTo(1).Within(0.001f));
            Assert.That(west.ExitDirection!.Value.X, Is.EqualTo(-1).Within(0.001f));

            // Turned a quarter turn, it leads down rather than sideways.
            east.FacingDegrees = 90;
            Assert.That(east.ExitDirection!.Value.Y, Is.EqualTo(1).Within(0.001f));
        }

        /// <summary>
        /// A branch is one conversation, read through in order; the next conversation takes the next branch
        /// and comes back round to the first. Nothing has to be switched on for that — a second branch is
        /// the author saying so — and a character with one branch repeats it, as every character used to.
        /// </summary>
        [Test]
        public void EachConversationIsTheNextBranchAlong()
        {
            var villager = create<GenericNpc>(new EntityRecord { Id = "npc-1", Kind = EntityKinds.NPC });

            villager.SetDialogueBranches(
                new[] { new[] { "первая", "и её продолжение" }, new[] { "вторая" } },
                new[] { new[] { "first", "continued" }, new[] { "second" } });

            Assert.That(villager.BranchCount, Is.EqualTo(2));

            Assert.That(conversation(villager), Is.EqualTo(new[] { "первая", "и её продолжение" }));
            Assert.That(conversation(villager), Is.EqualTo(new[] { "вторая" }));
            Assert.That(conversation(villager), Is.EqualTo(new[] { "первая", "и её продолжение" }),
                "the rotation should come back round");

            // One branch is a character who says the same thing every time.
            var guard = create<GenericNpc>(new EntityRecord { Id = "npc-2", Kind = EntityKinds.NPC });
            guard.SetDialoguePages(new[] { "Стой." }, new[] { "Halt." });

            Assert.That(conversation(guard), Is.EqualTo(new[] { "Стой." }));
            Assert.That(conversation(guard), Is.EqualTo(new[] { "Стой." }));
        }

        /// <summary>
        /// A branch the player walked out of is not spent: the next conversation picks it up from the start
        /// rather than skipping to the next one.
        /// </summary>
        [Test]
        public void AnAbandonedBranchIsWaitingNextTime()
        {
            var villager = create<GenericNpc>(new EntityRecord { Id = "npc-1", Kind = EntityKinds.NPC });

            villager.SetDialogueBranches(
                new[] { new[] { "первая", "и её продолжение" }, new[] { "вторая" } },
                new[] { new[] { "first", "continued" }, new[] { "second" } });

            villager.BeginDialogue();
            villager.AdvanceDialogue();
            Assert.That(villager.CurrentDialogueLine, Is.EqualTo("и её продолжение"));
            villager.CloseDialogue();

            Assert.That(conversation(villager), Is.EqualTo(new[] { "первая", "и её продолжение" }));
            Assert.That(conversation(villager), Is.EqualTo(new[] { "вторая" }));
        }

        /// <summary>
        /// Branches are stored only once there is a second one, so a world of one-branch characters keeps
        /// exactly the shape it had — and the first branch is written the old way either way, so a client
        /// that knows nothing about branches still has something to show.
        /// </summary>
        [Test]
        public void BranchesAreSavedAndTheOldFieldStillHoldsTheFirstOne()
        {
            var villager = create<GenericNpc>(new EntityRecord { Id = "npc-1", Kind = EntityKinds.NPC });

            villager.SetDialoguePages(new[] { "одна" }, new[] { "one" });
            Assert.That(capture(villager).DialogueBranches, Is.Null, "one branch is what the old field is for");

            villager.SetDialogueBranches(
                new[] { new[] { "одна" }, new[] { "две" } },
                new[] { new[] { "one" }, new[] { "two" } });

            EntityRecord saved = capture(villager);

            Assert.That(saved.DialogueBranches!["ru"].Length, Is.EqualTo(2));
            Assert.That(saved.DialogueBranches["ru"][1], Is.EqualTo(new[] { "две" }));
            Assert.That(saved.DialogueLocalized!["ru"], Is.EqualTo(new[] { "одна" }));
            Assert.That(saved.Dialogue, Is.EqualTo(new[] { "one" }));

            // And what was saved is what comes back.
            var reloaded = create<GenericNpc>(saved);

            Assert.That(reloaded.BranchCount, Is.EqualTo(2));
            Assert.That(conversation(reloaded), Is.EqualTo(new[] { "одна" }));
            Assert.That(conversation(reloaded), Is.EqualTo(new[] { "две" }));
        }

        /// <summary>One whole conversation: every line it said, in order, until it closed itself.</summary>
        private static string[] conversation(GenericNpc npc)
        {
            var said = new List<string>();

            npc.BeginDialogue();

            while (npc.IsDialogueOpen)
            {
                said.Add(npc.CurrentDialogueLine);
                npc.AdvanceDialogue();

                Assert.That(said.Count, Is.LessThan(30), "a conversation that never ends");
            }

            return said.ToArray();
        }

        /// <summary>
        /// A story reward is stored only where it could ever be paid. Without a flag there is no "first
        /// time", so a reward on its own would be a promise nothing could keep.
        /// </summary>
        [Test]
        public void AStoryRewardIsOnlyKeptAlongsideAFlag()
        {
            var npc = create<GenericNpc>(new EntityRecord
            {
                Id = "mora", Kind = EntityKinds.NPC, StoryFlag = "mora.met", StoryFlagValue = 2,
                StoryExperience = 250, StoryCoins = 30,
            });

            Assert.That(npc.StoryExperience, Is.EqualTo(250));
            Assert.That(npc.StoryCoins, Is.EqualTo(30));

            EntityRecord saved = capture(npc);
            Assert.That(saved.StoryExperience, Is.EqualTo(250));
            Assert.That(saved.StoryCoins, Is.EqualTo(30));

            npc.StoryFlag = null;
            saved = capture(npc);

            Assert.That(saved.StoryExperience, Is.Null);
            Assert.That(saved.StoryCoins, Is.Null);
        }
    }
}
