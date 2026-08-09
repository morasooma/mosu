// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Graphics.Containers;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Scoring.Render
{
    public partial class RenderResultsScreen : SoloResultsScreen
    {
        private const double statistics_expand_delay = 1000;

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
