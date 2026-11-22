using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Online.Legacy
{
    /// <summary>Adapts stable Bancho spectating, which does not use lazer's SignalR hub.</summary>
    public partial class StableSpectatorClient : SpectatorClient
    {
        private readonly StableBanchoSession session;
        private int? watchedUser;
        private SpectatorState? watchedState;
        private readonly Queue<StableSpectatorFrames> waitingFrames = new Queue<StableSpectatorFrames>();

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private osu.Game.Rulesets.RulesetStore rulesets { get; set; } = null!;

        public override IBindable<bool> IsConnected => session.IsConnected;

        public StableSpectatorClient(StableBanchoSession session) => this.session = session;

        [BackgroundDependencyLoader]
        private void load()
        {
            session.SpectatorJoined += spectatorJoined;
            session.SpectatorLeft += spectatorLeft;
            session.SpectatorFramesReceived += framesReceived;
            session.UserStatisticsUpdated += userStatisticsUpdated;
        }

        private void spectatorJoined(int userId)
        {
            _ = ((ISpectatorClient)this).UserStartedWatching(new[]
            {
                new SpectatorUser { OnlineID = userId, Username = session.Users.GetValueOrDefault(userId)?.Username ?? userId.ToString() },
            });
        }

        private void spectatorLeft(int userId) => _ = ((ISpectatorClient)this).UserEndedWatching(userId);

        protected override Task<bool> BeginPlayingInternal(long? scoreToken, SpectatorState state) => Task.FromResult(false);

        protected override Task SendFramesInternal(FrameDataBundle bundle)
        {
            return Task.CompletedTask;
        }

        protected override Task EndPlayingInternal(SpectatorState state) => Task.CompletedTask;

        protected override async Task WatchUserInternal(int userId)
        {
            if (watchedUser is int previous && previous != userId)
                await StopWatchingUserInternal(previous).ConfigureAwait(false);
            watchedUser = userId;
            watchedState = null;
            waitingFrames.Clear();
            await session.SendPacketAsync(StableBanchoPacketCodec.CreateInt32Packet(StableBanchoClientPacket.StartSpectating, userId)).ConfigureAwait(false);
            byte[] request = new byte[6];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(request, 1);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(2), userId);
            await session.SendPacketAsync(new StableBanchoPacket(StableBanchoClientPacket.UserStatsRequest, request)).ConfigureAwait(false);
            Schedule(() =>
            {
                var stats = session.GetLatestUserStatistics(userId);
                if (stats is { Action: 2 }) userStatisticsUpdated(stats);
            });
        }

        protected override Task StopWatchingUserInternal(int userId)
        {
            if (watchedUser != userId) return Task.CompletedTask;
            watchedUser = null;
            watchedState = null;
            waitingFrames.Clear();
            return session.SendPacketAsync(new StableBanchoPacket(StableBanchoClientPacket.StopSpectating));
        }

        private void userStatisticsUpdated(StableBanchoUserStatistics stats)
        {
            if (watchedUser != stats.UserId)
                return;
            if (stats.Action != 2 || stats.BeatmapId <= 0)
            {
                if (watchedState != null)
                {
                    var ended = SpectatorSnapshot.State(watchedState);
                    ended.State = SpectatedUserState.Quit;
                    _ = ((ISpectatorClient)this).UserFinishedPlaying(stats.UserId, ended);
                    watchedState = null;
                    waitingFrames.Clear();
                }
                return;
            }
            var state = new SpectatorState
            {
                BeatmapID = stats.BeatmapId, BeatmapChecksum = stats.BeatmapChecksum,
                RulesetID = stats.RulesetId, State = SpectatedUserState.Playing,
                Mods = LegacyModsToApi(stats.RulesetId, stats.Mods),
            };
            if (watchedState?.Equals(state) == true) return;
            watchedState = state;
            _ = ((ISpectatorClient)this).UserBeganPlaying(stats.UserId, state);
            while (waitingFrames.TryDequeue(out var pending))
                framesReceived(pending);
        }

        private APIMod[] LegacyModsToApi(int mode, int mods)
        {
            var ruleset = rulesets.GetRuleset(mode)?.CreateInstance();
            if (ruleset == null) return Array.Empty<APIMod>();
            var converted = ruleset.ConvertFromLegacyMods((osu.Game.Beatmaps.Legacy.LegacyMods)mods).Select(m => new APIMod(m)).ToList();
            var classic = ruleset.CreateModFromAcronym("CL");
            if (classic != null && StableModCompatibility.RequiresClassic(mode)) converted.Add(new APIMod(classic));
            return converted.ToArray();
        }

        private void framesReceived(StableSpectatorFrames bundle)
        {
            if (watchedUser is not int userId) return;
            if (bundle.Action == StableReplayAction.NewSong)
            {
                watchedState = null;
                var stats = session.GetLatestUserStatistics(userId);
                if (stats is { Action: 2 }) userStatisticsUpdated(stats);
            }
            if (watchedState == null)
            {
                // Frame and status packets can arrive in either order. Preserve a
                // bounded backlog rather than losing the beginning of the replay.
                if (bundle.Action != StableReplayAction.NewSong)
                {
                    if (waitingFrames.Count == 32) waitingFrames.Dequeue();
                    waitingFrames.Enqueue(bundle);
                }
                return;
            }
            if (bundle.Action is StableReplayAction.Completion or StableReplayAction.Fail or StableReplayAction.SongSelect)
            {
                var state = SpectatorSnapshot.State(watchedState);
                state.State = bundle.Action == StableReplayAction.Completion ? SpectatedUserState.Passed
                    : bundle.Action == StableReplayAction.Fail ? SpectatedUserState.Failed : SpectatedUserState.Quit;
                _ = ((ISpectatorClient)this).UserFinishedPlaying(userId, state);
                return;
            }
            if (bundle.Frames.Count == 0) return;
            var s = bundle.Score;
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = s.Count300, [HitResult.Ok] = s.Count100, [HitResult.Meh] = s.Count50,
                [HitResult.Perfect] = s.CountGeki, [HitResult.Good] = s.CountKatu, [HitResult.Miss] = s.CountMiss,
            };
            int watchedMode = watchedState.RulesetID ?? 0;
            if (watchedMode == 2)
            {
                statistics.Remove(HitResult.Perfect);
                statistics.Remove(HitResult.Good);
                statistics[HitResult.LargeTickHit] = s.Count100;
                statistics[HitResult.SmallTickHit] = s.Count50;
                statistics[HitResult.SmallTickMiss] = s.CountKatu;
                statistics.Remove(HitResult.Ok);
                statistics.Remove(HitResult.Meh);
            }
            if (watchedMode == 0)
            {
                statistics.Remove(HitResult.Perfect);
                statistics.Remove(HitResult.Good);
            }
            else if (watchedMode == 1)
            {
                statistics.Remove(HitResult.Perfect);
                statistics.Remove(HitResult.Good);
                statistics.Remove(HitResult.Meh);
                statistics[HitResult.LargeBonus] = s.CountGeki + s.CountKatu;
            }
            var scoreInfo = new osu.Game.Scoring.ScoreInfo
            {
                Ruleset = rulesets.GetRuleset(watchedMode)!,
                Statistics = statistics,
            };
            double accuracy = statistics.Values.Sum() == 0 ? 1 : StableScoreStatistics.CalculateAccuracy(scoreInfo);
            var header = new FrameHeader(s.TotalScore, accuracy, s.CurrentCombo, s.MaxCombo, statistics,
                new ScoreProcessorStatistics
                {
                    BaseScore = accuracy * 300 * statistics.Values.Sum(),
                    MaximumBaseScore = 300 * statistics.Values.Sum(),
                    AccuracyJudgementCount = statistics.Values.Sum(),
                }, DateTimeOffset.UtcNow, watchedState.Mods.ToArray(), null, null);
            _ = ((ISpectatorClient)this).UserSentFrames(userId, new FrameDataBundle(header, bundle.Frames.ToArray()));
        }

        protected override Task DisconnectInternal() => watchedUser is int user ? StopWatchingUserInternal(user) : Task.CompletedTask;
        public override async Task Reconnect() => await session.LoginNowAsync().ConfigureAwait(false);

        protected override void Dispose(bool isDisposing)
        {
            session.SpectatorJoined -= spectatorJoined;
            session.SpectatorLeft -= spectatorLeft;
            session.SpectatorFramesReceived -= framesReceived;
            session.UserStatisticsUpdated -= userStatisticsUpdated;
            base.Dispose(isDisposing);
        }
    }
}
