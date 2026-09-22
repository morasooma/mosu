// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Edit;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match;
using osu.Game.Screens.OnlinePlay.Playlists;
using osuTK;
using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Queue
{
    /// <summary>
    /// Relax pool composition screen backed by the same playlist controls used by playlists rooms.
    /// </summary>
    public partial class RelaxPoolEditorScreen : OsuScreen
    {
        public override string Title => "Relax pool";
        public override bool ShowFooter => true;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private readonly Room room = new Room { Name = "Relax pool", Type = MatchType.Playlists };
        private DrawableRoomPlaylist playlist = null!;
        private BasicSearchTextBox searchTextBox = null!;
        private FillFlowContainer starBands = null!;
        private OsuSpriteText mapCount = null!;
        private bool saving;
        private bool exitConfirmed;
        private HashSet<int> savedBeatmapIds;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        public RelaxPoolEditorScreen(RelaxRankedPoolResponse pool)
        {
            savedBeatmapIds = pool.Beatmaps.Select(beatmap => beatmap.BeatmapId).ToHashSet();
            room.Playlist = pool.Beatmaps.Select(createPlaylistItem)
                                .OrderBy(item => item.Beatmap.StarRating)
                                .ThenBy(item => item.Beatmap.OnlineID)
                                .ToArray();
        }

        protected override BackgroundScreen CreateBackground() => new MatchmakingBackgroundScreen(colourProvider);

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding
                {
                    Top = 80,
                    Bottom = ScreenFooter.HEIGHT + 20,
                    Horizontal = 70,
                },
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Margin = new MarginPadding { Bottom = 20 },
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = "Relax pool",
                                        Font = OsuFont.TorusAlternate.With(size: 32, weight: FontWeight.SemiBold),
                                    },
                                    mapCount = new OsuSpriteText
                                    {
                                        Font = OsuFont.Torus.With(size: 16),
                                        Colour = colourProvider.Content2,
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Absolute, 300),
                                    new Dimension(GridSizeMode.Absolute, 20),
                                    new Dimension(),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        createStarSummary(),
                                        Empty(),
                                        createPlaylistArea(),
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private Drawable createStarSummary() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 12,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4.Opacity(0.96f),
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(16),
                    Child = starBands = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                    }
                }
            }
        };

        private Drawable createPlaylistArea() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 12,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4.Opacity(0.96f),
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(16),
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                                Children = new Drawable[]
                                {
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 52,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(),
                                            new Dimension(GridSizeMode.Absolute, 230),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Text = "Beatmaps",
                                                    Font = OsuFont.TorusAlternate.With(size: 22, weight: FontWeight.SemiBold),
                                                },
                                                new PurpleRoundedButton
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 42,
                                                    Text = "+ Add beatmaps",
                                                    Action = () => this.Push(new RelaxPoolSongSelect(room)),
                                                }
                                            }
                                        }
                                    },
                                    searchTextBox = new BasicSearchTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 38,
                                        PlaceholderText = "Search by artist, title, or difficulty",
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            playlist = new DrawableRoomPlaylist
                            {
                                RelativeSizeAxes = Axes.Both,
                                AllowDeletion = true,
                                RequestDeletion = removeItem,
                            }
                        }
                    }
                }
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            room.PropertyChanged += onRoomPropertyChanged;
            searchTextBox.Current.BindValueChanged(_ => refreshPlaylist());
            refresh();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.Playlist))
                Scheduler.AddOnce(refresh);
        }

        private void removeItem(PlaylistItem item)
        {
            room.Playlist = room.Playlist.Where(existing => existing.Beatmap.OnlineID != item.Beatmap.OnlineID).ToArray();
        }

        private void refresh()
        {
            refreshPlaylist();
            mapCount.Text = $"{room.Playlist.Count} {(room.Playlist.Count == 1 ? "beatmap" : "beatmaps")} \u2022 sorted by star rating";

            starBands.Clear();
            starBands.Add(new OsuSpriteText
            {
                Text = "Star rating",
                Font = OsuFont.TorusAlternate.With(size: 22, weight: FontWeight.SemiBold),
                Margin = new MarginPadding { Bottom = 6 },
            });

            int maximumBand = Math.Max(12, room.Playlist.Select(item => (int)Math.Floor(item.Beatmap.StarRating)).DefaultIfEmpty(0).Max());
            foreach (int start in Enumerable.Range(0, maximumBand + 1))
            {
                int count = room.Playlist.Count(item => item.Beatmap.StarRating >= start && item.Beatmap.StarRating < start + 1);
                starBands.Add(new StarBandRow(start, count, colourProvider));
            }
        }

        private void refreshPlaylist()
        {
            string query = searchTextBox.Current.Value.Trim();
            IEnumerable<PlaylistItem> items = room.Playlist;

            if (query.Length > 0)
                items = items.Where(item => matchesSearch(item, query));

            playlist.Items.ReplaceRange(0, playlist.Items.Count, items);
        }

        private static bool matchesSearch(PlaylistItem item, string query)
        {
            var beatmap = item.Beatmap;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;

            return contains(metadata.Artist, query)
                   || contains(metadata.ArtistUnicode, query)
                   || contains(metadata.Title, query)
                   || contains(metadata.TitleUnicode, query)
                   || contains(beatmap.DifficultyName, query);
        }

        private static bool contains(string? value, string query) => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

        private bool hasUnsavedChanges => !savedBeatmapIds.SetEquals(
            room.Playlist.Select(item => item.Beatmap.OnlineID));

        private void save(bool exitAfterSave = false)
        {
            if (saving)
                return;

            saving = true;
            var request = new ReplaceRelaxRankedPoolRequest
            {
                BeatmapIds = room.Playlist.Select(item => item.Beatmap.OnlineID).Distinct().ToArray()
            };
            request.Success += response =>
            {
                saving = false;

                if (response.RejectedBeatmapIds.Length > 0)
                {
                    var rejectedIds = response.RejectedBeatmapIds.ToHashSet();
                    room.Playlist = room.Playlist.Where(item => !rejectedIds.Contains(item.Beatmap.OnlineID)).ToArray();
                }

                savedBeatmapIds = room.Playlist.Select(item => item.Beatmap.OnlineID).ToHashSet();
                notifications.Post(new SimpleNotification
                {
                    Text = response.Rejected == 0
                        ? $"Relax pool saved: {response.Accepted} maps."
                        : $"Saved {response.Accepted}; rejected {response.Rejected} invalid maps.",
                    IsImportant = response.Rejected > 0,
                });

                if (exitAfterSave)
                {
                    exitConfirmed = true;
                    this.Exit();
                }
            };
            request.Failure += error =>
            {
                saving = false;
                notifications.Post(new SimpleNotification { Text = error.Message, IsImportant = true });
            };
            api.Queue(request);
        }

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons() =>
        [
            new ScreenFooterButton
            {
                Text = "Save pool",
                Icon = FontAwesome.Solid.Save,
                Action = () => save(),
            }
        ];

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (exitConfirmed || !hasUnsavedChanges)
                return base.OnExiting(e);

            if (saving)
            {
                notifications.Post(new SimpleNotification { Text = "Wait for the Relax pool to finish saving." });
                return true;
            }

            if (dialogOverlay == null)
                return true;

            if (dialogOverlay.CurrentDialog is PromptForSaveDialog currentPrompt)
            {
                currentPrompt.Flash();
                return true;
            }

            dialogOverlay.Push(new PromptForSaveDialog(
                exit: () =>
                {
                    exitConfirmed = true;
                    this.Exit();
                },
                saveAndExit: () => save(true),
                cancel: () => { }));

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            room.PropertyChanged -= onRoomPropertyChanged;
            base.Dispose(isDisposing);
        }

        private static PlaylistItem createPlaylistItem(RelaxRankedPoolBeatmap beatmap)
        {
            var apiBeatmap = new APIBeatmap
            {
                OnlineID = beatmap.BeatmapId,
                OnlineBeatmapSetID = beatmap.BeatmapsetId,
                RulesetID = 0,
                StarRating = beatmap.StarRating,
                DifficultyName = beatmap.Version,
                Length = beatmap.TotalLength * 1000,
                BeatmapSet = new APIBeatmapSet
                {
                    OnlineID = beatmap.BeatmapsetId,
                    Artist = beatmap.Artist,
                    ArtistUnicode = beatmap.Artist,
                    Title = beatmap.Title,
                    TitleUnicode = beatmap.Title,
                }
            };

            return new PlaylistItem(apiBeatmap)
            {
                ID = beatmap.BeatmapId,
                RulesetID = 0,
                RequiredMods = Array.Empty<APIMod>(),
                AllowedMods = Array.Empty<APIMod>(),
                Freestyle = false,
            };
        }

        private partial class StarBandRow : CompositeDrawable
        {
            public StarBandRow(int start, int count, OverlayColourProvider colours)
            {
                RelativeSizeAxes = Axes.X;
                Height = 32;
                Masking = true;
                CornerRadius = 6;
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = count > 0 ? colours.Background2 : colours.Background5,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 10,
                        Text = $"{start}\u2013{start + 1}\u2605",
                        Font = OsuFont.Torus.With(size: 14, weight: count > 0 ? FontWeight.SemiBold : FontWeight.Regular),
                        Colour = count > 0 ? Colour4.White : colours.Content2,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -30,
                        Text = count.ToString(),
                        Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                        Colour = count > 0 ? Colour4.White : colours.Content2,
                    }
                };
            }
        }
    }
}
