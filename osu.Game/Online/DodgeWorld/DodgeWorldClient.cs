// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Online.Multiplayer;
using osuTK;

namespace osu.Game.Online.DodgeWorld
{
    /// <summary>
    /// The local player's end of a Dodge World room: who else is in it, and what the server's copy of it
    /// is doing.
    /// </summary>
    /// <remarks>
    /// Movement is sent from here and taken on trust; everything that costs health or pays out is the
    /// server's answer arriving here. A room the published world does not contain is not run by the server
    /// at all (<see cref="ServerSimulated"/>), and the client falls back to simulating its mobs itself.
    /// </remarks>
    public abstract partial class DodgeWorldClient : Component, IDodgeWorldClient
    {
        /// <summary>
        /// The shortest interval between position updates, in milliseconds.
        /// </summary>
        /// <remarks>
        /// Twenty a second: enough for walking to look continuous once the receiver interpolates, and
        /// little enough that a busy room does not turn into a flood. Nothing is sent at all while the
        /// player stands still, because the state has not changed.
        /// </remarks>
        public const double TIME_BETWEEN_SENDS = 50;

        public abstract IBindable<bool> IsConnected { get; }

        /// <summary>
        /// Everyone else in the room the local player is in, by user id.
        /// </summary>
        public IBindableDictionary<int, DodgeWorldPlayerState> Players => players;

        private readonly BindableDictionary<int, DodgeWorldPlayerState> players =
            new BindableDictionary<int, DodgeWorldPlayerState>();

        /// <summary>
        /// The room the local player has told the server about, or null when not in the world.
        /// </summary>
        public string? CurrentRoomId { get; private set; }

        /// <summary>
        /// Whether the server is running the current room's mobs. False until a room has been joined and
        /// the server has said so, and false for good in a room the published world does not contain.
        /// </summary>
        public IBindable<bool> ServerSimulated => serverSimulated;

        private readonly BindableBool serverSimulated = new BindableBool();

        /// <summary>Everything hostile in the room, as of the last snapshot.</summary>
        public DodgeWorldRoomSnapshot RoomState { get; private set; } = new DodgeWorldRoomSnapshot();

        /// <summary>Raised when a new snapshot arrives, after <see cref="RoomState"/> is updated.</summary>
        public event Action? RoomStateReceived;

        /// <summary>A mob died: its id, who killed it, and the zone it belongs to.</summary>
        public event Action<int, int, string>? MobDefeated;

        /// <summary>A kill of the local player's was rewarded, or refused. Decided entirely on the server.</summary>
        public event Action<DodgeWorldReward>? RewardReceived;

        /// <summary>Somebody else swung: their user id and the direction.</summary>
        public event Action<int, Vector2>? UserAttacked;

        /// <summary>The server decided the local player's health.</summary>
        public event Action<int>? HealthReceived;

        /// <summary>The server killed the local player.</summary>
        public event Action? DeathReceived;

        public event Action<string, string>? SystemNotificationReceived;

        private readonly DodgeWorldPlayerState local = new DodgeWorldPlayerState();

        private DodgeWorldPlayerState? lastSent;
        private double lastSendTime;
        private Task? lastSend;

        /// <summary>
        /// Distinguishes the answer to the join in flight from the answer to one already superseded,
        /// which is what happens when a player walks through two rooms quickly.
        /// </summary>
        private int joinCount;

        [BackgroundDependencyLoader]
        private void load()
        {
            IsConnected.BindValueChanged(connected => Schedule(() =>
            {
                players.Clear();
                clearRoomState();
                lastSent = null;

                // A reconnection leaves the server knowing nothing about this player, so the room has
                // to be entered again rather than assumed.
                if (connected.NewValue && CurrentRoomId != null)
                    join(CurrentRoomId);
            }), true);
        }

        /// <summary>
        /// Enters a room, or moves to a different one.
        /// </summary>
        public void JoinRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
                return;

            CurrentRoomId = roomId;
            players.Clear();
            clearRoomState();
            join(roomId);
        }

        public void LeaveRoom()
        {
            if (CurrentRoomId == null)
                return;

            CurrentRoomId = null;
            players.Clear();
            clearRoomState();
            joinCount++;
            lastSent = null;

            if (IsConnected.Value)
                LeaveRoomInternal().FireAndForget();
        }

        /// <summary>
        /// Records where the local player is now. Sending is throttled and skipped entirely when
        /// nothing has changed, so this can be called every frame.
        /// </summary>
        public void SetLocalState(Vector2 position, Vector2 facing, bool moving, int health)
        {
            local.Position = position;
            local.Facing = facing;
            local.Moving = moving;
            local.Health = health;
        }

        /// <summary>
        /// Asks the server to resolve a swing. The caller has already played the animation: waiting for
        /// an answer would put the sword a round trip behind the click.
        /// </summary>
        public void Attack(Vector2 direction)
        {
            if (CurrentRoomId == null || !IsConnected.Value || !serverSimulated.Value)
                return;

            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || direction.LengthSquared <= 0)
                return;

            AttackInternal(direction).FireAndForget();
        }

        private void clearRoomState()
        {
            serverSimulated.Value = false;
            RoomState = new DodgeWorldRoomSnapshot();
            RoomStateReceived?.Invoke();
        }

        private void join(string roomId)
        {
            joinCount++;
            int token = joinCount;

            if (!IsConnected.Value)
                return;

            // The join carries the position, so it counts as the first send: without this the very next
            // frame would repeat the same numbers as an update.
            DodgeWorldPlayerState joining = local.Clone();
            lastSent = joining;
            lastSendTime = Time.Current;

            JoinRoomInternal(roomId, joining).ContinueWith(task => Schedule(() =>
            {
                // A newer join has been sent, so this answer describes a room already left.
                if (token != joinCount)
                    return;

                if (!task.IsCompletedSuccessfully)
                {
                    Logger.Log($"Joining Dodge World room '{roomId}' failed: {task.Exception?.Message}", LoggingTarget.Network);
                    return;
                }

                DodgeWorldRoomJoin joined = task.GetResultSafely();
                serverSimulated.Value = joined.ServerSimulated;

                foreach (DodgeWorldUser user in joined.Players)
                {
                    if (user.State.IsValid)
                        players[user.UserId] = user.State;
                }
            }));
        }

        protected override void Update()
        {
            base.Update();

            if (CurrentRoomId == null || !IsConnected.Value)
                return;

            if (Time.Current - lastSendTime < TIME_BETWEEN_SENDS)
                return;

            // One send in flight at a time: a slow connection should drop intermediate positions
            // rather than queue them up and arrive late.
            if (lastSend?.IsCompleted == false)
                return;

            if (local.Equals(lastSent))
                return;

            lastSendTime = Time.Current;
            lastSent = local.Clone();
            lastSend = UpdateStateInternal(lastSent).ContinueWith(task =>
            {
                if (!task.IsCompletedSuccessfully)
                    Logger.Log($"Sending Dodge World state failed: {task.Exception?.Message}", LoggingTarget.Network);
            });
        }

        protected abstract Task<DodgeWorldRoomJoin> JoinRoomInternal(string roomId, DodgeWorldPlayerState state);

        protected abstract Task LeaveRoomInternal();

        protected abstract Task UpdateStateInternal(DodgeWorldPlayerState state);

        protected abstract Task AttackInternal(Vector2 direction);

        protected abstract Task DisconnectInternal();

        public abstract Task Reconnect();

        #region IDodgeWorldClient

        Task IDodgeWorldClient.UserJoined(int userId, DodgeWorldPlayerState state)
        {
            Schedule(() =>
            {
                if (state.IsValid)
                    players[userId] = state;
            });

            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.UserLeft(int userId)
        {
            Schedule(() => players.Remove(userId));
            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.RoomStateChanged(DodgeWorldRoomSnapshot snapshot)
        {
            Schedule(() =>
            {
                if (CurrentRoomId == null)
                    return;

                RoomState = snapshot;
                RoomStateReceived?.Invoke();
            });

            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.MobDefeated(int mobId, int byUserId, string zoneId)
        {
            Schedule(() => MobDefeated?.Invoke(mobId, byUserId, zoneId));
            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.RewardGranted(DodgeWorldReward reward)
        {
            // Not gated on being in a room: the grant has already happened, and a player who walked out
            // the moment their last kill landed is still owed the news of it.
            Schedule(() => RewardReceived?.Invoke(reward));
            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.UserAttacked(int userId, float directionX, float directionY)
        {
            Schedule(() =>
            {
                if (float.IsFinite(directionX) && float.IsFinite(directionY))
                    UserAttacked?.Invoke(userId, new Vector2(directionX, directionY));
            });

            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.HealthChanged(int health)
        {
            Schedule(() => HealthReceived?.Invoke(health));
            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.Died()
        {
            Schedule(() => DeathReceived?.Invoke());
            return Task.CompletedTask;
        }

        Task IDodgeWorldClient.UserStateChanged(int userId, DodgeWorldPlayerState state)
        {
            Schedule(() =>
            {
                // Only for players already known to be here: an update that arrives before its join,
                // or after its leave, would otherwise conjure a player out of nothing.
                if (state.IsValid && players.ContainsKey(userId))
                    players[userId] = state;
            });

            return Task.CompletedTask;
        }

        Task IStatefulUserHubClient.DisconnectRequested()
        {
            Schedule(() => DisconnectInternal().FireAndForget());
            return Task.CompletedTask;
        }

        Task IStatefulUserHubClient.ServerShuttingDown()
        {
            // Waits until the player is out of the world, so that a shutdown does not interrupt them.
            this.ReconnectWhenReady(IsConnected, () => CurrentRoomId == null, Reconnect);
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
