// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Online.Spectator
{
    public abstract partial class SpectatorClient : Component, ISpectatorClient
    {
        /// <summary>
        /// The maximum milliseconds between frame bundle sends.
        /// </summary>
        public const double TIME_BETWEEN_SENDS = 200;

        /// <summary>
        /// Whether the <see cref="SpectatorClient"/> is currently connected.
        /// This is NOT thread safe and usage should be scheduled.
        /// </summary>
        public abstract IBindable<bool> IsConnected { get; }

        /// <summary>
        /// The states of all users currently being watched by the local user.
        /// </summary>
        [UsedImplicitly] // Marked virtual due to mock use in testing
        public virtual IBindableDictionary<int, SpectatorState> WatchedUserStates => watchedUserStates;

        /// <summary>
        /// All users who are currently watching the local user.
        /// </summary>
        public IBindableList<SpectatorUser> WatchingUsers => watchingUsers;

        /// <summary>
        /// Whether the local user is playing.
        /// </summary>
        private bool isPlaying { get; set; }

        private bool loggedFramesOutsideGameplayScope;

        /// <summary>
        /// Called whenever new frames arrive from the server.
        /// </summary>
        [UsedImplicitly] // Marked virtual due to mock use in testing
        public virtual event Action<int, FrameDataBundle>? OnNewFrames;

        /// <summary>
        /// Called whenever a user starts a play session, or immediately if the user is being watched and currently in a play session.
        /// </summary>
        public event Action<int, SpectatorState>? OnUserBeganPlaying;

        /// <summary>
        /// Called whenever a user finishes a play session.
        /// </summary>
        public event Action<int, SpectatorState>? OnUserFinishedPlaying;

        /// <summary>
        /// Called whenever a user-submitted score has been fully processed.
        /// </summary>
        public event Action<int, long>? OnUserScoreProcessed;

        /// <summary>
        /// A dictionary containing all users currently being watched, with the number of watching components for each user.
        /// </summary>
        private readonly Dictionary<int, int> watchedUsersRefCounts = new Dictionary<int, int>();

        private readonly BindableDictionary<int, SpectatorState> watchedUserStates = new BindableDictionary<int, SpectatorState>();

        private readonly BindableList<SpectatorUser> watchingUsers = new BindableList<SpectatorUser>();
        private readonly SpectatorState currentState = new SpectatorState();

        private IBeatmap? currentBeatmap;
        private Score? currentScore;
        private long? currentScoreToken;
        private ScoreProcessor? currentScoreProcessor;

        private readonly Queue<FrameDataBundle> pendingFrameBundles = new Queue<FrameDataBundle>();

        private readonly List<LegacyReplayFrame> pendingFrames = new List<LegacyReplayFrame>();
        private readonly List<TagCoopReplayFrame> pendingTagCoopFrames = new List<TagCoopReplayFrame>();
        private bool replaceTagCoopReplayOnNextBundle;
        private TaskCompletionSource<bool>? replacementTagCoopBundleSent;
        private Score? nextTagCoopScore;
        private TagCoopReplayPlayer[] nextTagCoopPlayers = Array.Empty<TagCoopReplayPlayer>();

        private double lastPurgeTime;

        private Task? lastSend;

        private const int max_pending_frames = 30;

        [BackgroundDependencyLoader]
        private void load()
        {
            IsConnected.BindValueChanged(connected => Schedule(() =>
            {
                if (connected.NewValue)
                {
                    // get all the users that were previously being watched
                    var users = new Dictionary<int, int>(watchedUsersRefCounts);
                    watchedUsersRefCounts.Clear();

                    // resubscribe to watched users.
                    foreach ((int user, int watchers) in users)
                    {
                        for (int i = 0; i < watchers; i++)
                            WatchUser(user);
                    }

                    // re-send state in case it wasn't received
                    if (isPlaying)
                        // TODO: this is likely sent out of order after a reconnect scenario. needs further consideration.
                        BeginPlayingInternal(currentScoreToken, currentState);
                }
                else
                {
                    watchedUserStates.Clear();
                    watchingUsers.Clear();
                }
            }), true);
        }

        Task ISpectatorClient.UserBeganPlaying(int userId, SpectatorState state)
        {
            Schedule(() =>
            {
                if (watchedUsersRefCounts.ContainsKey(userId))
                    watchedUserStates[userId] = state;

                OnUserBeganPlaying?.Invoke(userId, state);
            });

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserFinishedPlaying(int userId, SpectatorState state)
        {
            Schedule(() =>
            {
                if (watchedUsersRefCounts.ContainsKey(userId))
                    watchedUserStates[userId] = state;

                OnUserFinishedPlaying?.Invoke(userId, state);
            });

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserSentFrames(int userId, FrameDataBundle data)
        {
            // Extension-only bundles may be accepted by a newer spectator server for replay
            // storage, but there is no ordinary replay timeline data for live spectators to
            // consume. Do not expose such bundles to handlers written against the legacy
            // non-empty FrameDataBundle contract.
            if (data.Frames.Count == 0)
                return Task.CompletedTask;

            data.Frames[^1].Header = data.Header;

            Schedule(() => OnNewFrames?.Invoke(userId, data));

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserScoreProcessed(int userId, long scoreId)
        {
            Schedule(() => OnUserScoreProcessed?.Invoke(userId, scoreId));

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserStartedWatching(SpectatorUser[] users)
        {
            Schedule(() =>
            {
                foreach (var user in users)
                {
                    if (!watchingUsers.Contains(user))
                        watchingUsers.Add(user);
                }
            });

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserEndedWatching(int userId)
        {
            Schedule(() =>
            {
                watchingUsers.RemoveAll(u => u.OnlineID == userId);
            });

            return Task.CompletedTask;
        }

        public void BeginPlaying(long? scoreToken, GameplayState state, Score score)
        {
            // This schedule is only here to match the one below in `EndPlaying`.
            Schedule(() =>
            {
                if (isPlaying)
                    throw new InvalidOperationException($"Cannot invoke {nameof(BeginPlaying)} when already playing");

                // transfer state at point of beginning play
                currentState.BeatmapID = score.ScoreInfo.BeatmapInfo!.OnlineID;
                currentState.RulesetID = score.ScoreInfo.RulesetID;
                currentState.Mods = score.ScoreInfo.Mods.Select(m => new APIMod(m)).ToArray();
                currentState.State = SpectatedUserState.Playing;
                currentState.MaximumStatistics = state.ScoreProcessor.MaximumStatistics;
                currentState.TagCoopPlayers = ReferenceEquals(nextTagCoopScore, score)
                    ? nextTagCoopPlayers
                    : Array.Empty<TagCoopReplayPlayer>();
                nextTagCoopScore = null;
                nextTagCoopPlayers = Array.Empty<TagCoopReplayPlayer>();

                setStateForScore(scoreToken, state, score);

                BeginPlayingInternal(currentScoreToken, currentState).ContinueWith(t =>
                {
                    bool success = t.GetResultSafely();

                    if (!success)
                    {
                        Schedule(() =>
                        {
                            if (IsConnected.Value)
                                Logger.Log($"Clearing {nameof(SpectatorClient)} state due to failed {nameof(BeginPlayingInternal)} call.");

                            clearScoreState();

                            currentState.BeatmapID = null;
                            currentState.RulesetID = null;
                            currentState.Mods = [];
                            currentState.State = SpectatedUserState.Idle;
                            currentState.MaximumStatistics = [];
                        });
                    }
                });
            });
        }

        public void HandleFrame(ReplayFrame frame) => Schedule(() =>
        {
            if (!isPlaying)
            {
                if (IsConnected.Value && !loggedFramesOutsideGameplayScope)
                {
                    Logger.Log($"Frames arrived at {nameof(SpectatorClient)} outside of gameplay scope and will be ignored.");
                    loggedFramesOutsideGameplayScope = true;
                }
                return;
            }

            // In Tag Co-op the ordinary recorder only contains the local player's input. The
            // compatibility stream is instead assembled from every labelled player track below.
            if (currentState.TagCoopPlayers.Length > 0)
                return;

            appendCompatibilityFrame(frame);

            if (pendingFrames.Count > max_pending_frames)
                purgePendingFrames();
        });

        /// <summary>
        /// Configures the ordered players represented by additional labelled Tag Co-op tracks.
        /// The configuration is consumed only when the matching score begins, preventing an
        /// abandoned multiplayer screen from leaking Tag Co-op metadata into a later solo play.
        /// </summary>
        public void ConfigureTagCoopReplay(Score score, IEnumerable<TagCoopReplayPlayer> players) => Schedule(() =>
        {
            nextTagCoopScore = score;
            nextTagCoopPlayers = players.DistinctBy(player => player.UserID).ToArray();
        });

        /// <summary>
        /// Adds a sample to the fork-owned Tag Co-op replay extension without changing the
        /// ordinary replay track consumed by default clients.
        /// </summary>
        public void HandleTagCoopFrame(TagCoopReplayFrame frame, ReplayFrame? compatibilityFrame = null) => Schedule(() =>
        {
            if (!isPlaying || currentState.TagCoopPlayers.Length == 0)
                return;

            pendingTagCoopFrames.Add(frame);

            if (compatibilityFrame != null)
                appendCompatibilityFrame(compatibilityFrame);

            if (pendingFrames.Count > max_pending_frames || pendingTagCoopFrames.Count > max_pending_frames)
                purgePendingFrames();
        });

        /// <summary>
        /// Replaces the in-flight Tag Co-op data with the post-game native player tracks and
        /// their compatibility replay immediately before the spectator session is finalised.
        /// </summary>
        public Task HandleCompletedTagCoopReplayAsync(IEnumerable<TagCoopReplayFrame> playerFrames, IEnumerable<ReplayFrame> compatibilityFrames)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void replace()
            {
                if (!isPlaying || currentState.TagCoopPlayers.Length == 0)
                {
                    completion.TrySetResult(false);
                    return;
                }

                pendingFrames.Clear();
                pendingTagCoopFrames.Clear();

                pendingTagCoopFrames.AddRange(playerFrames);
                replaceTagCoopReplayOnNextBundle = true;
                replacementTagCoopBundleSent = completion;

                foreach (ReplayFrame frame in compatibilityFrames)
                    appendCompatibilityFrame(frame);

                purgePendingFrames();
            }

            if (ThreadSafety.IsUpdateThread)
                replace();
            else
                Schedule(replace);

            return completion.Task;
        }

        public void EndPlaying(GameplayState state)
        {
            // This method is most commonly called via Dispose(), which is can be asynchronous (via the AsyncDisposalQueue).
            // We probably need to find a better way to handle this...
            Schedule(() =>
            {
                if (!isPlaying)
                    return;

                // Disposal can take some time, leading to EndPlaying potentially being called after a future play session.
                // Account for this by ensuring the score of the current play matches the one in the provided state.
                if (currentScore != state.Score)
                    return;

                if (pendingFrames.Count > 0 || pendingTagCoopFrames.Count > 0)
                    purgePendingFrames();

                clearScoreState();

                if (state.HasPassed)
                    currentState.State = SpectatedUserState.Passed;
                else if (state.HasFailed)
                    currentState.State = SpectatedUserState.Failed;
                else
                    currentState.State = SpectatedUserState.Quit;

                EndPlayingInternal(currentState).FireAndForget();
            });
        }

        private void setStateForScore(long? scoreToken, GameplayState state, Score score)
        {
            isPlaying = true;
            loggedFramesOutsideGameplayScope = false;

            currentBeatmap = state.Beatmap;
            currentScore = score;
            currentScoreToken = scoreToken;
            currentScoreProcessor = state.ScoreProcessor;
        }

        private void clearScoreState()
        {
            isPlaying = false;

            currentBeatmap = null;
            currentScore = null;
            currentScoreProcessor = null;
            currentScoreToken = null;
            pendingTagCoopFrames.Clear();
        }

        public virtual void WatchUser(int userId)
        {
            Debug.Assert(ThreadSafety.IsUpdateThread);

            if (!watchedUsersRefCounts.TryAdd(userId, 1))
            {
                watchedUsersRefCounts[userId]++;
                return;
            }

            WatchUserInternal(userId).FireAndForget();
        }

        public void StopWatchingUser(int userId)
        {
            // This method is most commonly called via Dispose(), which is asynchronous.
            // Todo: This should not be a thing, but requires framework changes.
            Schedule(() =>
            {
                if (watchedUsersRefCounts.TryGetValue(userId, out int watchers) && watchers > 1)
                {
                    watchedUsersRefCounts[userId]--;
                    return;
                }

                watchedUsersRefCounts.Remove(userId);
                watchedUserStates.Remove(userId);
                StopWatchingUserInternal(userId).FireAndForget();
            });
        }

        /// <summary>
        /// Contains the actual implementation of the "begin play" operation.
        /// </summary>
        /// <returns>Whether the server-side invocation to start play succeeded.</returns>
        protected abstract Task<bool> BeginPlayingInternal(long? scoreToken, SpectatorState state);

        protected abstract Task SendFramesInternal(FrameDataBundle bundle);

        protected abstract Task EndPlayingInternal(SpectatorState state);

        protected abstract Task WatchUserInternal(int userId);

        protected abstract Task StopWatchingUserInternal(int userId);

        protected override void Update()
        {
            base.Update();

            if ((pendingFrames.Count > 0 || pendingTagCoopFrames.Count > 0) && Time.Current - lastPurgeTime > TIME_BETWEEN_SENDS)
                purgePendingFrames();
        }

        private void purgePendingFrames()
        {
            if (pendingFrames.Count == 0 && pendingTagCoopFrames.Count == 0)
                return;

            if (!isPlaying)
            {
                // it is possible for this to happen if the `BeginPlayingInternal()` call takes a long time,
                // the client accumulates a purgeable bundle of frames in the meantime,
                // and then `BeginPlayingInternal()` finally fails and `clearScoreState()` is called to abort the streaming session.
                Logger.Log($"{nameof(SpectatorClient)} dropping pending frames as the user is no longer considered to be playing.");
                pendingFrames.Clear();
                pendingTagCoopFrames.Clear();
                return;
            }

            Debug.Assert(currentScore != null);
            Debug.Assert(currentScoreProcessor != null);

            var frames = pendingFrames.ToArray();
            var tagCoopFrames = pendingTagCoopFrames.ToArray();
            var bundle = new FrameDataBundle(currentScore.ScoreInfo, currentScoreProcessor, frames, tagCoopFrames)
            {
                ReplaceTagCoopReplay = replaceTagCoopReplayOnNextBundle,
            };

            pendingFrames.Clear();
            pendingTagCoopFrames.Clear();
            replaceTagCoopReplayOnNextBundle = false;
            lastPurgeTime = Time.Current;

            pendingFrameBundles.Enqueue(bundle);

            sendNextBundleIfRequired();
        }

        private void appendCompatibilityFrame(ReplayFrame frame)
        {
            if (frame is not IConvertibleReplayFrame convertible)
                return;

            Debug.Assert(currentBeatmap != null);

            var convertedFrame = convertible.ToLegacy(currentBeatmap);

            // This reduces redundancy of frames in the resulting replay. It is also done at
            // ReplayRecorder, but this streaming flow is handled separately.
            if (pendingFrames.LastOrDefault()?.IsEquivalentTo(convertedFrame) == true)
                pendingFrames[^1] = convertedFrame;
            else
                pendingFrames.Add(convertedFrame);
        }

        private void sendNextBundleIfRequired()
        {
            Debug.Assert(ThreadSafety.IsUpdateThread);

            if (lastSend?.IsCompleted == false)
                return;

            if (!pendingFrameBundles.TryPeek(out var bundle))
                return;

            bool completesTagCoopReplacement = bundle.ReplaceTagCoopReplay;

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lastSend = tcs.Task;

            SendFramesInternal(bundle).ContinueWith(t =>
            {
                // Handle exception outside of `Schedule` to ensure it doesn't go unobserved.
                bool wasSuccessful = t.Exception == null;

                return Schedule(() =>
                {
                    // If the last bundle send wasn't successful, try again without dequeuing.
                    if (wasSuccessful)
                    {
                        pendingFrameBundles.Dequeue();

                        if (completesTagCoopReplacement)
                        {
                            replacementTagCoopBundleSent?.TrySetResult(true);
                            replacementTagCoopBundleSent = null;
                        }
                    }

                    tcs.SetResult(wasSuccessful);
                    sendNextBundleIfRequired();
                });
            });
        }

        #region Disconnection handling

        /// <summary>
        /// Invoked just prior to disconnection.
        /// </summary>
        public event Action? Disconnecting;

        public event Action<string, string>? SystemNotificationReceived;

        protected abstract Task DisconnectInternal();

        public abstract Task Reconnect();

        Task IStatefulUserHubClient.DisconnectRequested()
        {
            Schedule(() =>
            {
                Disconnecting?.Invoke();
                DisconnectInternal().FireAndForget();
            });
            return Task.CompletedTask;
        }

        Task IStatefulUserHubClient.ServerShuttingDown()
        {
            this.ReconnectWhenReady(IsConnected, () => watchedUsersRefCounts.Count == 0, Reconnect);
            return Task.CompletedTask;
        }

        Task IStatefulUserHubClient.SystemNotification(string notificationId, string message)
        {
            Schedule(() => SystemNotificationReceived?.Invoke(notificationId, message));
            return Task.CompletedTask;
        }

        #endregion
    }
}
