// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Screens;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Multiplayer;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Match.Gameplay
{
    public partial class ScreenGameplay : MultiplayerPlayer
    {
        /// <summary>
        /// This is a Ranked Play session — all mod multipliers are forced to 1.00x.
        /// </summary>
        protected override bool IsRankedPlaySession => true;

        public ScreenGameplay(Room room, PlaylistItem playlistItem, MultiplayerRoomUser[] users)
            : base(room, playlistItem, users, showFailingOverlay: false)
        {
        }
        internal static ScoreInfo CreateScoreInfoForSubmission(ScoreInfo gameplayScore)
        {
            ModRelax[] relaxMods = gameplayScore.Mods.OfType<ModRelax>().ToArray();

            if (relaxMods.Length == 0 || gameplayScore.BeatmapInfo == null)
                return gameplayScore;

            ScoreInfo submissionScore = gameplayScore.DeepClone();
            var multiplierCalculator = submissionScore.Ruleset.CreateInstance().CreateScoreMultiplierCalculator(
                new ScoreMultiplierContext(gameplayScore.BeatmapInfo.Difficulty));

            submissionScore.TotalScore = (long)Math.Round(submissionScore.TotalScoreWithoutMods * multiplierCalculator.CalculateFor(relaxMods));
            return submissionScore;
        }

        protected override async Task PrepareScoreForResultsAsync(Score score)
        {
            await base.PrepareScoreForResultsAsync(score).ConfigureAwait(false);

            Scheduler.Add(() =>
            {
                if (this.IsCurrentScreen())
                    this.Exit();
            });
        }
    }
}
