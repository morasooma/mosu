// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.DodgeWorld;
using osu.Game.Screens.OnlinePlay.DodgeWorld;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Tests.Visual.Online;

namespace osu.Game.Tests.Visual.OnlinePlay
{
    /// <summary>
    /// Covers the screen against a realtime server that owns the room: what arriving decisions do to a
    /// screen that is standing in a world, which is the half no unit test can reach.
    /// </summary>
    public partial class TestSceneDodgeWorldServerRoom : ScreenTestScene
    {
        /// <summary>
        /// Two rooms, starting in the plaza. The tests walk east first, so that dying is a room change
        /// rather than a reload of the room already being stood in.
        /// </summary>
        private const string server_world = """
            {"version":1,"initial_room_id":"mora-plaza",
             "rooms":[{"id":"mora-plaza","name":"Mora Plaza","spawn_x":0,"spawn_y":350,"entities":[]},
                      {"id":"east","name":"East District","spawn_x":12,"spawn_y":34,"entities":[]}]}
            """;

        [Cached(typeof(DodgeWorldClient))]
        private readonly TestDodgeWorldClient client = new TestDodgeWorldClient { ServerRunsTheRoom = true };

        private DodgeWorldScreen screen = null!;

        /// <summary>
        /// Added once for the whole scene rather than per test: it is one cached dependency, and a drawable
        /// cannot be put in a container twice.
        /// </summary>
        [BackgroundDependencyLoader]
        private void load()
        {
            Add(client);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("serve a world", () =>
            {
                var api = (DummyAPIAccess)API;
                api.Login("world-owner", "password");
                api.HandleRequest = request =>
                {
                    if (request is not GetDodgeWorldRequest get)
                        return false;

                    get.TriggerSuccess(response());
                    return true;
                };
            });

            AddUntilStep("client loaded", () => client.IsLoaded);
            AddStep("connect", () => client.SetConnected(true));

            AddStep("load screen", () => LoadScreen(screen = new DodgeWorldScreen()));
            AddUntilStep("world available", () => screen.WorldLoadedForTesting);
            AddStep("walk east", () => screen.SwitchRoomForTesting("east"));
            AddUntilStep("the room is the server's", () => client.ServerSimulated.Value);
        }

        /// <summary>
        /// The server decides death, and the client obeys by walking the player home. This crashed the
        /// game on the first live run: entering a room joins the realtime room, and joining resets
        /// whether the server owns it, which reloaded the room from inside the middle of building it.
        /// </summary>
        [Test]
        public void TestServerDeathReturnsHomeWithoutCrashing()
        {
            AddAssert("standing in the east room",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo("east"));

            AddStep("the server kills the player", () => ((IDodgeWorldClient)client).Died());

            AddUntilStep("back home",
                () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddAssert("with health restored", () => screen.LocalPlayerForTesting.Health, () => Is.GreaterThan(0));

            // The room is intact: an entity list left holding drawables that are no longer in the world is
            // exactly what threw, and the next room change would throw again.
            AddStep("walk back east", () => screen.SwitchRoomForTesting("east"));
            AddUntilStep("east again", () => screen.CurrentRoomIdForTesting, () => Is.EqualTo("east"));
        }

        /// <summary>
        /// A plain room change with the server running the room, to separate "walking between rooms" from
        /// "the server killed me" as causes.
        /// </summary>
        [Test]
        public void TestChangingRoomsWithAServerRoom()
        {
            AddStep("walk home", () => screen.SwitchRoomForTesting(DodgeWorldDefaults.ROOT_ROOM_ID));
            AddUntilStep("home", () => screen.CurrentRoomIdForTesting, () => Is.EqualTo(DodgeWorldDefaults.ROOT_ROOM_ID));

            AddStep("walk back", () => screen.SwitchRoomForTesting("east"));
            AddUntilStep("east", () => screen.CurrentRoomIdForTesting, () => Is.EqualTo("east"));
        }

        /// <summary>
        /// Health arrives from the server for the local player only, and is applied rather than argued
        /// with.
        /// </summary>
        [Test]
        public void TestServerHealthIsApplied()
        {
            AddStep("the server reports a wound", () => ((IDodgeWorldClient)client).HealthChanged(42));
            AddUntilStep("which is shown", () => screen.LocalPlayerForTesting.Health, () => Is.EqualTo(42));
        }

        private static DodgeWorldApiResponse response() => new DodgeWorldApiResponse
        {
            Revision = 3,
            CanEdit = false,
            OnlineUsers = 2,
            World = JObject.Parse(server_world),
            Progression = new DodgeWorldProgressionResponse
            {
                Level = 1,
                ExperienceForNextLevel = 100,
            },
        };
    }
}
