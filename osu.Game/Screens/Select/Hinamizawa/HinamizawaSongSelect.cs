using System;
using osu.Framework.Localisation;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Hinamizawa;
using osu.Game.Rulesets;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select.Hinamizawa
{
    /// <summary>The original song-select UI with a screen-owned remote catalogue, not a second browser.</summary>
    public partial class HinamizawaSongSelect : SongSelect, IWorkingBeatmapCache
    {
        public override string Title => "Hinamizawa";
        protected override bool SupportsStableStyle => true;
        protected override bool ShouldFetchOnlineMetadata => false;
        protected override IBindableList<BeatmapSetInfo> ExternalBeatmapSource => source;
        private readonly BindableList<BeatmapSetInfo> source = new BindableList<BeatmapSetInfo>();
        private readonly Dictionary<int, APIBeatmap> catalogue = new Dictionary<int, APIBeatmap>();
        private readonly Dictionary<Guid, WorkingBeatmap> previews = new Dictionary<Guid, WorkingBeatmap>();
        private readonly List<HinamizawaWorkingBeatmap> played = new List<HinamizawaWorkingBeatmap>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly HashSet<int> requestedSayobotCursors = new HashSet<int>();
        private readonly HashSet<int> saved = new HashSet<int>();
        private HinamizawaClient client = null!;
        private ITrackStore previewTracks = null!;
        private CancellationTokenSource? searchCancellation;
        private CancellationTokenSource? operationCancellation;
        private int previewPublicationVersion;
        private ScheduledDelegate? debounce;
        private HinamizawaSearch search = new HinamizawaSearch();
        private HinamizawaSearch facets = new HinamizawaSearch();
        private BeatmapCatalogueProvider catalogueProvider;
        protected virtual BeatmapCatalogueProvider DefaultCatalogueProvider => BeatmapCatalogueProvider.Sayobot;
        private FilterCriteria? criteria;
        private int nextPage;
        private bool loading;
        private bool more;
        private bool busy;
        private bool ready;
        private HinamizawaArchiveProvider archiveProvider;
        private int? catalogueTotal;
        private bool exiting;
        private int lastSelectedDifficultyId = -1;
        private WorkingBeatmap? previousBeatmap;
        private Container filterPanel = null!;
        private OsuSpriteText status = null!;
        private ScreenFooterButton? cancelButton;
        private ScreenFooterButton? providerButton;
        private ScreenFooterButton? catalogueButton;
        private readonly Dictionary<string, OsuTextBox> ranges = new Dictionary<string, OsuTextBox>();
        private OsuDropdown<string> category = null!;
        private OsuDropdown<string> sort = null!;
        private OsuDropdown<string> genre = null!;
        private OsuDropdown<string> language = null!;
        private OsuDropdown<string> explicitContent = null!;
        private OsuDropdown<string> extra = null!;
        private OsuTextBox creator = null!;
        internal int? SelectedDifficultyId => Carousel.CurrentBeatmap?.OnlineID;
        internal int CatalogueCount => source.Count;

        [Resolved] private AudioManager audio { get; set; } = null!;
        [Resolved] private GameHost host { get; set; } = null!;
        [Resolved] private LargeTextureStore textures { get; set; } = null!;
        [Resolved] private BeatmapManager manager { get; set; } = null!;
        [Resolved] private RulesetStore rulesets { get; set; } = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IWorkingBeatmapCache>(this);
            return dependencies;
        }
        protected virtual HinamizawaClient CreateMirrorClient(Storage storage) => new HinamizawaClient(storage.GetFullPath("cache/hinamizawa"));
        protected virtual bool LoadMirrorMedia => true;

        [BackgroundDependencyLoader]
        private void load(Storage storage, Configuration.OsuConfigManager config)
        {
            catalogueProvider = DefaultCatalogueProvider;
            client = CreateMirrorClient(storage);
            client.TrimCache();
            previewTracks = audio.GetTrackStore(new TrustedDomainOnlineStore(config));
            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.X, Height = 54, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                Margin = new MarginPadding { Bottom = 85, Left = 30, Right = 30 },
                Child = status = new TruncatingSpriteText
                {
                    RelativeSizeAxes = Axes.X, Text = global::osu.Game.Localisation.ClientTranslationStrings.J5188, Font = OsuFont.GetFont(size: 21),
                },
            });
            AddInternal(filterPanel = new Container
            {
                Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(460, 610), Depth = -100,
                Masking = true, CornerRadius = 10, Alpha = 0,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(30, 27, 43, 250) },
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both, Padding = new MarginPadding(18),
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 9),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5189, Font = OsuFont.GetFont(size: 18) },
                                category = dropdown(new[] { ("Ranked", "ranked"), ("Loved", "loved"), ("Pending", "pending"), ("Graveyard", "graveyard"), (global::osu.Game.Localisation.ClientTranslationStrings.J5190, "") }),
                                sort = dropdown(new[] { (global::osu.Game.Localisation.ClientTranslationStrings.J5191, "ranked_desc"), (global::osu.Game.Localisation.ClientTranslationStrings.J5192, "plays_desc"), (global::osu.Game.Localisation.ClientTranslationStrings.J5193, "favourites_desc"), (global::osu.Game.Localisation.ClientTranslationStrings.J5194, "difficulty_desc"), (global::osu.Game.Localisation.ClientTranslationStrings.J5195, "title_asc") }),
                                creator = input(global::osu.Game.Localisation.ClientTranslationStrings.J5196),
                                genre = dropdown(new[] { (global::osu.Game.Localisation.ClientTranslationStrings.J5197, ""), (global::osu.Game.Localisation.ClientTranslationStrings.J5198, "2"), (global::osu.Game.Localisation.ClientTranslationStrings.J5199, "3"), (global::osu.Game.Localisation.ClientTranslationStrings.J5200, "4"), (global::osu.Game.Localisation.ClientTranslationStrings.J5201, "5"), (global::osu.Game.Localisation.ClientTranslationStrings.J5202, "6"), (global::osu.Game.Localisation.ClientTranslationStrings.J5203, "7"), (global::osu.Game.Localisation.ClientTranslationStrings.J5204, "9"), (global::osu.Game.Localisation.ClientTranslationStrings.J5205, "10"), (global::osu.Game.Localisation.ClientTranslationStrings.J5206, "11"), (global::osu.Game.Localisation.ClientTranslationStrings.J5207, "12"), (global::osu.Game.Localisation.ClientTranslationStrings.J5208, "13"), (global::osu.Game.Localisation.ClientTranslationStrings.J5209, "14") }),
                                language = dropdown(new[] { (global::osu.Game.Localisation.ClientTranslationStrings.J5210, ""), (global::osu.Game.Localisation.ClientTranslationStrings.J5211, "2"), (global::osu.Game.Localisation.ClientTranslationStrings.J5212, "3"), (global::osu.Game.Localisation.ClientTranslationStrings.J5213, "4"), (global::osu.Game.Localisation.ClientTranslationStrings.J5214, "5"), (global::osu.Game.Localisation.ClientTranslationStrings.J5215, "6"), (global::osu.Game.Localisation.ClientTranslationStrings.J5216, "7"), (global::osu.Game.Localisation.ClientTranslationStrings.J5217, "8"), (global::osu.Game.Localisation.ClientTranslationStrings.J5218, "9"), (global::osu.Game.Localisation.ClientTranslationStrings.J5219, "10"), (global::osu.Game.Localisation.ClientTranslationStrings.J5220, "11"), (global::osu.Game.Localisation.ClientTranslationStrings.J5221, "12"), (global::osu.Game.Localisation.ClientTranslationStrings.J5222, "13"), (global::osu.Game.Localisation.ClientTranslationStrings.J5202, "14") }),
                                explicitContent = dropdown(new[] { (global::osu.Game.Localisation.ClientTranslationStrings.J5223, "hide"), (global::osu.Game.Localisation.ClientTranslationStrings.J5224, "show"), (global::osu.Game.Localisation.ClientTranslationStrings.J5225, "only") }),
                                extra = dropdown(new[] { (global::osu.Game.Localisation.ClientTranslationStrings.J5226, ""), (global::osu.Game.Localisation.ClientTranslationStrings.J5227, "video"), (global::osu.Game.Localisation.ClientTranslationStrings.J5228, "storyboard") }),
                                range("stars", global::osu.Game.Localisation.ClientTranslationStrings.J5229), range("bpm", "BPM"), range("length", global::osu.Game.Localisation.ClientTranslationStrings.J5230), range("pp", global::osu.Game.Localisation.ClientTranslationStrings.J5231),
                                new HinamizawaButton { RelativeSizeAxes = Axes.X, Text = global::osu.Game.Localisation.ClientTranslationStrings.J5232, Action = applyFacets },
                                new HinamizawaButton { RelativeSizeAxes = Axes.X, Text = global::osu.Game.Localisation.ClientTranslationStrings.J5233, Action = () => filterPanel.Hide() },
                            },
                        },
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ready = true;
            Ruleset.BindValueChanged(_ => queueSearch());
        }
        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons()
        {
            var buttons = base.CreateFooterButtons().Where(b => b is not FooterButtonOptions).ToList();
            buttons.Add(new ScreenFooterButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5234, Icon = FontAwesome.Solid.Filter, Action = () => { if (filterPanel.Alpha == 0) filterPanel.Show(); else filterPanel.Hide(); } });
            buttons.Add(new ScreenFooterButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5235, Icon = FontAwesome.Solid.Download, Action = () => prepare(true) });
            buttons.Add(catalogueButton = new ScreenFooterButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5236, Icon = FontAwesome.Solid.Search, Action = toggleCatalogueProvider });
            buttons.Add(providerButton = new ScreenFooterButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5237, Icon = FontAwesome.Solid.ExchangeAlt, Action = toggleArchiveProvider });
            buttons.Add(cancelButton = new ScreenFooterButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5238, Icon = FontAwesome.Solid.Sync, Action = () => { if (busy) operationCancellation?.Cancel(); else { more = true; RequestMoreBeatmaps(); } } });
            return buttons;
        }
        public override IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap)
        {
            yield return new OsuMenuItem(global::osu.Game.Localisation.ClientTranslationStrings.J5239, MenuItemType.Highlighted, () => SelectAndRun(beatmap, OnStart));
            yield return new OsuMenuItem(global::osu.Game.Localisation.ClientTranslationStrings.J5240, MenuItemType.Standard, () => prepare(true, beatmap.OnlineID));
        }
        protected override void OnFilterCriteriaChanged(FilterCriteria newCriteria) { criteria = newCriteria; queueSearch(); }
        private void queueSearch()
        {
            if (!ready || exiting) return;
            var value = facets with
            {
                Query = string.Join(" ", criteria?.SearchTerms.Select(t => t.SearchTerm) ?? Array.Empty<string>()),
                Mode = Ruleset.Value.OnlineID is >= 0 and <= 3 ? Ruleset.Value.OnlineID : 0,
                MinStars = facets.MinStars ?? criteria?.StarDifficulty.Min,
                MaxStars = facets.MaxStars ?? criteria?.StarDifficulty.Max,
                MinBpm = facets.MinBpm ?? criteria?.BPM.Min,
                MaxBpm = facets.MaxBpm ?? criteria?.BPM.Max,
                MinLength = facets.MinLength ?? criteria?.Length.Min / 1000,
                MaxLength = facets.MaxLength ?? criteria?.Length.Max / 1000,
                Creator = string.IsNullOrEmpty(facets.Creator) ? criteria?.Creator.SearchTerm ?? "" : facets.Creator,
            };
            if (value == search && searchCancellation?.IsCancellationRequested == false) return;
            search = value;
            more = false;
            searchCancellation?.Cancel(); operationCancellation?.Cancel();
            debounce?.Cancel();
            debounce = Scheduler.AddDelayed(() =>
            {
                source.Clear(); catalogue.Clear(); seen.Clear(); requestedSayobotCursors.Clear(); catalogueTotal = null;
                nextPage = 0; more = true; loading = false;
                searchCancellation = new CancellationTokenSource();
                RequestMoreBeatmaps();
            }, 400);
        }
        protected override void RequestMoreBeatmaps()
        {
            if (!ready || !this.IsCurrentScreen() || loading || !more || searchCancellation?.IsCancellationRequested != false) return;
            loading = true;
            var cancellation = searchCancellation;
            int page = nextPage;
            var requestedSearch = search;
            var requestedProvider = catalogueProvider;
            if (requestedProvider == BeatmapCatalogueProvider.Sayobot && !requestedSayobotCursors.Add(page))
            {
                more = false;
                status.Text = formatCatalogueStatus("Sayobot") + " · повторный offset; продолжение остановлено.";
                loading = false;
                return;
            }
            _ = fetch();
            async Task fetch()
            {
                try
                {
                    var result = requestedProvider == BeatmapCatalogueProvider.Sayobot
                        ? await client.SearchSayobotAsync(requestedSearch.Query, requestedSearch.Mode, page, cancellation.Token).ConfigureAwait(false)
                        : await client.SearchAsync(requestedSearch, page, cancellation.Token).ConfigureAwait(false);
                    Schedule(() =>
                    {
                        if (searchCancellation != cancellation || cancellation.IsCancellationRequested || exiting) return;
                        nextPage = result.Source == "Sayobot" ? result.NextOffset ?? nextPage : nextPage + 1;
                        loading = false; more = result.HasMore;
                        if (result.TotalCount.HasValue) catalogueTotal = result.TotalCount;
                        foreach (var set in result.Sets)
                        {
                            if (set.OnlineID <= 0 || !seen.Add(set.OnlineID)) continue;
                            var detached = createSet(set);
                            if (detached.Beatmaps.Count > 0) source.Add(detached);
                        }
                        status.Text = result.PaginationWarning == null
                            ? formatCatalogueStatus(result.Source)
                            : formatCatalogueStatus(result.Source) + " · " + result.PaginationWarning;
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { Schedule(() => { if (searchCancellation == cancellation) { loading = false; more = false; status.Text = "Ошибка каталога: " + error.Message; } }); }
            }
        }
        private BeatmapSetInfo createSet(APIBeatmapSet online)
        {
            var set = new BeatmapSetInfo { OnlineID = online.OnlineID, Status = online.Status, DateRanked = online.Ranked, DateSubmitted = online.Submitted };
            foreach (var map in online.Beatmaps.Where(b => b.RulesetID is >= 0 and <= 3))
            {
                var ruleset = rulesets.GetRuleset(map.RulesetID);
                if (ruleset == null) continue;
                var metadata = new BeatmapMetadata
                {
                    Title = online.Title, TitleUnicode = online.TitleUnicode, Artist = online.Artist, ArtistUnicode = online.ArtistUnicode,
                    Author = new RealmUser { OnlineID = online.AuthorID, Username = online.AuthorString }, Source = online.Source, Tags = online.Tags,
                    PreviewTime = 0, AudioFile = $"{(catalogueProvider == BeatmapCatalogueProvider.Sayobot ? "sayobot" : "hinamizawa")}-difficulty-{map.OnlineID}", BackgroundFile = $"{(catalogueProvider == BeatmapCatalogueProvider.Sayobot ? "sayobot" : "hinamizawa")}-difficulty-{map.OnlineID}",
                };
                var info = new BeatmapInfo(ruleset, new BeatmapDifficulty
                {
                    ApproachRate = map.ApproachRate, OverallDifficulty = map.OverallDifficulty, CircleSize = map.CircleSize, DrainRate = map.DrainRate,
                }, metadata)
                {
                    IsExternalCatalogue = true, BeatmapSet = set, OnlineID = map.OnlineID, MD5Hash = map.Checksum, OnlineMD5Hash = map.Checksum,
                    DifficultyName = map.DifficultyName, StarRating = map.StarRating, BPM = map.BPM > 0 ? map.BPM : online.BPM,
                    Length = map.Length, Status = map.Status, MaxCombo = map.MaxCombo, TotalObjectCount = map.TotalObjectCount,
                    EndTimeObjectCount = map.EndTimeObjectCount,
                };
                set.Beatmaps.Add(info);
                catalogue[info.OnlineID] = map;
            }
            return set;
        }
        protected override void PublishSelectedWorkingBeatmap(WorkingBeatmap workingBeatmap)
        {
            int version = ++previewPublicationVersion;
            if (!LoadMirrorMedia || workingBeatmap is not HinamizawaPreviewBeatmap || workingBeatmap.TrackLoaded)
            {
                base.PublishSelectedWorkingBeatmap(workingBeatmap);
                return;
            }

            // MusicController synchronously requests Track on publication. Resolve remote bytes
            // first, outside the update thread; only the still-selected difficulty may publish.
            _ = loadPreview();
            async Task loadPreview()
            {
                try
                {
                    await Task.Run(() => workingBeatmap.LoadTrack()).ConfigureAwait(false);
                    Schedule(() =>
                    {
                        if (!exiting && version == previewPublicationVersion && this.IsCurrentScreen()
                            && Carousel.CurrentBeatmap?.ID == workingBeatmap.BeatmapInfo.ID)
                            base.PublishSelectedWorkingBeatmap(workingBeatmap);
                    });
                }
                catch (Exception error)
                {
                    Schedule(() =>
                    {
                        if (!exiting && version == previewPublicationVersion && !busy)
                            status.Text = "Не удалось загрузить аудио: " + error.Message;
                    });
                }
            }
        }

        protected override WorkingBeatmap ResolveWorkingBeatmap(BeatmapInfo beatmap, bool refetch = false)
        {
            if (!beatmap.IsExternalCatalogue)
                return played.FirstOrDefault(w => w.BeatmapInfo.ID == beatmap.ID) ?? base.ResolveWorkingBeatmap(beatmap, refetch);
            if (!previews.TryGetValue(beatmap.ID, out var preview))
                previews[beatmap.ID] = preview = LoadMirrorMedia ? new HinamizawaPreviewBeatmap(beatmap, previewTracks, textures, audio) : new HinamizawaPreviewBeatmap(beatmap, previewTracks, textures, audio, loadMedia: false);
            return preview;
        }
        WorkingBeatmap IWorkingBeatmapCache.GetWorkingBeatmap(BeatmapInfo? beatmap) => beatmap == null ? manager.GetWorkingBeatmap(null) : ResolveWorkingBeatmap(beatmap);
        void IWorkingBeatmapCache.Invalidate(BeatmapSetInfo set) { }
        void IWorkingBeatmapCache.Invalidate(BeatmapInfo beatmap) { }
        protected override void OnStart() => prepare(false);
        private void prepare(bool persist, int? requestedId = null)
        {
            int id = requestedId ?? Carousel.CurrentBeatmap?.OnlineID ?? -1;
            if (busy || !catalogue.TryGetValue(id, out var difficulty) || !this.IsCurrentScreen()) return;
            if (persist && saved.Contains(difficulty.OnlineBeatmapSetID)) { status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5245; return; }
            busy = true;
            cancelButton!.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5246;
            var cancellation = operationCancellation = new CancellationTokenSource();
            var selectedProvider = archiveProvider;
            var set = difficulty.BeatmapSet!;
            status.Text = persist ? global::osu.Game.Localisation.ClientTranslationStrings.J5247 : global::osu.Game.Localisation.ClientTranslationStrings.J5248;
            var progress = new Progress<HinamizawaDownloadProgress>(value => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested || exiting) return;
                string size = formatBytes(value.BytesReceived);
                string total = value.TotalBytes is > 0 ? formatBytes(value.TotalBytes.Value) : "размер неизвестен";
                string rate = value.BytesPerSecond > 0 ? $" · {formatBytes((long)value.BytesPerSecond)}/с" : string.Empty;
                string eta = value.SecondsRemaining is double seconds ? $" · осталось {TimeSpan.FromSeconds(seconds):mm\\:ss}" : string.Empty;
                status.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ5251(size, total, rate, eta);
            }));
            _ = run();
            async Task run()
            {
                HinamizawaWorkingBeatmap? working = null;
                try
                {
                    string revision = catalogueProvider == BeatmapCatalogueProvider.Sayobot
                        ? "sayobot-unpinned-" + string.Join("|", set.Beatmaps.OrderBy(b => b.OnlineID).Select(b => b.LastUpdated.ToUnixTimeSeconds()))
                        : string.Join("|", set.Beatmaps.OrderBy(b => b.OnlineID).Select(b => b.Checksum));
                    string archive = await client.GetArchiveAsync(set.OnlineID, null, cancellation.Token, revision, progress, selectedProvider).ConfigureAwait(false);
                    Schedule(() => { if (!cancellation.IsCancellationRequested && !exiting) status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5252; });
                    if (persist)
                    {
                        using var stream = File.OpenRead(archive);
                        if (await manager.Import(new ImportTask(stream, Path.GetFileName(archive)), cancellationToken: cancellation.Token).ConfigureAwait(false) == null)
                            throw new InvalidDataException("Не удалось сохранить карту.");
                        Schedule(() => { saved.Add(set.OnlineID); if (!exiting) status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5253; });
                    }
                    else
                    {
                        var ruleset = rulesets.GetRuleset(difficulty.RulesetID)!;
                        working = await Task.Run(() => HinamizawaWorkingBeatmap.Create(HinamizawaClient.ExtractArchive(archive, cancellation.Token), difficulty, ruleset, audio, host), cancellation.Token).ConfigureAwait(false);
                        var playable = working;
                        Schedule(() =>
                        {
                            if (cancellation.IsCancellationRequested || exiting || !this.IsCurrentScreen() || Carousel.CurrentBeatmap?.OnlineID != id) { playable.Dispose(); return; }
                            played.Add(playable);
                            Beatmap.Value = playable;
                            Ruleset.Value = ruleset;
                            this.Push(new PlayerLoader(() => new HinamizawaPlayer()));
                        });
                        working = null;
                    }
                }
                catch (OperationCanceledException) { Schedule(() => { if (!exiting) status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5254; }); }
                catch (Exception error) { Schedule(() => { if (!exiting) status.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ5255(selectedProvider, error.Message); }); }
                finally { working?.Dispose(); Schedule(() => { busy = false; if (cancelButton != null) cancelButton.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5238; }); }
            }
        }
        private void toggleCatalogueProvider()
        {
            catalogueProvider = catalogueProvider == BeatmapCatalogueProvider.Sayobot ? BeatmapCatalogueProvider.Hinamizawa : BeatmapCatalogueProvider.Sayobot;
            if (catalogueButton != null) catalogueButton.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ5256(catalogueProvider);
            facets = catalogueProvider == BeatmapCatalogueProvider.Sayobot
                ? new HinamizawaSearch(Mode: -1, Status: "", Sort: "", Explicit: "")
                : new HinamizawaSearch();
            search = new HinamizawaSearch();
            searchCancellation?.Cancel(); operationCancellation?.Cancel(); debounce?.Cancel();
            source.Clear(); catalogue.Clear(); seen.Clear(); requestedSayobotCursors.Clear(); nextPage = 0; more = true; loading = false; catalogueTotal = null;
            queueSearch();
        }

        private void toggleArchiveProvider()
        {
            archiveProvider = archiveProvider == HinamizawaArchiveProvider.Hinamizawa ? HinamizawaArchiveProvider.NeriNyan : HinamizawaArchiveProvider.Hinamizawa;
            operationCancellation?.Cancel();
            if (providerButton != null) providerButton.Text = archiveProvider == HinamizawaArchiveProvider.Hinamizawa ? global::osu.Game.Localisation.ClientTranslationStrings.J5257 : global::osu.Game.Localisation.ClientTranslationStrings.J5258;
            status.Text = formatCatalogueStatus("catalogue");
            if (busy) return;
            status.Text = formatCatalogueStatus("catalogue");
        }

        private string formatCatalogueStatus(string sourceName)
        {
            string count = catalogueTotal.HasValue ? $"загружено {source.Count} из {catalogueTotal.Value}" : $"загружено {source.Count}; полный размер каталога неизвестен";
            string media = catalogueProvider == BeatmapCatalogueProvider.Sayobot ? "· медиа-превью: Hinamizawa (не Sayobot)" : "· медиа-превью: Hinamizawa";
            string disclaimer = catalogueProvider == BeatmapCatalogueProvider.Sayobot ? "· Sayobot не предоставляет expected MD5" : string.Empty;
            return $"Каталог {catalogueProvider} ({sourceName}): {count} {media} {disclaimer} · зеркало архива: {archiveProvider}";
        }

        private static string formatBytes(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} МиБ" : $"{bytes / 1024d:0} КиБ";

        private static OsuTextBox input(LocalisableString placeholder) => new OsuTextBox { RelativeSizeAxes = Axes.X, Height = 38, PlaceholderText = placeholder };
        private static OsuDropdown<T> dropdown<T>((LocalisableString Label, T Value)[] entries) where T : notnull
        {
            var dropdown = new HinamizawaDropdown<T> { RelativeSizeAxes = Axes.X };
            foreach (var entry in entries) dropdown.Labels[entry.Value] = entry.Label;
            dropdown.Items = entries.Select(e => e.Value); dropdown.Current.Value = entries[0].Value;
            return dropdown;
        }
        private Drawable range(string key, LocalisableString label)
        {
            var min = ranges[key + "Min"] = input(LocalisableString.Interpolate($"{label}{(global::osu.Game.Localisation.ClientTranslationStrings.J5268)}"));
            var max = ranges[key + "Max"] = input(global::osu.Game.Localisation.ClientTranslationStrings.J5269);
            return new GridContainer { RelativeSizeAxes = Axes.X, Height = 38, ColumnDimensions = new[] { new Dimension(GridSizeMode.Relative, 0.5f), new Dimension() }, Content = new[] { new Drawable[] { min, max } } };
        }
        private double? number(string key)
        {
            string value = ranges[key].Text.Trim().Replace(',', '.');
            if (value.Length == 0) return null;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number) || number < 0) throw new ArgumentException("В диапазоне нужно положительное число.");
            return number;
        }
        private void applyFacets()
        {
            if (catalogueProvider == BeatmapCatalogueProvider.Sayobot)
            {
                facets = new HinamizawaSearch(Mode: -1, Status: "", Sort: "", Explicit: "");
                filterPanel.Hide();
                status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5270;
                queueSearch();
                return;
            }
            try
            {
                facets = new HinamizawaSearch(Status: category.Current.Value, Sort: sort.Current.Value, Creator: creator.Text,
                    Genre: genre.Current.Value, Language: language.Current.Value, Explicit: explicitContent.Current.Value, Extra: extra.Current.Value,
                    MinStars: number("starsMin"), MaxStars: number("starsMax"), MinBpm: number("bpmMin"), MaxBpm: number("bpmMax"),
                    MinLength: number("lengthMin"), MaxLength: number("lengthMax"), MinPP: number("ppMin"), MaxPP: number("ppMax"));
                filterPanel.Hide(); queueSearch();
            }
            catch (Exception error) { status.Text = error.Message; }
        }
        protected override void Update()
        {
            base.Update();
            int selected = Carousel.CurrentBeatmap?.OnlineID ?? -1;
            if (selected == lastSelectedDifficultyId) return;
            lastSelectedDifficultyId = selected;
            if (busy) operationCancellation?.Cancel();
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            previousBeatmap = Beatmap.Value;
            if (Ruleset.Value.OnlineID is < 0 or > 3) Ruleset.Value = rulesets.GetRuleset(0)!;
            base.OnEntering(e); queueSearch();
        }
        public override void OnResuming(ScreenTransitionEvent e)
        {
            var info = source.SelectMany(s => s.Beatmaps).FirstOrDefault(b => b.OnlineID == Beatmap.Value.BeatmapInfo.OnlineID);
            if (info != null) Beatmap.Value = ResolveWorkingBeatmap(info);
            base.OnResuming(e); RequestMoreBeatmaps();
        }
        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e)) return true;
            exiting = true; debounce?.Cancel(); searchCancellation?.Cancel(); operationCancellation?.Cancel();
            if (previousBeatmap != null) Beatmap.Value = previousBeatmap;
            return false;
        }
        protected override void Dispose(bool isDisposing)
        {
            exiting = true; debounce?.Cancel(); searchCancellation?.Cancel(); operationCancellation?.Cancel();
            foreach (var working in played) working.Dispose();
            foreach (var preview in previews.Values)
            {
                preview.CancelAsyncLoad();
                if (preview.TrackLoaded) preview.Track.Dispose();
            }
            previewTracks?.Dispose(); client?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
