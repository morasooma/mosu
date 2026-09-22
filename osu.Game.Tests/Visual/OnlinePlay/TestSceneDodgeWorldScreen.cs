// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Textures;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Screens.OnlinePlay.DodgeWorld;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Net;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Textures;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.OnlinePlay
{
    /// <summary>
    /// Covers what only a live screen can show: loading, publishing, and the editor's presentation.
    /// </summary>
    /// <remarks>
    /// Movement and combat are covered by <c>WorldSimulationTest</c> against the simulation directly,
    /// so this no longer has to drive a fight through the input manager to assert on the rules.
    /// </remarks>
    public partial class TestSceneDodgeWorldScreen : ScreenTestScene
    {
        private const string server_world = """
            {"version":1,"initial_room_id":"east","default_weapon_skin_id":"rose",
             "weapon_skins":[{"id":"rose","display_name":"Rose sword","texture":"https://localhost/dodge-world/sword.png","display_width":176}],
             "rooms":[{"id":"mora-plaza","name":"Mora Plaza","spawn_x":0,"spawn_y":350,"entities":[]},
                      {"id":"east","name":"East District","spawn_x":12,"spawn_y":34,
                       "entities":[{"id":"guide","kind":"npc","display_name":"Guide","x":20,"y":10,
                                    "dialogue_localized":{"ru":["Первая реплика","Вторая реплика"],"en":["Page one","Page two"]}}]}]}
            """;

        [Test]
        public void TestServerWorldLoadsAndPublishes()
        {
            DodgeWorldScreen screen = null!;
            var published = false;

            AddStep("log in and serve a world", () =>
            {
                var api = (DummyAPIAccess)API;
                api.Login("world-owner", "password");
                api.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetDodgeWorldRequest get:
                            get.TriggerSuccess(response(7));
                            return true;

                        case ReplaceDodgeWorldRequest replace:
                            published = true;
                            replace.TriggerSuccess(response(8));
                            return true;

                        default:
                            return false;
                    }
                };
            });

            AddStep("load screen", () => LoadScreen(screen = new DodgeWorldScreen()));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddAssert("opened the document's initial room", () => screen.CurrentRoomIdForTesting, () => Is.EqualTo("east"));
            AddAssert("every room loaded", () => screen.RoomCountForTesting, () => Is.EqualTo(2));
            AddAssert("server granted editing", () => screen.CanEditForTesting);
            AddAssert("revision stored", () => screen.RevisionForTesting, () => Is.EqualTo(7));
            AddAssert("room entities built", () => screen.EntityCountForTesting, () => Is.EqualTo(1));

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("publish", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.S);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("publish sent", () => published);
            AddAssert("new revision stored", () => screen.RevisionForTesting, () => Is.EqualTo(8));
        }

        private static DodgeWorldApiResponse response(long revision) => new DodgeWorldApiResponse
        {
            Revision = revision,
            CanEdit = true,
            OnlineUsers = 9,
            World = JObject.Parse(server_world),
            Progression = new DodgeWorldProgressionResponse
            {
                Level = 3,
                Experience = 75,
                ExperienceForNextLevel = 200,
                Coins = 42,
            },
        };

        [Test]
        public void TestLocalWorldOpensDefaultRoom()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddAssert("editor hidden until opened", () => screen.EditorPanelAlphaForTesting, () => Is.Zero);
            AddAssert("default room has its surfaces", () => surfaces(screen), () => Is.EqualTo(3));
            AddAssert("Mora is present", () => screen.EntitiesForTesting.OfType<MoraNpc>().Count(), () => Is.EqualTo(1));

            // Her bundled sprite used to be the only thing she could look like.
            AddAssert("Mora can be re-skinned",
                () => screen.EntitiesForTesting.OfType<MoraNpc>().First(), () => Is.InstanceOf<ITexturedEntity>());

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddUntilStep("editor visible", () => screen.EditorPanelAlphaForTesting, () => Is.EqualTo(1));
        }

        private static int surfaces(DodgeWorldScreen screen) =>
            screen.EntitiesForTesting.Count(entity => entity.LayoutKind == EntityKinds.SURFACE);

        /// <summary>
        /// The property editor used to show every field of every kind at once, unlabelled. It should
        /// now show captions, and only the ones belonging to the selection.
        /// </summary>
        [Test]
        public void TestInspectorShowsOnlyTheSelectionsLabelledFields()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("add a mob zone", () => screen.AddMobSpawnZone());

            AddAssert("mob fields are captioned", () => labels(screen),
                () => Does.Contain("Здоровье моба").And.Contains("Скорость пули").And.Contains("Пауза между залпами"));
            AddAssert("name is always offered", () => labels(screen), () => Does.Contain("Название"));
            AddAssert("no passage fields on a mob zone", () => labels(screen),
                () => Does.Not.Contain("ID комнаты назначения"));

            AddStep("select a passage instead", () =>
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First()));

            AddAssert("passage fields appear", () => labels(screen), () => Does.Contain("ID комнаты назначения"));
            AddAssert("no mob fields on a passage", () => labels(screen), () => Does.Not.Contain("Здоровье моба"));
        }

        /// <summary>
        /// A session backed by nothing but memory, so tests neither reach the network nor share a
        /// world file with one another.
        /// </summary>
        private static DodgeWorldSession freshSession() => new InMemorySession(DefaultWorld.CreateDocument());

        private sealed class InMemorySession : DodgeWorldSession
        {
            private readonly System.Collections.Generic.HashSet<WarpKey> openedWarps = new System.Collections.Generic.HashSet<WarpKey>();
            private DodgeWorldDocument document;

            /// <summary>
            /// Warps open for free, as in any world with no coin balance behind it.
            /// </summary>
            public override void UnlockWarp(string roomId, string entityId, System.Action<WarpOutcome> onCompleted)
            {
                bool added = openedWarps.Add(new WarpKey(roomId, entityId));
                SetUnlockedWarps(openedWarps.ToArray());
                onCompleted(added ? WarpOutcome.Paid : WarpOutcome.AlreadyUnlocked);
            }

            public override void TravelToWarp(string roomId, string entityId, System.Action<WarpOutcome> onCompleted) =>
                onCompleted(openedWarps.Contains(new WarpKey(roomId, entityId)) ? WarpOutcome.Paid : WarpOutcome.NotUnlocked);

            public InMemorySession(DodgeWorldDocument document)
            {
                this.document = document;
                SetCanEdit(true);
            }

            public override void Load(System.Action<DodgeWorldDocument> onLoaded)
            {
                SetAvailability(DodgeWorldAvailability.Ready);
                onLoaded(document);
            }

            public override void Publish(DodgeWorldDocument updated, System.Action<PublishOutcome> onCompleted)
            {
                document = updated;
                SetRevision(Revision.Value + 1);
                onCompleted(PublishOutcome.Published);
            }

            public override void StoreTexture(TextureImport import, System.Action<string> onStored, System.Action onFailed) => onFailed();
        }

        private static System.Collections.Generic.IReadOnlyList<string> labels(DodgeWorldScreen screen) =>
            screen.EditorPanelForTesting.Inspector.FieldLabels;

        /// <summary>
        /// An obstacle course is one device repeated, so copying has to keep everything about it and give
        /// the copy its own identity — an id is what a reward claim and a warp price are keyed by.
        /// </summary>
        [Test]
        public void TestAnObjectCanBeDuplicatedAndPastedIntoAnotherRoom()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("add an emitter and aim it", () =>
            {
                screen.AddEmitter();
                emitterOf(screen).Direction = 210;
                emitterOf(screen).PhaseMilliseconds = 450;
            });

            AddStep("duplicate it", () => screen.DuplicateSelected());

            AddAssert("there are two", () => screen.EntitiesForTesting.OfType<WorldEmitter>().Count(), () => Is.EqualTo(2));
            AddAssert("the copy keeps the settings", () => emitterOf(screen).Direction, () => Is.EqualTo(210));
            AddAssert("the copy keeps the phase", () => emitterOf(screen).PhaseMilliseconds, () => Is.EqualTo(450));
            AddAssert("the copy has its own id", () => screen.EntitiesForTesting.OfType<WorldEmitter>()
                                                             .Select(emitter => emitter.EntityId).Distinct().Count(),
                () => Is.EqualTo(2));
            AddAssert("the copy is not on top of the original", () => screen.EntitiesForTesting.OfType<WorldEmitter>()
                                                                           .Select(emitter => emitter.Position).Distinct().Count(),
                () => Is.EqualTo(2));

            AddStep("copy it", () => screen.CopySelected());
            AddStep("branch a room off a passage", () =>
            {
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight));
                screen.CreateConnectedRoom();
            });

            AddUntilStep("now in the new room", () => screen.CurrentRoomIdForTesting,
                () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddAssert("the new room has no emitter yet", () => screen.EntitiesForTesting.OfType<WorldEmitter>(), () => Is.Empty);

            AddStep("paste", () => screen.PasteCopied());
            AddAssert("the emitter crossed rooms", () => emitterOf(screen).Direction, () => Is.EqualTo(210));
        }

        private static WorldEmitter emitterOf(DodgeWorldScreen screen) =>
            (WorldEmitter)screen.EntitiesForTesting.Last(entity => entity is WorldEmitter);

        /// <summary>
        /// Panels all sit at one depth, so which is on top used to be decided by the container's own
        /// insertion order and could not be changed at all. It is the room's list order now.
        /// </summary>
        [Test]
        public void TestTheAuthorDecidesWhichOverlappingObjectIsOnTop()
        {
            DodgeWorldScreen screen = null!;
            string first = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("add two panels on one spot", () =>
            {
                screen.AddSurface();
                first = screen.EntitiesForTesting[^1].EntityId;
                screen.AddSurface();
            });

            AddAssert("the newer panel is in front", () => frontmostSurface(screen).EntityId, () => Is.Not.EqualTo(first));

            AddStep("send it back", () => screen.SendSelectionToBack());
            AddAssert("the older panel is in front now", () => frontmostSurface(screen).EntityId, () => Is.EqualTo(first));

            AddStep("select the older one", () => screen.SelectForTesting(
                screen.EntitiesForTesting.First(entity => entity.EntityId == first)));
            AddStep("bring it to the front", () => screen.BringSelectionToFront());
            AddAssert("it stays in front", () => frontmostSurface(screen).EntityId, () => Is.EqualTo(first));
        }

        /// <summary>
        /// The frontmost object is the one drawn last, which is the one with the lowest depth.
        /// </summary>
        private static EditableWorldEntity frontmostSurface(DodgeWorldScreen screen) =>
            screen.EntitiesForTesting.Where(entity => entity is RoomSurface).OrderBy(entity => entity.Depth).First();

        /// <summary>
        /// A floor built out of panels should look like a floor. Two panels laid flush used to show their
        /// seam twice over — both drew a border there, and the one in front drew its border across the
        /// other's fill.
        /// </summary>
        [Test]
        public void TestPanelsLaidFlushDropTheSeamBetweenThem()
        {
            DodgeWorldScreen screen = null!;
            RoomSurface left = null!;
            RoomSurface right = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            // Placed clear of the room's own three panels and well apart, so this is about these two and
            // nothing else.
            AddStep("add two panels apart from each other", () =>
            {
                screen.AddSurface();
                left = (RoomSurface)screen.EntitiesForTesting[^1];
                screen.AddSurface();
                right = (RoomSurface)screen.EntitiesForTesting[^1];

                left.Size = right.Size = new Vector2(200, 100);
                left.Position = new Vector2(-900, -450);
                right.Position = new Vector2(-400, -450);
            });

            AddUntilStep("a panel on its own keeps its frame",
                () => left.FramedOutlineForTesting && right.FramedOutlineForTesting);

            AddStep("lay them flush", () => right.Position = new Vector2(-700, -450));

            AddUntilStep("the seam is gone", () => left.DrawnOutlineForTesting,
                () => Is.EqualTo(Edges.Top | Edges.Left | Edges.Bottom));
            AddAssert("and gone from the other side too", () => right.DrawnOutlineForTesting,
                () => Is.EqualTo(Edges.Top | Edges.Right | Edges.Bottom));
            AddAssert("the frame stepped aside for it", () => left.FramedOutlineForTesting, () => Is.False);

            AddStep("pull them apart", () => right.Position = new Vector2(-400, -450));

            AddUntilStep("both are framed again", () => left.FramedOutlineForTesting && right.FramedOutlineForTesting);
            AddAssert("no loose edges left over", () => left.DrawnOutlineForTesting, () => Is.EqualTo(Edges.None));
        }

        /// <summary>
        /// A wide panel meeting a narrow one loses its outline exactly where the two meet and keeps the
        /// rest. That is the difference between a corridor with a mouth and a corridor with a line drawn
        /// across it, which is what dropping whole sides at a time gave.
        /// </summary>
        [Test]
        public void TestACorridorMouthKeepsTheFloorEitherSideOfIt()
        {
            DodgeWorldScreen screen = null!;
            RoomSurface hall = null!;
            RoomSurface corridor = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            // Clear of the room's own three panels, so this is about these two and nothing else.
            AddStep("lay a corridor off a hall", () =>
            {
                screen.AddSurface();
                hall = (RoomSurface)screen.EntitiesForTesting[^1];
                screen.AddSurface();
                corridor = (RoomSurface)screen.EntitiesForTesting[^1];

                hall.Size = new Vector2(400, 160);
                hall.Position = new Vector2(-850, -420);

                corridor.Size = new Vector2(100, 160);
                corridor.Position = new Vector2(-850, -260);
            });

            AddUntilStep("the corridor's mouth is open", () => corridor.DrawnOutlineForTesting,
                () => Is.EqualTo(Edges.Bottom | Edges.Left | Edges.Right));

            AddAssert("and the hall's floor carries on either side of it", () =>
                    hall.DrawnStretchesForTesting.Count(span => span.Side == Edges.Bottom),
                () => Is.EqualTo(2));

            AddAssert("with the mouth itself left out", () =>
                    hall.DrawnStretchesForTesting.Where(span => span.Side == Edges.Bottom).Sum(span => span.To - span.From),
                () => Is.LessThan(0.85f));
        }

        /// <summary>
        /// The chat panel is a window: it can be moved out of the way and made big enough to read.
        /// </summary>
        [Test]
        public void TestTheChatPanelCanBeMovedAndResized()
        {
            DodgeWorldScreen screen = null!;
            Vector2 wasAt = Vector2.Zero;
            Vector2 wasSized = Vector2.Zero;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("remember where it sits", () =>
            {
                wasAt = screen.ChatForTesting.OffsetForTesting;
                wasSized = screen.ChatForTesting.SizeForTesting;
            });

            // Two moves after the press: the framework either starts a drag or applies one per mouse move.
            AddStep("drag it by its title", () =>
            {
                Quad panel = screen.ChatForTesting.ScreenSpaceDrawQuad;
                InputManager.MoveMouseTo(panel.TopLeft + new Vector2(panel.Width / 2, 16));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(panel.TopLeft + new Vector2(panel.Width / 2 - 40, -24));
                InputManager.MoveMouseTo(panel.TopLeft + new Vector2(panel.Width / 2 - 80, -64));
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddAssert("it followed the cursor up and to the left",
                () => screen.ChatForTesting.OffsetForTesting.X < wasAt.X - 40
                      && screen.ChatForTesting.OffsetForTesting.Y < wasAt.Y - 30);

            AddStep("drag its corner outwards", () =>
            {
                Quad panel = screen.ChatForTesting.ScreenSpaceDrawQuad;
                InputManager.MoveMouseTo(panel.TopLeft + new Vector2(8, 8));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(panel.TopLeft + new Vector2(-40, -30));
                InputManager.MoveMouseTo(panel.TopLeft + new Vector2(-90, -70));
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddAssert("it grew", () => screen.ChatForTesting.SizeForTesting.X, () => Is.GreaterThan(wasSized.X));
            AddAssert("in both directions", () => screen.ChatForTesting.SizeForTesting.Y, () => Is.GreaterThan(wasSized.Y));
        }

        /// <summary>
        /// Clicking the same spot again reaches what is behind, which is how every editor with overlapping
        /// objects behaves. Without it an object underneath another could not be selected at all — the one
        /// in front always won the click.
        /// </summary>
        [Test]
        public void TestClickingTwiceReachesTheObjectUnderneath()
        {
            DodgeWorldScreen screen = null!;
            EditableWorldEntity behind = null!;
            EditableWorldEntity inFront = null!;
            Vector2 overlap = Vector2.Zero;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            // Left where the editor puts them, in the middle of the view, so they are actually on screen to
            // be clicked. The room's own panels are under the cursor too, which is what the walk round the
            // whole stack below accounts for.
            AddStep("add two panels on one spot", () =>
            {
                screen.AddSurface();
                behind = screen.EntitiesForTesting[^1];
                screen.AddSurface();
                inFront = screen.EntitiesForTesting[^1];
            });

            AddUntilStep("the stack settled", () => inFront.Depth, () => Is.LessThan(behind.Depth));
            AddStep("click the overlap", () =>
            {
                overlap = inFront.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(overlap);
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("the front one is chosen first", () => screen.SelectedForTesting, () => Is.SameAs(inFront));

            AddStep("click the same spot again", () => InputManager.Click(MouseButton.Left));
            AddAssert("now the one underneath", () => screen.SelectedForTesting, () => Is.SameAs(behind));

            // The room's own panels are under the cursor as well, so the walk carries on into them rather
            // than flipping between the two on top — which is the difference between reaching everything
            // and reaching only the first two.
            AddStep("keep clicking", () => InputManager.Click(MouseButton.Left));
            AddAssert("it keeps walking rather than flipping back",
                () => screen.SelectedForTesting, () => Is.Not.SameAs(inFront));
        }

        /// <summary>
        /// Dragging moves what is selected, even with another object drawn over it. Placing an object and
        /// immediately dragging it used to pick up its neighbour instead.
        /// </summary>
        [Test]
        public void TestDraggingMovesTheSelectionAndNotWhateverIsDrawnOverIt()
        {
            DodgeWorldScreen screen = null!;
            EditableWorldEntity behind = null!;
            EditableWorldEntity inFront = null!;
            Vector2 startedAt = Vector2.Zero;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("add two panels on one spot", () =>
            {
                screen.AddSurface();
                behind = screen.EntitiesForTesting[^1];
                screen.AddSurface();
                inFront = screen.EntitiesForTesting[^1];
            });

            Vector2 grabbedAt = Vector2.Zero;

            AddStep("select the one underneath", () =>
            {
                screen.SelectForTesting(behind);
                startedAt = behind.Position;
                grabbedAt = inFront.ScreenSpaceDrawQuad.Centre;
            });

            AddStep("press on the overlap", () =>
            {
                InputManager.MoveMouseTo(grabbedAt);
                InputManager.PressButton(MouseButton.Left);
            });

            // Two moves, not one: the move that passes the drag threshold starts the drag, and only the
            // next one is applied to whatever took it.
            AddStep("begin the drag", () => InputManager.MoveMouseTo(grabbedAt + new Vector2(60, 0)));
            AddStep("drag on", () => InputManager.MoveMouseTo(grabbedAt + new Vector2(160, 0)));
            AddStep("let go", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("the selected panel moved", () => behind.Position, () => Is.Not.EqualTo(startedAt));
            AddAssert("the panel on top stayed", () => inFront.Position, () => Is.EqualTo(startedAt));
            AddAssert("the selection did not change", () => screen.SelectedForTesting, () => Is.SameAs(behind));
        }

        /// <summary>
        /// A corridor has to work in both directions without anything being authored about it: walking back
        /// through a door should put the player at that door, not at wherever the room's entrance happens
        /// to be.
        /// </summary>
        [Test]
        public void TestWalkingBackThroughADoorArrivesAtThatDoor()
        {
            DodgeWorldScreen screen = null!;
            string branch = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("branch a room off the east passage", () =>
            {
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight));
                screen.CreateConnectedRoom();
            });

            AddUntilStep("now in the new room", () => screen.CurrentRoomIdForTesting,
                () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddStep("remember which room", () => branch = screen.CurrentRoomIdForTesting);
            AddStep("close editor", () => InputManager.Key(Key.F2));

            AddStep("walk into the way back", () => screen.SimulationForTesting.PlacePlayer(
                screen.EntitiesForTesting.OfType<WorldPassage>()
                      .First(passage => passage.DestinationRoomId == DodgeWorldDefaults.ROOT_ROOM_ID).Position));

            AddUntilStep("back where we started", () => screen.CurrentRoomIdForTesting,
                () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddAssert("standing beside the door we came out of", () =>
            {
                WorldPassage door = screen.EntitiesForTesting.OfType<WorldPassage>()
                                          .First(passage => passage.DestinationRoomId == branch);
                float distance = (screen.LocalPlayerForTesting.Position - door.Position).Length;

                // Clear of the door, so turning round is a choice, but next to it rather than at the
                // room's entrance on the other side of the map.
                return distance > door.TriggerRadius && distance < door.TriggerRadius + 120;
            });
        }

        /// <summary>
        /// Two corridors between the same two rooms are two corridors: walking through one of them has to
        /// come out at its own far end. Arrival used to take «whichever passage here leads back to that
        /// room», and whichever meant the first one found — so the second corridor delivered people to the
        /// first one's door.
        /// </summary>
        [Test]
        public void TestASecondCorridorComesOutAtItsOwnFarEnd()
        {
            DodgeWorldScreen screen = null!;
            string branch = null!;
            string westDoor = null!;
            string farEnd = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("branch a room off the east passage", () =>
            {
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight));
                screen.CreateConnectedRoom();
            });

            AddUntilStep("now in the new room", () => screen.CurrentRoomIdForTesting,
                () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddStep("remember which room", () => branch = screen.CurrentRoomIdForTesting);

            AddStep("link the west passage to it as well", () =>
            {
                screen.SwitchRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID);

                SidePassage west = screen.EntitiesForTesting.OfType<SidePassage>().First(passage => !passage.PointsRight);
                westDoor = west.EntityId;
                screen.SelectForTesting(west);
                screen.LinkSelectedPassageToPreviousRoom();
            });

            AddStep("give the branch a second way back", () =>
            {
                screen.SwitchRoomForTesting(branch);
                screen.AddSidePassage();

                EditableWorldEntity added = screen.EntitiesForTesting[^1];
                added.Position = new Vector2(300, 400);
                farEnd = added.EntityId;

                screen.SelectForTesting(added);
                screen.LinkSelectedPassageToPreviousRoom();
            });

            AddAssert("the two new doors are a pair", () =>
                screen.EntitiesForTesting.First(entity => entity.EntityId == farEnd) is WorldPassage door
                && door.ArrivalEntityId == westDoor);

            AddStep("close editor", () => InputManager.Key(Key.F2));
            AddStep("go back and walk into the west passage", () =>
            {
                screen.SwitchRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID);
                screen.SimulationForTesting.PlacePlayer(
                    screen.EntitiesForTesting.First(entity => entity.EntityId == westDoor).Position);
            });

            AddUntilStep("arrived in the branch", () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(branch));

            AddAssert("standing at the far end of the corridor we walked into", () =>
            {
                WorldPassage[] waysBack = screen.EntitiesForTesting.OfType<WorldPassage>()
                                                .Where(passage => passage.DestinationRoomId == DodgeWorldDefaults.ROOT_ROOM_ID)
                                                .ToArray();

                WorldPassage mine = waysBack.First(passage => passage.EntityId == farEnd);
                WorldPassage other = waysBack.First(passage => passage.EntityId != farEnd);

                float toMine = (screen.LocalPlayerForTesting.Position - mine.Position).Length;
                float toOther = (screen.LocalPlayerForTesting.Position - other.Position).Length;

                return toMine < toOther && toMine > mine.TriggerRadius && toMine < mine.TriggerRadius + 120;
            });
        }

        /// <summary>
        /// Where a passage leads is a question about the world, and the map is what shows the world.
        /// Typing an id into a field asks the author to remember what they are already looking at.
        /// </summary>
        [Test]
        public void TestAPassageIsLinkedByPickingARoomOnTheMap()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("branch a room off the east passage", () =>
            {
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight));
                screen.CreateConnectedRoom();
            });

            AddUntilStep("now in the new room", () => screen.CurrentRoomIdForTesting,
                () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddStep("select a passage that leads nowhere", () => screen.SelectForTesting(
                screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.DestinationRoomId == null)));

            AddStep("ask where it goes", () => screen.LinkSelectedPassage());
            AddAssert("the map is asking", () => screen.WorldMapForTesting.IsLinking);

            AddStep("pick the plaza", () => screen.WorldMapForTesting.ClickRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddAssert("the map closed", () => !screen.WorldMapForTesting.IsOpen);
            AddAssert("the passage leads there now", () => screen.EntitiesForTesting.OfType<SidePassage>()
                                                                 .Count(passage => passage.DestinationRoomId == DodgeWorldDefaults.ROOT_ROOM_ID),
                () => Is.EqualTo(1));
        }

        /// <summary>
        /// Anything can be deleted, and Ctrl+Z brings it back.
        /// </summary>
        [Test]
        public void TestDeleteAndUndo()
        {
            DodgeWorldScreen screen = null!;
            var before = 0;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("select Mora", () =>
            {
                before = screen.EntityCountForTesting;
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<MoraNpc>().First());
            });

            AddStep("delete it", () => screen.DeleteSelected());
            AddAssert("entity is gone", () => screen.EntityCountForTesting, () => Is.EqualTo(before - 1));
            AddAssert("Mora is gone", () => screen.EntitiesForTesting.OfType<MoraNpc>(), () => Is.Empty);

            AddStep("undo", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Z);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddAssert("entity count restored", () => screen.EntityCountForTesting, () => Is.EqualTo(before));
            AddAssert("Mora is back", () => screen.EntitiesForTesting.OfType<MoraNpc>().Count(), () => Is.EqualTo(1));
        }

        /// <summary>
        /// Growing a room should extend it away from its existing layout rather than pushing both
        /// edges out and leaving everything already placed adrift in the middle.
        /// </summary>
        [Test]
        public void TestGrowingARoomKeepsItsContentsInPlace()
        {
            DodgeWorldScreen screen = null!;
            Vector2 mora = Vector2.Zero;
            Vector2 size = Vector2.Zero;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("remember the layout", () =>
            {
                size = screen.RoomSizeForTesting;
                mora = screen.EntitiesForTesting.OfType<MoraNpc>().First().Position;
            });

            AddStep("grow the room", () => screen.ApplyRoomProperties(string.Empty,
                (size.X + 400).ToString("0", CultureInfo.InvariantCulture),
                (size.Y + 600).ToString("0", CultureInfo.InvariantCulture)));

            AddAssert("room grew", () => screen.RoomSizeForTesting, () => Is.EqualTo(size + new Vector2(400, 600)));

            // Room coordinates run from the centre, so holding the distance to the top left corner
            // means the stored position moves by half of the growth.
            AddAssert("Mora kept her place in the room",
                () => screen.EntitiesForTesting.OfType<MoraNpc>().First().Position,
                () => Is.EqualTo(mora - new Vector2(200, 300)));

            AddStep("undo", () => screen.Undo());
            AddAssert("size restored", () => screen.RoomSizeForTesting, () => Is.EqualTo(size));
            AddAssert("Mora restored",
                () => screen.EntitiesForTesting.OfType<MoraNpc>().First().Position, () => Is.EqualTo(mora));
        }

        /// <summary>
        /// Walking should show what is ahead. The deadzone alone left the camera trailing the player,
        /// so most of the screen was taken up by where they had come from.
        /// </summary>
        [Test]
        public void TestCameraLeansTowardsWhereThePlayerWalks()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            // A room far larger than the screen, so the answer is about following rather than about
            // the camera running into the room's edge.
            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("make the room large", () => screen.ApplyRoomProperties(string.Empty, "6000", "4000"));
            AddStep("close editor", () => InputManager.Key(Key.F2));

            AddStep("walk right", () => InputManager.PressKey(Key.D));
            AddUntilStep("player is well clear of the spawn",
                () => screen.LocalPlayerForTesting.Position.X, () => Is.GreaterThan(600));

            // Trailing by the deadzone alone would put the camera 175 units behind the player.
            AddAssert("camera is not trailing behind",
                () => screen.CameraPositionForTesting.X,
                () => Is.GreaterThan(screen.LocalPlayerForTesting.Position.X - 100));

            AddStep("stop walking", () => InputManager.ReleaseKey(Key.D));
            AddUntilStep("camera eases back to the player",
                () => screen.CameraPositionForTesting.X,
                () => Is.LessThan(screen.LocalPlayerForTesting.Position.X - 150));
        }

        /// <summary>
        /// Escape is needed inside the world, and leaving by accident while reaching for it was easy.
        /// </summary>
        [Test]
        public void TestBackClosesWhatIsOpenBeforeLeaving()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("select Mora", () => screen.SelectForTesting(screen.EntitiesForTesting.OfType<MoraNpc>().First()));

            AddAssert("back clears the selection", () => screen.OnBackButton());
            AddAssert("still editing", () => screen.EditorModeForTesting);

            AddAssert("back leaves the editor", () => screen.OnBackButton());
            AddAssert("editor closed", () => !screen.EditorModeForTesting);

            AddAssert("back asks before leaving the world", () => screen.OnBackButton());
            AddAssert("a second back leaves", () => !screen.OnBackButton());
        }

        /// <summary>
        /// Walks the whole warp feature: a warp is opened where it stands, and travel is offered only
        /// between warps this player has opened.
        /// </summary>
        [Test]
        public void TestWarpsAreOpenedThenTravelledBetween()
        {
            DodgeWorldScreen screen = null!;
            string branchedRoomId = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("stand on the plaza warp", () => standOnWarp(screen));
            AddStep("open it", () => InputManager.Key(Key.E));
            AddAssert("plaza warp is open", () => warp(screen).Unlocked);

            AddStep("branch a new room", () =>
            {
                InputManager.Key(Key.F2);
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight));
                screen.CreateConnectedRoom();
            });

            AddUntilStep("now in the new room", () => screen.CurrentRoomIdForTesting, () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddStep("close the editor", () =>
            {
                branchedRoomId = screen.CurrentRoomIdForTesting;
                InputManager.Key(Key.F2);
            });

            AddStep("stand on that room's warp", () => standOnWarp(screen));
            AddStep("open it", () => InputManager.Key(Key.E));
            AddAssert("second warp is open", () => warp(screen).Unlocked);

            AddStep("ask where it leads", () => InputManager.Key(Key.E));
            AddAssert("the map is the picker", () => screen.WorldMapForTesting.IsTravelling);
            AddAssert("with every room still drawn",
                () => screen.WorldMapForTesting.NodeCountForTesting, () => Is.EqualTo(screen.RoomCountForTesting));

            // The warp being stood on is not a destination, and neither is anything unopened: the rooms
            // holding those are on the map as context, not as somewhere to go.
            AddAssert("only the plaza can be travelled to",
                () => string.Join(",", screen.WorldMapForTesting.TravelDestinationsForTesting),
                () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddStep("travel", () => screen.WorldMapForTesting.ClickRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddUntilStep("back in the plaza",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddAssert("the map closed", () => !screen.WorldMapForTesting.IsOpen);
            AddAssert("and is no longer a picker", () => !screen.WorldMapForTesting.IsTravelling);
            AddAssert("left the branched room", () => branchedRoomId, () => Is.Not.EqualTo(screen.CurrentRoomIdForTesting));

            // Arrival is at the warp itself rather than at the room's entrance.
            AddAssert("standing on the plaza warp",
                () => (screen.LocalPlayerForTesting.Position - warp(screen).Position).Length, () => Is.LessThan(1));
        }

        /// <summary>
        /// A room can hold more than one warp, and then the map has no way to say which one was meant.
        /// That is the one question the list still answers.
        /// </summary>
        [Test]
        public void TestARoomWithSeveralWarpsFallsBackToTheList()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("give the plaza a second warp", () =>
            {
                screen.AddWarp();

                // Away from the first, so that standing on one is not standing on both.
                WorldWarp[] points = screen.EntitiesForTesting.OfType<WorldWarp>().ToArray();
                points[1].Position = points[0].Position + new Vector2(400, 0);
            });

            AddStep("stand on the first warp", () => standOnWarpAt(screen, 0));
            AddStep("open it", () => InputManager.Key(Key.E));
            AddStep("stand on the second", () => standOnWarpAt(screen, 1));
            AddStep("open it", () => InputManager.Key(Key.E));

            AddAssert("both are open",
                () => screen.EntitiesForTesting.OfType<WorldWarp>().Count(point => point.Unlocked), () => Is.EqualTo(2));

            AddStep("branch a new room", () =>
            {
                InputManager.Key(Key.F2);
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight));
                screen.CreateConnectedRoom();
            });

            AddUntilStep("now in the new room",
                () => screen.CurrentRoomIdForTesting, () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddStep("close the editor", () => InputManager.Key(Key.F2));

            AddStep("stand on the new room's warp", () => standOnWarp(screen));
            AddStep("open it", () => InputManager.Key(Key.E));

            AddStep("ask where it leads", () => InputManager.Key(Key.E));
            AddAssert("the plaza is one destination on the map",
                () => screen.WorldMapForTesting.TravelDestinationsForTesting,
                () => Is.EqualTo(new[] { DodgeWorldDefaults.ROOT_ROOM_ID }));

            AddStep("pick the plaza", () => screen.WorldMapForTesting.ClickRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID));

            // Two warps stand there, so it asks rather than guessing.
            AddAssert("the map gave way to the list", () => !screen.WorldMapForTesting.IsOpen);
            AddAssert("which offers both", () => screen.WarpMenuForTesting.Destinations.Count, () => Is.EqualTo(2));

            AddStep("travel to the first", () => screen.WarpMenuForTesting.Destinations[0].Action!.Invoke());
            AddUntilStep("in the plaza",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
        }

        /// <summary>
        /// The story loop end to end: a hidden object, a conversation that records progress, and the object
        /// appearing without the player having to leave the room.
        /// </summary>
        [Test]
        public void TestAStoryFlagRevealsWhatItGates()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("make Mora a story point and gate a door on it", () =>
            {
                InputManager.Key(Key.F2);

                MoraNpc mora = screen.EntitiesForTesting.OfType<MoraNpc>().First();
                mora.StoryFlag = "mora.met";
                mora.StoryFlagValue = 1;

                screen.AddPortal();
                EditableWorldEntity door = screen.EntitiesForTesting.Last();
                door.Visibility = StoryCondition.Read("mora.met", null, null);

                InputManager.Key(Key.F2);
            });

            AddUntilStep("the door is hidden from the player",
                () => screen.EntitiesForTesting.Any(entity => entity.Visibility.IsSet), () => Is.False);

            // Hidden is hidden, not deleted: the record has to survive a room change, or authoring a story
            // would quietly delete everything the player has not unlocked yet.
            AddStep("walk somewhere and back", () =>
            {
                screen.SwitchRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID);
                screen.SwitchRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID);
            });

            AddAssert("the door still exists in the room",
                () => screen.RoomsForTesting[DodgeWorldDefaults.ROOT_ROOM_ID].Entities
                            .Count(record => record.VisibleIfFlag == "mora.met"), () => Is.EqualTo(1));

            AddStep("stand next to Mora", () => screen.SimulationForTesting.PlacePlayer(
                screen.EntitiesForTesting.OfType<MoraNpc>().First().Position));

            AddStep("talk to her", () => InputManager.Key(Key.E));
            AddUntilStep("she is talking", () => screen.DialogueOpenForTesting);

            AddStep("hear her out", () =>
            {
                for (int i = 0; i < 12; i++)
                    screen.EntitiesForTesting.OfType<MoraNpc>().First().AdvanceDialogue();
            });

            AddUntilStep("the door appeared without leaving the room",
                () => screen.EntitiesForTesting.Any(entity => entity.Visibility.IsSet), () => Is.True);
        }

        private static WorldWarp warp(DodgeWorldScreen screen) => screen.EntitiesForTesting.OfType<WorldWarp>().First();

        private static void standOnWarp(DodgeWorldScreen screen) =>
            screen.SimulationForTesting.PlacePlayer(warp(screen).Position);

        private static void standOnWarpAt(DodgeWorldScreen screen, int index) =>
            screen.SimulationForTesting.PlacePlayer(screen.EntitiesForTesting.OfType<WorldWarp>().ElementAt(index).Position);

        /// <summary>
        /// The world is shared, so it does not stop for a conversation — and since it does not stop, the
        /// player is not held in place either: walking away is what ends a conversation.
        /// </summary>
        [Test]
        public void TestTheWorldKeepsRunningDuringAConversation()
        {
            DodgeWorldScreen screen = null!;
            double clock = 0;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world loaded", () => screen.IsLoaded && screen.IsCurrentScreen() && screen.WorldLoadedForTesting);

            AddStep("walk towards Mora", () => InputManager.PressKey(Key.W));
            AddUntilStep("player is near Mora", () => screen.LocalPlayerForTesting.Position.Y, () => Is.LessThan(180));
            AddStep("stop walking", () => InputManager.ReleaseKey(Key.W));

            AddStep("open dialogue", () => InputManager.Key(Key.E));
            AddAssert("dialogue opened", () => screen.DialogueOpenForTesting);
            AddUntilStep("Mora's bubble shows her first line",
                () => screen.EntitiesForTesting.OfType<MoraNpc>().First().BubbleForTesting!.LineForTesting,
                () => Is.EqualTo("Welcome to Mora Plaza. Every road in Dodge World begins here."));

            AddStep("remember the world's clock", () => clock = screen.WorldClockForTesting);
            AddUntilStep("the world carries on while she talks",
                () => screen.WorldClockForTesting, () => Is.GreaterThan(clock + 100));
            AddAssert("and she is still talking", () => screen.DialogueOpenForTesting);

            // One press per step: Mora has three pages, and the press that closes the last one would
            // reopen the conversation if it landed in the same frame as another.
            AddStep("advance to page 2", () => InputManager.Key(Key.E));
            AddStep("advance to page 3", () => InputManager.Key(Key.E));
            AddStep("close dialogue", () => InputManager.Key(Key.E));

            AddAssert("dialogue closed", () => !screen.DialogueOpenForTesting);
            AddUntilStep("the bubble emptied with it",
                () => screen.EntitiesForTesting.OfType<MoraNpc>().First().BubbleForTesting!.LineForTesting, () => Is.Empty);
        }

        /// <summary>
        /// A question with two answers is a switch. These were text boxes holding the word «да», so nothing
        /// said which words counted and nothing happened until the box was committed.
        /// </summary>
        [Test]
        public void TestAYesOrNoPropertyIsASwitch()
        {
            DodgeWorldScreen screen = null!;
            GenericNpc npc = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("add an NPC and select it", () =>
            {
                screen.AddNpc();
                npc = (GenericNpc)screen.EntitiesForTesting[^1];
                screen.SelectForTesting(npc);
            });

            AddAssert("aliveness is offered as a switch",
                () => screen.EditorPanelForTesting.Inspector.ControlForTesting("Живое существо"),
                () => Is.InstanceOf<LabelledToggleField>());

            AddAssert("and it starts on", () => npc.Alive);

            // Flipped through the switch's own value, which is the path a click on it takes: aiming a
            // cursor at it would be testing the framework's hit-testing rather than this panel.
            AddStep("switch it off", () =>
                ((LabelledToggleField)screen.EditorPanelForTesting.Inspector.ControlForTesting("Живое существо")!)
                .FlipForTesting());

            // Applied on the switch, with nothing to press afterwards.
            AddUntilStep("it is a prop now", () => npc.Alive, () => Is.False);
        }

        /// <summary>
        /// The author writes a second branch in the editor and the character starts alternating: the whole
        /// feature, from the panel to what the player hears.
        /// </summary>
        [Test]
        public void TestASecondBranchIsWrittenInTheEditorAndAlternates()
        {
            DodgeWorldScreen screen = null!;
            GenericNpc villager = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("add an NPC and select it", () =>
            {
                screen.AddNpc();
                villager = (GenericNpc)screen.EntitiesForTesting[^1];
                screen.SelectForTesting(villager);
            });

            AddStep("write the first branch", () =>
                screen.EditorPanelForTesting.Dialogue.SetLineForTesting("Привет.", "Hello."));

            AddStep("add a second branch and write it", () =>
            {
                screen.EditorPanelForTesting.Dialogue.AddBranchForTesting();
                screen.EditorPanelForTesting.Dialogue.SetLineForTesting("А, снова ты.", "Oh, you again.");
            });

            AddStep("apply", () => screen.EditorPanelForTesting.Dialogue.Apply());

            AddAssert("the NPC has two branches", () => villager.BranchCount, () => Is.EqualTo(2));

            AddStep("close editor", () => InputManager.Key(Key.F2));
            AddStep("stand next to it", () => screen.SimulationForTesting.PlacePlayer(villager.Position + new Vector2(60, 0)));

            // Asserted in English: the test's account reads English, so that is what the bubble shows.
            AddStep("talk", () => InputManager.Key(Key.E));
            AddUntilStep("the first branch is what it says",
                () => villager.BubbleForTesting!.LineForTesting, () => Is.EqualTo("Hello."));
            AddStep("hear it out", () => InputManager.Key(Key.E));

            AddStep("talk again", () => InputManager.Key(Key.E));
            AddUntilStep("now it says the second branch",
                () => villager.BubbleForTesting!.LineForTesting, () => Is.EqualTo("Oh, you again."));
            AddStep("hear that out", () => InputManager.Key(Key.E));

            AddStep("and again", () => InputManager.Key(Key.E));
            AddUntilStep("back round to the first",
                () => villager.BubbleForTesting!.LineForTesting, () => Is.EqualTo("Hello."));
        }

        /// <summary>
        /// And walking out of a conversation ends it, which is the other half of the world not stopping:
        /// a player who can walk must not be left talking to somebody across the room.
        /// </summary>
        [Test]
        public void TestWalkingAwayEndsAConversation()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world loaded", () => screen.IsLoaded && screen.IsCurrentScreen() && screen.WorldLoadedForTesting);

            AddStep("stand next to Mora", () => screen.SimulationForTesting.PlacePlayer(
                screen.EntitiesForTesting.OfType<MoraNpc>().First().Position + new Vector2(0, 60)));

            AddStep("talk to her", () => InputManager.Key(Key.E));
            AddAssert("dialogue opened", () => screen.DialogueOpenForTesting);

            AddStep("walk away", () => InputManager.PressKey(Key.S));
            AddUntilStep("the conversation ends on its own", () => !screen.DialogueOpenForTesting);
            AddStep("stop walking", () => InputManager.ReleaseKey(Key.S));

            AddAssert("the player really did leave",
                () => screen.LocalPlayerForTesting.Position.Y,
                () => Is.GreaterThan(screen.EntitiesForTesting.OfType<MoraNpc>().First().Position.Y + 100));
        }

        /// <summary>
        /// A bubble over the speaker's head is over a head whose height the author chooses, so a tall
        /// object put its line off the top of the screen. Beside the speaker it is always the same
        /// distance above the ground.
        /// </summary>
        [Test]
        public void TestATallObjectStillShowsWhatItSays()
        {
            DodgeWorldScreen screen = null!;
            GenericNpc statue = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("add a very tall object", () =>
            {
                screen.AddNpc();
                statue = (GenericNpc)screen.EntitiesForTesting[^1];
                statue.Size = new Vector2(200, 900);
            });
            AddStep("close editor", () => InputManager.Key(Key.F2));

            AddStep("stand next to it", () => screen.SimulationForTesting.PlacePlayer(statue.Position + new Vector2(40, 0)));
            AddStep("press E", () => InputManager.Key(Key.E));
            AddUntilStep("it is talking", () => screen.DialogueOpenForTesting);

            AddUntilStep("its line is on the screen", () =>
            {
                Quad bubble = statue.BubbleForTesting!.ScreenSpaceDrawQuad;

                return bubble.TopLeft.Y > 0 && bubble.BottomLeft.Y < screen.ScreenSpaceDrawQuad.BottomLeft.Y;
            });
        }

        /// <summary>
        /// Dying should return the player to the starting arena, not to the spawn of whichever room
        /// they happened to die in.
        /// </summary>
        [Test]
        public void TestDeathReturnsToStartingRoom()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("select the east passage", () =>
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight)));
            AddStep("branch a new room off it", () => screen.CreateConnectedRoom());

            AddUntilStep("now in the new room",
                () => screen.CurrentRoomIdForTesting, () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddStep("close editor", () => InputManager.Key(Key.F2));

            AddStep("spawn something lethal on the player", () =>
            {
                WorldSimulation simulation = screen.SimulationForTesting;

                simulation.SpawnMob(new MobSpawnDefinition("lethal", screen.LocalPlayerForTesting.Position, Vector2.Zero,
                    Count: 1, MaxHealth: 999, ContactDamage: PlayerState.MAXIMUM_HEALTH, DetectionRadius: 0,
                    ProjectileCount: 0, ProjectileDamage: 1, ProjectileSpeed: 100, ProjectileRange: 100,
                    AttackCooldown: 2200));
            });

            AddUntilStep("returned to the starting arena",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddAssert("player is alive again",
                () => screen.LocalPlayerForTesting.Health, () => Is.EqualTo(PlayerState.MAXIMUM_HEALTH));
        }

        /// <summary>
        /// A world can begin somewhere other than the seeded hub, and a room can be removed again — neither
        /// was possible from the editor, so a wrong turn was permanent and every world started in the same
        /// place.
        /// </summary>
        [Test]
        public void TheStartingRoomIsChosenAndARoomCanBeRemoved()
        {
            DodgeWorldScreen screen = null!;
            string branched = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("select the east passage", () =>
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight)));
            AddStep("branch a new room off it", () => screen.CreateConnectedRoom());

            AddUntilStep("now in the new room", () =>
            {
                branched = screen.CurrentRoomIdForTesting;
                return branched != DodgeWorldDefaults.ROOT_ROOM_ID;
            });

            AddStep("start the world here", () => screen.MakeThisRoomInitial());

            // Death is the plainest observable consequence of the choice: it returns players to the start.
            AddStep("close editor", () => InputManager.Key(Key.F2));
            AddStep("something lethal", () => screen.SimulationForTesting.SpawnMob(
                new MobSpawnDefinition("lethal", screen.LocalPlayerForTesting.Position, Vector2.Zero,
                    Count: 1, MaxHealth: 999, ContactDamage: PlayerState.MAXIMUM_HEALTH, DetectionRadius: 0,
                    ProjectileCount: 0, ProjectileDamage: 1, ProjectileSpeed: 100, ProjectileRange: 100,
                    AttackCooldown: 2200)));

            AddUntilStep("whole again", () => screen.LocalPlayerForTesting.Health, () => Is.EqualTo(PlayerState.MAXIMUM_HEALTH));
            AddAssert("death returned them to the room the world now starts in",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(branched));

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("go back to the plaza", () => screen.SwitchRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddStep("ask to delete it", () => screen.DeleteThisRoom());
            AddAssert("nothing happened on the first press",
                () => screen.RoomsForTesting.ContainsKey(DodgeWorldDefaults.ROOT_ROOM_ID), () => Is.True);

            AddStep("ask again", () => screen.DeleteThisRoom());

            AddAssert("the room is gone",
                () => screen.RoomsForTesting.ContainsKey(DodgeWorldDefaults.ROOT_ROOM_ID), () => Is.False);
            AddAssert("and the author is standing in the starting room",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(branched));
            AddAssert("nothing leads to the room that is gone",
                () => screen.RoomsForTesting.Values.SelectMany(room => room.Entities)
                            .Any(entity => entity.DestinationRoomId == DodgeWorldDefaults.ROOT_ROOM_ID), () => Is.False);

            AddStep("try to delete the starting room", () => screen.DeleteThisRoom());
            AddStep("and again, in case it asked twice", () => screen.DeleteThisRoom());
            AddAssert("the starting room is still there",
                () => screen.RoomsForTesting.ContainsKey(branched), () => Is.True);
        }

        /// <summary>
        /// How hard the sword hits is set in the editor and takes effect at once. A world carrying no sword
        /// of its own has one made for it to hold the number, which keeps the built-in picture.
        /// </summary>
        [Test]
        public void TheSwordsDamageIsAuthoredInTheEditor()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddAssert("one point of damage to begin with",
                () => screen.LocalPlayerForTesting.SwordDamage, () => Is.EqualTo(WeaponSkin.DEFAULT_DAMAGE));

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("sharpen the sword", () => screen.EditorPanelForTesting.SwordDamage.Value = 7);

            AddAssert("the sword hits for seven now",
                () => screen.LocalPlayerForTesting.SwordDamage, () => Is.EqualTo(7));

            AddStep("close editor", () => InputManager.Key(Key.F2));

            AddStep("stand a mob in front of the player", () =>
            {
                WorldSimulation simulation = screen.SimulationForTesting;

                simulation.SpawnMob(new MobSpawnDefinition("target",
                    screen.LocalPlayerForTesting.Position + new Vector2(60, 0), Vector2.Zero,
                    Count: 1, MaxHealth: 50, ContactDamage: 1, DetectionRadius: 0,
                    ProjectileCount: 0, ProjectileDamage: 1, ProjectileSpeed: 100, ProjectileRange: 100,
                    AttackCooldown: 2200));
            });

            AddStep("swing at it", () => screen.SimulationForTesting.Swing(new Vector2(1, 0)));

            AddAssert("the swing took off seven",
                () => screen.SimulationForTesting.Mobs.Single().Health, () => Is.EqualTo(43));
        }

        [Test]
        public void TestBundledWeaponReferencesAreIndependent()
        {
            DodgeWorldScreen screen = null!;
            Texture? first = null;
            Texture? second = null;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("request two sword textures", () =>
            {
                first = screen.TextureLibraryForTesting.GetWeaponTexture();
                second = screen.TextureLibraryForTesting.GetWeaponTexture();
            });

            AddAssert("both textures loaded", () => first != null && second != null);
            AddAssert("references are independent", () => ReferenceEquals(first, second), () => Is.False);
            AddStep("dispose first owner", () => first!.Dispose());
            AddAssert("second owner remains usable", () => DodgeWorldTexture.IsUsable(second));
            AddStep("dispose second owner", () => second!.Dispose());
        }

        /// <summary>
        /// Getting hit should be heard, whoever worked the hit out — the local simulation in a room the
        /// server is not running, or the server itself in one it is. One hit is one sound: the two can
        /// report the same hit, and two sources can land together.
        /// </summary>
        [Test]
        public void TestTakingDamageIsHeardOnce()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("stand something harmful on the player", () =>
            {
                WorldSimulation simulation = screen.SimulationForTesting;

                simulation.SpawnMob(new MobSpawnDefinition("scratcher", screen.LocalPlayerForTesting.Position, Vector2.Zero,
                    Count: 1, MaxHealth: 999, ContactDamage: 1, DetectionRadius: 0,
                    ProjectileCount: 0, ProjectileDamage: 1, ProjectileSpeed: 100, ProjectileRange: 100,
                    AttackCooldown: 2200));
            });

            AddUntilStep("the hit was sounded", () => screen.HurtSoundCountForTesting, () => Is.GreaterThan(0));

            int sounded = 0;

            // Emptied first, or the mob's next scratch lands in the middle of the counting below.
            AddStep("take the room away from it", () => screen.SimulationForTesting.ReplaceLayout(RoomLayout.EMPTY));

            AddStep("two hits land together", () =>
            {
                sounded = screen.HurtSoundCountForTesting;
                int health = screen.LocalPlayerForTesting.Health;

                screen.ReportServerHealthForTesting(health - 1);
                screen.ReportServerHealthForTesting(health - 2);
            });

            AddAssert("heard as one", () => screen.HurtSoundCountForTesting, () => Is.EqualTo(sounded + 1));

            AddStep("the server heals", () => screen.ReportServerHealthForTesting(PlayerState.MAXIMUM_HEALTH));
            AddAssert("healing is silent", () => screen.HurtSoundCountForTesting, () => Is.EqualTo(sounded + 1));
        }

        /// <summary>
        /// Every kind the document can hold must be placeable, or a world can only ever contain the
        /// objects the default layout happened to include. Portals and side passages are how rooms
        /// are wired together, so leaving them out of the editor made the world graph unauthorable.
        /// </summary>
        [Test]
        public void TestEveryKindOfObjectCanBePlaced()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("add a portal", () => screen.AddPortal());
            AddStep("add a side passage", () => screen.AddSidePassage());
            AddStep("add a kiosk", () => screen.AddTerminal());
            AddStep("add a warp", () => screen.AddWarp());
            AddStep("add an NPC", () => screen.AddNpc());
            AddStep("add a mob zone", () => screen.AddMobSpawnZone());
            AddStep("add a collision zone", () => screen.AddCollisionZone());
            AddStep("add a surface", () => screen.AddSurface());
            AddStep("add an emitter", () => screen.AddEmitter());
            AddStep("add a beam", () => screen.AddBeam());

            AddAssert("both hazards are there",
                () => screen.EntitiesForTesting.OfType<WorldHazardDevice>().Count(), () => Is.EqualTo(2));

            AddAssert("the portal is unlinked and can be branched from",
                () => screen.EntitiesForTesting.OfType<WorldPortal>().Any(portal => string.IsNullOrEmpty(portal.DestinationRoomId)));

            AddStep("select the new portal", () => screen.SelectForTesting(
                screen.EntitiesForTesting.OfType<WorldPortal>().First(portal => string.IsNullOrEmpty(portal.DestinationRoomId))));
            AddStep("branch a room off it", () => screen.CreateConnectedRoom());

            AddUntilStep("the branch was created and entered",
                () => screen.CurrentRoomIdForTesting, () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));

            // Round-tripping proves the placed objects survive being written into the document and read
            // back, which is what publishing does.
            AddStep("return to the plaza", () => screen.ReturnToRootRoom());
            AddAssert("the placed objects came back", () => new[]
            {
                screen.EntitiesForTesting.OfType<WorldPortal>().Count(),
                screen.EntitiesForTesting.OfType<SidePassage>().Count(),
                screen.EntitiesForTesting.OfType<WorldTerminal>().Count(),
            }, () => Is.EqualTo(new[] { 2, 3, 3 }));
        }

        /// <summary>
        /// The import button used to ask the host for a system file dialog, which desktop does not
        /// provide, so pressing it did nothing at all.
        /// </summary>
        [Test]
        public void TestImportingATextureOpensABrowser()
        {
            DodgeWorldScreen screen = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("open editor", () => InputManager.Key(Key.F2));

            AddStep("ask to import a texture", () => screen.ImportTexture());
            AddUntilStep("a file browser is showing", () => screen.TexturePickerForTesting.IsOpen);

            AddAssert("back closes the browser first", () => screen.OnBackButton());
            AddUntilStep("the browser closed", () => !screen.TexturePickerForTesting.IsOpen);
            AddAssert("and the editor is still open", () => screen.EditorModeForTesting);
        }

        /// <summary>
        /// A long line has to wrap and be readable in full: the document allows a page of 500
        /// characters, and clipping an author's sentence would be worse than a taller bubble.
        /// </summary>
        [Test]
        public void TestALongLineGrowsTheBubble()
        {
            DodgeWorldScreen screen = null!;
            MoraNpc mora = null!;
            float singleLine = 0;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("find Mora", () => mora = screen.EntitiesForTesting.OfType<MoraNpc>().First());

            AddStep("give her one short line", () => mora.SetDialoguePages(new[] { "Коротко." }, new[] { "Short." }));
            AddStep("walk towards Mora", () => InputManager.PressKey(Key.W));
            AddUntilStep("player is near Mora", () => screen.LocalPlayerForTesting.Position.Y, () => Is.LessThan(180));
            AddStep("stop walking", () => InputManager.ReleaseKey(Key.W));

            AddStep("open dialogue", () => InputManager.Key(Key.E));
            AddUntilStep("the bubble is showing", () => mora.BubbleForTesting!.LineForTesting, () => Is.EqualTo("Short."));

            AddStep("remember the one-line height", () => singleLine = mora.BubbleForTesting!.TextHeightForTesting);
            AddAssert("a short line leaves the bubble at its normal size",
                () => mora.BubbleForTesting!.DrawHeight, () => Is.EqualTo(DialogueBubble.SIZE.Y));

            AddStep("give her a long one", () => mora.SetDialoguePages(
                new[] { string.Join(" ", Enumerable.Repeat("совершенно обычное предложение про мир", 12)) },
                new[] { string.Join(" ", Enumerable.Repeat("a perfectly ordinary sentence about the world", 12)) }));

            AddUntilStep("the text wrapped onto many lines",
                () => mora.BubbleForTesting!.TextHeightForTesting, () => Is.GreaterThan(singleLine * 4));
            AddUntilStep("and the bubble grew to hold it",
                () => mora.BubbleForTesting!.DrawHeight, () => Is.GreaterThan(DialogueBubble.SIZE.Y));

            AddStep("close dialogue", () => screen.OnBackButton());
        }

        /// <summary>
        /// The map is how the shape of the world becomes visible: rooms only know the ids of the rooms
        /// they lead to, so nothing else shows how they fit together.
        /// </summary>
        [Test]
        public void TestWorldMapDrawsRoomsAndTheirLinks()
        {
            DodgeWorldScreen screen = null!;
            string branchedRoomId = null!;

            AddStep("load local world", () => LoadScreen(screen = new DodgeWorldScreen(freshSession())));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);

            AddStep("open the map", () => InputManager.Key(Key.M));
            AddUntilStep("the map is showing", () => screen.WorldMapForTesting.IsOpen);
            AddAssert("one room, no links", () => screen.WorldMapForTesting.NodeCountForTesting, () => Is.EqualTo(1));
            AddAssert("nothing to link to", () => screen.WorldMapForTesting.LinkCountForTesting, () => Is.Zero);

            AddStep("close the map", () => InputManager.Key(Key.M));
            AddUntilStep("the map closed", () => !screen.WorldMapForTesting.IsOpen);

            AddStep("open editor", () => InputManager.Key(Key.F2));
            AddStep("select the east passage", () =>
                screen.SelectForTesting(screen.EntitiesForTesting.OfType<SidePassage>().First(passage => passage.PointsRight)));
            AddStep("branch a new room off it", () => screen.CreateConnectedRoom());
            AddUntilStep("now in the new room", () => screen.CurrentRoomIdForTesting, () => Is.Not.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddStep("remember which room", () => branchedRoomId = screen.CurrentRoomIdForTesting);

            // The passage was linked in this session, so the map has to read the live room rather than
            // whatever was last published.
            AddStep("open the map", () => screen.ToggleWorldMap());
            AddUntilStep("both rooms are on it", () => screen.WorldMapForTesting.NodeCountForTesting, () => Is.EqualTo(2));
            AddAssert("joined by one line", () => screen.WorldMapForTesting.LinkCountForTesting, () => Is.EqualTo(1));

            AddAssert("neither room has been placed by hand", () => screen.RoomsForTesting.Values.All(room => room.MapPosition == null));

            AddStep("place the branch by hand", () =>
            {
                RoomDefinition branched = screen.RoomsForTesting[branchedRoomId];
                branched.MapX = 900;
                branched.MapY = -240;
            });

            AddStep("reopen the map", () =>
            {
                screen.ToggleWorldMap();
                screen.ToggleWorldMap();
            });

            AddAssert("the placement survived a room capture",
                () => screen.RoomsForTesting[branchedRoomId].MapPosition, () => Is.EqualTo(new Vector2(900, -240)));

            AddAssert("back closes the map before leaving", () => screen.OnBackButton());
            AddUntilStep("the map closed", () => !screen.WorldMapForTesting.IsOpen);
            AddAssert("and the editor is still open", () => screen.EditorModeForTesting);
        }
    }
}
