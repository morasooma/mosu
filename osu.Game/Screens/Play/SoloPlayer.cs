// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Screens.Play
{
    public partial class SoloPlayer : SubmittingPlayer
    {
        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly SoloGameplayLeaderboardProvider leaderboardProvider = new SoloGameplayLeaderboardProvider();

#if DEBUG
        [Resolved]
        private SessionStatics sessionStatics { get; set; } = null!;
#endif

        public SoloPlayer([CanBeNull] PlayerConfiguration configuration = null)
            : base(configuration)
        {
            Configuration.ShowLeaderboard = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(leaderboardProvider);
        }

        protected override void PrepareReplay()
        {
            base.PrepareReplay();

#if DEBUG
            if (DrawableRuleset is IHasReplayBotPlayback replayBotPlayback)
                replayBotPlayback.SetReplayBotScore(getLoadedReplayBotScore());
#endif
        }
#if DEBUG
        private Score getLoadedReplayBotScore()
        {
            Score loadedScore = sessionStatics.Get<Score>(Static.LoadedReplayBotScore);

            if (loadedScore == null || loadedScore.Replay.Frames.Count == 0)
                return null;

            if (!rulesetMatches(loadedScore) || !beatmapMatches(loadedScore))
                return null;

            return loadedScore.DeepClone();
        }

        private bool rulesetMatches(Score score)
            => string.Equals(score.ScoreInfo.Ruleset?.ShortName, Ruleset.Value.ShortName, StringComparison.OrdinalIgnoreCase);

        private bool beatmapMatches(Score score)
        {
            string currentHash = Beatmap.Value.BeatmapInfo.Hash;
            string replayHash = score.ScoreInfo.BeatmapHash ?? score.ScoreInfo.BeatmapInfo?.Hash;

            if (!string.IsNullOrEmpty(currentHash) && !string.IsNullOrEmpty(replayHash))
                return string.Equals(currentHash, replayHash, StringComparison.OrdinalIgnoreCase);

            int currentOnlineId = Beatmap.Value.BeatmapInfo.OnlineID;
            int replayOnlineId = score.ScoreInfo.BeatmapInfo?.OnlineID ?? 0;

            return currentOnlineId > 0 && replayOnlineId == currentOnlineId;
        }
#endif
    }
}
