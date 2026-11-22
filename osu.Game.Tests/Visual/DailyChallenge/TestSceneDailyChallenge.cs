// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Metadata;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.Metadata;
using osu.Game.Tests.Visual.OnlinePlay;
using osuTK.Input;

namespace osu.Game.Tests.Visual.DailyChallenge
{
    public partial class TestSceneDailyChallenge : OnlinePlayTestScene
    {
        [Cached(typeof(MetadataClient))]
        private TestMetadataClient metadataClient = new TestMetadataClient();

        [Cached(typeof(INotificationOverlay))]
        private NotificationOverlay notificationOverlay = new NotificationOverlay();

        private partial class ProposalNavigationScreen : Screens.OsuScreen { }

        [BackgroundDependencyLoader]
        private void load()
        {
            base.Content.Add(notificationOverlay);
            base.Content.Add(metadataClient);
            var store = new osu.Game.Database.RealmDetachedBeatmapStore();
            Dependencies.CacheAs<osu.Game.Database.BeatmapStore>(store);
            base.Content.Add(store);
        }

        [Test]
        public void TestDailyChallenge()
        {
            var room = new Room
            {
                Name = "Daily Challenge: June 4, 2024",
                Description = "Highlights of May 2024",
                Playlist =
                [
                    new PlaylistItem(TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())
                    {
                        RequiredMods = [new APIMod(new OsuModTraceable())],
                        AllowedMods = [new APIMod(new OsuModDoubleTime())]
                    }
                ],
                EndDate = DateTimeOffset.Now.AddHours(12),
                Category = RoomCategory.DailyChallenge
            };

            AddStep("add room", () => API.Perform(new CreateRoomRequest(room)));
            AddStep("push screen", () => LoadScreen(new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
        }

        [Test]
        public void TestReturningFromProposalRestoresDailyRulesetAndRequiredMods()
        {
            var room = new Room
            {
                Name = "Daily restore regression",
                Playlist = [new PlaylistItem(TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())
                {
                    RequiredMods = [new APIMod(new OsuModHidden())],
                    AllowedMods = []
                }],
                EndDate = DateTimeOffset.Now.AddHours(12), Category = RoomCategory.DailyChallenge,
            };
            Screens.OnlinePlay.DailyChallenge.DailyChallenge daily = null!;
            Screens.OsuScreen child = null!;
            AddStep("create room", () => API.Perform(new CreateRoomRequest(room)));
            AddStep("open daily", () => LoadScreen(daily = new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
            AddUntilStep("daily ready", () => daily.IsCurrentScreen());
            AddStep("open child selection", () => daily.Push(child = new ProposalNavigationScreen()));
            AddUntilStep("child ready", () => child.IsCurrentScreen());
            AddStep("browse another ruleset and mods", () =>
            {
                child.Ruleset.Value = new osu.Game.Rulesets.Taiko.TaikoRuleset().RulesetInfo;
                child.Mods.Value = [];
            });
            AddStep("return", () => child.Exit());
            AddUntilStep("daily resumed", () => daily.IsCurrentScreen());
            AddAssert("daily ruleset restored", () => daily.Ruleset.Value.OnlineID == 0);
            AddAssert("daily required mods restored", () => daily.Mods.Value.Single() is OsuModHidden);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestProposalFromDailyChallengeInitialisesModCustomisation(bool disableShear)
        {
            var room = new Room
            {
                Name = "Daily proposal navigation",
                Playlist = [new PlaylistItem(TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())
                {
                    RequiredMods = [new APIMod(new OsuModHidden())], AllowedMods = []
                }],
                EndDate = DateTimeOffset.Now.AddHours(12), Category = RoomCategory.DailyChallenge,
            };
            Screens.OnlinePlay.DailyChallenge.DailyChallenge daily = null!;
            Screens.OnlinePlay.DailyChallenge.DailyChallengeProposalSongSelect proposal = null!;
            osu.Game.Overlays.Mods.ModSelectOverlay overlay = null!;
            var config = Dependencies.Get<osu.Game.Configuration.OsuConfigManager>();
            float originalScale = 1;
            bool originalShear = false;
            osu.Game.Configuration.ForkSongSelectStyle originalStyle = default;
            AddStep("use modern at user scale", () =>
            {
                originalScale = config.Get<float>(osu.Game.Configuration.OsuSetting.UIScale);
                originalShear = OsuGame.DisableShear.Value;
                originalStyle = config.Get<osu.Game.Configuration.ForkSongSelectStyle>(osu.Game.Configuration.OsuSetting.ForkSongSelectStyle);
                config.SetValue(osu.Game.Configuration.OsuSetting.ForkSongSelectStyle, osu.Game.Configuration.ForkSongSelectStyle.Modern);
                config.SetValue(osu.Game.Configuration.OsuSetting.UIScale, 0.9f);
                OsuGame.DisableShear.Value = disableShear;
            });
            AddStep("create room", () => API.Perform(new CreateRoomRequest(room)));
            AddStep("open daily", () => LoadScreen(daily = new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
            AddUntilStep("daily ready", () => daily.IsCurrentScreen());
            AddStep("open proposal from daily", () => daily.Push(proposal = new Screens.OnlinePlay.DailyChallenge.DailyChallengeProposalSongSelect()));
            AddUntilStep("proposal ready", () => proposal.IsCurrentScreen() && !proposal.IsFiltering);
            AddStep("open required mods", () =>
            {
                overlay = (osu.Game.Overlays.Mods.ModSelectOverlay)proposal.CreateFooterButtons().OfType<FooterButtonMods>().Single().Overlay!;
                overlay.Show();
            });
            AddUntilStep("overlay visible", () => overlay.IsPresent);
            AddStep("select DA", () => overlay.ChildrenOfType<osu.Game.Overlays.Mods.ModPanel>().Single(p => p.Mod is OsuModDifficultyAdjust).TriggerClick());
            AddAssert("customisation initialised and enabled", () => overlay.ChildrenOfType<osu.Game.Overlays.Mods.ModCustomisationPanel>().Single().Enabled.Value);
            AddAssert("settings open", () => overlay.ChildrenOfType<osu.Game.Overlays.Mods.ModCustomisationPanel>().Single().ExpandedState.Value
                == osu.Game.Overlays.Mods.ModCustomisationPanel.ModCustomisationPanelState.ExpandedByMod);
            AddUntilStep("DA controls visible", () => overlay.ChildrenOfType<OsuSliderBar<float>>().Any(s => s.IsPresent));
            AddUntilStep("hover difficulty slider", () =>
            {
                var slider = overlay.ChildrenOfType<OsuSliderBar<float>>().First();
                InputManager.MoveMouseTo(slider);
                return slider.IsHovered;
            });
            AddStep("adjust difficulty via keyboard", () => InputManager.Key(Key.Right));
            AddAssert("DA setting changed", () => proposal.Mods.Value.OfType<OsuModDifficultyAdjust>().Single().CircleSize.Value != null);
            AddStep("close mods", () => overlay.Hide());
            AddStep("return to daily", () => proposal.Exit());
            AddUntilStep("daily restored", () => daily.IsCurrentScreen());
            AddAssert("mandatory mod preserved", () => daily.Mods.Value.Single() is OsuModHidden);
            AddStep("restore display settings", () =>
            {
                config.SetValue(osu.Game.Configuration.OsuSetting.UIScale, originalScale);
                config.SetValue(osu.Game.Configuration.OsuSetting.ForkSongSelectStyle, originalStyle);
                OsuGame.DisableShear.Value = originalShear;
            });
        }

        [Test]
        public void TestUseTheseModsUnavailableIfNoFreeMods()
        {
            var room = new Room
            {
                Name = "Daily Challenge: June 4, 2024",
                Playlist =
                [
                    new PlaylistItem(TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())
                    {
                        RequiredMods = [new APIMod(new OsuModTraceable())],
                        AllowedMods = []
                    }
                ],
                EndDate = DateTimeOffset.Now.AddHours(12),
                Category = RoomCategory.DailyChallenge
            };

            AddStep("add room", () => API.Perform(new CreateRoomRequest(room)));
            Screens.OnlinePlay.DailyChallenge.DailyChallenge screen = null!;
            AddStep("push screen", () => LoadScreen(screen = new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
            AddUntilStep("wait for pushed", () => screen.IsCurrentScreen());
            AddStep("force transforms to finish", () => FinishTransforms(true));
            AddStep("right click second score", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<BeatmapLeaderboardScore>().ElementAt(1));
                InputManager.Click(MouseButton.Right);
            });
            AddAssert("use these mods not present",
                () => this.ChildrenOfType<OsuContextMenu>().All(m => m.Items.All(item => item.Text.Value != "Use these mods")));
        }

        [Test]
        public void TestNotifications()
        {
            var room = new Room
            {
                Name = "Daily Challenge: June 4, 2024",
                Playlist =
                [
                    new PlaylistItem(TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())
                    {
                        RequiredMods = [new APIMod(new OsuModTraceable())],
                        AllowedMods = [new APIMod(new OsuModDoubleTime())]
                    }
                ],
                EndDate = DateTimeOffset.Now.AddHours(12),
                Category = RoomCategory.DailyChallenge
            };

            AddStep("add room", () => API.Perform(new CreateRoomRequest(room)));
            AddStep("set daily challenge info", () => metadataClient.DailyChallengeInfo.Value = new DailyChallengeInfo { RoomID = room.RoomID!.Value });

            Screens.OnlinePlay.DailyChallenge.DailyChallenge screen = null!;
            AddStep("push screen", () => LoadScreen(screen = new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
            AddUntilStep("wait for screen", () => screen.IsCurrentScreen());
            AddStep("daily challenge ended", () => metadataClient.DailyChallengeInfo.Value = null);
            AddAssert("notification posted", () => notificationOverlay.AllNotifications.OfType<SimpleNotification>().Any(n => n.Text == DailyChallengeStrings.ChallengeEndedNotification));
        }

        [Test]
        public void TestConclusionNotificationDoesNotFireOnDisconnect()
        {
            var room = new Room
            {
                Name = "Daily Challenge: June 4, 2024",
                Playlist =
                [
                    new PlaylistItem(TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())
                    {
                        RequiredMods = [new APIMod(new OsuModTraceable())],
                        AllowedMods = [new APIMod(new OsuModDoubleTime())]
                    }
                ],
                EndDate = DateTimeOffset.Now.AddHours(12),
                Category = RoomCategory.DailyChallenge
            };

            AddStep("add room", () => API.Perform(new CreateRoomRequest(room)));
            AddStep("set daily challenge info", () => metadataClient.DailyChallengeInfo.Value = new DailyChallengeInfo { RoomID = room.RoomID!.Value });

            Screens.OnlinePlay.DailyChallenge.DailyChallenge screen = null!;
            AddStep("push screen", () => LoadScreen(screen = new Screens.OnlinePlay.DailyChallenge.DailyChallenge(room)));
            AddUntilStep("wait for screen", () => screen.IsCurrentScreen());
            AddStep("disconnect from metadata server", () => metadataClient.Disconnect());
            AddUntilStep("wait for disconnection", () => metadataClient.DailyChallengeInfo.Value, () => Is.Null);
            AddAssert("no notification posted", () => notificationOverlay.AllNotifications, () => Is.Empty);
            AddStep("reconnect to metadata server", () => metadataClient.Reconnect());
        }
    }
}
