// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using MessagePack;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Spectator;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Tests.Visual.Multiplayer
{
    public partial class TestSceneMultiplayerGameplayLeaderboardTeams : MultiplayerGameplayLeaderboardTestScene
    {
        [Test]
        [NonParallelizable]
        public void TestRelaxUsesMosuScoreForDisplayOrderingAndTeamTotals()
        {
            long resultsScore = 0;
            AddStep("send an SS Relax play and a frame from an older client", () =>
            {
                bool previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
                bool previousStable = MosuServerEnvironment.UsesStableProtocol;
                try
                {
                    MosuServerEnvironment.IsThirdPartyServer = false;
                    MosuServerEnvironment.UsesStableProtocol = false;

                    var ruleset = new OsuRuleset();
                    var circle = new HitCircle();
                    var map = new Beatmap { HitObjects = [circle] };
                    var mods = new[] { new OsuModRelax() };
                    using var processor = ruleset.CreateScoreProcessor();
                    processor.Mods.Value = mods;
                    processor.ApplyBeatmap(map);
                    processor.ApplyResult(new JudgementResult(circle, circle.Judgement) { Type = HitResult.Great });

                    var score = new ScoreInfo(map.BeatmapInfo, ruleset.RulesetInfo) { Mods = mods };
                    processor.PopulateScore(score);
                    resultsScore = score.GetDisplayScore(ScoringMode.Standardised);
                    Assert.That(resultsScore, Is.EqualTo(1_000_000));

                    var frame = new LegacyReplayFrame(0, 0, 0, ReplayButtonState.None);
                    var bundle = new FrameDataBundle(score, processor, [frame]);
                    byte[] packet = MessagePackSerializer.Serialize(bundle, SignalRUnionWorkaroundResolver.OPTIONS);
                    var received = MessagePackSerializer.Deserialize<FrameDataBundle>(packet, SignalRUnionWorkaroundResolver.OPTIONS);
                    Assert.That(received.Header.TotalScore, Is.EqualTo(100_000), "The replay score must stay on the original scale.");
                    SendFrames(2, received);
                    SendFrames(3, new FrameDataBundle(new FrameHeader(500_000, 1, 1, 1, [], new ScoreProcessorStatistics(), default, [], null, null), [frame]));
                }
                finally
                {
                    MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
                    MosuServerEnvironment.UsesStableProtocol = previousStable;
                }
            });

            AddUntilStep("both scores reached the team leaderboard", () => LeaderboardProvider!.TeamScores.Values.All(score => score.Value > 0));
            AddAssert("live Relax row matches the results score", () => LeaderboardProvider!.Scores.Single(s => s.User.OnlineID == 2).GetDisplayScore(ScoringMode.Standardised) == resultsScore);
            AddUntilStep("Relax leads in the live standings", () => LeaderboardProvider!.Scores.Single(s => s.User.OnlineID == 2).Position.Value == 1);
            AddAssert("team totals use the same scale", () => LeaderboardProvider!.TeamScores[0].Value == resultsScore && LeaderboardProvider.TeamScores[1].Value == 500_000);
        }

        protected override MultiplayerRoomUser CreateUser(int userId)
        {
            var user = base.CreateUser(userId);
            user.MatchState = new TeamVersusUserState
            {
                TeamID = userId % 2
            };
            return user;
        }

        protected override MultiplayerLeaderboardProvider CreateLeaderboardProvider() =>
            new MultiplayerLeaderboardProvider(MultiplayerUsers.ToArray())
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("Add external display components", () =>
            {
                LoadComponentAsync(new MatchScoreDisplay
                {
                    Team1Score = { BindTarget = LeaderboardProvider!.TeamScores[0] },
                    Team2Score = { BindTarget = LeaderboardProvider.TeamScores[1] }
                }, Add);

                GameplayMatchScoreDisplay matchScoreDisplay;
                LoadComponentAsync(matchScoreDisplay = new GameplayMatchScoreDisplay
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Team1Score = { BindTarget = LeaderboardProvider.TeamScores[0] },
                    Team2Score = { BindTarget = LeaderboardProvider.TeamScores[1] },
                }, Add);

                Leaderboard!.CollapseDuringGameplay.BindValueChanged(_ => matchScoreDisplay.Expanded.Value = !Leaderboard.CollapseDuringGameplay.Value);
            });
        }
    }
}
