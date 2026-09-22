// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Online.API.Requests;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Net
{
    /// <summary>
    /// Where a Dodge World is read from and written back to.
    /// </summary>
    /// <remarks>
    /// The screen talks only to this abstraction, so that "am I on the server or in a local preview"
    /// is answered once, by construction, instead of being re-tested at every call site that loads,
    /// publishes, uploads a texture or claims a reward.
    /// </remarks>
    internal abstract class DodgeWorldSession
    {
        /// <summary>
        /// Progress of the initial load. Drives what the screen tells the player.
        /// </summary>
        public IBindable<DodgeWorldAvailability> Availability => availability;

        /// <summary>
        /// Revision of the document currently held. Publishing sends this back for conflict detection.
        /// </summary>
        public IBindable<long> Revision => revision;

        /// <summary>
        /// Whether this session may publish changes.
        /// </summary>
        public IBindable<bool> CanEdit => canEdit;

        public IBindable<int> OnlineUsers => onlineUsers;

        public IBindable<DodgeWorldProgressionResponse?> Progression => progression;

        private readonly Bindable<DodgeWorldAvailability> availability = new Bindable<DodgeWorldAvailability>(DodgeWorldAvailability.Loading);
        private readonly Bindable<long> revision = new Bindable<long>();
        private readonly Bindable<bool> canEdit = new Bindable<bool>();
        private readonly Bindable<int> onlineUsers = new Bindable<int>();
        private readonly Bindable<DodgeWorldProgressionResponse?> progression = new Bindable<DodgeWorldProgressionResponse?>();

        /// <summary>
        /// Fetches the world. <paramref name="onLoaded"/> runs only on success; failures are reported
        /// through <see cref="Availability"/>.
        /// </summary>
        public abstract void Load(Action<DodgeWorldDocument> onLoaded);

        /// <summary>
        /// Writes the world back to wherever it came from.
        /// </summary>
        public abstract void Publish(DodgeWorldDocument document, Action<PublishOutcome> onCompleted);

        /// <summary>
        /// Persists an imported image and reports the path or URL the world should reference.
        /// </summary>
        /// <remarks>
        /// Implementations are responsible for validating the upload; acceptable formats and sizes
        /// differ between a server upload and a local file copy.
        /// </remarks>
        public abstract void StoreTexture(TextureImport upload, Action<string> onStored, Action onFailed);

        /// <summary>
        /// Records the standing the realtime server reported after a kill it rewarded.
        /// </summary>
        /// <remarks>
        /// Rewards are not requested from here. The realtime server sees the kill and has the API grant it,
        /// so this only takes delivery of the result — there is no path by which this client can ask to be
        /// paid.
        /// </remarks>
        public void ApplyProgression(int level, long experience, long experienceForNextLevel, long coins) =>
            SetProgression(new DodgeWorldProgressionResponse
            {
                Level = level,
                Experience = experience,
                ExperienceForNextLevel = experienceForNextLevel,
                Coins = coins,
            });

        /// <summary>
        /// The player's story progress, by flag name. A flag that has never been set is simply absent and
        /// reads as zero, so a story can be rewritten without invalidating anybody's progress.
        /// </summary>
        public IBindable<IReadOnlyDictionary<string, long>> Flags => flags;

        private readonly Bindable<IReadOnlyDictionary<string, long>> flags =
            new Bindable<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());

        /// <summary>
        /// Records that the player has finished with a story point.
        /// </summary>
        /// <remarks>
        /// The flag and value are passed for the sake of sessions with no server behind them, which apply
        /// them here by default: a preview or an offline world should still be walkable. A server session
        /// overrides this to send only the room and entity, letting the server read the rest from the
        /// published world exactly as it does for a warp's price — a client that could name its own flag
        /// could write its own story.
        /// </remarks>
        public virtual void RaiseStoryFlag(string roomId, string entityId, string flag, int value,
                                           Action<StoryOutcome>? onCompleted = null) =>
            onCompleted?.Invoke(new StoryOutcome(RaiseFlagLocally(flag, value), 0, 0));

        protected void SetFlags(IReadOnlyDictionary<string, long>? value) =>
            flags.Value = value ?? new Dictionary<string, long>();

        /// <summary>
        /// Applies a flag locally, for a session that has nowhere to send it.
        /// </summary>
        /// <returns>Whether this raised it, which is what a story point pays on.</returns>
        protected bool RaiseFlagLocally(string flag, long value)
        {
            var updated = new Dictionary<string, long>(flags.Value, StringComparer.Ordinal);
            long current = updated.TryGetValue(flag, out long stored) ? stored : 0;

            // Raised, never set: the same rule the server follows, so a local preview behaves like the
            // real thing rather than being its own dialect.
            updated[flag] = Math.Max(current, value);
            flags.Value = updated;

            return updated[flag] > current;
        }

        /// <summary>
        /// The warps this player has paid to open. Per-player state, so it is not part of the world.
        /// </summary>
        public IBindable<IReadOnlyCollection<WarpKey>> UnlockedWarps => unlockedWarps;

        private readonly Bindable<IReadOnlyCollection<WarpKey>> unlockedWarps =
            new Bindable<IReadOnlyCollection<WarpKey>>(Array.Empty<WarpKey>());

        public bool IsWarpUnlocked(string roomId, string entityId) =>
            unlockedWarps.Value.Contains(new WarpKey(roomId, entityId));

        /// <summary>
        /// Pays a warp's opening price. The price itself is decided by whoever owns the world state,
        /// never by the caller.
        /// </summary>
        public virtual void UnlockWarp(string roomId, string entityId, Action<WarpOutcome> onCompleted) =>
            onCompleted(WarpOutcome.Failed);

        /// <summary>
        /// Pays to travel to a warp this player has already opened.
        /// </summary>
        public virtual void TravelToWarp(string roomId, string entityId, Action<WarpOutcome> onCompleted) =>
            onCompleted(WarpOutcome.Failed);

        protected void SetUnlockedWarps(IReadOnlyCollection<WarpKey> value) => unlockedWarps.Value = value;

        protected void SetAvailability(DodgeWorldAvailability value) => availability.Value = value;

        protected void SetRevision(long value) => revision.Value = value;

        protected void SetCanEdit(bool value) => canEdit.Value = value;

        protected void SetOnlineUsers(int value) => onlineUsers.Value = value;

        protected void SetProgression(DodgeWorldProgressionResponse? value) => progression.Value = value;
    }
}
