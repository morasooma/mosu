// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Legacy;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay.Lounge.Components;

namespace osu.Game.Screens.OnlinePlay.Lounge
{
    /// <summary>
    /// Polls for rooms for the main lounge listing.
    /// </summary>
    public partial class LoungeListingPoller : PollingComponent
    {
        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        public required Action<Room[]> RoomsReceived { get; init; }
        public readonly IBindable<LoungeFilterCriteria?> Filter = new Bindable<LoungeFilterCriteria?>();

        private GetRoomsRequest? lastPollRequest;

        protected override async Task Poll()
        {
            if (stableBanchoSession != null)
            {
                if (Filter.Value == null)
                    return;

                if (!stableBanchoSession.IsInLobby.Value)
                    await stableBanchoSession.JoinLobbyAsync().ConfigureAwait(false);

                LoungeFilterCriteria criteria = Filter.Value;
                Scheduler.Add(() => publishStableRooms(criteria));
                return;
            }

            if (!api.IsLoggedIn)
            {
                await base.Poll().ConfigureAwait(false);
                return;
            }

            if (Filter.Value == null)
            {
                await base.Poll().ConfigureAwait(false);
                return;
            }

            lastPollRequest?.Cancel();

            var tcs = new TaskCompletionSource<bool>();
            var req = new GetRoomsRequest(Filter.Value);

            req.Success += result =>
            {
                result.RemoveAll(r => r.Category == RoomCategory.DailyChallenge);

                if (!Filter.Value.Full)
                    result.RemoveAll(r => r.ParticipantCount == r.MaxParticipants);

                RoomsReceived(result.ToArray());
                tcs.SetResult(true);
            };

            req.Failure += _ => tcs.SetResult(false);

            api.Queue(req);

            lastPollRequest = req;

            await tcs.Task.ConfigureAwait(false);
        }

        private void publishStableRooms(LoungeFilterCriteria criteria)
        {
            if (stableBanchoSession == null)
                return;

            var users = stableBanchoSession.Users.ToDictionary(pair => pair.Key, pair => pair.Value);
            IEnumerable<Room> rooms = stableBanchoSession.Matches
                                                         .ToArray()
                                                         .Select(match => StableMultiplayerRoomConverter.ToApiRoom(match, users, stableBanchoSession.GetAvatarUrl));

            if (!string.IsNullOrWhiteSpace(criteria.SearchString))
                rooms = rooms.Where(room => room.Name.Contains(criteria.SearchString, StringComparison.OrdinalIgnoreCase));
            if (criteria.Status == RoomStatusFilter.Idle)
                rooms = rooms.Where(room => room.Status == RoomStatus.Idle);
            else if (criteria.Status == RoomStatusFilter.Playing)
                rooms = rooms.Where(room => room.Status == RoomStatus.Playing);
            if (criteria.Permissions == RoomPermissionsFilter.Public)
                rooms = rooms.Where(room => !room.HasPassword);
            else if (criteria.Permissions == RoomPermissionsFilter.Private)
                rooms = rooms.Where(room => room.HasPassword);
            if (criteria.Mode == RoomModeFilter.Owned)
                rooms = rooms.Where(room => room.Host?.Id == api.LocalUser.Value.Id);
            else if (criteria.Mode != RoomModeFilter.Open)
                rooms = [];
            if (criteria.Ruleset != null)
                rooms = rooms.Where(room => room.Playlist.FirstOrDefault()?.RulesetID == criteria.Ruleset.OnlineID);
            if (!criteria.Full)
                rooms = rooms.Where(room => room.ParticipantCount < room.MaxParticipants);

            RoomsReceived(rooms.ToArray());
        }
    }
}
