// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Overlays.Dashboard.CurrentlyOnline;
using osu.Game.Tests.Visual.Metadata;
using osu.Game.Tests.Visual.Spectator;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneCurrentlyOnlineDisplay : OsuTestScene
    {
        private static readonly string[] usernames =
        {
            "fieryrage",
            "Kerensa",
            "MillhioreF",
            "Player01",
            "smoogipoo",
            "Ephemeral",
            "BTMC",
            "Cilvery",
            "m980",
            "HappyStick",
            "LittleEndu",
            "frenzibyte",
            "Zallius",
            "BanchoBot",
            "rocketminer210",
            "pishifat"
        };

        private readonly APIUser streamingUser = new APIUser { Id = 2, Username = "Test user" };

        private TestSpectatorClient spectatorClient = null!;
        private TestMetadataClient metadataClient = null!;
        private CurrentlyOnlineDisplay currentlyOnline = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("set up components", () =>
            {
                spectatorClient = new TestSpectatorClient();
                metadataClient = new TestMetadataClient();

                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    switch (req)
                    {
                        case LookupUsersRequest lookupUsersRequest:
                            var users = lookupUsersRequest.UserIds.Select(id =>
                            {
                                // tests against failed lookups
                                if (id == 13)
                                    return null;

                                return new APIUser
                                {
                                    Id = id,
                                    Username = usernames[id % usernames.Length],
                                };
                            }).ToList();
                            lookupUsersRequest.TriggerSuccess(new GetUsersResponse { Users = users });
                            return true;

                        default:
                            return false;
                    }
                };

                Children = new Drawable[]
                {
                    spectatorClient,
                    metadataClient,
                    new DependencyProvidingContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        CachedDependencies = new (Type, object)[]
                        {
                            (typeof(SpectatorClient), spectatorClient),
                            (typeof(MetadataClient), metadataClient),
                            (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Purple)),
                        },
                        Child = currentlyOnline = new CurrentlyOnlineDisplay()
                    },
                };
            });
        }

        [Test]
        public void TestAggregateCountDoesNotCreateHiddenPanels()
        {
            AddStep("three connected users", () => metadataClient.OnlineUserCountUpdated(3));
            AddStep("one visible user", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence
            {
                Status = UserStatus.Online, Platform = "Linux", BuildKind = "Dev",
                Activity = new UserActivity.InSoloGame { RulesetID = 0, ModAcronyms = new[] { "RX" } }
            }));
            AddUntilStep("visible panel loaded", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().Count() == 1);
            AddAssert("count includes hidden users", () => metadataClient.OnlineUserCount.Value == 3);
            AddAssert("no duplicate count in tab", () => !currentlyOnline.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>()
                .Any(text => text.Text.ToString().StartsWith("Online:")));
            AddStep("visible user goes invisible", () => metadataClient.UserPresenceUpdated(streamingUser.Id, null));
            AddUntilStep("no visible panels", () => !currentlyOnline.ChildrenOfType<OnlineUserPanel>().Any());
            AddAssert("count unchanged by invisibility", () => metadataClient.OnlineUserCount.Value == 3);
        }

        [Test]
        public void TestRankedSpectateAvailableBetweenMapsAndRoutesToRanked()
        {
            AddStep("ranked between maps", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence
            {
                Status = UserStatus.Online,
                Activity = new UserActivity.InLobby { IsRankedPlay = true, RoomID = 42 }
            }));
            AddUntilStep("panel loaded", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().Count() == 1);
            AddAssert("watch enabled between maps", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().Single().CanSpectate.Value);
            AddAssert("ranked route", () => osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.RankedPlaySpectatorLoader.Create(streamingUser, metadataClient)
                is osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.RankedPlaySpectatorLoader);
            AddStep("ranked gameplay", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence
            {
                Status = UserStatus.Online,
                Activity = new UserActivity.InMultiplayerGame { RankedPlayRoomID = 42 }
            }));
            AddAssert("same ranked route during gameplay", () => osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.RankedPlaySpectatorLoader.Create(streamingUser, metadataClient)
                is osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.RankedPlaySpectatorLoader);
            AddStep("solo gameplay", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence
            {
                Status = UserStatus.Online, Activity = new UserActivity.InSoloGame()
            }));
            AddAssert("solo route preserved", () => osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.RankedPlaySpectatorLoader.Create(streamingUser, metadataClient)
                is osu.Game.Screens.Play.SoloSpectatorScreen);
        }

        [Test]
        public void TestExactBatchDoesNotRequestEmptyIds()
        {
            bool requestedEmptyIds = false;
            var api = (DummyAPIAccess)API;
            var handler = api.HandleRequest;
            AddStep("track lookups", () => api.HandleRequest = request =>
            {
                if (request is LookupUsersRequest lookup && lookup.UserIds.Length == 0)
                    requestedEmptyIds = true;
                return handler!(request);
            });
            AddStep("add fifty users", () =>
            {
                for (int id = 100; id < 150; id++)
                    metadataClient.UserPresenceUpdated(id, new UserPresence { Status = UserStatus.Online });
            });
            AddUntilStep("fifty panels loaded", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().Count() == 50);
            AddAssert("no empty lookup", () => !requestedEmptyIds);
        }

        [Test]
        public void TestBasicDisplay()
        {
            IDisposable token = null!;

            AddStep("Begin watching user presence", () => token = metadataClient.BeginWatchingUserPresence());
            AddStep("Add online user", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence { Status = UserStatus.Online, Activity = new UserActivity.ChoosingBeatmap() }));
            AddUntilStep("Panel loaded", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().FirstOrDefault()?.User.Id == 2);
            AddAssert("Spectate button disabled", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().First().CanSpectate.Value, () => Is.False);

            AddStep("User began playing", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence { Status = UserStatus.Online, Activity = new UserActivity.InSoloGame() }));
            AddAssert("Spectate button enabled", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().First().CanSpectate.Value, () => Is.True);

            AddStep("User finished playing",
                () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence { Status = UserStatus.Online, Activity = new UserActivity.ChoosingBeatmap() }));
            AddAssert("Spectate button disabled", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().First().CanSpectate.Value, () => Is.False);

            AddStep("Remove playing user", () => metadataClient.UserPresenceUpdated(streamingUser.Id, null));
            AddUntilStep("Panel no longer present", () => !currentlyOnline.ChildrenOfType<OnlineUserPanel>().Any());
            AddStep("End watching user presence", () => token.Dispose());
        }

        [Test]
        public void TestUserWasPlayingBeforeWatchingUserPresence()
        {
            IDisposable token = null!;

            AddStep("Begin watching user presence", () => token = metadataClient.BeginWatchingUserPresence());
            AddStep("Add online user", () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence { Status = UserStatus.Online, Activity = new UserActivity.InSoloGame() }));
            AddUntilStep("Panel loaded", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().FirstOrDefault()?.User.Id == streamingUser.Id);
            AddAssert("Spectate button enabled", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().First().CanSpectate.Value, () => Is.True);

            AddStep("User finished playing",
                () => metadataClient.UserPresenceUpdated(streamingUser.Id, new UserPresence { Status = UserStatus.Online, Activity = new UserActivity.ChoosingBeatmap() }));
            AddAssert("Spectate button disabled", () => currentlyOnline.ChildrenOfType<OnlineUserPanel>().First().CanSpectate.Value, () => Is.False);
            AddStep("Remove playing user", () => metadataClient.UserPresenceUpdated(streamingUser.Id, null));
            AddStep("End watching user presence", () => token.Dispose());
        }
    }
}
