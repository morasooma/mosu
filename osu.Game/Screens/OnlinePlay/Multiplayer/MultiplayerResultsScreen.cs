// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Legacy;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Playlists;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    public partial class MultiplayerResultsScreen : PlaylistItemScoreResultsScreen
    {
        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        public MultiplayerResultsScreen(ScoreInfo score, long roomId, PlaylistItem playlistItem)
            : base(score, roomId, playlistItem)
        {
        }

        protected override Task<ScoreInfo[]> FetchScores()
        {
            if (stableBanchoSession == null || Score == null)
                return base.FetchScores();

            var scores = new List<ScoreInfo> { Score };
            int localUserId = Score.User.Id;

            foreach (StableBanchoMatchScore stableScore in stableBanchoSession.GetLatestMatchScores())
            {
                if (stableScore.UserId == localUserId)
                    continue;

                StableBanchoScoreFrame frame = stableScore.Frame;
                int totalHits = frame.Count300 + frame.Count100 + frame.Count50 + frame.CountMiss;
                var statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = frame.Count300,
                    [HitResult.Ok] = frame.Count100,
                    [HitResult.Meh] = frame.Count50,
                    [HitResult.Miss] = frame.CountMiss,
                };

                ScoreInfo score = Score.DeepClone();
                score.ID = Guid.NewGuid();
                score.OnlineID = -stableScore.UserId;
                score.LegacyOnlineID = -1;
                score.User = new APIUser
                {
                    Id = stableScore.UserId,
                    Username = stableScore.Username,
                    AvatarUrl = stableBanchoSession.GetAvatarUrl(stableScore.UserId),
                };
                score.TotalScore = frame.TotalScore;
                score.TotalScoreWithoutMods = frame.TotalScore;
                score.LegacyTotalScore = frame.TotalScore;
                score.MaxCombo = frame.MaxCombo;
                score.Combo = frame.CurrentCombo;
                score.Statistics = statistics;
                score.Accuracy = totalHits == 0
                    ? 1
                    : (frame.Count300 * 300d + frame.Count100 * 100d + frame.Count50 * 50d) / (totalHits * 300d);
                score.Rank = score.Ruleset.CreateInstance().CreateScoreProcessor().RankFromScore(score.Accuracy, statistics);
                scores.Add(score);
            }

            ScoreInfo[] ordered = scores.OrderByDescending(score => score.TotalScore).ToArray();
            for (int i = 0; i < ordered.Length; i++)
                ordered[i].Position = i + 1;

            return Task.FromResult(ordered);
        }

        protected override Task<ScoreInfo[]> FetchNextPage(int direction)
            => stableBanchoSession == null ? base.FetchNextPage(direction) : Task.FromResult<ScoreInfo[]>([]);
    }
}
