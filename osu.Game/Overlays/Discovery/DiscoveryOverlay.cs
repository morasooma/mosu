using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Discovery
{
    public partial class DiscoveryOverlay : FullscreenOverlay<DiscoveryHeader>
    {
        public override IconUsage Icon => osu.Game.Graphics.OsuIcon.NavigationCompass;
        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private IBeatmapApiProvider beatmapApi { get; set; } = null!;
        [Resolved] private OsuConfigManager config { get; set; } = null!;
        [Resolved] private RulesetStore rulesets { get; set; } = null!;
        [Resolved] private IBindable<RulesetInfo> selectedRuleset { get; set; } = null!;
        [Resolved] private RealmAccess realm { get; set; } = null!;
        [Resolved] private Storage storage { get; set; } = null!;
        [Resolved] private BeatmapModelDownloader downloader { get; set; } = null!;
        [Resolved] private BeatmapManager beatmaps { get; set; } = null!;
        [Resolved] private PreviewTrackManager previewManager { get; set; } = null!;
        [Resolved(CanBeNull = true)] private DiscoveryAudioService? audioAnalysis { get; set; }
        private readonly Dictionary<int, double> audioWeights = new Dictionary<int, double>();
        private readonly HashSet<int> audioAttempted = new HashSet<int>();
        private CancellationTokenSource? audioCancellation;
        private bool audioLoading;
        private bool audioFailed;
        protected virtual bool AudioRankingEnabled => audioAnalysis?.Enabled == true;
        protected virtual LocalisableString AudioRankingStatus => audioAnalysis?.Status.Value ?? DiscoveryStrings.AudioAnalysing;
        private const int audio_queue_limit = 100;
        private const int audio_attempt_limit = 360;
        private const int catalogue_pool_limit = 240;
        [Resolved] private GameHost host { get; set; } = null!;
        private Scheduler searchScheduler => host.UpdateThread.Scheduler;
        private double catalogueTime => host.UpdateThread.Clock.CurrentTime;
        private bool backgroundAudioSearch;
        private IBindable<bool> keepSearchingInBackground = null!;
        private bool canContinueInBackground => backgroundAudioSearch && audioSearch && keepSearchingInBackground.Value;
        private bool canRunSearch => State.Value == Visibility.Visible || canContinueInBackground;
        private bool canShowRecommendation => State.Value == Visibility.Visible && !savedView && current == null;
        private bool audioSearch => AudioRankingEnabled && workMode == DiscoveryWorkMode.Taste;
        private int audioBatchSize => audioAnalysis?.CandidateLimit ?? 12;
        private int roundLimit => audioSearch ? 36 : 12;
        private int pendingAudioMaps;
        private bool needsCatalogueRefill => audioSearch
            ? !audioFailed && audioAttempted.Count < audio_attempt_limit && candidates.Count < audio_queue_limit
                && candidatePool.Count < catalogue_pool_limit && pendingAudioMaps < audioBatchSize * 2
            : candidates.Count < 6;
        internal virtual Task<IReadOnlyDictionary<int, double>> RankAudioAsync(IReadOnlyList<APIBeatmap> seeds, IReadOnlyList<APIBeatmap> maps,
            DiscoverySeedSource source, CancellationToken token, Action<int, double> scoreReady) => audioAnalysis!.RankAsync(seeds, maps, source, token, scoreReady);
        private IDisposable? musicSilence;
        private DiscoveryFilters? filters;
        private readonly BindableDouble minimumStars = new BindableDouble(1) { MinValue = 0, MaxValue = 15, Precision = 0.25 };
        private readonly BindableDouble maximumStars = new BindableDouble(5) { MinValue = 0, MaxValue = 15, Precision = 0.25 };
        private readonly BindableDouble maximumMinutes = new BindableDouble(15) { MinValue = 1, MaxValue = 30, Precision = 1 };
        private readonly BindableDouble minimumBpm = new BindableDouble(0) { MinValue = 0, MaxValue = 500, Precision = 5 };
        private readonly BindableDouble maximumBpm = new BindableDouble(500) { MinValue = 0, MaxValue = 500, Precision = 5 };
        private OsuDropdown<SearchCategory> category = null!;
        private OsuDropdown<DiscoverySeedSource> seedSource = null!;
        private FormTextBox manualReferences = null!;
        private DiscoverySeedSource activeSeedSource;
        private SoloScoreInfo[] topScores = Array.Empty<SoloScoreInfo>();
        private APIBeatmap[] frequentMaps = Array.Empty<APIBeatmap>();
        private readonly List<APIBeatmap> manualMaps = new List<APIBeatmap>();
        private string appliedReferences = string.Empty;
        private int profileGeneration;
        private FillFlowContainer filterControls = null!;
        private ThemeLibraryButton moreFilters = null!;
        private ThemeLibraryButton retrySearch = null!;
        private bool filtersExpanded;
        private bool catalogueFailed;
        private bool catalogueHadError;
        private ThemeLibraryButton fullPreview = null!;
        private ThemeLibraryButton repeat = null!;
        private FillFlowContainer previewControls = null!;
        private OsuSpriteText playbackTime = null!;
        private ScheduledDelegate? refill;
        private ScheduledDelegate? prefetchDelay;
        private ScheduledDelegate? fullDownloadWatch;
        private CancellationTokenSource? prefetchCancellation;
        private CancellationTokenSource? activePrefetchCancellation;
        private (int Id, Task<(Beatmap Map, byte[]? Audio)> Task)? prefetched;
        private Task<(Beatmap Map, byte[]? Audio)>? activeMedia;
        private int activeMediaId;
        private readonly HashSet<int> installedSets = new HashSet<int>();
        private readonly List<APIBeatmapSet> candidatePool = new List<APIBeatmapSet>();
        private readonly Queue<APIBeatmapSet> recentlyShown = new Queue<APIBeatmapSet>();
        private readonly Dictionary<string, Cursor?> searchCursors = new Dictionary<string, Cursor?>();
        private readonly Dictionary<string, BeatmapCatalogueSource> searchSources = new Dictionary<string, BeatmapCatalogueSource>();
        private readonly HashSet<string> exhaustedSearches = new HashSet<string>();
        private int rounds;
        private string? lastArtist;
        private string? lastMapper;

        private readonly DiscoveryMedia media = new DiscoveryMedia();
        private readonly SemaphoreSlim nativePreparation = new SemaphoreSlim(1, 1);
        private readonly HashSet<DiscoveryGameplayPreview> pendingPreviews = new HashSet<DiscoveryGameplayPreview>();
        private readonly object pendingLock = new object();
        private readonly List<DiscoveryCandidate> candidates = new List<DiscoveryCandidate>();
        private readonly HashSet<int> sessionSeen = new HashSet<int>();
        private readonly List<APIRequest> requests = new List<APIRequest>();
        private DiscoveryHistory history = new DiscoveryHistory();
        private DiscoveryRecommendations? profile;
        private DiscoveryCandidate? current;
        private DiscoveryGameplayPreview? preview;
        private CancellationTokenSource? mediaCancellation;
        private ScheduledDelegate? previewDelay;
        private GridContainer root = null!;
        private Container body = null!;
        private OsuScrollContainer cardScroll = null!;
        private Container information = null!;
        private Container coverContent = null!;
        private Container previewPanel = null!;
        private Container previewContent = null!;
        private FillFlowContainer controls = null!;
        private FillFlowContainer actions = null!;
        private FillFlowContainer details = null!;
        private OsuSpriteText status = null!;
        private OsuSpriteText previewStatus = null!;
        private OsuSpriteText progress = null!;
        private TruncatingSpriteText title = null!;
        private TruncatingSpriteText artist = null!;
        private TruncatingSpriteText difficulty = null!;
        private TruncatingSpriteText mapper = null!;
        private OsuSpriteText statistics = null!;
        private OsuSpriteText reason = null!;
        private IconButton close = null!;
        private ThemeLibraryButton next = null!;
        private ThemeLibraryButton save = null!;
        private ThemeLibraryButton download = null!;
        private ThemeLibraryButton pause = null!;
        private ThemeLibraryButton savedTab = null!;
        private ThemeLibraryButton forYouTab = null!;
        private DiscoveryLibrary library = null!;
        private IBindable<RulesetInfo> toolbarRuleset = null!;
        private ThemeLibraryButton tasteMode = null!;
        private ThemeLibraryButton gameplayMode = null!;
        private DiscoveryWorkMode workMode;
        private readonly DiscoverySimilarity similarity = new DiscoverySimilarity();
        private readonly List<DiscoveryNeighbour> neighbours = new List<DiscoveryNeighbour>();
        private readonly Dictionary<int, double> gameplayWeights = new Dictionary<int, double>();
        private readonly HashSet<int> queriedSeeds = new HashSet<int>();
        private CancellationTokenSource? similarityCancellation;
        private bool similarityLoading;
        private bool similarityUnavailable;
        private readonly HashSet<Guid> feedbackInFlight = new HashSet<Guid>();
        private Guid impressionId;
        private double shownAt;
        private double nextFeedbackFlush;
        private readonly HashSet<int> resolvedSimilaritySets = new HashSet<int>();
        private Box informationBackground = null!;
        private Box previewBackground = null!;
        private Box accent = null!;
        private IBindable<Colour4> themeColour = null!;
        private IBindable<APIUser> localUser = null!;
        private int generation;
        private int cardGeneration;
        private int batch;
        private int userId;
        private int mode;
        private string historyFile = string.Empty;
        private bool loading;
        private bool savedView;
        private double lastFetch = double.NegativeInfinity;
        private float appliedToolbarHeight;

        internal LocalisableString SearchStatus => status.Text;
        internal int ReadyCount => candidates.Count;
        internal bool HasVisibleCard => information.IsPresent || previewPanel.IsPresent;
        internal bool FiltersVisible => filterControls.IsPresent;

        protected virtual bool IsInstalled(APIBeatmapSet set) =>
            library.ContainsSong(set)
            || StablePathManager.IsBeatmapSetAvailableLocally(set.OnlineID)
            || downloader.GetExistingDownload(set) != null
            || beatmaps.QueryBeatmapSet(s => s.OnlineID == set.OnlineID && !s.DeletePending) != null;

        protected virtual Task<IReadOnlyList<DiscoveryNeighbour>> FindSimilarAsync(int seedId, CancellationToken token) => similarity.SearchAsync(seedId, token);

        // Only one next-card clip is prefetched; no archives or collection parsing.
        protected virtual Task<(Beatmap Map, byte[]? Audio)> LoadPreviewAsync(APIBeatmap map, CancellationToken token) => loadMedia(map, token);
        protected virtual Drawable CreateCover(APIBeatmapSet set) => new UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType.Cover)
        {
            RelativeSizeAxes = Axes.Both, OnlineInfo = set,
        };

        private async Task<(Beatmap Map, byte[]? Audio)> loadMedia(APIBeatmap map, CancellationToken token)
        {
            var result = await media.LoadAsync(map, token).ConfigureAwait(false);
            return (result.Map, result.Audio);
        }

        public DiscoveryOverlay() : base(OverlayColourScheme.Purple, ThemeSurfaceContext.Settings) { }
        protected override DiscoveryHeader CreateHeader() => new DiscoveryHeader();

        [BackgroundDependencyLoader]
        private void load()
        {
            if (audioAnalysis != null) audioAnalysis.SettingsChanged += audioSettingsChanged;
            library = new DiscoveryLibrary(realm);
            Header.Add(close = new IconButton
            {
                Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Position = new Vector2(-12, 8), Size = new Vector2(40),
                Icon = FontAwesome.Solid.Times, TooltipText = DiscoveryStrings.Close, Action = Hide,
            });
            Child = root = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Absolute, 68), new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.AutoSize), new Dimension() },
                Content = new[]
                {
                    new Drawable[] { Header },
                    new Drawable[]
                    {
                        controls = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Spacing = new Vector2(12), Padding = new MarginPadding { Horizontal = 24, Vertical = 10 },
                            Children = new Drawable[]
                            {
                                forYouTab = new ThemeLibraryButton { Text = DiscoveryStrings.ForYou, Width = 140, Action = () => switchView(false) },
                                savedTab = new ThemeLibraryButton { Text = DiscoveryStrings.Saved, Width = 160, Action = () => switchView(true) },
                                moreFilters = new ThemeLibraryButton { Text = DiscoveryStrings.Filters, Width = 130, Action = toggleFilters },
                                tasteMode = new ThemeLibraryButton { Text = DiscoveryStrings.TasteMode, Width = 150, Selected = true, Action = () => changeWorkMode(DiscoveryWorkMode.Taste) },
                                gameplayMode = new ThemeLibraryButton { Text = DiscoveryStrings.GameplayMode, Width = 170, Action = () => changeWorkMode(DiscoveryWorkMode.Gameplay) },
                                retrySearch = new ThemeLibraryButton { Text = DiscoveryStrings.Retry, Width = 160, Action = applyFilters },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        filterControls = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Spacing = new Vector2(10), Padding = new MarginPadding { Horizontal = 24, Bottom = 12 },
                            Children = new Drawable[]
                            {
                                seedSource = new FormEnumDropdown<DiscoverySeedSource> { Caption = DiscoveryStrings.SeedSource, Width = 210 },
                                manualReferences = new FormTextBox { Caption = DiscoveryStrings.ExampleMaps, PlaceholderText = DiscoveryStrings.ExampleMapsHint, Width = 420 },
                                new FormSliderBar<double> { Caption = DiscoveryStrings.MinStars, Current = minimumStars, Width = 180, KeyboardStep = 0.25f },
                                new FormSliderBar<double> { Caption = DiscoveryStrings.MaxStars, Current = maximumStars, Width = 180, KeyboardStep = 0.25f },
                                category = new FormDropdown<SearchCategory> { Caption = DiscoveryStrings.MapStatus, Width = 180, Items = new[] { SearchCategory.Any, SearchCategory.Leaderboard, SearchCategory.Ranked, SearchCategory.Qualified, SearchCategory.Loved, SearchCategory.Pending, SearchCategory.Graveyard }, Current = { Value = SearchCategory.Any } },
                                new FormSliderBar<double> { Caption = DiscoveryStrings.MinBpm, Current = minimumBpm, Width = 160, KeyboardStep = 5 },
                                new FormSliderBar<double> { Caption = DiscoveryStrings.MaxBpm, Current = maximumBpm, Width = 160, KeyboardStep = 5 },
                                new FormSliderBar<double> { Caption = DiscoveryStrings.MaxLength, Current = maximumMinutes, Width = 160, KeyboardStep = 1 },
                                new FormEnumDropdown<BeatmapCatalogueSource>
                                {
                                    Caption = DiscoveryStrings.Catalogue,
                                    Width = 180,
                                    Current = { BindTarget = config.GetBindable<BeatmapCatalogueSource>(OsuSetting.BeatmapCatalogueSource) },
                                },
                                new ThemeLibraryButton { Text = DiscoveryStrings.Apply, Width = 140, Action = applyFilters },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Padding = new MarginPadding { Horizontal = 24 },
                            Children = new Drawable[]
                            {
                                status = new TruncatingSpriteText { Font = OsuFont.GetFont(size: 17), RelativeSizeAxes = Axes.X },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        cardScroll = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = body = new Container
                            {
                                RelativeSizeAxes = Axes.X, Height = 550, Padding = new MarginPadding { Bottom = 24 },
                                Children = new Drawable[]
                                {
                                    information = new Container
                                    {
                                        Masking = true, CornerRadius = 8,
                                        Children = new Drawable[]
                                        {
                                            informationBackground = new Box { RelativeSizeAxes = Axes.Both },
                                            coverContent = new Container { RelativeSizeAxes = Axes.X, Height = 150, Masking = true },
                                            accent = new Box { RelativeSizeAxes = Axes.X, Height = 4 },
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Horizontal = 24, Top = 174, Bottom = 24 },
                                                Children = new Drawable[]
                                                {
                                                    details = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(10),
                                                        Children = new Drawable[]
                                                        {
                                                            progress = new OsuSpriteText { Font = OsuFont.GetFont(size: 15) },
                                                            artist = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 23) },
                                                            title = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 34, weight: FontWeight.Bold) },
                                                            difficulty = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 20, weight: FontWeight.SemiBold) },
                                                            mapper = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 17) },
                                                            statistics = new OsuSpriteText { Font = OsuFont.GetFont(size: 20) },
                                                            reason = new TruncatingSpriteText { Font = OsuFont.GetFont(size: 16), RelativeSizeAxes = Axes.X },
                                                        },
                                                    },
                                                    actions = new FillFlowContainer
                                                    {
                                                        Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Spacing = new Vector2(10),
                                                        Children = new Drawable[]
                                                        {
                                                            next = new ThemeLibraryButton { Text = DiscoveryStrings.Next, Width = 130, Action = () => advance(false) },
                                                            save = new ThemeLibraryButton { Text = DiscoveryStrings.Save, Width = 160, Primary = true, Action = () => advance(true) },
                                                            download = new ThemeLibraryButton { Text = DiscoveryStrings.Download, Width = 220, Action = downloadCurrent },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                    previewPanel = new Container
                                    {
                                        Masking = true, CornerRadius = 8,
                                        Children = new Drawable[]
                                        {
                                            previewBackground = new Box { RelativeSizeAxes = Axes.Both },
                                            previewContent = new Container { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Bottom = 104 } },
                                            previewStatus = new TruncatingSpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 16), RelativeSizeAxes = Axes.X, Width = 0.9f },
                                            previewControls = new FillFlowContainer
                                            {
                                                Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Padding = new MarginPadding(8), Spacing = new Vector2(6),
                                                Children = new Drawable[]
                                                {
                                                    pause = new ThemeLibraryButton { Text = DiscoveryStrings.Pause, Width = 110, Height = 36, Action = () => { preview?.TogglePlayback(); pause.Text = preview?.Playing == true ? DiscoveryStrings.Pause : DiscoveryStrings.Play; } },
                                                    repeat = new ThemeLibraryButton { Text = DiscoveryStrings.Repeat, Width = 110, Height = 36, Action = () => preview?.Seek(0) },
                                                    fullPreview = new ThemeLibraryButton { Text = DiscoveryStrings.FullPreview, Width = 240, Height = 36, Action = requestFullPreview },
                                                    new ThemeLibraryButton { Text = "−10 s", Width = 80, Height = 32, Action = () => preview?.Seek(preview.ClipPosition - 10000) },
                                                    new ThemeLibraryButton { Text = "+10 s", Width = 80, Height = 32, Action = () => preview?.Seek(preview.ClipPosition + 10000) },
                                                    playbackTime = new OsuSpriteText { Font = OsuFont.GetFont(size: 16) },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
            filterControls.Hide();
            themeColour = ColourProvider.GetColourBindable(OverlayColour.Background2);
            themeColour.BindValueChanged(_ => updateTheme(), true);
            toolbarRuleset = selectedRuleset.GetBoundCopy();
            toolbarRuleset.BindValueChanged(_ => searchContextChanged());
            localUser = api.LocalUser.GetBoundCopy();
            localUser.BindValueChanged(_ => searchContextChanged());
            catalogueSource = config.GetBindable<BeatmapCatalogueSource>(OsuSetting.BeatmapCatalogueSource);
            catalogueSource.BindValueChanged(_ => searchContextChanged());
            keepSearchingInBackground = config.GetBindable<bool>(OsuSetting.ForkAiAudioBackgroundSearch);
            keepSearchingInBackground.BindValueChanged(change =>
            {
                if (!change.NewValue && backgroundAudioSearch && State.Value != Visibility.Visible) cancelWork();
            });
            clearCard();
        }

        private void updateTheme()
        {
            informationBackground.Colour = ColourProvider.Background3;
            previewBackground.Colour = ColourProvider.Background6;
            accent.Colour = ColourProvider.Colour2;
            close.IconColour = ColourProvider.Content1;
            close.IconHoverColour = ColourProvider.Colour1;
            title.Colour = ColourProvider.Content1;
            artist.Colour = difficulty.Colour = statistics.Colour = ColourProvider.Content2;
            mapper.Colour = reason.Colour = progress.Colour = status.Colour = previewStatus.Colour = playbackTime.Colour = ColourProvider.Content2;
        }

        protected override void PopIn()
        {
            base.PopIn();
            musicSilence ??= previewManager.SilenceMainTrack();
            filtersExpanded = false; moreFilters.Selected = false; filterControls.Hide();
            if (canContinueInBackground)
            {
                if (!savedView && profile != null) { updateFeedback(); showNext(); }
                else if (savedView) switchView(true);
                scheduleRefill();
            }
            else restart();
        }

        protected override void PopOut()
        {
            if (canContinueInBackground)
            {
                // Closing Discovery always stops playback; only the opted-in job continues.
                if (!savedView && current != null) candidatePool.RemoveAll(s => s.OnlineID == current.Map.OnlineBeatmapSetID);
                if (!savedView) clearCard();
                else stopPreview();
                prefetchDelay?.Cancel();
                prefetchCancellation?.Cancel(); prefetchCancellation?.Dispose(); prefetchCancellation = null;
                prefetched = null; activeMedia = null;
            }
            else cancelWork();
            musicSilence?.Dispose(); musicSilence = null;
            base.PopOut();
        }

        private void audioSettingsChanged() => searchScheduler.Add(searchContextChanged, false);

        private void searchContextChanged()
        {
            if (IsDisposed) return;
            if (State.Value == Visibility.Visible) restart();
            else if (backgroundAudioSearch) cancelWork();
        }

        private void cancelWork()
        {
            backgroundAudioSearch = false;
            audioCancellation?.Cancel(); audioCancellation?.Dispose(); audioCancellation = null;
            audioLoading = audioFailed = false; audioWeights.Clear(); audioAttempted.Clear(); pendingAudioMaps = 0;
            generation++;
            similarityCancellation?.Cancel(); similarityCancellation?.Dispose(); similarityCancellation = null;
            similarityLoading = false;
            foreach (var request in requests.ToArray()) request.Cancel();
            requests.Clear();
            loading = false; tasteLookups = 0;
            refill?.Cancel(); fullDownloadWatch?.Cancel();
            prefetchDelay?.Cancel();
            prefetchCancellation?.Cancel(); prefetchCancellation?.Dispose(); prefetchCancellation = null;
            prefetched = null; activeMedia = null;
            stopPreview();
        }

        private bool isCurrent(int version) => !IsDisposed && canRunSearch && generation == version && api.LocalUser.Value.Id == userId;

        private void restart()
        {
            cancelWork();
            clearCard();
            candidates.Clear(); candidatePool.Clear(); installedSets.Clear(); exhaustedSearches.Clear();
            sessionSeen.Clear(); recentlyShown.Clear(); profile = null; batch = rounds = 0; lastArtist = lastMapper = null;
            filters = null; catalogueFailed = catalogueHadError = false;
            savedView = false; forYouTab.Selected = true; savedTab.Selected = false;
            userId = api.LocalUser.Value.Id;
            mode = selectedRuleset.Value?.OnlineID ?? 0;
            gameplayMode.Enabled.Value = mode == 0;
            gameplayMode.Text = DiscoveryStrings.GameplayMode;
            if (mode != 0) { workMode = DiscoveryWorkMode.Taste; tasteMode.Selected = true; gameplayMode.Selected = false; }
            neighbours.Clear(); gameplayWeights.Clear(); queriedSeeds.Clear(); resolvedSimilaritySets.Clear(); similarityUnavailable = false;
            resolvedTasteSets.Clear();
            historyFile = DiscoveryHistory.Filename(api.Endpoints.APIUrl, userId, mode);
            history = DiscoveryHistory.Load(storage, historyFile);
            flushFeedback();
            seedSource.Current.Value = activeSeedSource = history.SeedSource;
            manualReferences.Current.Value = appliedReferences = history.ManualReferences;
            topScores = Array.Empty<SoloScoreInfo>(); frequentMaps = Array.Empty<APIBeatmap>(); manualMaps.Clear();
            profileGeneration++;
            foreach (var recent in history.Recent) recentlyShown.Enqueue(recent);
            lastArtist = history.Recent.LastOrDefault()?.Artist;
            lastMapper = history.Recent.LastOrDefault()?.AuthorString;
            searchCursors.Clear(); searchSources.Clear();
            if (userId <= 1) { status.Text = DiscoveryStrings.Login; return; }
            if (mode is < 0 or > 3) { status.Text = DiscoveryStrings.Unsupported; return; }
            backgroundAudioSearch = audioSearch;
            status.Text = DiscoveryStrings.Loading;
            int version = generation;
            if (activeSeedSource == DiscoverySeedSource.Manual) { rebuildProfile(); resolveManual(true); return; }
            var request = new GetUserScoresRequest(userId, ScoreType.Best, new PaginationParameters(50), rulesets.GetRuleset(mode)!);
            requests.Add(request);
            request.Success += scores =>
            {
                if (!isCurrent(version)) return;
                requests.Remove(request);
                topScores = scores.ToArray();
                loadFrequent(version);
            };
            request.Failure += _ =>
            {
                if (!isCurrent(version)) return;
                requests.Remove(request);
                loadFrequent(version);
            };
            api.Queue(request);
        }

        private void loadFrequent(int version)
        {
            var request = new GetUserMostPlayedBeatmapsRequest(userId, new PaginationParameters(100));
            requests.Add(request);
            bool completed = false;
            ScheduledDelegate? timeout = null;
            timeout = searchScheduler.AddDelayed(() => { if (isCurrent(version)) { request.Cancel(); finish(null); } }, 10000);
            request.Success += items => finish(items);
            request.Failure += _ => finish(null);
            api.Queue(request);
            void finish(List<APIUserMostPlayedBeatmap>? items)
            {
                if (completed || !isCurrent(version)) return;
                completed = true; timeout?.Cancel(); requests.Remove(request);
                frequentMaps = (items ?? new List<APIUserMostPlayedBeatmap>()).Where(i => i != null && i.PlayCount > 0 && i.BeatmapSet != null)
                    .Select(i =>
                    {
                        var map = i.BeatmapInfo;
                        if (map != null) map.UserPlayCount = i.PlayCount;
                        return map;
                    }).OfType<APIBeatmap>().Where(m => m.RulesetID == mode).ToArray();
                rebuildProfile();
                minimumStars.Value = profile!.DefaultFilters.MinimumStars;
                maximumStars.Value = profile.DefaultFilters.MaximumStars;
                filters = currentFilters();
                if (activeSeedSource == DiscoverySeedSource.Manual) resolveManual(true);
                else loadTaste(version);
            }
        }

        private void rebuildProfile()
        {
            profile = new DiscoveryRecommendations(topScores, mode, frequentMaps, manualMaps, activeSeedSource);
            profile.UpdateFeedback(history.Saved);
        }

        private IEnumerable<APIBeatmap> similaritySeeds() => activeSeedSource == DiscoverySeedSource.Manual
            ? profile?.Seeds ?? Array.Empty<APIBeatmap>()
            : DiscoverySimilarity.Seeds(profile?.Seeds ?? Array.Empty<APIBeatmap>(), history.Saved);

        private void resolveManual(bool loadServerTaste)
        {
            if (!DiscoverySeedSelection.TryParse(appliedReferences, out var references))
            {
                status.Text = DiscoveryStrings.InvalidExamples;
                return;
            }
            int version = generation;
            int selection = ++profileGeneration;
            int remaining = references.Length;
            bool failed = false;
            var resolved = new Dictionary<DiscoveryReference, APIBeatmap>();
            status.Text = DiscoveryStrings.Loading;
            foreach (var reference in references)
            {
                APIRequest request;
                if (reference.IsSet)
                {
                    var lookup = new GetBeatmapSetRequest(reference.Id);
                    lookup.Success += set => finish(lookup, reference, DiscoverySeedSelection.Resolve(reference, set, mode, profile?.TargetStars ?? 2));
                    request = lookup;
                }
                else
                {
                    var lookup = new GetBeatmapRequest(reference.Id);
                    lookup.Success += map => finish(lookup, reference, map);
                    request = lookup;
                }
                requests.Add(request);
                bool completed = false;
                ScheduledDelegate? timeout = null;
                timeout = searchScheduler.AddDelayed(() => { if (isCurrent(version)) { request.Cancel(); complete(null); } }, 10000);
                request.Failure += _ => complete(null);
                if (request is GetBeatmapSetRequest setRequest) setRequest.Success += _ => complete(resolved.GetValueOrDefault(reference));
                if (request is GetBeatmapRequest mapRequest) mapRequest.Success += _ => complete(resolved.GetValueOrDefault(reference));
                beatmapApi.Queue(request);
                void complete(APIBeatmap? map)
                {
                    if (completed) return;
                    completed = true; timeout?.Cancel(); requests.Remove(request);
                    if (!isCurrent(version) || selection != profileGeneration) return;
                    if (map == null) failed = true;
                    if (--remaining > 0) return;
                    if (failed) { status.Text = DiscoveryStrings.ExamplesUnavailable; return; }
                    manualMaps.Clear();
                    manualMaps.AddRange(references.Select(r => resolved[r]));
                    rebuildProfile();
                    minimumStars.Value = profile!.DefaultFilters.MinimumStars;
                    maximumStars.Value = profile.DefaultFilters.MaximumStars;
                    filters = currentFilters();
                    if (loadServerTaste) loadTaste(version);
                    else fetchBatch();
                }
            }
            void finish(APIRequest request, DiscoveryReference reference, APIBeatmap? map)
            {
                if (!isCurrent(version) || selection != profileGeneration || !requests.Contains(request)) return;
                if (map?.BeatmapSet == null || map.RulesetID != mode || map.OnlineID <= 0 || map.OnlineID >= 2_000_000_000
                    || (!reference.IsSet && map.OnlineID != reference.Id)) return;
                resolved[reference] = map;
            }
        }

        private readonly HashSet<int> resolvedTasteSets = new HashSet<int>();
        private int tasteLookups;
        private void loadTaste(int version)
        {
            var request = new DiscoveryTasteRequest();
            requests.Add(request);
            bool completed = false;
            ScheduledDelegate? timeout = null;
            timeout = searchScheduler.AddDelayed(() => { if (isCurrent(version)) { request.Cancel(); finish(null); } }, 10000);
            request.Success += result => finish(result);
            request.Failure += _ => finish(null);
            api.Queue(request);
            void finish(DiscoveryTasteProfile? result)
            {
                if (completed || !isCurrent(version)) return;
                completed = true; timeout?.Cancel(); requests.Remove(request);
                if (result != null) profile!.SetTaste(result);
                lastFetch = double.NegativeInfinity;
                fetchBatch();
            }
        }

        private void fetchTasteCandidates()
        {
            if (workMode != DiscoveryWorkMode.Taste || profile == null || tasteLookups > 0) return;
            int version = generation;
            var selected = profile.Taste.Candidates.Where(c => !resolvedTasteSets.Contains(c.SetId)
                && !profile.KnownSets.Contains(c.SetId) && !history.Seen.Contains(c.SetId) && !history.Skipped.Contains(c.SetId)).Take(8).ToArray();
            tasteLookups = selected.Length;
            foreach (var candidate in selected)
            {
                resolvedTasteSets.Add(candidate.SetId);
                var request = new GetBeatmapSetRequest(candidate.SetId);
                requests.Add(request);
                ScheduledDelegate? timeout = null;
                bool completed = false;
                timeout = searchScheduler.AddDelayed(() => { if (isCurrent(version)) { request.Cancel(); complete(); } }, 10000);
                request.Success += set =>
                {
                    if (!isCurrent(version)) return;
                    addCatalogueCandidate(set);
                    updateFeedback();
                    scheduleRefill();
                    complete();
                };
                request.Failure += _ => complete();
                api.Queue(request);
                void complete()
                {
                    if (completed) return;
                    completed = true; timeout?.Cancel(); requests.Remove(request);
                    if (!isCurrent(version)) return;
                    tasteLookups--;
                    if (tasteLookups == 0 && canShowRecommendation) showNext();
                }
            }
        }

        private void fetchBatch()
        {
            if (profile == null || loading || rounds >= roundLimit || audioSearch && !needsCatalogueRefill
                || !canRunSearch || catalogueTime - lastFetch < 3000) return;
            lastFetch = catalogueTime;
            loading = true;
            catalogueFailed = false;
            retrySearch.Enabled.Value = false;
            if (current == null && !audioLoading) status.Text = DiscoveryStrings.Loading;
            int version = generation;
            rounds++;
            if (workMode == DiscoveryWorkMode.Gameplay) fetchSimilar();
            else fetchTasteCandidates();
            var queries = catalogueQueries(batch++).ToArray();
            int pending = queries.Length;
            int successful = 0;
            bool failed = false;
            var sets = new List<APIBeatmapSet>();
            foreach (var (query, genre, searchKey) in queries)
            {
                var request = new SearchBeatmapSetsRequest(query, rulesets.GetRuleset(mode)!,
                    cursor: searchCursors.GetValueOrDefault(searchKey),
                    searchCategory: filters?.Category ?? SearchCategory.Any, sortCriteria: SortCriteria.Updated,
                    sortDirection: SortDirection.Descending, genre: genre)
                {
                    CatalogueSource = searchSources.GetValueOrDefault(searchKey, source()),
                };
                requests.Add(request);
                request.Success += response =>
                {
                    if (!isCurrent(version)) return;
                    successful++;
                    searchCursors[searchKey] = response.Cursor;
                    searchSources[searchKey] = request.ActualCatalogueSource ?? request.CatalogueSource;
                    // A locally filtered page may be empty while raw catalogue pages remain.
                    if (response.Cursor == null) exhaustedSearches.Add(searchKey);
                    sets.AddRange(response.BeatmapSets);
                    complete(request);
                };
                request.Failure += error =>
                {
                    if (!isCurrent(version)) return;
                    failed = true;
                    exhaustedSearches.Add(searchKey); // Other queries/cursors can still progress.
                    Logger.Log($"Discovery catalogue source={request.CatalogueSource}: {error.Message}", LoggingTarget.Network);
                    complete(request);
                };
                beatmapApi.Queue(request);
            }
            if (queries.Length == 0) finish();

            void complete(APIRequest request)
            {
                requests.Remove(request);
                if (--pending == 0) finish();
            }

            void finish()
            {
                loading = false;
                catalogueHadError |= failed;
                catalogueFailed = failed && successful == 0 && !canContinueSearch();
                retrySearch.Enabled.Value = true;
                foreach (var set in sets.DistinctBy(s => s.OnlineID)) addCatalogueCandidate(set);
                updateFeedback();
                if (canShowRecommendation && (tasteLookups == 0 || audioSearch) && !(workMode == DiscoveryWorkMode.Gameplay && similarityLoading)) showNext();
                if (State.Value == Visibility.Visible) prefetchNext();
                scheduleRefill();
            }
        }

        private IEnumerable<(string Query, SearchGenre Genre, string Key)> catalogueQueries(int queryBatch)
        {
            if (profile == null) yield break;
            string range = (filters ?? profile.DefaultFilters).Query;
            foreach (string query in profile.Queries(queryBatch, filters, workMode))
            {
                // The automatic genre preference is only supported by the official API.
                // BPM, stars, length, mode and category still apply to mirror results.
                var genre = source() == BeatmapCatalogueSource.Official && workMode == DiscoveryWorkMode.Taste && query == range
                    ? profile.Taste.Genre(queryBatch) : SearchGenre.Any;
                string key = query + "|" + genre;
                if (!exhaustedSearches.Contains(key)) yield return (query, genre, key);
            }
        }

        private bool canContinueSearch() => rounds < roundLimit && profile != null
            && (workMode == DiscoveryWorkMode.Gameplay && (hasSimilaritySeeds() || hasSimilarityLookups())
                || workMode == DiscoveryWorkMode.Taste && profile.Taste.Candidates.Any(c => !resolvedTasteSets.Contains(c.SetId)
                    && !profile.KnownSets.Contains(c.SetId) && !history.Seen.Contains(c.SetId) && !history.Skipped.Contains(c.SetId))
                || Enumerable.Range(batch, audioSearch ? roundLimit - rounds : 1).Any(b => catalogueQueries(b).Any()));

        private void scheduleRefill()
        {
            if (!canRunSearch || loading || catalogueFailed || !needsCatalogueRefill || !canContinueSearch()
                || refill is { Completed: false, Cancelled: false }) return;
            // The host scheduler keeps running while the graphical overlay is hidden.
            refill = searchScheduler.AddDelayed(() => { refill = null; fetchBatch(); }, Math.Max(1, 3100 - (catalogueTime - lastFetch)));
        }

        private void addCatalogueCandidate(APIBeatmapSet set)
        {
            if (candidatePool.Count >= catalogue_pool_limit || set.OnlineID <= 0 || set.HasExplicitContent || history.HasSeen(set)
                || candidatePool.Any(s => s.OnlineID == set.OnlineID)) return;
            if (audioSearch && (audioAttempted.Contains(set.OnlineID) || profile!.KnownSets.Contains(set.OnlineID)
                || !set.Beatmaps.Any(m => m.RulesetID == mode && (filters ?? profile.DefaultFilters).Matches(m)))) return;
            if (IsInstalled(set)) { installedSets.Add(set.OnlineID); return; }
            if (candidatePool.Count(s => string.Equals(s.Artist, set.Artist, StringComparison.OrdinalIgnoreCase)) < 6
                && candidatePool.Count(s => string.Equals(s.AuthorString, set.AuthorString, StringComparison.OrdinalIgnoreCase)) < 8)
                candidatePool.Add(set);
        }

        private bool hasSimilaritySeeds() => mode == 0 && !similarityUnavailable && profile != null
            && similaritySeeds().Any(s => !queriedSeeds.Contains(s.OnlineID));

        private bool hasSimilarityLookups() => mode == 0 && profile != null && neighbours.Any(n => !resolvedSimilaritySets.Contains(n.SetId)
            && !profile.KnownSets.Contains(n.SetId) && !history.Seen.Contains(n.SetId) && !history.Skipped.Contains(n.SetId));

        private void fetchSimilar()
        {
            if (similarityLoading || !hasSimilaritySeeds() && !hasSimilarityLookups()) return;
            int version = generation;
            var selected = similaritySeeds().Where(s => !queriedSeeds.Contains(s.OnlineID)).Take(2).ToArray();
            queriedSeeds.UnionWith(selected.Select(s => s.OnlineID));
            similarityLoading = true;
            similarityCancellation?.Dispose();
            var cancellation = similarityCancellation = new CancellationTokenSource();
            _ = search();
            async Task search()
            {
                try
                {
                    var results = await Task.WhenAll(selected.Select(s => FindSimilarAsync(s.OnlineID, cancellation.Token))).ConfigureAwait(false);
                    Schedule(() =>
                    {
                        if (!isCurrent(version)) return;
                        neighbours.AddRange(results.SelectMany(r => r));
                        var merged = DiscoverySimilarity.Merge(neighbours);
                        foreach (var n in merged) gameplayWeights[n.MapId] = n.Similarity;
                        // Fetch authoritative catalogue metadata, never fabricate preview anchors or checksums from similarity output.
                        var lookup = merged.Where(n => !resolvedSimilaritySets.Contains(n.SetId) && !profile.KnownSets.Contains(n.SetId)
                            && !history.Seen.Contains(n.SetId) && !history.Skipped.Contains(n.SetId)).DistinctBy(n => n.SetId).Take(8).ToArray();
                        int remaining = lookup.Length;
                        var pendingLookups = new HashSet<APIRequest>();
                        ScheduledDelegate? timeout = null;
                        timeout = Scheduler.AddDelayed(() =>
                        {
                            if (!isCurrent(version) || remaining == 0) return;
                            foreach (var request in pendingLookups.ToArray()) { request.Cancel(); complete(request); }
                        }, 10000);
                        similarityLoading = remaining > 0;
                        if (remaining == 0 && !savedView && current == null) { updateFeedback(); showNext(); }
                        foreach (var n in lookup)
                        {
                            resolvedSimilaritySets.Add(n.SetId);
                            var request = new GetBeatmapSetRequest(n.SetId);
                            requests.Add(request);
                            pendingLookups.Add(request);
                            request.Success += set =>
                            {
                                if (!isCurrent(version)) return;
                                if (!history.HasSeen(set) && !IsInstalled(set) && candidatePool.Count < 240 && candidatePool.All(s => s.OnlineID != set.OnlineID)
                                    && candidatePool.Count(s => string.Equals(s.Artist, set.Artist, StringComparison.OrdinalIgnoreCase)) < 6
                                    && candidatePool.Count(s => string.Equals(s.AuthorString, set.AuthorString, StringComparison.OrdinalIgnoreCase)) < 8)
                                    candidatePool.Add(set);
                                updateFeedback();
                                if (!savedView && current == null && candidates.Any(c => c.Reason == DiscoveryReason.Gameplay)) showNext();
                                complete(request);
                            };
                            request.Failure += _ => { if (isCurrent(version)) complete(request); };
                            // Mosu's metadata lookup does not require an official account. Catalogue search selection is unchanged.
                            api.Queue(request);
                        }
                        void complete(APIRequest request)
                        {
                            if (!pendingLookups.Remove(request)) return;
                            requests.Remove(request);
                            if (--remaining != 0) return;
                            timeout?.Cancel();
                            similarityLoading = false;
                            updateFeedback();
                            if (!savedView && current == null) showNext();
                            prefetchNext();
                            scheduleRefill();
                        }
                    });
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception e)
                {
                    Logger.Log($"Discovery similarity unavailable, using catalogue: {e.Message}", LoggingTarget.Network);
                    Schedule(() => { if (isCurrent(version)) { similarityLoading = false; similarityUnavailable = true; if (current == null && !savedView) showNext(); } });
                }
            }
        }

        private void recordFeedback(string action)
        {
            if (current == null || userId <= 1 || api.LocalUser.Value.Id != userId) return;
            var map = current.Map;
            history.PendingFeedback.Add(new DiscoveryFeedbackEvent
            {
                ImpressionId = impressionId, BeatmapId = map.OnlineID, BeatmapSetId = map.OnlineBeatmapSetID,
                Ruleset = mode, Action = action, WorkMode = savedView ? "saved" : workMode == DiscoveryWorkMode.Gameplay ? "gameplay" : "taste",
                Source = current.Reason == DiscoveryReason.Gameplay ? "mapmatcher" : current.Reason == DiscoveryReason.Listeners ? "mosu_listeners" : "catalogue",
                AlgorithmVersion = current.Reason == DiscoveryReason.Audio ? "clap-music-v1" : workMode == DiscoveryWorkMode.Taste ? "music-v2" : "hybrid-v1",
                SeedIds = neighbours.Where(n => n.MapId == map.OnlineID).Select(n => n.SeedId).Distinct().Take(24).ToArray(),
                ElapsedMs = (long)Math.Clamp(Time.Current - shownAt, 0, 86400000),
            });
            history.PendingFeedback = history.PendingFeedback.TakeLast(256).ToList();
            history.Save(storage, historyFile);
            flushFeedback();
        }

        private void flushFeedback()
        {
            if (userId <= 1 || api.LocalUser.Value.Id != userId || string.IsNullOrEmpty(historyFile)) return;
            nextFeedbackFlush = Time.Current + 30000;
            history.PendingFeedback.RemoveAll(e => e.OccurredAt < DateTimeOffset.UtcNow.AddDays(-7));
            var owner = history;
            string file = historyFile;
            foreach (var item in owner.PendingFeedback.Where(e => !feedbackInFlight.Contains(e.EventId)).Take(Math.Max(0, 4 - feedbackInFlight.Count)).ToArray())
            {
                feedbackInFlight.Add(item.EventId);
                var request = new DiscoveryFeedbackRequest(item, userId, api.Endpoints.APIUrl);
                request.Success += () =>
                {
                    feedbackInFlight.Remove(item.EventId);
                    owner.PendingFeedback.RemoveAll(e => e.EventId == item.EventId);
                    if (ReferenceEquals(owner, history)) owner.Save(storage, file);
                    else
                    {
                        var persisted = DiscoveryHistory.Load(storage, file);
                        persisted.PendingFeedback.RemoveAll(e => e.EventId == item.EventId);
                        persisted.Save(storage, file);
                    }
                };
                request.Failure += _ => feedbackInFlight.Remove(item.EventId);
                api.Queue(request);
            }
        }

        private void changeWorkMode(DiscoveryWorkMode value)
        {
            if (workMode == value) return;
            if (value == DiscoveryWorkMode.Gameplay && mode != 0) { status.Text = DiscoveryStrings.GameplayOsuOnly; return; }
            workMode = value;
            tasteMode.Selected = value == DiscoveryWorkMode.Taste;
            gameplayMode.Selected = value == DiscoveryWorkMode.Gameplay;
            applyFilters();
        }

        private void toggleFilters()
        {
            filtersExpanded = !filtersExpanded;
            moreFilters.Selected = filtersExpanded;
            if (filtersExpanded) filterControls.Show(); else filterControls.Hide();
        }

        private DiscoveryFilters currentFilters() => new DiscoveryFilters(Math.Min(minimumStars.Value, maximumStars.Value), Math.Max(minimumStars.Value, maximumStars.Value),
            category.Current.Value, Math.Min(minimumBpm.Value, maximumBpm.Value), Math.Max(minimumBpm.Value, maximumBpm.Value), maximumMinutes.Value * 60000);

        private void applyFilters()
        {
            if (profile == null) { restart(); return; }
            bool changed = activeSeedSource != seedSource.Current.Value || appliedReferences != manualReferences.Current.Value;
            if (seedSource.Current.Value == DiscoverySeedSource.Manual && !DiscoverySeedSelection.TryParse(manualReferences.Current.Value, out _))
            {
                status.Text = DiscoveryStrings.InvalidExamples;
                return;
            }
            activeSeedSource = seedSource.Current.Value;
            appliedReferences = manualReferences.Current.Value;
            history.SeedSource = activeSeedSource; history.ManualReferences = appliedReferences;
            if (!history.Save(storage, historyFile)) { status.Text = DiscoveryStrings.SaveFailed; return; }
            cancelWork(); clearCard();
            backgroundAudioSearch = audioSearch;
            filtersExpanded = false; moreFilters.Selected = false; filterControls.Hide();
            candidates.Clear(); candidatePool.Clear(); searchCursors.Clear(); searchSources.Clear(); exhaustedSearches.Clear();
            neighbours.Clear(); gameplayWeights.Clear(); queriedSeeds.Clear(); resolvedSimilaritySets.Clear(); similarityUnavailable = false;
            resolvedTasteSets.Clear();
            rounds = batch = 0; catalogueFailed = catalogueHadError = false; filters = currentFilters();
            lastFetch = double.NegativeInfinity;
            savedView = false; forYouTab.Selected = true; savedTab.Selected = false;
            if (changed)
            {
                rebuildProfile();
                minimumStars.Value = profile!.DefaultFilters.MinimumStars;
                maximumStars.Value = profile.DefaultFilters.MaximumStars;
                filters = currentFilters();
                if (activeSeedSource == DiscoverySeedSource.Manual) { resolveManual(true); return; }
                if (topScores.Length == 0 && frequentMaps.Length == 0) { restart(); return; }
                loadTaste(generation);
                return;
            }
            fetchBatch();
        }

        private IBindable<BeatmapCatalogueSource> catalogueSource = null!;
        private BeatmapCatalogueSource source() => catalogueSource.Value;

        private void switchView(bool saved)
        {
            savedView = saved;
            savedIndex = 0;
            forYouTab.Selected = !saved; savedTab.Selected = saved;
            clearCard();
            if (saved)
            {
                var set = history.Saved.LastOrDefault();
                var map = set?.Beatmaps.Where(m => m.RulesetID == mode).OrderBy(m => Math.Abs(m.StarRating - (profile?.TargetStars ?? 2))).FirstOrDefault();
                if (map == null) { status.Text = DiscoveryStrings.EmptySaved; return; }
                show(new DiscoveryCandidate(map, DiscoveryReason.Similar, 0));
            }
            else showNext();
        }

        private int savedIndex;
        private void advance(bool keep, string? action = null)
        {
            if (current == null) return;
            var set = current.Map.BeatmapSet!;
            if (savedView)
            {
                if (history.Saved.Count == 0) return;
                savedIndex = (savedIndex + 1) % history.Saved.Count;
                var nextMap = history.Saved[history.Saved.Count - 1 - savedIndex].Beatmaps.FirstOrDefault(m => m.RulesetID == mode);
                if (nextMap != null) show(new DiscoveryCandidate(nextMap, DiscoveryReason.Similar, 0));
                return;
            }
            sessionSeen.Add(set.OnlineID);
            lastArtist = set.Artist; lastMapper = set.AuthorString;
            candidatePool.RemoveAll(s => s.OnlineID == set.OnlineID);
            candidates.RemoveAll(c => c.Map.OnlineBeatmapSetID == set.OnlineID);
            recordFeedback(action ?? (keep ? "save" : "skip"));
            if (keep) history.Like(set, current.Map.OnlineID);
            else history.Skipped.Add(set.OnlineID);
            updateFeedback();
            bool persisted = history.Save(storage, historyFile);
            showNext();
            if (!persisted) status.Text = DiscoveryStrings.SaveFailed;
        }

        private void showNext()
        {
            clearCard();
            while (candidates.Count > 0)
            {
                var candidate = candidates[0]; candidates.RemoveAt(0);
                if (history.HasSeen(candidate.Map.BeatmapSet!)) continue;
                if (IsInstalled(candidate.Map.BeatmapSet!))
                {
                    installedSets.Add(candidate.Map.OnlineBeatmapSetID);
                    candidatePool.RemoveAll(s => s.OnlineID == candidate.Map.OnlineBeatmapSetID);
                    continue;
                }
                show(candidate);
                scheduleRefill();
                return;
            }
            bool searching = similarityLoading || tasteLookups > 0 || !catalogueFailed && (loading || needsCatalogueRefill && canContinueSearch());
            status.Text = audioSearch && audioLoading ? AudioRankingStatus : audioSearch && audioFailed ? DiscoveryStrings.AudioFailed
                : searching ? DiscoveryStrings.Loading : catalogueHadError ? (source() == BeatmapCatalogueSource.Official && beatmapApi.OfficialApi?.IsAvailable != true
                    ? DiscoveryStrings.OfficialUnavailable : DiscoveryStrings.Failed)
                : audioSearch && audioAttempted.Count > 0 ? DiscoveryStrings.AudioQueueEmpty : DiscoveryStrings.Empty;
            scheduleRefill();
        }

        private void show(DiscoveryCandidate candidate)
        {
            stopPreview();
            information.Show(); previewPanel.Show();
            current = candidate;
            var map = candidate.Map;
            var set = map.BeatmapSet!;
            if (!savedView)
            {
                sessionSeen.Add(set.OnlineID);
                history.RememberShown(set);
                recentlyShown.Enqueue(set);
                while (recentlyShown.Count > 12) recentlyShown.Dequeue();
            }
            coverContent.Child = CreateCover(set);
            title.Text = set.Title;
            artist.Text = set.Artist;
            difficulty.Text = map.DifficultyName;
            mapper.Text = set.AuthorString;
            statistics.Text = FormattableString.Invariant($"{map.StarRating:0.00}★   {map.BPM:0} BPM   {TimeSpan.FromMilliseconds(map.Length):m\\:ss}");
            reason.Text = candidate.Reason switch { DiscoveryReason.Audio => DiscoveryStrings.AudioSimilar, DiscoveryReason.Gameplay => DiscoveryStrings.GameplaySimilar, DiscoveryReason.Listeners => DiscoveryStrings.Listeners, DiscoveryReason.Music => DiscoveryStrings.Music, DiscoveryReason.Artist => DiscoveryStrings.Artist, DiscoveryReason.Mapper => DiscoveryStrings.Mapper, _ => DiscoveryStrings.Similar };
            impressionId = Guid.NewGuid();
            shownAt = Time.Current;
            recordFeedback("show");
            progress.Text = DiscoveryStrings.Progress(candidates.Count + 1, history.Saved.Count);
            next.Enabled.Value = download.Enabled.Value = true;
            save.Enabled.Value = !savedView;
            status.Text = !savedView && !history.Save(storage, historyFile) ? DiscoveryStrings.SaveFailed : string.Empty;
            previewStatus.Text = DiscoveryStrings.PreviewLoading;
            previewStatus.Show(); previewControls.Show();
            pause.Enabled.Value = repeat.Enabled.Value = false;
            fullPreview.Enabled.Value = true;
            int version = ++cardGeneration;
            var cancellation = mediaCancellation = new CancellationTokenSource();
            activeMedia = prefetched is { } ready && ready.Id == map.OnlineID ? ready.Task : null;
            if (activeMedia != null) { activePrefetchCancellation = prefetchCancellation; prefetchCancellation = null; prefetched = null; }
            activeMediaId = map.OnlineID;
            previewDelay = Scheduler.AddDelayed(() => _ = preparePreview(map, version, cancellation.Token), 200);
            prefetchNext();
        }

        private async Task preparePreview(APIBeatmap map, int version, CancellationToken token)
        {
            try
            {
                var task = activeMediaId == map.OnlineID && activeMedia != null ? activeMedia : LoadPreviewAsync(map, token);
                var result = await task.WaitAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await nativePreparation.WaitAsync(token).ConfigureAwait(false);
                var component = new DiscoveryGameplayPreview(result.Map, result.Audio, rulesets.GetRuleset(mode)!, token);
                try
                {
                    // Only one native conversion/replay/load can be in flight. The replay API has no
                    // cancellation argument, so check around it as well as inside conversion/loading.
                    await Task.Run(() => { token.ThrowIfCancellationRequested(); LoadComponent(component); }, token).ConfigureAwait(false);
                    lock (pendingLock)
                    {
                        token.ThrowIfCancellationRequested();
                        if (IsDisposed) throw new OperationCanceledException(token);
                        pendingPreviews.Add(component);
                        Schedule(() =>
                        {
                            lock (pendingLock)
                                if (!pendingPreviews.Remove(component)) return;
                            if (!validCard(version, token)) { component.Dispose(); return; }
                            attachPreview(component);
                        });
                    }
                }
                catch { component.Dispose(); throw; }
                finally { nativePreparation.Release(); }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception e)
            {
                Logger.Log($"Discovery preview map={map.OnlineID} set={map.OnlineBeatmapSetID}: {e.Message}", LoggingTarget.Network);
                Schedule(() => { if (validCard(version, token)) previewStatus.Text = DiscoveryStrings.MediaFailed; });
            }
        }

        private void attachPreview(DiscoveryGameplayPreview component)
        {
            previewContent.Child = preview = component;
            if (component.HasGameplay) previewStatus.Hide();
            else { previewStatus.Text = DiscoveryStrings.NoAnchor; previewStatus.Show(); }
            previewControls.Show(); pause.Text = DiscoveryStrings.Pause;
            pause.Enabled.Value = repeat.Enabled.Value = true;
            fullPreview.Enabled.Value = !component.FullAudio;
        }

        private async Task<(Beatmap Map, byte[]? Audio)> fetchPreview(APIBeatmap map, CancellationToken token) =>
            await LoadPreviewAsync(map, token).ConfigureAwait(false);

        private void prefetchNext()
        {
            prefetchDelay?.Cancel();
            prefetchDelay = Scheduler.AddDelayed(beginPrefetch, 250);
        }

        private void beginPrefetch()
        {
            var nextMap = candidates.FirstOrDefault()?.Map;
            if (nextMap == null || prefetched?.Id == nextMap.OnlineID || State.Value != Visibility.Visible) return;
            prefetchCancellation?.Cancel(); prefetchCancellation?.Dispose();
            prefetchCancellation = new CancellationTokenSource();
            var task = fetchPreview(nextMap, prefetchCancellation.Token);
            prefetched = (nextMap.OnlineID, task);
            _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void requestFullPreview()
        {
            if (current == null) return;
            var selected = current.Map;
            var installed = findLocalMap(selected.OnlineID);
            if (installed != null) { _ = loadFullPreview(installed); return; }
            recordFeedback("full_download");
            bool began = DownloadSet(selected.BeatmapSet!);
            history.Like(selected.BeatmapSet!, selected.OnlineID); updateFeedback();
            if (!history.Save(storage, historyFile)) status.Text = DiscoveryStrings.SaveFailed;
            if (!began && downloader.GetExistingDownload(selected.BeatmapSet!) == null)
            {
                previewStatus.Text = DiscoveryStrings.MediaFailed; previewStatus.Show();
                return;
            }
            fullPreview.Enabled.Value = false;
            previewStatus.Text = DiscoveryStrings.FullLoading; previewStatus.Show();
            int version = cardGeneration;
            double started = Time.Current;
            fullDownloadWatch?.Cancel();
            fullDownloadWatch = Scheduler.AddDelayed(() =>
            {
                if (version != cardGeneration) { fullDownloadWatch?.Cancel(); return; }
                if (Time.Current - started > 120000)
                {
                    fullDownloadWatch?.Cancel();
                    fullPreview.Enabled.Value = true;
                    previewStatus.Text = DiscoveryStrings.MediaFailed;
                    return;
                }
                var local = findLocalMap(selected.OnlineID);
                if (local == null)
                {
                    if (downloader.GetExistingDownload(selected.BeatmapSet!) != null) return;
                    fullDownloadWatch?.Cancel();
                    fullPreview.Enabled.Value = true;
                    previewStatus.Text = DiscoveryStrings.MediaFailed;
                    return;
                }
                fullDownloadWatch?.Cancel();
                _ = loadFullPreview(local);
            }, 500, true);
        }

        private BeatmapInfo? findLocalMap(int onlineId) => beatmaps.QueryBeatmap(m => m.OnlineID == onlineId) ?? beatmaps.QueryOnlineBeatmapId(onlineId);

        private async Task loadFullPreview(BeatmapInfo local)
        {
            stopPreview();
            int version = cardGeneration;
            previewControls.Hide();
            var cancellation = mediaCancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            previewStatus.Text = DiscoveryStrings.FullLoading; previewStatus.Show();
            try
            {
                var result = await LoadFullPreviewAsync(local, token).ConfigureAwait(false);
                await nativePreparation.WaitAsync(token).ConfigureAwait(false);
                var component = new DiscoveryGameplayPreview(result.Map, result.Audio, rulesets.GetRuleset(mode)!, token, fullAudio: true);
                try
                {
                    await Task.Run(() => { token.ThrowIfCancellationRequested(); LoadComponent(component); }, token).ConfigureAwait(false);
                    lock (pendingLock)
                    {
                        token.ThrowIfCancellationRequested();
                        if (IsDisposed) throw new OperationCanceledException(token);
                        pendingPreviews.Add(component);
                        Schedule(() =>
                        {
                            lock (pendingLock) if (!pendingPreviews.Remove(component)) return;
                            if (!validCard(version, token)) { component.Dispose(); return; }
                            attachPreview(component);
                        });
                    }
                }
                catch { component.Dispose(); throw; }
                finally { nativePreparation.Release(); }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception e)
            {
                Logger.Log($"Full recommendation preview: {e.Message}", LoggingTarget.Runtime);
                Schedule(() =>
                {
                    if (!validCard(version, token)) return;
                    previewStatus.Text = DiscoveryStrings.MediaFailed;
                    previewControls.Show(); fullPreview.Enabled.Value = true;
                    pause.Enabled.Value = repeat.Enabled.Value = false;
                });
            }
        }

        protected virtual Task<(IBeatmap Map, byte[] Audio)> LoadFullPreviewAsync(BeatmapInfo local, CancellationToken token) => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var working = beatmaps.GetWorkingBeatmap(local);
            if (working.Beatmap.HitObjects.Count > 25000) throw new System.IO.InvalidDataException("This map is too large for a preview.");
            using var stream = working.GetStream(working.BeatmapSetInfo.GetPathForFile(working.Metadata.AudioFile) ?? working.Metadata.AudioFile);
            if (stream == null || stream.Length > 40 * 1024 * 1024) throw new System.IO.InvalidDataException("Audio exceeds the full-preview limit.");
            using var bytes = new System.IO.MemoryStream();
            var buffer = new byte[65536];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                if (bytes.Length + read > 40 * 1024 * 1024) throw new System.IO.InvalidDataException("Audio exceeds the full-preview limit.");
                bytes.Write(buffer, 0, read);
            }
            return (Map: working.Beatmap, Audio: bytes.ToArray());
        }, token);

        private bool validCard(int version, CancellationToken token) => !IsDisposed && !token.IsCancellationRequested && State.Value == Visibility.Visible && version == cardGeneration;

        protected virtual bool DownloadSet(APIBeatmapSet set) => downloader.Download(set, config.Get<bool>(OsuSetting.PreferNoVideo));

        private void updateFeedback()
        {
            if (profile == null) return;
            profile.UpdateFeedback(history.Saved);
            var excluded = history.Skipped.Concat(history.Saved.Select(s => s.OnlineID)).Concat(sessionSeen).Concat(installedSets)
                .Concat(current == null ? Array.Empty<int>() : new[] { current.Map.OnlineBeatmapSetID }).ToHashSet();
            var recent = recentlyShown.ToArray();
            var diversePool = candidatePool.Where(s => !history.HasSeen(s) &&
                recent.Count(r => string.Equals(r.Artist, s.Artist, StringComparison.OrdinalIgnoreCase)) < 2
                && recent.Count(r => string.Equals(r.AuthorString, s.AuthorString, StringComparison.OrdinalIgnoreCase)) < 3);
            var eligible = profile.Rank(diversePool, excluded, filters, workMode, gameplayWeights);
            var ranked = eligible;
            if (audioSearch)
            {
                pendingAudioMaps = ranked.Count(c => !audioAttempted.Contains(c.Map.OnlineBeatmapSetID));
                ranked = ranked.Where(c => audioWeights.ContainsKey(c.Map.OnlineBeatmapSetID))
                    .Select(c => c with { Weight = audioWeights[c.Map.OnlineBeatmapSetID], Reason = DiscoveryReason.Audio })
                    .OrderByDescending(c => c.Weight).ToArray();
            }
            candidates.Clear();
            candidates.AddRange(DiscoveryRecommendations.Diversify(ranked, audioSearch ? audio_queue_limit : 24,
                (savedView ? null : current?.Map.BeatmapSet?.Artist) ?? lastArtist, (savedView ? null : current?.Map.BeatmapSet?.AuthorString) ?? lastMapper));
            if (audioSearch)
            {
                // Do not hold the ready list for a new batch; let this worker replenish it independently.
                beginAudioRanking(eligible);
                if (!savedView && current != null) progress.Text = DiscoveryStrings.Progress(candidates.Count + 1, history.Saved.Count);
                scheduleRefill();
            }
        }

        private void beginAudioRanking(IReadOnlyList<DiscoveryCandidate> ranked)
        {
            if (audioLoading || audioFailed || profile == null || candidates.Count >= audio_queue_limit || audioAttempted.Count >= audio_attempt_limit) return;
            int count = Math.Min(audioBatchSize, Math.Min(audio_attempt_limit - audioAttempted.Count, audio_queue_limit - candidates.Count));
            var selected = ranked.Where(c => !audioAttempted.Contains(c.Map.OnlineBeatmapSetID)).Take(count).Select(c => c.Map).ToArray();
            if (selected.Length == 0) return;
            audioAttempted.UnionWith(selected.Select(m => m.OnlineBeatmapSetID));
            pendingAudioMaps -= selected.Length;
            var seeds = profile.Seeds.ToArray();
            var source = activeSeedSource;
            int version = generation;
            audioLoading = true;
            audioCancellation?.Dispose();
            var cancellation = audioCancellation = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await RankAudioAsync(seeds, selected, source, cancellation.Token, (id, score) => searchScheduler.Add(() =>
                    {
                        if (!isCurrent(version) || cancellation.IsCancellationRequested || !double.IsFinite(score)) return;
                        audioWeights[id] = score;
                        // While hidden, collect scores without re-ranking the whole pool
                        // on the game's update thread for every single completed preview.
                        if (State.Value != Visibility.Visible) return;
                        updateFeedback();
                        if (canShowRecommendation) showNext();
                    })).ConfigureAwait(false);
                    searchScheduler.Add(() =>
                    {
                        if (!isCurrent(version) || cancellation.IsCancellationRequested) return;
                        foreach (var entry in result) if (double.IsFinite(entry.Value)) audioWeights[entry.Key] = entry.Value;
                        // Ineligible/failed previews must not clog the bounded metadata pool.
                        candidatePool.RemoveAll(s => audioAttempted.Contains(s.OnlineID) && !audioWeights.ContainsKey(s.OnlineID));
                        audioLoading = false;
                        updateFeedback();
                        if (canShowRecommendation) showNext();
                        else if (State.Value == Visibility.Visible && !savedView) prefetchNext();
                    });
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception e)
                {
                    Logger.Log($"Discovery audio ranking failed: {e.GetType().Name}");
                    searchScheduler.Add(() =>
                    {
                        if (!isCurrent(version) || cancellation.IsCancellationRequested) return;
                        audioLoading = false; audioFailed = true;
                        if (canShowRecommendation) showNext();
                    });
                }
            });
        }

        private void downloadCurrent()
        {
            if (current == null) return;
            var set = current.Map.BeatmapSet!;
            // Intent is the like, not successful transfer/import. A failed or cancelled
            // download does not mean the player stopped liking the song.
            DownloadSet(set);
            if (!savedView) { advance(true, "download"); return; }
            recordFeedback("download");
            history.Like(set, current.Map.OnlineID);
            updateFeedback();
            if (!history.Save(storage, historyFile)) status.Text = DiscoveryStrings.SaveFailed;
        }

        private void clearCard()
        {
            stopPreview(); current = null;
            information.Hide(); previewPanel.Hide();
            coverContent?.Clear();
            title.Text = artist.Text = difficulty.Text = mapper.Text = statistics.Text = reason.Text = progress.Text = string.Empty;
            next.Enabled.Value = save.Enabled.Value = download.Enabled.Value = false;
            previewStatus.Text = string.Empty; previewControls.Hide();
        }

        private void stopPreview(bool clear = true)
        {
            cardGeneration++;
            previewDelay?.Cancel(); fullDownloadWatch?.Cancel();
            activePrefetchCancellation?.Cancel(); activePrefetchCancellation?.Dispose(); activePrefetchCancellation = null;
            mediaCancellation?.Cancel(); mediaCancellation?.Dispose(); mediaCancellation = null;
            lock (pendingLock)
            {
                foreach (var pending in pendingPreviews) pending.Dispose();
                pendingPreviews.Clear();
            }
            preview?.StopPlayback();
            if (clear) previewContent?.Clear();
            preview = null;
        }

        protected override void Update()
        {
            base.Update();
            if (body == null) return;
            if (Time.Current >= nextFeedbackFlush) flushFeedback();
            // One visible phase while the audio worker owns the empty card. Catalogue
            // prefetch must not replace model loading/inference with "searching maps".
            if (audioSearch && audioLoading && current == null && !savedView)
                status.Text = AudioRankingStatus;
            if (string.IsNullOrEmpty(status.Text.ToString())) status.Hide(); else status.Show();
            float width = Math.Max(0, body.DrawWidth - 48);
            bool stacked = width < 650;
            float infoWidth = stacked ? width : width * 0.4f;
            float availableHeight = Math.Max(0, cardScroll.DrawHeight - 24);
            float cardHeight = Math.Max(410, availableHeight);
            information.Size = new Vector2(infoWidth, stacked ? 230 : cardHeight);
            coverContent.Height = stacked ? 0 : Math.Clamp(information.Height * 0.23f, 70, 150);
            ((Container)details.Parent!).Padding = new MarginPadding { Horizontal = 24, Top = coverContent.Height + (stacked ? 12 : 24), Bottom = stacked ? 12 : 24 };
            foreach (var secondary in new Drawable[] { progress, mapper, reason })
                if (stacked) secondary.Hide(); else secondary.Show();
            details.Spacing = new Vector2(stacked ? 4 : information.Height < 500 ? 6 : 10);
            title.Font = OsuFont.GetFont(size: stacked ? 22 : information.Height < 500 ? 26 : 34, weight: FontWeight.Bold);
            artist.Font = OsuFont.GetFont(size: stacked ? 18 : 23);
            difficulty.Font = OsuFont.GetFont(size: stacked ? 16 : 20, weight: FontWeight.SemiBold);
            statistics.Font = OsuFont.GetFont(size: stacked ? 16 : 20);
            next.Height = save.Height = download.Height = stacked ? 36 : 48;
            previewPanel.Position = stacked ? new Vector2(24, information.Height + 12) : new Vector2(24 + infoWidth + 18, 0);
            information.Position = new Vector2(24, 0);
            previewPanel.Size = new Vector2(stacked ? width : width - infoWidth - 18, stacked ? Math.Max(240, cardHeight - information.Height - 12) : cardHeight);
            body.Height = current == null ? 0 : (stacked ? information.Height + 12 + previewPanel.Height : information.Height) + 24;
            float toolbarHeight = controls.DrawHeight + 12;
            if (Math.Abs(appliedToolbarHeight - toolbarHeight) > 1)
            {
                appliedToolbarHeight = toolbarHeight;
                root.RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Absolute, toolbarHeight), new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.AutoSize), new Dimension() };
            }
            previewContent.Padding = new MarginPadding { Bottom = Math.Max(100, previewControls.DrawHeight + 12) };
            if (current == null && !loading && !audioLoading) retrySearch.Show(); else retrySearch.Hide();
            foreach (var control in filterControls.Children)
            {
                control.RelativeSizeAxes = Axes.None;
                control.Width = control == manualReferences ? Math.Max(180, width - 48) : Math.Min(210, Math.Max(130, (width - 30) / (width < 650 ? 2 : 4)));
            }
            if (preview != null)
                playbackTime.Text = $"{TimeSpan.FromMilliseconds(preview.ClipPosition):m\\:ss} / {TimeSpan.FromMilliseconds(preview.Duration):m\\:ss}";
            float buttonWidth = Math.Max(90, (infoWidth - 58) / 2);
            next.Width = save.Width = buttonWidth;
            download.Width = Math.Max(90, infoWidth - 48);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (audioAnalysis != null) audioAnalysis.SettingsChanged -= audioSettingsChanged;
            catalogueSource?.UnbindAll();
            keepSearchingInBackground?.UnbindAll();
            localUser?.UnbindAll();
            audioCancellation?.Cancel(); audioCancellation?.Dispose();
            generation++;
            similarityCancellation?.Cancel(); similarityCancellation?.Dispose();
            toolbarRuleset?.UnbindAll();
            library?.Dispose();
            foreach (var request in requests.ToArray()) request.Cancel();
            requests.Clear();
            refill?.Cancel(); fullDownloadWatch?.Cancel(); prefetchDelay?.Cancel();
            prefetchCancellation?.Cancel(); prefetchCancellation?.Dispose();
            musicSilence?.Dispose(); musicSilence = null;
            stopPreview(false);
            media.Dispose();
            base.Dispose(isDisposing);
        }
    }

    public partial class DiscoveryHeader : OverlayHeader
    {
        protected override OverlayTitle CreateTitle() => new DiscoveryTitle();
        private partial class DiscoveryTitle : OverlayTitle
        {
            public DiscoveryTitle() { Title = DiscoveryStrings.Title; Icon = FontAwesome.Solid.Compass; }
        }
    }
}
