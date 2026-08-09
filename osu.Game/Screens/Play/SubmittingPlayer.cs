// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A player used by the public source build. Scores and replays are kept locally;
    /// this class intentionally contains no online score-submission implementation.
    /// </summary>
    public abstract partial class SubmittingPlayer : Player
    {
        [Resolved]
        private SessionStatics statics { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        protected bool OnlineRecordSendingDisabled => true;

        protected SubmittingPlayer(PlayerConfiguration configuration = null)
            : base(configuration)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (DrawableRuleset == null)
                return;

            AddInternal(new PlayerTouchInputDetector());
            AddInternal(new GameplayOffsetControl
            {
                Margin = new MarginPadding(20),
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            });
        }

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart) => new MasterGameplayClockContainer(beatmap, gameplayStart)
        {
            ShouldValidatePlaybackRate = true,
        };

        public override bool AllowCriticalSettingsAdjustment
        {
            get
            {
                if (!IsBreakTime.Value && GameplayClockContainer.CurrentTime - GameplayClockContainer.GameplayStartTime > 10000)
                    return false;

                if (GameplayClockContainer.IsPaused.Value)
                    return false;

                return base.AllowCriticalSettingsAdjustment;
            }
        }

        protected override async Task PrepareScoreForResultsAsync(Score score)
        {
            await base.PrepareScoreForResultsAsync(score).ConfigureAwait(false);
            score.ScoreInfo.Date = DateTimeOffset.Now;
        }

        protected override void StartGameplay()
        {
            base.StartGameplay();

            realm.WriteAsync(r =>
            {
                var realmBeatmap = r.Find<BeatmapInfo>(Beatmap.Value.BeatmapInfo.ID);
                if (realmBeatmap != null)
                    realmBeatmap.LastPlayed = DateTimeOffset.Now;
            });
        }

        public override bool Pause()
        {
            bool wasPaused = GameplayClockContainer.IsPaused.Value;
            bool paused = base.Pause();

            if (!wasPaused && paused)
                Score.ScoreInfo.Pauses.Add((int)Math.Round(GameplayClockContainer.CurrentTime));

            return paused;
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            bool exiting = base.OnExiting(e);
            statics.SetValue(Static.LastLocalUserScore, Score?.ScoreInfo.DeepClone());
            return exiting;
        }

        protected override ResultsScreen CreateResults(ScoreInfo score) => new SoloResultsScreen(score);
    }
}
