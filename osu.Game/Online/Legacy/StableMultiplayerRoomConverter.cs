// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;

namespace osu.Game.Online.Legacy
{
    public static class StableMultiplayerRoomConverter
    {
        public static Room ToApiRoom(StableBanchoMatch match, IReadOnlyDictionary<int, StableBanchoUserPresence> users, Func<int, string>? getAvatarUrl = null)
        {
            PlaylistItem playlistItem = createPlaylistItem(match);
            APIUser? host = match.HostUserId > 0 ? CreateUser(match.HostUserId, users, getAvatarUrl) : null;

            return new Room
            {
                RoomID = match.Id,
                // Stable match chat is a named Bancho channel, not a lazer numeric chat channel.
                ChannelId = 0,
                Name = match.Name,
                Password = match.HasPassword ? " " : null,
                Host = host,
                Category = RoomCategory.Realtime,
                Type = isTeamMatch(match.TeamType) ? MatchType.TeamVersus : MatchType.HeadToHead,
                QueueMode = QueueMode.HostOnly,
                Status = match.InProgress ? RoomStatus.Playing : RoomStatus.Idle,
                Availability = RoomAvailability.Public,
                ParticipantCount = match.SlotUserIds.Count(id => id.HasValue),
                MaxParticipants = (byte)match.SlotStatuses.Count(status => status != StableMatchSlotStatus.Locked),
                RecentParticipants = match.SlotUserIds.Where(id => id.HasValue).Select(id => CreateUser(id!.Value, users, getAvatarUrl)).ToArray(),
                Playlist = new List<PlaylistItem> { playlistItem },
            };
        }

        public static MultiplayerRoom ToMultiplayerRoom(StableBanchoMatch match, IReadOnlyDictionary<int, StableBanchoUserPresence> users, Func<int, string>? getAvatarUrl = null)
        {
            var playlistItem = new MultiplayerPlaylistItem(createPlaylistItem(match));
            byte maximumParticipants = (byte)match.SlotStatuses.Count(status => status != StableMatchSlotStatus.Locked);
            StandardMatchRoomState matchState = isTeamMatch(match.TeamType)
                ? TeamVersusRoomState.CreateDefault(maximumParticipants)
                : StandardMatchRoomState.Create(maximumParticipants);
            matchState.Slots = match.SlotUserIds.Take(maximumParticipants).ToArray();
            var room = new MultiplayerRoom(match.Id)
            {
                ChannelID = 0,
                State = match.InProgress ? MultiplayerRoomState.Playing : MultiplayerRoomState.Open,
                Settings = new MultiplayerRoomSettings
                {
                    Name = match.Name,
                    Password = match.Password,
                    MatchType = isTeamMatch(match.TeamType) ? MatchType.TeamVersus : MatchType.HeadToHead,
                    QueueMode = QueueMode.HostOnly,
                    PlaylistItemId = playlistItem.ID,
                    MaxParticipants = maximumParticipants,
                },
                Playlist = new List<MultiplayerPlaylistItem> { playlistItem },
                MatchState = matchState,
            };

            room.Users = match.SlotUserIds.Where(id => id.HasValue).Select((id, index) => createRoomUser(id!.Value, match, index, users, getAvatarUrl)).ToList();
            room.Host = room.Users.SingleOrDefault(user => user.UserID == match.HostUserId);
            return room;
        }

        public static APIUser CreateUser(int userId, IReadOnlyDictionary<int, StableBanchoUserPresence> users, Func<int, string>? getAvatarUrl = null)
        {
            users.TryGetValue(userId, out StableBanchoUserPresence? presence);
            return new APIUser
            {
                Id = userId,
                Username = presence?.Username ?? $"User {userId}",
                AvatarUrl = getAvatarUrl?.Invoke(userId) ?? string.Empty,
            };
        }

        private static MultiplayerRoomUser createRoomUser(int userId, StableBanchoMatch match, int occupiedIndex, IReadOnlyDictionary<int, StableBanchoUserPresence> users,
                                                           Func<int, string>? getAvatarUrl)
        {
            int slot = findNthOccupiedSlot(match, occupiedIndex);
            return new MultiplayerRoomUser(userId)
            {
                User = CreateUser(userId, users, getAvatarUrl),
                State = toUserState(match.SlotStatuses[slot]),
                BeatmapAvailability = match.SlotStatuses[slot] == StableMatchSlotStatus.NoBeatmap ? BeatmapAvailability.NotDownloaded() : BeatmapAvailability.LocallyAvailable(),
                RulesetId = match.RulesetId,
                BeatmapId = match.BeatmapId > 0 ? match.BeatmapId : null,
                MatchState = isTeamMatch(match.TeamType) ? new TeamVersusUserState { TeamID = match.SlotTeams[slot] == 1 ? 1 : 0 } : null,
            };
        }

        private static int findNthOccupiedSlot(StableBanchoMatch match, int occupiedIndex)
        {
            for (int slot = 0; slot < match.SlotUserIds.Length; slot++)
            {
                if (!match.SlotUserIds[slot].HasValue)
                    continue;
                if (occupiedIndex-- == 0)
                    return slot;
            }

            return 0;
        }

        private static PlaylistItem createPlaylistItem(StableBanchoMatch match)
        {
            string artist = string.Empty;
            string title = match.BeatmapName;
            string difficulty = string.Empty;
            int separator = title.IndexOf(" - ", System.StringComparison.Ordinal);
            if (separator >= 0)
            {
                artist = title[..separator];
                title = title[(separator + 3)..];
            }

            int difficultyStart = title.LastIndexOf(" [", System.StringComparison.Ordinal);
            if (difficultyStart >= 0 && title.EndsWith(']'))
            {
                difficulty = title[(difficultyStart + 2)..^1];
                title = title[..difficultyStart];
            }

            var beatmap = new APIBeatmap
            {
                OnlineID = match.BeatmapId,
                Checksum = match.BeatmapChecksum,
                RulesetID = match.RulesetId,
                DifficultyName = difficulty,
                BeatmapSet = new APIBeatmapSet { Artist = artist, Title = title },
            };

            return new PlaylistItem(beatmap)
            {
                // A stable match has one mutable beatmap rather than a playlist. Keep a
                // stable synthetic item ID while the underlying beatmap changes.
                ID = 1,
                OwnerID = match.HostUserId,
                RulesetID = match.RulesetId,
                RequiredMods = toApiMods(match.Mods),
                AllowedMods = match.FreeMods ? toApiMods(all_stable_freemod_mods) : [],
                Freestyle = match.FreeMods,
            };
        }

        private const int all_stable_freemod_mods = (int)(LegacyMods.NoFail | LegacyMods.Easy | LegacyMods.Hidden | LegacyMods.HardRock | LegacyMods.SuddenDeath | LegacyMods.Flashlight | LegacyMods.Perfect);

        private static APIMod[] toApiMods(int mods)
        {
            var result = new List<APIMod> { new APIMod { Acronym = "CL" } };
            var legacy = (LegacyMods)mods;

            add(LegacyMods.NoFail, "NF");
            add(LegacyMods.Easy, "EZ");
            add(LegacyMods.TouchDevice, "TD");
            add(LegacyMods.Hidden, "HD");
            add(LegacyMods.HardRock, "HR");
            add(legacy.HasFlag(LegacyMods.Perfect) ? LegacyMods.Perfect : LegacyMods.SuddenDeath, legacy.HasFlag(LegacyMods.Perfect) ? "PF" : "SD");
            add(legacy.HasFlag(LegacyMods.Nightcore) ? LegacyMods.Nightcore : LegacyMods.DoubleTime, legacy.HasFlag(LegacyMods.Nightcore) ? "NC" : "DT");
            add(LegacyMods.HalfTime, "HT");
            add(LegacyMods.Flashlight, "FL");
            add(LegacyMods.SpunOut, "SO");
            return result.ToArray();

            void add(LegacyMods flag, string acronym)
            {
                if (legacy.HasFlag(flag))
                    result.Add(new APIMod { Acronym = acronym });
            }
        }

        private static bool isTeamMatch(byte teamType) => teamType is 2 or 3;

        private static MultiplayerUserState toUserState(StableMatchSlotStatus status) => status switch
        {
            StableMatchSlotStatus.Ready => MultiplayerUserState.Ready,
            StableMatchSlotStatus.Playing => MultiplayerUserState.Playing,
            StableMatchSlotStatus.Complete => MultiplayerUserState.Results,
            _ => MultiplayerUserState.Idle,
        };
    }
}
