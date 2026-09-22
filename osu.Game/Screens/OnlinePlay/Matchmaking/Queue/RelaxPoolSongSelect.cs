// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osu.Game.Screens.OnlinePlay.Playlists;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Queue
{
    /// <summary>
    /// The regular playlists beatmap selector, restricted to adding unmodded osu! beatmaps to a Relax pool.
    /// </summary>
    public partial class RelaxPoolSongSelect : PlaylistsSongSelect
    {
        public override string Title => "Add beatmaps to Relax pool";

        private readonly Room room;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        public RelaxPoolSongSelect(Room room)
            : base(room, "Add to Relax pool")
        {
            this.room = room;

            // PlaylistsSongSelect uses room-specific screen offsets. The pool selector should retain
            // the regular SongSelect geometry and only reuse playlist selection behaviour.
            Padding = new osu.Framework.Graphics.MarginPadding();
            TopPadding = 0;
        }

        public override void AddNewItem()
        {
            if (!tryCreateItem(Beatmap.Value.BeatmapInfo, out var item))
                return;

            replacePlaylist(room.Playlist.Append(item));
        }

        private void addCurrentSet()
        {
            var additions = new List<PlaylistItem>();

            foreach (var beatmap in Beatmap.Value.BeatmapSetInfo.Beatmaps)
            {
                if (tryCreateItem(beatmap, out var item, false))
                    additions.Add(item);
            }

            replacePlaylist(room.Playlist.Concat(additions));
        }

        private bool tryCreateItem(BeatmapInfo beatmap, out PlaylistItem item, bool notifyFailure = true)
        {
            item = null!;

            if (beatmap.OnlineID <= 0 || beatmap.Ruleset.OnlineID != 0)
            {
                if (notifyFailure)
                    notifications.Post(new SimpleNotification { Text = "Only online osu! difficulties can be added." });
                return false;
            }

            item = new PlaylistItem(beatmap)
            {
                ID = beatmap.OnlineID,
                RulesetID = 0,
                RequiredMods = Array.Empty<APIMod>(),
                AllowedMods = Array.Empty<APIMod>(),
                Freestyle = false,
            };
            return true;
        }

        private void replacePlaylist(IEnumerable<PlaylistItem> items)
        {
            room.Playlist = items
                            .DistinctBy(item => item.Beatmap.OnlineID)
                            .OrderBy(item => item.Beatmap.StarRating)
                            .ThenBy(item => item.Beatmap.OnlineID)
                            .ToArray();
        }

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons() =>
        [
            new ScreenFooterButton
            {
                Text = "Add whole set",
                Icon = osu.Framework.Graphics.Sprites.FontAwesome.Solid.LayerGroup,
                Action = addCurrentSet,
            }
        ];
    }
}
