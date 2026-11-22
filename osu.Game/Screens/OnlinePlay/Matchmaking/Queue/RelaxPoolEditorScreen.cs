// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Localisation;
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
        public override string Title => $"{modeName} pool";
        private readonly string modeName;
        private readonly int rulesetId;
        private long revision;
        private readonly Dictionary<int, string> contributors;
        private readonly Dictionary<int, DateTimeOffset> addedAt;

        public enum PoolSortOrder
        {
            [LocalisableDescription(typeof(RankedPlayLobbyStrings), nameof(RankedPlayLobbyStrings.PoolStarsAscending))]
            StarsAscending,
            [LocalisableDescription(typeof(RankedPlayLobbyStrings), nameof(RankedPlayLobbyStrings.PoolStarsDescending))]
            StarsDescending,
            [LocalisableDescription(typeof(RankedPlayLobbyStrings), nameof(RankedPlayLobbyStrings.PoolNewestFirst))]
            NewestFirst,
            [LocalisableDescription(typeof(RankedPlayLobbyStrings), nameof(RankedPlayLobbyStrings.PoolOldestFirst))]
            OldestFirst,
            [LocalisableDescription(typeof(RankedPlayLobbyStrings), nameof(RankedPlayLobbyStrings.PoolTitle))]
            Title,
            [LocalisableDescription(typeof(RankedPlayLobbyStrings), nameof(RankedPlayLobbyStrings.PoolArtist))]
            Artist,
        }

        private partial class PoolPlaylist(Dictionary<int, string> contributors) : DrawableRoomPlaylist
        {
            protected override DrawableRoomPlaylistItem CreateDrawablePlaylistItem(PlaylistItem item)
                => new DrawableRoomPlaylistItem(item) { PoolContributor = contributors.GetValueOrDefault(item.Beatmap.OnlineID, "you (unsaved)") };
        }
        public override bool ShowFooter => true;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private readonly Room room = new Room { Name = "Relax pool", Type = MatchType.Playlists };
        private DrawableRoomPlaylist playlist = null!;
        private BasicSearchTextBox searchTextBox = null!;
        private FormDropdown<PoolSortOrder> sortDropdown = null!;
        private FormCheckBox onlyMineCheckBox = null!;
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

        public RelaxPoolEditorScreen(RelaxRankedPoolResponse pool, string modeName = "Relax")
        {
            this.modeName = modeName;
            rulesetId = pool.RulesetId;
            revision = pool.Revision;
            contributors = pool.Beatmaps.ToDictionary(map => map.BeatmapId, map => map.AddedByUsername ?? "unknown");
            addedAt = pool.Beatmaps.ToDictionary(map => map.BeatmapId, map => map.AddedAt);
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
                                        Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4855(modeName),
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
                                Depth = -1,
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
                                                    Text = global::osu.Game.Localisation.ClientTranslationStrings.J4856,
                                                    Font = OsuFont.TorusAlternate.With(size: 22, weight: FontWeight.SemiBold),
                                                },
                                                new PurpleRoundedButton
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 42,
                                                    Text = global::osu.Game.Localisation.ClientTranslationStrings.J4857,
                                                    Action = () => this.Push(new RelaxPoolSongSelect(room, rulesetId, modeName)),
                                                }
                                            }
                                        }
                                    },
                                    searchTextBox = new BasicSearchTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 38,
                                        PlaceholderText = global::osu.Game.Localisation.ClientTranslationStrings.J4858,
                                    },
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 60,
                                        ColumnDimensions = new[] { new Dimension(), new Dimension() },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                sortDropdown = new FormDropdown<PoolSortOrder>
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Caption = RankedPlayLobbyStrings.PoolSort,
                                                    Items = Enum.GetValues<PoolSortOrder>(),
                                                    Current = { Value = PoolSortOrder.StarsAscending },
                                                },
                                                new Container
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Padding = new MarginPadding { Left = 8 },
                                                    Child = onlyMineCheckBox = new FormCheckBox
                                                    {
                                                        Caption = RankedPlayLobbyStrings.PoolOnlyMine,
                                                        HintText = RankedPlayLobbyStrings.PoolOnlyMineHint,
                                                        ExtendedHeight = true,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            playlist = new PoolPlaylist(contributors)
                            {
                                RelativeSizeAxes = Axes.Both,
                                AllowDeletion = true,
                                ShowItemOwners = true,
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
            sortDropdown.Current.BindValueChanged(_ => refreshPlaylist());
            onlyMineCheckBox.Current.BindValueChanged(_ => refreshPlaylist());
            refresh();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.Playlist))
            {
                foreach (var item in room.Playlist)
                    addedAt.TryAdd(item.Beatmap.OnlineID, DateTimeOffset.UtcNow);
                Scheduler.AddOnce(refresh);
            }
        }

        private void removeItem(PlaylistItem item)
        {
            room.Playlist = room.Playlist.Where(existing => existing.Beatmap.OnlineID != item.Beatmap.OnlineID).ToArray();
        }

        private void refresh()
        {
            refreshPlaylist();

            starBands.Clear();
            starBands.Add(new OsuSpriteText
            {
                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4859,
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

            if (onlyMineCheckBox.Current.Value)
                items = items.Where(item => item.OwnerID == api.LocalUser.Value.Id);

            IOrderedEnumerable<PlaylistItem> sorted = sortDropdown.Current.Value switch
            {
                PoolSortOrder.StarsDescending => items.OrderByDescending(item => item.Beatmap.StarRating),
                PoolSortOrder.NewestFirst => items.OrderByDescending(item => addedAt.GetValueOrDefault(item.Beatmap.OnlineID)),
                PoolSortOrder.OldestFirst => items.OrderBy(item => addedAt.GetValueOrDefault(item.Beatmap.OnlineID)),
                PoolSortOrder.Title => items.OrderBy(item => item.Beatmap.Metadata.Title, StringComparer.OrdinalIgnoreCase),
                PoolSortOrder.Artist => items.OrderBy(item => item.Beatmap.Metadata.Artist, StringComparer.OrdinalIgnoreCase),
                _ => items.OrderBy(item => item.Beatmap.StarRating),
            };
            var visible = sorted.ThenBy(item => item.Beatmap.OnlineID).ToArray();
            playlist.Items.ReplaceRange(0, playlist.Items.Count, visible);
            mapCount.Text = RankedPlayLobbyStrings.PoolShowing(visible.Length, room.Playlist.Count);
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

        private void save(bool exitAfterSave = false, int conflictRetries = 0)
        {
            if (saving)
                return;

            saving = true;
            var submittedIds = room.Playlist.Select(item => item.Beatmap.OnlineID).ToHashSet();
            var request = new ReplaceRelaxRankedPoolRequest
            {
                RulesetId = rulesetId,
                Revision = revision,
                BeatmapIds = submittedIds.ToArray()
            };
            request.Success += response =>
            {
                saving = false;
                revision++;

                if (response.RejectedBeatmapIds.Length > 0)
                {
                    var rejectedIds = response.RejectedBeatmapIds.ToHashSet();
                    room.Playlist = room.Playlist.Where(item => !rejectedIds.Contains(item.Beatmap.OnlineID)).ToArray();
                }

                // Only the submitted snapshot was saved. Edits made while the request was in flight stay dirty.
                savedBeatmapIds = submittedIds.Except(response.RejectedBeatmapIds).ToHashSet();
                notifications.Post(new SimpleNotification
                {
                    Text = response.Rejected == 0
                        ? global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4860(modeName, response.Accepted)
                        : global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4861(response.Accepted, response.Rejected),
                    IsImportant = response.Rejected > 0,
                });

                if (exitAfterSave && !hasUnsavedChanges)
                {
                    exitConfirmed = true;
                    this.Exit();
                }
                else
                    reload(mergeEdits: true);
            };
            request.Failure += error =>
            {
                saving = false;
                if (request.HasRevisionConflict || error is APIException { StatusCode: HttpStatusCode.Conflict })
                {
                    // Rebase only our additions/removals onto the latest snapshot; never replace someone else's edits.
                    reload(mergeEdits: true, afterReload: conflictRetries < 3 ? () => save(exitAfterSave, conflictRetries + 1) : null);
                    if (conflictRetries >= 3)
                        notifications.Post(new SimpleNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.J4862, IsImportant = true });
                    return;
                }
                notifications.Post(new SimpleNotification { Text = error.Message, IsImportant = true });
            };
            api.Queue(request);
        }

        private void reload() => reload(mergeEdits: false);

        private void reload(bool mergeEdits, Action? afterReload = null)
        {
            if (saving) return;
            saving = true;
            var request = new GetRelaxRankedPoolRequest { RulesetId = rulesetId };
            request.Success += pool =>
            {
                saving = false;
                revision = pool.Revision;
                contributors.Clear();
                addedAt.Clear();
                foreach (var map in pool.Beatmaps)
                {
                    contributors[map.BeatmapId] = map.AddedByUsername ?? "unknown";
                    addedAt[map.BeatmapId] = map.AddedAt;
                }
                var localAdditions = room.Playlist.Where(item => !savedBeatmapIds.Contains(item.Beatmap.OnlineID)).ToArray();
                var localRemovals = savedBeatmapIds.Except(room.Playlist.Select(item => item.Beatmap.OnlineID)).ToHashSet();
                var remoteItems = pool.Beatmaps.Select(createPlaylistItem);
                room.Playlist = (mergeEdits
                        ? remoteItems.Where(item => !localRemovals.Contains(item.Beatmap.OnlineID))
                                     .Concat(localAdditions).DistinctBy(item => item.Beatmap.OnlineID)
                        : remoteItems)
                    .OrderBy(item => item.Beatmap.StarRating).ToArray();
                savedBeatmapIds = pool.Beatmaps.Select(map => map.BeatmapId).ToHashSet();
                refresh();
                afterReload?.Invoke();
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
                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4863,
                Icon = FontAwesome.Solid.Sync,
                Action = () =>
                {
                    if (saving)
                        return;
                    if (hasUnsavedChanges)
                    {
                        dialogOverlay?.Push(new osu.Game.Overlays.Dialog.ConfirmDialog(
                            global::osu.Game.Localisation.ClientTranslationStrings.J4864, reload));
                        return;
                    }
                    reload();
                },
            },
            new ScreenFooterButton
            {
                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4865,
                Icon = FontAwesome.Solid.Save,
                Action = () => save(),
            }
        ];

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (saving)
            {
                notifications.Post(new SimpleNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.J4866 });
                return true;
            }

            if (exitConfirmed || !hasUnsavedChanges)
                return base.OnExiting(e);

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

        private PlaylistItem createPlaylistItem(RelaxRankedPoolBeatmap beatmap)
        {
            int mapRulesetId = rulesetId switch { 4 or 5 => 0, 6 => 1, 7 => 2, _ => rulesetId };
            var apiBeatmap = new APIBeatmap
            {
                OnlineID = beatmap.BeatmapId,
                OnlineBeatmapSetID = beatmap.BeatmapsetId,
                RulesetID = mapRulesetId,
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
                OwnerID = beatmap.AddedBy ?? 0,
                RulesetID = mapRulesetId,
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
