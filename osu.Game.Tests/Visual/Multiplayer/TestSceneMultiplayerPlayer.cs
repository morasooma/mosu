// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.TagCoop;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Visual.Multiplayer
{
    public partial class TestSceneMultiplayerPlayer : MultiplayerTestScene
    {
        private MultiplayerPlayer player = null!;

        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notificationOverlay;

        public TestSceneMultiplayerPlayer()
        {
            Add(notificationOverlay = new NotificationOverlay());
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("join room", () => JoinRoom(CreateDefaultRoom()));
            WaitForJoined();
        }

        [Test]
        public void TestGameplay()
        {
            setup();

            AddUntilStep("wait for gameplay start", () => player.LocalUserPlaying.Value);
        }

        [Test]
        public void TestTagCoopGameplayInitialisation()
        {
            AddStep("configure tag co-op room", () =>
            {
                MultiplayerClient.ServerAPIRoom!.Type = MatchType.TagCoop;

                int[] playerOrder = MultiplayerClient.ServerRoom!.Users.Select(user => user.UserID).OrderBy(id => id).ToArray();
                var roomState = TagCoopRoomState.CreateDefault(MultiplayerClient.ServerRoom.Settings.MaxParticipants);
                roomState.PlayerOrder = playerOrder;
                MultiplayerClient.ServerRoom.MatchState = roomState;

                ((IMultiplayerClient)MultiplayerClient).MatchRoomStateChanged(roomState).WaitSafely();
            });

            setup();

            AddAssert("tag co-op controller loaded", () => player.ChildrenOfType<TagCoopGameplayController>().Single().IsLoaded);
            AddAssert("turn display uses bottom-right HUD anchor", () =>
            {
                var display = player.ChildrenOfType<TagCoopTurnDisplay>().Single();
                return display.Anchor == Anchor.BottomRight && display.Origin == Anchor.BottomRight;
            });
            AddUntilStep("wait for gameplay start", () => player.LocalUserPlaying.Value);
            AddAssert("combined replay contains player list", () =>
                player.GameplayState.Score.ScoreInfo.TagCoopReplay?.Players.Select(p => p.UserID)
                      .SequenceEqual(MultiplayerClient.ServerRoom!.Users.Select(user => user.UserID).OrderBy(id => id)) == true);
        }

        [Test]
        public void TestFail()
        {
            setup(() => new[] { new OsuModAutopilot() });

            AddUntilStep("wait for gameplay start", () => player.LocalUserPlaying.Value);
            AddStep("set health zero", () => player.ChildrenOfType<HealthProcessor>().Single().Health.Value = 0);
            AddUntilStep("wait for fail", () => player.ChildrenOfType<HealthProcessor>().Single().HasFailed);
            AddAssert("fail animation not shown", () => !player.GameplayState.HasFailed);

            // ensure that even after reaching a failed state, score processor keeps accounting for new hit results.
            // the testing method used here (autopilot + hold key) is sort-of dodgy, but works enough.
            AddAssert("score is zero", () => player.GameplayState.ScoreProcessor.TotalScore.Value == 0);
            AddStep("hold key", () => player.ChildrenOfType<OsuInputManager.RulesetKeyBindingContainer>().First().TriggerPressed(OsuAction.LeftButton));
            AddUntilStep("score changed", () => player.GameplayState.ScoreProcessor.TotalScore.Value > 0);
        }

        private void setup(Func<IReadOnlyList<Mod>>? mods = null)
        {
            AddStep("set beatmap", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
                SelectedMods.Value = mods?.Invoke() ?? Array.Empty<Mod>();
            });

            AddStep("Start track playing", () =>
            {
                Beatmap.Value.Track.Start();
            });

            AddStep("initialise gameplay", () =>
            {
                Stack.Push(player = new MultiplayerPlayer(MultiplayerClient.ServerAPIRoom!, new PlaylistItem(Beatmap.Value.BeatmapInfo)
                {
                    RulesetID = Beatmap.Value.BeatmapInfo.Ruleset.OnlineID,
                }, MultiplayerClient.ServerRoom!.Users.ToArray()));
            });

            AddUntilStep("wait for player to be current", () => player.IsCurrentScreen() && player.IsLoaded);

            AddAssert("gameplay clock is paused", () => player.ChildrenOfType<GameplayClockContainer>().Single().IsPaused.Value);
            AddAssert("gameplay clock is not running", () => !player.ChildrenOfType<GameplayClockContainer>().Single().IsRunning);

            AddStep("start gameplay", () => ((IMultiplayerClient)MultiplayerClient).GameplayStarted());

            AddUntilStep("gameplay clock is not paused", () => !player.ChildrenOfType<GameplayClockContainer>().Single().IsPaused.Value);
            AddAssert("gameplay clock is running", () => player.ChildrenOfType<GameplayClockContainer>().Single().IsRunning);
        }
    }
}
