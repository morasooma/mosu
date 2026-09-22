// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Matchmaking.Requests;
using osu.Game.Online.Matchmaking.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.RankedPlay;
using osu.Game.Online.Rooms;

namespace osu.Game.Online.Legacy
{
    /// <summary>
    /// Adapts the stable Bancho multiplayer protocol to lazer's multiplayer UI model.
    /// </summary>
    public partial class StableMultiplayerClient : MultiplayerClient
    {
        private static readonly TimeSpan join_timeout = TimeSpan.FromSeconds(15);
        private readonly StableBanchoSession session;
        private TaskCompletionSource<StableBanchoMatch>? pendingJoin;
        private bool serverAllPlayersLoaded;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        public override IBindable<bool> IsConnected => session.IsConnected;

        public StableMultiplayerClient(StableBanchoSession session)
        {
            this.session = session;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            session.MatchJoined += matchJoined;
            session.MatchJoinFailed += matchJoinFailed;
            session.MatchUpdated += matchUpdated;
            session.MatchStarted += matchStarted;
            session.AllPlayersLoaded += allPlayersLoaded;
            session.MatchCompleted += matchCompleted;
            session.MatchAborted += matchAborted;
            session.MatchSkipPassed += matchSkipPassed;
        }

        protected override async Task<MultiplayerRoom> CreateRoomInternal(MultiplayerRoom room)
        {
            StableBanchoMatch match = fromMultiplayerRoom(room);
            Task<StableBanchoMatch> joined = waitForJoin();
            await session.CreateMatchAsync(match).ConfigureAwait(false);
            return StableMultiplayerRoomConverter.ToMultiplayerRoom(await joined.ConfigureAwait(false), snapshotUsers(), session.GetAvatarUrl);
        }

        protected override async Task<MultiplayerRoom> JoinRoomInternal(long roomId, string? password = null)
        {
            Task<StableBanchoMatch> joined = waitForJoin();
            await session.JoinMatchAsync(checked((int)roomId), password ?? string.Empty).ConfigureAwait(false);
            return StableMultiplayerRoomConverter.ToMultiplayerRoom(await joined.ConfigureAwait(false), snapshotUsers(), session.GetAvatarUrl);
        }

        protected override Task LeaveRoomInternal() => session.LeaveMatchAsync();

        public override Task InvitePlayer(int userId) => session.InvitePlayerAsync(userId);

        public override Task TransferHost(int userId)
        {
            int slot = findSlot(userId);
            return slot >= 0 ? session.TransferHostAsync(slot) : Task.CompletedTask;
        }

        public override Task KickUser(int userId)
        {
            int slot = findSlot(userId);
            return slot >= 0 ? session.LockSlotAsync(slot) : Task.CompletedTask;
        }

        public override async Task ChangeSettings(MultiplayerRoomSettings settings)
        {
            StableBanchoMatch? match = session.CurrentMatch.Value?.Clone();
            if (match == null)
                return;

            bool passwordChanged = match.Password != settings.Password;
            match.Name = settings.Name;
            match.Password = settings.Password;
            match.HasPassword = !string.IsNullOrEmpty(settings.Password);
            match.TeamType = settings.MatchType == MatchType.TeamVersus ? (byte)2 : (byte)0;
            applyParticipantLimit(match, settings.MaxParticipants);
            await session.ChangeMatchSettingsAsync(match).ConfigureAwait(false);
            if (passwordChanged)
                await session.ChangeMatchPasswordAsync(match).ConfigureAwait(false);
        }

        public override async Task ChangeState(MultiplayerUserState newState)
        {
            Task request = newState switch
            {
                MultiplayerUserState.Ready => session.SetReadyAsync(true),
                MultiplayerUserState.Loaded => session.CompleteLoadingAsync(),
                MultiplayerUserState.FinishedPlay or MultiplayerUserState.Results => session.CompleteMatchAsync(),
                MultiplayerUserState.Idle => session.SetReadyAsync(false),
                _ => Task.CompletedTask,
            };

            await request.ConfigureAwait(false);

            int localUserId = API.LocalUser.Value.Id;
            await ((IMultiplayerClient)this).UserStateChanged(localUserId, newState).ConfigureAwait(false);

            if (newState == MultiplayerUserState.ReadyForGameplay && serverAllPlayersLoaded)
                beginGameplay();
        }

        public override Task ChangeBeatmapAvailability(BeatmapAvailability newBeatmapAvailability)
            => session.SetBeatmapAvailableAsync(newBeatmapAvailability.State == DownloadState.LocallyAvailable);

        public override Task ChangeUserStyle(int? beatmapId, int? rulesetId) => Task.CompletedTask;

        public override Task ChangeUserMods(IEnumerable<APIMod> newMods)
        {
            StableBanchoMatch? match = session.CurrentMatch.Value;
            if (match == null)
                return Task.CompletedTask;

            var ruleset = Rulesets.GetRuleset(match.RulesetId)?.CreateInstance();
            if (ruleset == null)
                return Task.CompletedTask;

            int legacyMods = (int)ruleset.ConvertToLegacyMods(newMods.Where(mod => mod.Acronym != "CL").Select(mod => mod.ToMod(ruleset)).ToArray());
            return session.ChangeModsAsync(legacyMods);
        }

        public override Task SendMatchRequest(MatchUserRequest request)
            => request switch
            {
                ChangeTeamRequest => session.ChangeTeamAsync(),
                ChangeSlotRequest changeSlot => session.ChangeSlotAsync(changeSlot.SlotID),
                _ => Task.CompletedTask,
            };

        public override Task StartMatch() => session.StartMatchAsync();

        public override Task AbortGameplay() => session.FailMatchAsync();

        public override Task AbortMatch() => session.CompleteMatchAsync();

        public override Task AddPlaylistItem(MultiplayerPlaylistItem item) => EditPlaylistItem(item);

        public override async Task EditPlaylistItem(MultiplayerPlaylistItem item)
        {
            StableBanchoMatch? match = session.CurrentMatch.Value?.Clone();
            if (match == null)
                return;

            match.BeatmapId = item.BeatmapID;
            match.BeatmapChecksum = item.BeatmapChecksum;
            match.RulesetId = checked((byte)item.RulesetID);
            match.BeatmapName = getStableBeatmapName(item.BeatmapID);

            var ruleset = Rulesets.GetRuleset(item.RulesetID)?.CreateInstance();
            if (ruleset != null)
                match.Mods = (int)ruleset.ConvertToLegacyMods(item.RequiredMods.Where(mod => mod.Acronym != "CL").Select(mod => mod.ToMod(ruleset)).ToArray());

            await session.ChangeMatchSettingsAsync(match).ConfigureAwait(false);
        }

        public override Task RemovePlaylistItem(long playlistItemId) => Task.CompletedTask;

        public override async Task VoteToSkipIntro()
        {
            await session.RequestSkipAsync().ConfigureAwait(false);
            await ((IMultiplayerClient)this).UserVotedToSkipIntro(API.LocalUser.Value.Id, true).ConfigureAwait(false);
        }

        // Stable multiplayer has no packet carrying break identity, so a break vote cannot be
        // represented safely. Lazer multiplayer uses the typed implementation instead.
        public override Task VoteToSkipBreak(MultiplayerBreakSkipRequest request) => Task.CompletedTask;

        public override Task DiscardCards(RankedPlayCardItem[] cards) => Task.CompletedTask;
        public override Task PlayCard(RankedPlayCardItem card) => Task.CompletedTask;
        public override Task Surrender() => Task.CompletedTask;
        public override Task<MatchmakingPool[]> GetMatchmakingPoolsOfType(MatchmakingPoolType type) => Task.FromResult(Array.Empty<MatchmakingPool>());
        public override Task<MatchmakingJoinLobbyResponse> MatchmakingJoinLobbyWithParams(MatchmakingJoinLobbyRequest request) => Task.FromResult(new MatchmakingJoinLobbyResponse());
        public override Task MatchmakingLeaveLobby() => Task.CompletedTask;
        public override Task MatchmakingJoinQueue(int poolId, bool randomMods = false) => Task.CompletedTask;
        public override Task MatchmakingLeaveQueue() => Task.CompletedTask;
        public override Task MatchmakingAcceptInvitation() => Task.CompletedTask;
        public override Task<MatchmakingIssueDuelResponse> MatchmakingIssueDuel(MatchmakingIssueDuelRequest request) => Task.FromResult(new MatchmakingIssueDuelResponse());
        public override Task<MatchmakingAcceptDuelResponse> MatchmakingAcceptDuel(MatchmakingAcceptDuelRequest request) => Task.FromResult(new MatchmakingAcceptDuelResponse());
        public override Task MatchmakingDeclineInvitation() => Task.CompletedTask;
        public override Task MatchmakingToggleSelection(long playlistItemId) => Task.CompletedTask;
        public override Task MatchmakingSkipToNextStage() => Task.CompletedTask;
        protected override Task DisconnectInternal() => session.LeaveMatchAsync();
        public override Task Reconnect() => session.LoginNowAsync();

        private Task<StableBanchoMatch> waitForJoin()
        {
            pendingJoin?.TrySetCanceled();
            pendingJoin = new TaskCompletionSource<StableBanchoMatch>(TaskCreationOptions.RunContinuationsAsynchronously);
            return pendingJoin.Task.WaitAsync(join_timeout);
        }

        private void matchJoined(StableBanchoMatch match)
        {
            pendingJoin?.TrySetResult(match);
            pendingJoin = null;
        }

        private void matchJoinFailed()
        {
            pendingJoin?.TrySetException(new InvalidOperationException("Stable Bancho rejected the multiplayer room join."));
            pendingJoin = null;
        }

        private void matchUpdated(StableBanchoMatch match)
        {
            if (Room == null || Room.RoomID != match.Id)
                return;

            MultiplayerRoom converted = StableMultiplayerRoomConverter.ToMultiplayerRoom(match, snapshotUsers(), session.GetAvatarUrl);
            var currentUsers = Room.Users.ToDictionary(user => user.UserID);
            var updatedUsers = converted.Users.ToDictionary(user => user.UserID);

            foreach (var removed in currentUsers.Keys.Except(updatedUsers.Keys).ToArray())
                ((IMultiplayerClient)this).UserLeft(currentUsers[removed]).FireAndForget();
            foreach (var added in updatedUsers.Keys.Except(currentUsers.Keys).ToArray())
                ((IMultiplayerClient)this).UserJoined(updatedUsers[added]).FireAndForget();
            foreach (var common in currentUsers.Keys.Intersect(updatedUsers.Keys))
            {
                ((IMultiplayerClient)this).UserStateChanged(common, updatedUsers[common].State).FireAndForget();
                if (!EqualityComparer<MatchUserState?>.Default.Equals(currentUsers[common].MatchState, updatedUsers[common].MatchState) && updatedUsers[common].MatchState != null)
                    ((IMultiplayerClient)this).MatchUserStateChanged(common, updatedUsers[common].MatchState!).FireAndForget();
            }

            MultiplayerPlaylistItem updatedItem = converted.Playlist.Single();
            if (!Room.Playlist.Single().Equals(updatedItem))
                ((IMultiplayerClient)this).PlaylistItemChanged(updatedItem).FireAndForget();
            ((IMultiplayerClient)this).SettingsChanged(converted.Settings).FireAndForget();
            if (converted.Host != null && Room.Host?.UserID != converted.Host.UserID)
                ((IMultiplayerClient)this).HostChanged(converted.Host.UserID).FireAndForget();
            ((IMultiplayerClient)this).RoomStateChanged(converted.State).FireAndForget();
        }

        private void matchStarted()
        {
            serverAllPlayersLoaded = false;
            ((IMultiplayerClient)this).RoomStateChanged(MultiplayerRoomState.WaitingForLoad).FireAndForget();
            if (Room != null)
            {
                foreach (MultiplayerRoomUser user in Room.Users)
                {
                    ((IMultiplayerClient)this).UserVotedToSkipIntro(user.UserID, false).FireAndForget();
                    if (user.State == MultiplayerUserState.Ready)
                        ((IMultiplayerClient)this).UserStateChanged(user.UserID, MultiplayerUserState.WaitingForLoad).FireAndForget();
                }
            }
            ((IMultiplayerClient)this).LoadRequested().FireAndForget();
        }

        private void allPlayersLoaded()
        {
            serverAllPlayersLoaded = true;
            if (LocalUser?.State == MultiplayerUserState.ReadyForGameplay)
                beginGameplay();
        }

        private void beginGameplay()
        {
            serverAllPlayersLoaded = false;
            if (Room != null)
            {
                foreach (MultiplayerRoomUser user in Room.Users.Where(user => user.State is MultiplayerUserState.WaitingForLoad or MultiplayerUserState.Loaded or MultiplayerUserState.ReadyForGameplay))
                    ((IMultiplayerClient)this).UserStateChanged(user.UserID, MultiplayerUserState.Playing).FireAndForget();
            }
            ((IMultiplayerClient)this).RoomStateChanged(MultiplayerRoomState.Playing).FireAndForget();
            ((IMultiplayerClient)this).GameplayStarted().FireAndForget();
        }

        private void matchCompleted()
        {
            ((IMultiplayerClient)this).ResultsReady().FireAndForget();
            ((IMultiplayerClient)this).RoomStateChanged(MultiplayerRoomState.Open).FireAndForget();
        }

        private void matchAborted() => ((IMultiplayerClient)this).GameplayAborted(GameplayAbortReason.HostAbortedTheMatch).FireAndForget();

        private void matchSkipPassed() => ((IMultiplayerClient)this).VoteToSkipIntroPassed().FireAndForget();

        private int findSlot(int userId)
        {
            StableBanchoMatch? match = session.CurrentMatch.Value;
            return match == null ? -1 : Array.FindIndex(match.SlotUserIds, id => id == userId);
        }

        private Dictionary<int, StableBanchoUserPresence> snapshotUsers() => session.Users.ToDictionary(pair => pair.Key, pair => pair.Value);

        private StableBanchoMatch fromMultiplayerRoom(MultiplayerRoom room)
        {
            MultiplayerPlaylistItem item = room.Playlist.First();
            int localUserId = API.LocalUser.Value.Id;
            var match = new StableBanchoMatch
            {
                Name = room.Settings.Name.Length > 50 ? room.Settings.Name[..50] : room.Settings.Name,
                Password = room.Settings.Password,
                HasPassword = !string.IsNullOrEmpty(room.Settings.Password),
                BeatmapId = item.BeatmapID,
                BeatmapChecksum = item.BeatmapChecksum,
                BeatmapName = getStableBeatmapName(item.BeatmapID),
                HostUserId = localUserId,
                RulesetId = checked((byte)item.RulesetID),
                TeamType = room.Settings.MatchType == MatchType.TeamVersus ? (byte)2 : (byte)0,
                WinCondition = 0,
                FreeMods = item.Freestyle,
            };

            for (int i = 0; i < match.SlotStatuses.Length; i++)
                match.SlotStatuses[i] = StableMatchSlotStatus.Open;
            applyParticipantLimit(match, room.Settings.MaxParticipants);
            match.SlotStatuses[0] = StableMatchSlotStatus.NotReady;
            match.SlotUserIds[0] = localUserId;

            var ruleset = Rulesets.GetRuleset(item.RulesetID)?.CreateInstance();
            if (ruleset != null)
                match.Mods = (int)ruleset.ConvertToLegacyMods(item.RequiredMods.Where(mod => mod.Acronym != "CL").Select(mod => mod.ToMod(ruleset)).ToArray());
            return match;
        }

        private static void applyParticipantLimit(StableBanchoMatch match, byte? maximumParticipants)
        {
            int maximum = Math.Clamp((int)(maximumParticipants ?? 16), 1, 16);
            for (int slot = 0; slot < match.SlotStatuses.Length; slot++)
            {
                if (match.SlotUserIds[slot].HasValue)
                    continue;
                match.SlotStatuses[slot] = slot < maximum ? StableMatchSlotStatus.Open : StableMatchSlotStatus.Locked;
            }
        }

        private string getStableBeatmapName(int beatmapId)
        {
            BeatmapInfo? beatmap = beatmaps.QueryBeatmap(candidate => candidate.OnlineID == beatmapId);
            if (beatmap == null)
                return beatmapId > 0 ? $"Beatmap {beatmapId}" : string.Empty;

            return $"{beatmap.Metadata.Artist} - {beatmap.Metadata.Title} [{beatmap.DifficultyName}]";
        }

        protected override void Dispose(bool isDisposing)
        {
            session.MatchJoined -= matchJoined;
            session.MatchJoinFailed -= matchJoinFailed;
            session.MatchUpdated -= matchUpdated;
            session.MatchStarted -= matchStarted;
            session.AllPlayersLoaded -= allPlayersLoaded;
            session.MatchCompleted -= matchCompleted;
            session.MatchAborted -= matchAborted;
            session.MatchSkipPassed -= matchSkipPassed;
            base.Dispose(isDisposing);
        }
    }
}
