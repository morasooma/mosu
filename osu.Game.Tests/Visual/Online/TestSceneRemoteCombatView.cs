// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Online.DodgeWorld;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Covers drawing the mobs the server is running: what a snapshot creates, retires and carries on.
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneRemoteCombatView : OsuTestScene
    {
        protected override bool UseOnlineAPI => false;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private TestDodgeWorldClient client = null!;
        private Container worldCamera = null!;
        private RemoteCombatView view = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create view", () =>
            {
                client = new TestDodgeWorldClient { ServerRunsTheRoom = true };
                worldCamera = new Container();

                Children = new Drawable[]
                {
                    client,
                    worldCamera,
                    view = new RemoteCombatView(worldCamera, colours, client, _ => MobAppearance.None),
                };
            });

            AddUntilStep("loaded", () => view.IsLoaded);
            AddStep("connect and enter a room", () =>
            {
                client.SetConnected(true);
                client.JoinRoom("mora-plaza");
            });
            AddUntilStep("the room is server-run", () => client.ServerSimulated.Value);
        }

        private void send(DodgeWorldRoomSnapshot snapshot) =>
            ((IDodgeWorldClient)client).RoomStateChanged(snapshot);

        [Test]
        public void TestMobsAppearAndAreRetiredWithTheSnapshot()
        {
            AddStep("two mobs arrive", () => send(new DodgeWorldRoomSnapshot
            {
                Mobs =
                [
                    new DodgeWorldMob { Id = 1, X = 100, Y = 0, Health = 3, MaximumHealth = 3, ZoneId = "zone" },
                    new DodgeWorldMob { Id = 2, X = -100, Y = 0, Health = 3, MaximumHealth = 3, ZoneId = "zone" },
                ],
            }));

            AddUntilStep("both are drawn", () => view.MobCountForTesting, () => Is.EqualTo(2));

            AddStep("one of them is gone", () => send(new DodgeWorldRoomSnapshot
            {
                Mobs = [new DodgeWorldMob { Id = 1, X = 100, Y = 0, Health = 2, MaximumHealth = 3, ZoneId = "zone" }],
            }));

            AddUntilStep("only one is left", () => view.MobCountForTesting, () => Is.EqualTo(1));

            AddStep("the room empties", () => send(new DodgeWorldRoomSnapshot()));
            AddUntilStep("nothing is drawn", () => view.MobCountForTesting, () => Is.Zero);
        }

        /// <summary>
        /// Twenty snapshots a second is not enough to draw a fast shot from directly, so a shot carries
        /// its velocity and is carried on between them.
        /// </summary>
        [Test]
        public void TestShotsKeepMovingBetweenSnapshots()
        {
            Vector2 first = Vector2.Zero;

            AddStep("a shot arrives", () => send(new DodgeWorldRoomSnapshot
            {
                Projectiles = [new DodgeWorldProjectile { Id = 5, X = 0, Y = 0, VelocityX = 400, VelocityY = 0 }],
            }));

            AddUntilStep("it is drawn", () => view.ProjectileCountForTesting, () => Is.EqualTo(1));
            AddStep("note where it is", () => first = worldCamera.Children.OfType<WorldProjectile>().Single().Position);
            AddWaitStep("wait without a snapshot", 10);
            AddAssert("it moved on by itself",
                () => worldCamera.Children.OfType<WorldProjectile>().Single().Position.X, () => Is.GreaterThan(first.X));

            AddStep("the shot is spent", () => send(new DodgeWorldRoomSnapshot()));
            AddUntilStep("and is no longer drawn", () => view.ProjectileCountForTesting, () => Is.Zero);
        }

        /// <summary>
        /// A death is announced as it happens, so a killed mob goes at once rather than standing there
        /// until the next snapshot leaves it out.
        /// </summary>
        [Test]
        public void TestAKilledMobGoesWithoutWaitingForASnapshot()
        {
            AddStep("a mob arrives", () => send(new DodgeWorldRoomSnapshot
            {
                Mobs = [new DodgeWorldMob { Id = 1, Health = 3, MaximumHealth = 3, ZoneId = "zone" }],
            }));

            AddUntilStep("it is drawn", () => view.MobCountForTesting, () => Is.EqualTo(1));
            AddStep("somebody kills it", () => ((IDodgeWorldClient)client).MobDefeated(1, 7, "zone"));
            AddUntilStep("it is gone already", () => view.MobCountForTesting, () => Is.Zero);
        }

        [Test]
        public void TestWarningsAppearAndAreRetired()
        {
            AddStep("a volley is telegraphed", () => send(new DodgeWorldRoomSnapshot
            {
                Telegraphs = [new DodgeWorldTelegraph { Id = 3, X = 0, Y = 0, Angles = [0, 1.5f], Range = 240 }],
            }));

            AddUntilStep("the warning is drawn", () => view.TelegraphCountForTesting, () => Is.EqualTo(1));

            AddStep("it fires", () => send(new DodgeWorldRoomSnapshot()));
            AddUntilStep("the warning is gone", () => view.TelegraphCountForTesting, () => Is.Zero);
        }

        /// <summary>
        /// Leaving a room must not leave its mobs standing in the next one.
        /// </summary>
        [Test]
        public void TestClearingRetiresEverything()
        {
            AddStep("a mob and a shot arrive", () => send(new DodgeWorldRoomSnapshot
            {
                Mobs = [new DodgeWorldMob { Id = 1, Health = 1, MaximumHealth = 1, ZoneId = "zone" }],
                Projectiles = [new DodgeWorldProjectile { Id = 2 }],
            }));

            AddUntilStep("both are drawn", () => view.MobCountForTesting + view.ProjectileCountForTesting, () => Is.EqualTo(2));

            AddStep("clear", () => view.Clear());

            AddAssert("nothing is tracked", () => view.MobCountForTesting + view.ProjectileCountForTesting, () => Is.Zero);
            AddUntilStep("and nothing is left in the world", () => !worldCamera.Children.OfType<WorldProjectile>().Any());
        }
    }
}
