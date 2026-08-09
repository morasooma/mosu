// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    public partial class ResultsScreen : RankedPlaySubScreen
    {
        public override LocalisableString StageHeading => "Results";

        public override bool ShowBeatmapBackground => true;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private RankedPlayMatchInfo matchInfo { get; set; } = null!;

        [Resolved]
        private BeatmapLookupCache beatmapLookupCache { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> globalBeatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> globalRuleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> globalMods { get; set; } = null!;

        private LoadingSpinner loadingSpinner = null!;
        private MainPanel? mainPanel;

        [BackgroundDependencyLoader]
        private void load()
        {
            CornerPieceVisibility.Value = Visibility.Hidden;

            AddRangeInternal(new Drawable[]
            {
                loadingSpinner = new LoadingSpinner
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            loadingSpinner.Show();

            fetchFinalScores().FireAndForget();
        }

        private async Task fetchFinalScores()
        {
            try
            {
                var room = client.Room;

                if (room == null)
                    return;

                long roomId = room.RoomID;
                long playlistItemId = room.Settings.PlaylistItemId;

                int localUserId = api.LocalUser.Value.OnlineID;
                int opponentId = matchInfo.RoomState.Users.Keys.Single(it => it != localUserId);
                List<MultiplayerScore> apiScores = await getFinalScoresAsync(roomId, playlistItemId, localUserId, opponentId).ConfigureAwait(false);

                ScoreInfo[] scores = apiScores.Select(s => s.CreateScoreInfo(scoreManager, rulesets, globalBeatmap.Value.BeatmapInfo)).ToArray();

                Debug.Assert(scores.Length <= 2);

                ScoreInfo playerScore = scores.SingleOrDefault(s => s.UserID == localUserId) ?? new ScoreInfo
                {
                    Rank = ScoreRank.F,
                    Ruleset = globalRuleset.Value,
                    User = new APIUser { Id = localUserId }
                };

                ScoreInfo opponentScore = scores.SingleOrDefault(s => s.UserID == opponentId) ?? new ScoreInfo
                {
                    Rank = ScoreRank.F,
                    Ruleset = globalRuleset.Value,
                    User = new APIUser { Id = opponentId }
                };

                await waitForDamageInfoAsync(localUserId, opponentId, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                int maxTotalScore = (int)Math.Max(playerScore.TotalScore, opponentScore.TotalScore);
                double damageMultiplier = matchInfo.RoomState.DamageMultiplier;

                RankedPlayUserInfo playerUserInfo = matchInfo.RoomState.Users[localUserId];
                RankedPlayUserInfo opponentUserInfo = matchInfo.RoomState.Users[opponentId];

                RankedPlayDamageInfo playerDamageInfo = getDamageInfoOrDefault(playerUserInfo, playerScore, maxTotalScore, damageMultiplier);
                RankedPlayDamageInfo opponentDamageInfo = getDamageInfoOrDefault(opponentUserInfo, opponentScore, maxTotalScore, damageMultiplier);

                // Should complete instantaneously due to prior lookups.
                APIBeatmap beatmap = (await beatmapLookupCache.GetBeatmapAsync(globalBeatmap.Value.BeatmapInfo.OnlineID).ConfigureAwait(false))!;

                Schedule(() =>
                {
                    LoadComponentAsync(new MainPanel
                    {
                        RelativeSizeAxes = Axes.Both,
                        // A little bit of room for the countdown timer...
                        Margin = new MarginPadding { Top = 45 },
                        PlayerScore = playerScore,
                        OpponentScore = opponentScore,
                        PlayerDamageInfo = playerDamageInfo,
                        OpponentDamageInfo = opponentDamageInfo,
                        Beatmap = beatmap,
                        Mods = globalMods.Value.ToArray(),
                    }, loaded =>
                    {
                        AddInternal(loaded);
                        mainPanel = loaded;
                    });
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to load scores for playlist item.");
                throw;
            }
            finally
            {
                Scheduler.Add(() => loadingSpinner.Hide());
            }
        }

        public override void OnExiting(RankedPlaySubScreen? next)
        {
            mainPanel?.StopAllSamples();
            base.OnExiting(next);
        }

        private async Task<List<MultiplayerScore>> getFinalScoresAsync(long roomId, long playlistItemId, int localUserId, int opponentId)
        {
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            List<MultiplayerScore> scores;

            do
            {
                var scoreLookup = new TaskCompletionSource<List<MultiplayerScore>>();
                var request = new IndexPlaylistScoresRequest(roomId, playlistItemId);

                request.Success += req => scoreLookup.TrySetResult(req.Scores);
                request.Failure += scoreLookup.SetException;

                api.Queue(request);
                scores = await scoreLookup.Task.ConfigureAwait(false);

                if (scores.Any(score => score.User?.Id == localUserId) && scores.Any(score => score.User?.Id == opponentId))
                    return scores;

                await Task.Delay(100).ConfigureAwait(false);
            } while (DateTime.UtcNow < deadline);

            return scores;
        }

        private async Task waitForDamageInfoAsync(int localUserId, int opponentId, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (matchInfo.RoomState.Users.TryGetValue(localUserId, out RankedPlayUserInfo? playerInfo)
                    && playerInfo.DamageInfo != null
                    && matchInfo.RoomState.Users.TryGetValue(opponentId, out RankedPlayUserInfo? opponentInfo)
                    && opponentInfo.DamageInfo != null)
                {
                    return;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        private static RankedPlayDamageInfo getDamageInfoOrDefault(RankedPlayUserInfo userInfo, ScoreInfo score, int maxTotalScore, double damageMultiplier)
        {
            if (userInfo.DamageInfo != null)
                return userInfo.DamageInfo;

            int rawDamage = maxTotalScore - (int)score.TotalScore;
            int damage = (int)Math.Ceiling(rawDamage * damageMultiplier);
            int oldLife = userInfo.Life;
            int newLife = Math.Max(0, oldLife - damage);

            return new RankedPlayDamageInfo
            {
                RawDamage = rawDamage,
                Damage = damage,
                DirectDamage = rawDamage,
                Multiplier = damageMultiplier,
                OldLife = oldLife,
                NewLife = newLife,
            };
        }
    }
}
