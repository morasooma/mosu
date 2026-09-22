// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Online.DodgeWorld;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Covers drawing the other players in a Dodge World room: that they appear, and that what the server
    /// says they did is actually drawn.
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneRemotePlayerView : OsuTestScene
    {
        protected override bool UseOnlineAPI => false;

        [Cached(typeof(UserLookupCache))]
        private readonly TestUserLookupCache userCache = new TestUserLookupCache();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private TestDodgeWorldClient client = null!;
        private Container worldCamera = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create view", () =>
            {
                client = new TestDodgeWorldClient();
                worldCamera = new Container();

                Children = new Drawable[]
                {
                    client,
                    worldCamera,
                    new RemotePlayerView(worldCamera, colours, client, () => null, WeaponSkin.CreateFallback),
                };
            });

            AddStep("connect and enter a room", () =>
            {
                client.SetConnected(true);
                client.JoinRoom("mora-plaza");
            });
        }

        /// <summary>
        /// A swing removes itself when it finishes, so one still playing when the scene is torn down would
        /// be removed from a disposing container off the update thread. The screen empties the camera on a
        /// room change for the same reason.
        /// </summary>
        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("empty the camera", () => worldCamera.Clear());
        }

        private void arrive(int userId) => ((IDodgeWorldClient)client).UserJoined(userId,
            new DodgeWorldPlayerState { X = 100, Y = 50, Health = 10 });

        private int swings => worldCamera.Children.OfType<SwordSwing>().Count();

        [Test]
        public void TestPlayersAppearAndLeave()
        {
            AddStep("somebody arrives", () => arrive(7));
            AddUntilStep("they are drawn", () => worldCamera.Children.OfType<WorldPlayer>().Count(), () => Is.EqualTo(1));

            AddStep("they leave", () => ((IDodgeWorldClient)client).UserLeft(7));
            AddUntilStep("and are gone", () => worldCamera.Children.OfType<WorldPlayer>().Any(), () => Is.False);
        }

        /// <summary>
        /// A swing the server relays has to be drawn, or the room looks like nobody is fighting: the
        /// swinger sees their own sword locally and everyone else sees nothing at all.
        /// </summary>
        [Test]
        public void TestSomebodyElsesSwingIsDrawn()
        {
            AddStep("somebody arrives", () => arrive(7));
            AddUntilStep("they are drawn", () => worldCamera.Children.OfType<WorldPlayer>().Any());

            AddStep("they swing", () => ((IDodgeWorldClient)client).UserAttacked(7, 1, 0));
            AddUntilStep("the swing is drawn", () => swings, () => Is.EqualTo(1));
            AddUntilStep("and clears itself when it finishes", () => swings, () => Is.Zero);

            AddStep("they swing again", () => ((IDodgeWorldClient)client).UserAttacked(7, 0, 1));
            AddUntilStep("which is drawn too", () => swings, () => Is.EqualTo(1));
        }

        /// <summary>
        /// A swing from somebody who is not in the room would otherwise conjure a sword out of nowhere,
        /// which is what a message arriving after they left looks like.
        /// </summary>
        [Test]
        public void TestASwingFromNobodyIsIgnored()
        {
            AddStep("a stranger swings", () => ((IDodgeWorldClient)client).UserAttacked(9, 1, 0));
            AddWaitStep("wait a while", 5);
            AddAssert("nothing was drawn", () => swings, () => Is.Zero);
        }

        /// <summary>
        /// A direction of nowhere is not a swing, and would draw a sword pointing at an arbitrary angle.
        /// </summary>
        [Test]
        public void TestASwingWithNoDirectionIsIgnored()
        {
            AddStep("somebody arrives", () => arrive(7));
            AddUntilStep("they are drawn", () => worldCamera.Children.OfType<WorldPlayer>().Any());

            AddStep("they swing at nowhere", () => ((IDodgeWorldClient)client).UserAttacked(7, 0, 0));
            AddWaitStep("wait a while", 5);
            AddAssert("nothing was drawn", () => swings, () => Is.Zero);
        }
    }
}
