// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Graphics.Containers;
using osu.Game.Scoring;
using osu.Game.Extensions;
using osu.Game.Screens.Ranking;

namespace osu.Game.Scoring.Render
{
    public partial class RenderResultsScreen : SoloResultsScreen
    {
        // Include the delayed rank reveal, S/SS effects and time to read the final values.
        // Shared by full replay renders and results appended to a trimmed render.
        internal const double DISPLAY_DURATION = 8000;

        private const double statistics_expand_delay = 1000;

        [Resolved(CanBeNull = true)]
        private ReplayRenderSnapshot? snapshot { get; set; }

        protected override Task<ScoreInfo[]> FetchScores()
        {
            if (snapshot == null)
                return Task.FromResult<ScoreInfo[]>([]);

            // Use the captured scope and positions; do not fetch a new global leaderboard
            // through the offline API or replace the score panel currently being presented.
            return Task.FromResult(snapshot.CreateLeaderboardScores().AllScores
                .Where(score => Score == null || (!score.Equals(Score) && !score.MatchesOnlineID(Score)))
                .OrderByTotalScore().ToArray());
        }

        public RenderResultsScreen(ScoreInfo score)
            : base(score)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Scheduler.AddDelayed(() =>
            {
                if (StatisticsPanel.State.Value == Visibility.Hidden)
                {
                    Logger.Log("[RenderResultsScreen] Expanding results statistics for render capture.",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                    StatisticsPanel.ToggleVisibility();
                }
            }, statistics_expand_delay);
        }
    }
}
