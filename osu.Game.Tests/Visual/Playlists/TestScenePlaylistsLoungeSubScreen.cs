// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.OnlinePlay.Lounge.Components;
using osu.Game.Screens.OnlinePlay.Playlists;
using osu.Game.Tests.Visual.OnlinePlay;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Playlists
{
    public partial class TestScenePlaylistsLoungeSubScreen : OnlinePlayTestScene
    {
        private PlaylistsLoungeSubScreen loungeScreen = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("select default mode", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddStep("push screen", () => LoadScreen(loungeScreen = new PlaylistsLoungeSubScreen()));
            AddUntilStep("wait for present", () => loungeScreen.IsCurrentScreen());
        }

        private RoomListing roomListing => loungeScreen.ChildrenOfType<RoomListing>().First();

        [Test]
        public void TestManyRooms()
        {
            createRooms(GenerateRooms(500));
        }

        [Test]
        public void TestRulesetFiltering()
        {
            AddStep("select osu! mode", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
            createRooms(GenerateRooms(2, new OsuRuleset().RulesetInfo)
                        .Concat(GenerateRooms(3, new CatchRuleset().RulesetInfo))
                        .ToArray());

            AddUntilStep("only osu! playlists visible", () => roomListing.DrawableRooms.Count(r => r.IsPresent) == 2);

            AddStep("select catch mode", () => Ruleset.Value = new CatchRuleset().RulesetInfo);
            AddUntilStep("only catch playlists visible", () => roomListing.DrawableRooms.Count(r => r.IsPresent) == 3);

            AddStep("select mania mode", () => Ruleset.Value = new ManiaRuleset().RulesetInfo);
            AddUntilStep("no playlists visible", () => roomListing.DrawableRooms.All(r => !r.IsPresent));
            AddUntilStep("empty state visible", () => roomListing.ChildrenOfType<OsuSpriteText>()
                                                                    .Single(t => t.Text.ToString() == OnlinePlayStrings.NoPlaylistsForSelectedMode.ToString()).IsPresent);
            AddStep("restore osu! mode", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
        }

        [Test]
        public void TestAdministratorPinsAndUnpinsExistingPlaylist()
        {
            DummyAPIAccess api = null!;
            createRooms(GenerateRooms(2));
            AddUntilStep("wait for playlists", () => roomListing.DrawableRooms.Count == 2);

            RoomPanel older = null!;
            string originalUrl = string.Empty;
            AddStep("simulate Mosu server pin persistence", () =>
            {
                api = (DummyAPIAccess)API;
                older = roomListing.DrawableRooms.Last();
                originalUrl = api.Endpoints.APIUrl;
                api.Endpoints.APIUrl = "http://127.0.0.1:18082";
                var handleRequest = api.HandleRequest!;
                api.HandleRequest = request =>
                {
                    if (request is not SetPlaylistPinnedRequest pin)
                        return handleRequest(request);

                    var serverRoom = OnlinePlayDependencies.RequestsHandler.ServerSideRooms.Single(room => room.RoomID == pin.RoomId);
                    serverRoom.Pinned = pin.Pinned;
                    pin.TriggerSuccess();
                    return true;
                };
            });

            AddAssert("pin is hidden for the playlist owner without admin rights", () => !older.ContextMenuItems.Any(item => item.Text.Value == PlaylistPinStrings.Pin));
            AddStep("enable administrator", () => api.LocalUser.Value.IsAdmin = true);
            AddStep("pin older playlist from its context menu", () => invoke(PlaylistPinStrings.Pin));
            AddUntilStep("same playlist card moves to the top", () => older.Room.Pinned && roomListing.DrawableRooms.FirstOrDefault() == older);
            AddStep("unpin older playlist from its context menu", () => invoke(PlaylistPinStrings.Unpin));
            AddUntilStep("same playlist card returns below the newer room", () => !older.Room.Pinned && roomListing.DrawableRooms.LastOrDefault() == older);
            AddStep("another admin pins the playlist", () =>
            {
                OnlinePlayDependencies.RequestsHandler.ServerSideRooms.Single(room => room.RoomID == older.Room.RoomID).Pinned = true;
                loungeScreen.RefreshRooms();
            });
            AddUntilStep("polling also moves the existing playlist to the top", () => older.Room.Pinned && roomListing.DrawableRooms.FirstOrDefault() == older);
            AddStep("restore account and server", () =>
            {
                api.LocalUser.Value.IsAdmin = false;
                api.Endpoints.APIUrl = originalUrl;
            });

            void invoke(LocalisableString text)
                => older.ContextMenuItems.Single(item => item.Text.Value == text).Action.Value!.Invoke();
        }

        [Test]
        public void TestScrollByDraggingRooms()
        {
            AddStep("reset mouse", () => InputManager.ReleaseButton(MouseButton.Left));

            createRooms(GenerateRooms(30));

            AddStep("move mouse to third room", () => InputManager.MoveMouseTo(roomListing.DrawableRooms[2]));
            AddStep("hold down", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag to top", () => InputManager.MoveMouseTo(roomListing.DrawableRooms[0]));

            AddAssert("first and second room masked", ()
                => !checkRoomVisible(roomListing.DrawableRooms[0]) &&
                   !checkRoomVisible(roomListing.DrawableRooms[1]));
        }

        [Test]
        public void TestScrollSelectedIntoView()
        {
            createRooms(GenerateRooms(30));

            AddStep("select last room", () => roomListing.DrawableRooms[^1].TriggerClick());

            AddUntilStep("first room is masked", () => !checkRoomVisible(roomListing.DrawableRooms[0]));
            AddUntilStep("last room is not masked", () => checkRoomVisible(roomListing.DrawableRooms[^1]));
        }

        private bool checkRoomVisible(RoomPanel panel) =>
            loungeScreen.ChildrenOfType<OsuScrollContainer>().First().ScreenSpaceDrawQuad
                        .Contains(panel.ScreenSpaceDrawQuad.Centre);

        private void createRooms(params Room[] rooms)
        {
            AddStep("create rooms", () =>
            {
                foreach (var room in rooms)
                    API.Queue(new CreateRoomRequest(room));
            });

            AddStep("refresh lounge", () => loungeScreen.RefreshRooms());
        }
    }
}
