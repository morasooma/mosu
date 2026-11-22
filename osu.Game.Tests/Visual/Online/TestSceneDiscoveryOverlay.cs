using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Development;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Discovery;
using osu.Game.Rulesets.UI;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneDiscoveryOverlay : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;
        private TestDiscoveryOverlay overlay = null!;
        private OsuConfigManager config = null!;
        private BeatmapManager fixtureManager = null!;
        private int scoreRequests;
        private GetUserMostPlayedBeatmapsRequest? pendingMostPlayed;
        private bool delayMostPlayed;
        private int catalogueRequests;
        private bool delayScores;
        private bool delayTaste;
        private bool failTaste;
        private DiscoveryTasteRequest? pendingTaste;
        private bool provideTasteNeighbours;
        private bool delaySetLookup;
        private GetBeatmapSetRequest? pendingSetLookup;
        private bool failCatalogue;
        private int emptyCataloguePages;
        private Func<SearchBeatmapSetsRequest, SearchBeatmapSetsResponse>? catalogueFixture;
        private bool provideGenre;
        private GetUserScoresRequest? pending;
        private readonly List<DiscoveryFeedbackEvent> feedbackEvents = new List<DiscoveryFeedbackEvent>();
        private bool failFeedback;
        private APIBeatmap seed = null!;
        private bool capture => !DebugUtils.IsNUnitRunning;
        protected virtual float CaptureWidth => float.TryParse(Environment.GetEnvironmentVariable("MOSU_DISCOVERY_CAPTURE_WIDTH"), out float width) ? width : 1050;
        protected virtual ThemeMode CaptureTheme => Environment.GetEnvironmentVariable("MOSU_DISCOVERY_CAPTURE_LIGHT") == "1" ? ThemeMode.Light : ThemeMode.Default;
        private int testMode;
        private bool originalLifetimeCache;
        private bool originalSkinPerformance;

        [SetUp]
        public void SaveGlobalPreviewSettings()
        {
            originalLifetimeCache = osu.Framework.Graphics.Performance.GraphicsPerformanceSettings.CacheStaticChildLifetimes;
            originalSkinPerformance = osu.Game.Skinning.SkinPerformanceMode.Enabled;
        }

        [TearDown]
        public void RestoreGlobalPreviewSettings()
        {
            osu.Framework.Graphics.Performance.GraphicsPerformanceSettings.CacheStaticChildLifetimes = originalLifetimeCache;
            osu.Game.Skinning.SkinPerformanceMode.Enabled = originalSkinPerformance;
        }
        private readonly List<BeatmapCatalogueSource> requestedSources = new List<BeatmapCatalogueSource>();
        private readonly List<SearchCategory> requestedCategories = new List<SearchCategory>();
        [Resolved] private osu.Game.Skinning.SkinManager skinManager { get; set; } = null!;
        [Resolved] private osu.Game.Rulesets.RulesetStore rulesets { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(osu.Framework.Platform.GameHost host, osu.Framework.Audio.AudioManager audio)
        {
            Dependencies.Cache(Realm);
            Dependencies.Cache(fixtureManager = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(config = new OsuConfigManager(LocalStorage));
            Dependencies.CacheAs<osu.Framework.Platform.Storage>(LocalStorage);
            Dependencies.CacheAs<IBeatmapApiProvider>(new BeatmapApiProvider(API, null));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("mock Mosu scores and anonymous catalogue", () =>
            {
                delayMostPlayed = false; pendingMostPlayed = null;
                scoreRequests = catalogueRequests = 0; delayScores = failCatalogue = delayTaste = failTaste = false; emptyCataloguePages = 0; pending = null; pendingTaste = null; provideTasteNeighbours = false; testMode = 0;
                delaySetLookup = false; pendingSetLookup = null;
                catalogueFixture = null; provideGenre = false;
                LocalStorage.DeleteDirectory("discovery");
                Ruleset.Value = rulesets.GetRuleset(0)!;
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 42, Username = "Discovery tester" };
                feedbackEvents.Clear(); failFeedback = false;
                requestedCategories.Clear();
                requestedSources.Clear();
                config.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Auto);
                config.SetValue(OsuSetting.ForkAiAudioBackgroundSearch, true);
                seed = makeSet(101).Beatmaps[0];
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    switch (request)
                    {
                        case DiscoveryTasteRequest taste:
                            pendingTaste = taste;
                            if (delayTaste) return true;
                            if (failTaste) { taste.TriggerFailure(new System.Net.Http.HttpRequestException("Fixture old API")); return true; }
                            taste.TriggerSuccess(new DiscoveryTasteProfile
                            {
                                Artists = new[] { new DiscoveryTasteArtist { Name = "Artist 0", Weight = 8, Plays = 30, Songs = 5 } },
                                Genres = provideGenre ? new[] { new DiscoveryTasteSignal { Name = "Electronic", Weight = 5 } } : Array.Empty<DiscoveryTasteSignal>(),
                                Candidates = provideTasteNeighbours ? new[] { new DiscoveryTasteCandidate { SetId = 200, Weight = 1, Reason = "listeners" } } : Array.Empty<DiscoveryTasteCandidate>(),
                            });
                            return true;
                        case DiscoveryFeedbackRequest feedback:
                            feedbackEvents.Add(feedback.Event);
                            if (failFeedback) feedback.TriggerFailure(new System.Net.Http.HttpRequestException("Fixture offline"));
                            else feedback.TriggerSuccess();
                            return true;
                        case GetBeatmapSetRequest lookup:
                            pendingSetLookup = lookup;
                            if (delaySetLookup) return true;
                            lookup.TriggerSuccess(makeSet(lookup.ID));
                            return true;
                        case GetUserMostPlayedBeatmapsRequest mostPlayed:
                            pendingMostPlayed = mostPlayed;
                            if (delayMostPlayed) return true;
                            var frequent = makeSet(90);
                            mostPlayed.TriggerSuccess(Newtonsoft.Json.JsonConvert.DeserializeObject<List<APIUserMostPlayedBeatmap>>(
                                Newtonsoft.Json.JsonConvert.SerializeObject(new[] { new { beatmap_id = frequent.Beatmaps[0].OnlineID,
                                    count = 25, beatmap = new { id = frequent.Beatmaps[0].OnlineID, beatmapset_id = frequent.OnlineID,
                                        mode_int = testMode, difficulty_rating = 3.25, bpm = 174, total_length = 180 },
                                    beatmapset = new { id = frequent.OnlineID, artist = frequent.Artist, title = frequent.Title } } }))!);
                            return true;
                        case GetBeatmapRequest mapLookup:
                            mapLookup.TriggerSuccess(makeSet(91).Beatmaps[0]);
                            return true;
                        case GetUserScoresRequest scores:
                            scoreRequests++;
                            pending = scores;
                            seed.RulesetID = testMode;
                            if (!delayScores) scores.TriggerSuccess(new List<SoloScoreInfo> { new SoloScoreInfo { Beatmap = seed } });
                            return true;
                        case SearchBeatmapSetsRequest search:
                            catalogueRequests++;
                            requestedCategories.Add(search.SearchCategory);
                            requestedSources.Add(search.CatalogueSource);
                            if (failCatalogue || capture && Environment.GetEnvironmentVariable("MOSU_DISCOVERY_CAPTURE_FAILURE") == "1")
                                search.TriggerFailure(new System.Net.Http.HttpRequestException("Fixture HTTP 400"));
                            else if (emptyCataloguePages > 0)
                            {
                                emptyCataloguePages--;
                                search.TriggerSuccess(new SearchBeatmapSetsResponse
                                {
                                    BeatmapSets = new List<APIBeatmapSet>(),
                                    Cursor = new Cursor { Properties = new Dictionary<string, Newtonsoft.Json.Linq.JToken> { ["mirror_page"] = 1 } },
                                });
                            }
                            else search.TriggerSuccess(catalogueFixture?.Invoke(search) ?? new SearchBeatmapSetsResponse { BeatmapSets = Enumerable.Range(102, 12).Select(makeSet).ToList() });
                            return true;
                    }
                    return false;
                };
                if (Environment.GetEnvironmentVariable("MOSU_DISCOVERY_SKIN_ARCHIVE") is string archive)
                    skinManager.CurrentSkinInfo.Value = skinManager.Import(new RetainedImportTask(archive)).GetAwaiter().GetResult();
                else skinManager.CurrentSkinInfo.Value = osu.Game.Skinning.ArgonSkin.CreateInfo().ToLiveUnmanaged();
                osu.Framework.Graphics.Performance.GraphicsPerformanceSettings.CacheStaticChildLifetimes = true;
                config.SetValue(OsuSetting.ForkSkinPerformanceMode, true);
                osu.Game.Skinning.SkinPerformanceMode.Enabled = true;
                config.SetValue(OsuSetting.BeatmapSkins, false);
                config.SetValue(OsuSetting.BeatmapColours, false);
                config.SetValue(OsuSetting.ForkThemeMode, CaptureTheme);
                if (capture && Environment.GetEnvironmentVariable("MOSU_DISCOVERY_THEME_JSON") is string themeFile)
                {
                    var theme = CustomThemeDefinition.FromJson(System.IO.File.ReadAllText(themeFile));
                    OverlayColourProvider.CustomTheme.Value = theme;
                    OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                    if (theme.Interface?.Animations is InterfaceAnimationMode animations) osu.Game.Graphics.InterfaceAnimation.Mode.Value = animations;
                }
                else if (capture) OverlayColourProvider.CurrentTheme.Value = CaptureTheme;
                Child = new osu.Framework.Graphics.Cursor.PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = overlay = new TestDiscoveryOverlay { RelativeSizeAxes = Axes.Y, Width = capture ? CaptureWidth : 1300 },
                };
                overlay.Show();
                if (capture && Environment.GetEnvironmentVariable("MOSU_DISCOVERY_CAPTURE_GAMEPLAY") == "1")
                    Scheduler.AddDelayed(() => button(DiscoveryStrings.GameplayMode).Action.Invoke(), 1000);
                if (capture && Environment.GetEnvironmentVariable("MOSU_DISCOVERY_CAPTURE_SCROLL") == "1")
                    Scheduler.AddDelayed(() => overlay.ChildrenOfType<osu.Game.Graphics.Containers.OsuScrollContainer>()
                        .First(s => s.ChildrenOfType<DiscoveryGameplayPreview>().Any()).ScrollToEnd(false), 10000);
            });
            AddUntilStep("native gameplay ready", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
        }

        private APIBeatmapSet makeSet(int id)
        {
            var set = new APIBeatmapSet { OnlineID = id, Artist = "Artist " + (id % 6), Title = "A song beyond your usual rotation " + id, AuthorString = "Mapper " + (id % 5), AuthorID = 100 };
            set.Beatmaps = new[] { new APIBeatmap { OnlineID = id * 10, OnlineBeatmapSetID = id, BeatmapSet = set, RulesetID = testMode, StarRating = 3.25, BPM = 174, DifficultyName = "Another beautiful detour", Length = 180000 } };
            return set;
        }

        private ThemeLibraryButton button(osu.Framework.Localisation.LocalisableString text) => overlay.ChildrenOfType<ThemeLibraryButton>().First(b => b.Text == text);
        private DiscoveryGameplayPreview preview => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Single();

        [Test]
        public void TestNativeClockPauseResumeAndBudget()
        {
            if (capture) return; // Interactive screenshots retain setup; NUnit executes the assertions below.
            double start = 0;
            double anchor = Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_MEDIA") != null
                ? double.Parse(Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_ANCHOR") ?? "18957", System.Globalization.CultureInfo.InvariantCulture) : 60000;
            AddAssert("one Mosu top-score request, bounded catalogue", () => scoreRequests == 1 && catalogueRequests is >= 1 and <= 3 && overlay.MediaRequests <= 2);
            if (Environment.GetEnvironmentVariable("MOSU_DISCOVERY_SKIN_ARCHIVE") == null)
                AddUntilStep("actual Argon note skin loaded", () => preview.ChildrenOfType<osu.Game.Rulesets.Osu.Skinning.Argon.ArgonMainCirclePiece>().Any());
            AddAssert("native ruleset present and samples muted", () => preview.ChildrenOfType<DrawableRuleset>().Single().Audio.Volume.Value == 0);
            AddUntilStep("gameplay follows preview anchor", () => Math.Abs(preview.ChildrenOfType<DrawableRuleset>().Single().FrameStableClock.CurrentTime - preview.MapTime) < 250 && preview.MapTime >= anchor);
            AddUntilStep("several seconds into preview", () => preview.MapTime > anchor + 2500);
            AddStep("pause", () => { start = preview.MapTime; button(DiscoveryStrings.Pause).Action.Invoke(); });
            AddWaitStep("paused", 5);
            AddAssert("clip and gameplay freeze without rewind", () => !preview.Playing && Math.Abs(preview.MapTime - start) < 150
                && Math.Abs(preview.ChildrenOfType<DrawableRuleset>().Single().FrameStableClock.CurrentTime - start) < 150);
            AddStep("resume", () => button(DiscoveryStrings.Play).Action.Invoke());
            AddUntilStep("clip progresses again", () => preview.Playing && preview.MapTime > start + 200);
            AddAssert("only current and one next clip requested", () => overlay.MediaRequests <= 2);
            AddUntilStep("notes actually visible, not only cursor", () => preview.VisibleHitObjects > 0);
            AddStep("seek clip forward", () => preview.Seek(7000));
            AddUntilStep("notes visible after seeking", () => preview.VisibleHitObjects > 0 && preview.ClipPosition >= 7000);
            AddStep("replay clip", () => preview.Seek(0));
            AddUntilStep("notes visible again after rewind", () => preview.VisibleHitObjects > 0 && preview.ClipPosition < 5000);
        }

        [Test]
        public void TestDelayedFrequencyFromOldAccountCannotRestartSearch()
        {
            GetUserMostPlayedBeatmapsRequest? old = null;
            int requestsAfterSignOut = 0;
            AddStep("request delayed frequency", () =>
            {
                delayMostPlayed = true;
                overlay.Hide(); overlay.Show();
            });
            AddUntilStep("frequency pending", () => pendingMostPlayed != null);
            AddStep("sign out before frequency completes", () =>
            {
                old = pendingMostPlayed;
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 1, Username = "Guest" };
            });
            AddWaitStep("sign out propagated", 2);
            AddStep("late old-account response", () =>
            {
                requestsAfterSignOut = catalogueRequests;
                old!.TriggerSuccess(new List<APIUserMostPlayedBeatmap>());
            });
            AddWaitStep("late callback processed", 2);
            AddAssert("no card or new catalogue work", () => !overlay.HasVisibleCard && catalogueRequests == requestsAfterSignOut);
        }

        [Test]
        public void TestAudioRankingActuallySelectsCardAndMissingAudioDoesNotLeakMetadata()
        {
            AddStep("enable local audio ranking fixture", () =>
            {
                overlay.AudioEnabled = true;
                overlay.AudioResult = new Dictionary<int, double> { [-1] = .95 };
                overlay.Hide(); overlay.Show();
            });
            AddUntilStep("audio ranked card visible", () => overlay.AudioSelectedSet > 0 && overlay.ShownSets.Last().OnlineID == overlay.AudioSelectedSet && overlay.HasVisibleCard);
            AddAssert("card explains audio model", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.AudioSimilar));
            AddAssert("references reached audio worker", () => overlay.AudioSeedIds.Count > 0);
            AddStep("no usable audio results", () =>
            {
                overlay.AudioResult = new Dictionary<int, double>();
                overlay.Hide(); overlay.Show();
            });
            AddWaitStep("ranking finishes", 5);
            AddAssert("unanalysed catalogue is not shown as audio matching", () => !overlay.HasVisibleCard);
        }

        [Test]
        public void TestAudioRefillRunsWithTheOverlayClosedAndStreamsReadyCards()
        {
            int initialRequests = 0, pendingSet = 0, shownBefore = 0, hiddenRequests = 0;
            bool unsupportedGenre = false;
            AddStep("mirror returns one map first, then many on its next page", () =>
            {
                provideGenre = true;
                catalogueFixture = search =>
                {
                    unsupportedGenre |= search.Genre != SearchGenre.Any;
                    int page = search.MirrorQuery.Page;
                    return new SearchBeatmapSetsResponse
                    {
                        BeatmapSets = (page == 0 ? new[] { 350 } : Enumerable.Range(360 + (page - 1) * 55, 55)).Select(id =>
                        {
                            var set = makeSet(id);
                            set.Artist = "Unique artist " + id; set.AuthorString = "Unique mapper " + id;
                            return set;
                        }).ToList(),
                        Cursor = page < 3 ? new Cursor { Properties = new Dictionary<string, Newtonsoft.Json.Linq.JToken> { ["mirror_page"] = page + 1 } } : null,
                    };
                };
                overlay.AudioEnabled = true;
                overlay.AudioPending = new TaskCompletionSource<IReadOnlyDictionary<int, double>>(TaskCreationOptions.RunContinuationsAsynchronously);
                overlay.AudioPhase = DiscoveryStrings.AudioLoading;
                initialRequests = catalogueRequests;
                overlay.Hide(); overlay.Show();
            });
            AddUntilStep("audio request started", () => overlay.AudioReady != null && overlay.SearchStatus == DiscoveryStrings.AudioLoading);
            AddStep("remember first page requests", () => initialRequests = catalogueRequests);
            AddUntilStep("mirror paginates concurrently without replacing model status", () =>
            {
                Assert.That(overlay.SearchStatus, Is.EqualTo(DiscoveryStrings.AudioLoading));
                return catalogueRequests > initialRequests;
            });
            AddAssert("mirror never receives an unsupported automatic genre", () => !unsupportedGenre);
            AddStep("first candidate checked while batch is still running", () =>
            {
                pendingSet = overlay.AudioMaps!.Single().OnlineBeatmapSetID;
                overlay.AudioReady!(pendingSet, .97);
            });
            AddUntilStep("first card visible without waiting for the batch", () => overlay.HasVisibleCard && overlay.ShownSets.LastOrDefault()?.OnlineID == pendingSet);
            AddStep("close recommendations and finish the first audio batch", () =>
            {
                shownBefore = overlay.ShownSets.Count;
                hiddenRequests = catalogueRequests;
                overlay.Hide();
                var batch = overlay.AudioPending!;
                overlay.AudioPending = null;
                overlay.ScoreAllAudio = true;
                batch.SetResult(new Dictionary<int, double> { [pendingSet] = .97 });
            });
            AddUntilStep("background fills the hundred-card queue without drawing cards", () => overlay.ReadyCount == 100);
            AddAssert("hidden window paginates without drawing a card or reloading scores", () => overlay.ShownSets.Count == shownBefore
                && !overlay.HasVisibleCard && catalogueRequests > hiddenRequests && scoreRequests == 2);
            AddStep("open and immediately skip through prepared cards", () =>
            {
                overlay.Show();
                for (int i = 0; i < 8; i++) button(DiscoveryStrings.Next).Action.Invoke();
            });
            AddAssert("skips use ready audio cards, without reloading scores", () => overlay.HasVisibleCard && overlay.ShownSets.Count == shownBefore + 9
                && overlay.ShownSets.Select(s => s.OnlineID).Distinct().Count() == overlay.ShownSets.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBackgroundAudioCanBeStoppedWithoutDisablingAudioRecommendations(bool disableAfterClosing)
        {
            CancellationToken oldSearch = default;
            int hiddenRequests = 0;
            double closedAt = 0;
            AddStep("hold audio analysis in the open window", () =>
            {
                overlay.AudioEnabled = true;
                overlay.AudioPending = new TaskCompletionSource<IReadOnlyDictionary<int, double>>(TaskCreationOptions.RunContinuationsAsynchronously);
                overlay.Hide(); overlay.Show();
            });
            AddUntilStep("audio analysis started", () => overlay.AudioReady != null);
            AddStep("close with background disabled or disable afterwards", () =>
            {
                oldSearch = overlay.AudioToken;
                if (!disableAfterClosing)
                {
                    config.SetValue(OsuSetting.ForkAiAudioBackgroundSearch, false);
                    Assert.That(oldSearch.IsCancellationRequested, Is.False, "the setting only controls work outside the open window");
                }
                overlay.Hide();
                if (disableAfterClosing)
                {
                    Assert.That(oldSearch.IsCancellationRequested, Is.False, "background work is still allowed before switching it off");
                    config.SetValue(OsuSetting.ForkAiAudioBackgroundSearch, false);
                }
                hiddenRequests = catalogueRequests;
                closedAt = Time.Current;
            });
            AddUntilStep("no catalogue refill or active audio after closing", () =>
            {
                Assert.That(oldSearch.IsCancellationRequested, Is.True);
                Assert.That(catalogueRequests, Is.EqualTo(hiddenRequests));
                return Time.Current - closedAt > 3300;
            });
            AddStep("reopen with background still disabled", () =>
            {
                overlay.AudioPending = null;
                overlay.ScoreAllAudio = true;
                overlay.Show();
            });
            AddUntilStep("foreground still shows analysed audio cards", () => overlay.HasVisibleCard && overlay.ChildrenOfType<OsuSpriteText>()
                .Any(t => t.Text == DiscoveryStrings.AudioSimilar));
        }

        [Test]
        public void TestFailedAudioDoesNotResumeCatalogueLoop()
        {
            int requestsAfterFailure = 0;
            double waitFrom = 0;
            AddStep("hold audio worker", () =>
            {
                overlay.AudioEnabled = true;
                overlay.AudioPending = new TaskCompletionSource<IReadOnlyDictionary<int, double>>(TaskCreationOptions.RunContinuationsAsynchronously);
                overlay.Hide(); overlay.Show();
            });
            AddUntilStep("audio started", () => overlay.AudioSeedIds.Count > 0);
            AddStep("fail audio worker", () => overlay.AudioPending!.TrySetException(new System.IO.InvalidDataException("fixture audio unavailable")));
            AddUntilStep("failure visible", () => overlay.SearchStatus == DiscoveryStrings.AudioFailed);
            AddStep("remember failure", () => { requestsAfterFailure = catalogueRequests; waitFrom = Time.Current; });
            AddUntilStep("failure stays visible without automatic searching", () =>
            {
                Assert.That(overlay.SearchStatus, Is.EqualTo(DiscoveryStrings.AudioFailed));
                Assert.That(catalogueRequests, Is.EqualTo(requestsAfterFailure));
                return Time.Current - waitFrom > 3400;
            });
        }

        [TestCase(620)]
        [TestCase(1300)]
        public void TestExampleFieldFitsExistingFilters(float width)
        {
            AddStep("resize and open filters", () =>
            {
                overlay.Width = width;
                button(DiscoveryStrings.Filters).Action.Invoke();
            });
            AddWaitStep("layout settles", 3);
            AddAssert("example field stays inside overlay", () =>
            {
                var field = overlay.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormTextBox>().Single();
                var outer = overlay.ScreenSpaceDrawQuad.AABBFloat;
                var bounds = field.ScreenSpaceDrawQuad.AABBFloat;
                return field.IsPresent && field.DrawWidth >= 180 && bounds.Left >= outer.Left && bounds.Right <= outer.Right + 1;
            });
        }

        [Test]
        public void TestInvalidManualReferenceKeepsFiltersOpen()
        {
            int requestsBefore = 0;
            AddStep("invalid example", () =>
            {
                requestsBefore = catalogueRequests;
                button(DiscoveryStrings.Filters).Action.Invoke();
                overlay.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuDropdown<DiscoverySeedSource>>().Single().Current.Value = DiscoverySeedSource.Manual;
                overlay.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormTextBox>().Single().Current.Value = "https://evil.example/beatmapsets/12";
                button(DiscoveryStrings.Apply).Action.Invoke();
            });
            AddAssert("filter remains editable without new fetch", () => overlay.FiltersVisible && catalogueRequests == requestsBefore
                && overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.InvalidExamples));
        }

        [Test]
        public void TestManualExamplesAndFrequencySourcesAreApplied()
        {
            AddStep("open filters and choose frequent", () =>
            {
                button(DiscoveryStrings.Filters).Action.Invoke();
                overlay.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuDropdown<DiscoverySeedSource>>().Single().Current.Value = DiscoverySeedSource.Frequent;
                button(DiscoveryStrings.Apply).Action.Invoke();
            });
            AddUntilStep("frequent recommendations ready", () => overlay.HasVisibleCard);
            AddStep("choose explicit set without hardest assumption", () =>
            {
                button(DiscoveryStrings.Filters).Action.Invoke();
                overlay.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuDropdown<DiscoverySeedSource>>().Single().Current.Value = DiscoverySeedSource.Manual;
                overlay.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormTextBox>().Single().Current.Value = "https://morasooma.net/beatmapsets/95#osu/950";
                button(DiscoveryStrings.Apply).Action.Invoke();
            });
            AddUntilStep("manual reference resolved", () => pendingSetLookup?.ID == 95);
            AddUntilStep("manual recommendations ready", () => overlay.HasVisibleCard);
            AddAssert("manual source kept", () => overlay.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuDropdown<DiscoverySeedSource>>().Single().Current.Value == DiscoverySeedSource.Manual);
        }

        [Test]
        public void TestCatalogueSelectionIsNeverOverridden()
        {
            AddAssert("Auto stays Auto", () => requestedSources.All(s => s == BeatmapCatalogueSource.Auto));
            foreach (var source in new[] { BeatmapCatalogueSource.Official, BeatmapCatalogueSource.Nekoha, BeatmapCatalogueSource.OsuDirect, BeatmapCatalogueSource.Mirria })
            {
                AddStep("select " + source, () =>
                {
                    requestedSources.Clear();
                    config.SetValue(OsuSetting.BeatmapCatalogueSource, source);
                    button(DiscoveryStrings.Apply).Action.Invoke();
                });
                AddUntilStep("request uses exactly selected source", () => requestedSources.Count > 0 && requestedSources.All(s => s == source));
            }
        }

        [Test]
        public void TestShownCardsDoNotReturnAfterReopenOrFilterReset()
        {
            int first = 0;
            AddStep("remember displayed card without pressing skip", () => first = overlay.ShownSets.Last().OnlineID);
            AddStep("close and reopen", () => { overlay.Hide(); overlay.Show(); });
            AddUntilStep("different card displayed", () => overlay.ShownSets.Count >= 2 && overlay.ShownSets.Last().OnlineID != first);
            AddStep("apply unchanged filters", () => button(DiscoveryStrings.Apply).Action.Invoke());
            AddUntilStep("third distinct card displayed", () => overlay.ShownSets.Count >= 3);
            AddAssert("no repeated IDs or songs", () => overlay.ShownSets.Select(s => s.OnlineID).Distinct().Count() == overlay.ShownSets.Count
                && overlay.ShownSets.Select(DiscoveryHistory.SongKey).Distinct().Count() == overlay.ShownSets.Count);
        }

        [Test]
        public void TestInstalledMapsAreExcludedAndFiltersApply()
        {
            AddAssert("all statuses included by default", () => requestedCategories.All(c => c == SearchCategory.Any));
            AddAssert("installed sets excluded before display", () => overlay.InstalledChecks > 0 && overlay.ShownSets.All(s => s.OnlineID != 102));
            int requests = 0;
            AddStep("apply graveyard and difficulty range", () =>
            {
                overlay.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuDropdown<SearchCategory>>().Single().Current.Value = SearchCategory.Graveyard;
                var sliders = overlay.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormSliderBar<double>>().ToArray();
                sliders[0].Current.Value = 3;
                sliders[1].Current.Value = 4;
                requests = catalogueRequests;
                button(DiscoveryStrings.Apply).Action.Invoke();
            });
            AddUntilStep("new catalogue requested", () => catalogueRequests > requests);
            AddAssert("graveyard is sent upstream", () => requestedCategories.Last() == SearchCategory.Graveyard);
        }

        [Test]
        public void TestAllFiltersAreCollapsedUntilRequested()
        {
            AddAssert("all filter controls initially hidden together", () => !overlay.FiltersVisible);
            AddStep("expand all filters", () => button(DiscoveryStrings.Filters).Action.Invoke());
            AddAssert("stars, status, BPM, length and apply available", () => overlay.FiltersVisible
                && overlay.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormSliderBar<double>>().Count() == 5
                && button(DiscoveryStrings.Apply).IsPresent);
            AddStep("collapse all filters", () => button(DiscoveryStrings.Filters).Action.Invoke());
            AddAssert("whole filter panel hidden again", () => !overlay.FiltersVisible);
        }

        private sealed class RetainedImportTask(string path) : ImportTask(path)
        {
            public override void DeleteFile() { }
        }

        [Test]
        public void TestMissingTimestampStillPlaysAudioAndOffersFullPreview()
        {
            AddStep("next map has no preview point", () => { overlay.MissingAnchor = true; button(DiscoveryStrings.Next).Action.Invoke(); button(DiscoveryStrings.Next).Action.Invoke(); });
            AddUntilStep("audio-only clip running", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any(p => !p.HasGameplay && p.Playing && p.ClipPosition > 100));
            AddAssert("listening and explicit full download remain available", () => button(DiscoveryStrings.Pause).Enabled.Value
                && button(DiscoveryStrings.FullPreview).Enabled.Value
                && overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.IsPresent && t.Text == DiscoveryStrings.NoAnchor));
        }

        [Test]
        public void TestCatalogueFailureIsNotEmptyAndDoesNotAutoRetry()
        {
            int requests = 0;
            AddStep("catalogue rejects query", () => { failCatalogue = true; button(DiscoveryStrings.Apply).Action.Invoke(); });
            AddUntilStep("error displayed instead of exhausted maps", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.Failed));
            AddAssert("empty panels are absent", () => !overlay.HasVisibleCard);
            AddStep("remember request count", () => requests = catalogueRequests);
            AddWaitStep("no retry storm", 35);
            AddAssert("no automatic requests after failure", () => catalogueRequests == requests);
            AddStep("retry explicitly", () => { failCatalogue = false; button(DiscoveryStrings.Retry).Action.Invoke(); });
            AddUntilStep("cards return", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
        }

        [Test]
        public void TestEmptyFilteredPageWithCursorContinuesSearching()
        {
            AddStep("first batch locally empty but not exhausted", () => { emptyCataloguePages = 3; button(DiscoveryStrings.Apply).Action.Invoke(); });
            AddAssert("still searching, not no maps", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.Loading));
            AddUntilStep("next catalogue page yields a card", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
        }

        [Test]
        public void TestArtistDiversitySurvivesFeedbackReranking()
        {
            AddStep("consume several recommendations", () =>
            {
                for (int i = 0; i < 7; i++) button(DiscoveryStrings.Next).Action.Invoke();
            });
            AddAssert("installed set never shown", () => overlay.ShownSets.All(s => s.OnlineID != 102));
            AddAssert("feed remains varied after each rerank", () =>
            {
                var shown = overlay.ShownSets;
                return shown.Count >= 7
                       && shown.GroupBy(s => s.Artist).Max(g => g.Count()) <= 2
                       && shown.GroupBy(s => s.AuthorString).Max(g => g.Count()) <= 3
                       && shown.Zip(shown.Skip(1)).All(pair => pair.First.Artist != pair.Second.Artist && pair.First.AuthorString != pair.Second.AuthorString);
            });
        }

        [Test]
        public void TestInstalledRealmLookupAndFullLocalAudio()
        {
            BeatmapManager localManager = null!;
            BeatmapSetInfo imported = null!;
            AddStep("create isolated beatmap manager", () =>
            {
                _ = new osu.Game.Rulesets.RealmRulesetStore(Realm);
                localManager = fixtureManager;
            });
            AddStep("import fixture into isolated library", () =>
            {
                string path = osu.Game.Tests.Resources.TestResources.GetTestBeatmapForImport();
                try { localManager.Import(path).GetAwaiter().GetResult(); }
                finally { System.IO.File.Delete(path); }
                imported = localManager.GetAllUsableBeatmapSets().Single();
            });
            AddAssert("production Realm query excludes imported set", () => overlay.CheckInstalled(new APIBeatmapSet { OnlineID = imported.OnlineID }));
            AddUntilStep("same song with different set ID excluded from real library metadata", () => overlay.CheckInstalled(new APIBeatmapSet
            {
                OnlineID = 987654321, Artist = imported.Beatmaps[0].Metadata.Artist.ToUpperInvariant(),
                Title = "  " + imported.Beatmaps[0].Metadata.Title + "!!!",
            }));
            AddAssert("unrelated song not excluded", () => !overlay.CheckInstalled(new APIBeatmapSet { OnlineID = 987654320, Artist = "unrelated", Title = "another song" }));
            AddStep("open saved imported difficulty", () =>
            {
                var map = imported.Beatmaps.First(m => m.Ruleset.OnlineID == 0);
                var online = makeSet(imported.OnlineID);
                online.Beatmaps[0].OnlineID = map.OnlineID;
                var history = new DiscoveryHistory();
                history.Like(online);
                history.Save(LocalStorage, DiscoveryHistory.Filename(API.Endpoints.APIUrl, 42, 0));
                overlay.Hide(); overlay.Show();
                button(DiscoveryStrings.Saved).Action.Invoke();
            });
            AddUntilStep("short clip ready", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddStep("watch full installed audio", () => button(DiscoveryStrings.FullPreview).Action.Invoke());
            AddUntilStep("full local preview loaded", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any(p => p.FullAudio));
            AddAssert("full audio is longer than short clip", () => preview.Duration > 30000);
            AddUntilStep("native notes visible in full preview", () => preview.VisibleHitObjects > 0);
            AddStep("stop preview before disposing local manager", () => overlay.Hide());
        }

        [Test]
        public void TestStableLocalOnlySongExcludedAcrossSetIds()
        {
            string path = string.Empty;
            AddStep("register local-only stable song without online IDs", () =>
            {
                path = System.IO.Path.GetTempFileName();
                var set = new BeatmapSetInfo { OnlineID = -1 };
                var map = new BeatmapInfo { OnlineID = -1, BeatmapSet = set, Metadata = new BeatmapMetadata
                {
                    Artist = "Stable Artist", Title = "Stable Song", ArtistUnicode = "安定", TitleUnicode = "曲",
                } };
                set.Beatmaps.Add(map);
                StablePathManager.Replace(new Dictionary<Guid, string> { [map.ID] = path }, new Dictionary<Guid, string>(), new[] { set });
            });
            AddAssert("different public set for installed song is excluded", () => overlay.CheckInstalled(new APIBeatmapSet { OnlineID = 998877, Artist = "stable-artist", Title = "STABLE SONG!" }));
            AddAssert("unicode metadata alias is excluded too", () => overlay.CheckInstalled(new APIBeatmapSet { OnlineID = 998878, Artist = "安定", Title = "曲" }));
            AddStep("remove private stable fixture", () =>
            {
                StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
                System.IO.File.Delete(path);
            });
            AddAssert("removed song is eligible again", () => !overlay.CheckInstalled(new APIBeatmapSet { OnlineID = 998877, Artist = "stable-artist", Title = "STABLE SONG!" }));
        }

        [Test]
        public void TestNotesSurviveRepeatedClipLoops()
        {
            for (int i = 0; i < 3; i++)
            {
                AddStep("seek near clip end", () => preview.Seek(preview.Duration - 500));
                AddUntilStep("clip loops back", () => preview.ClipPosition < 3000);
                AddUntilStep("notes survive looping", () => preview.VisibleHitObjects > 0);
            }
        }

        [Test]
        public void TestChoicesSavedAndAccountIsolation()
        {
            AddStep("save", () => button(DiscoveryStrings.Save).Action.Invoke());
            AddUntilStep("next preview loaded", () => overlay.MediaRequests >= 2 && overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddStep("open saved", () => button(DiscoveryStrings.Saved).Action.Invoke());
            AddUntilStep("saved map preview", () => overlay.MediaRequests >= 3 && overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddAssert("saved map available", () => !button(DiscoveryStrings.Save).Enabled.Value && button(DiscoveryStrings.Download).Enabled.Value);
            AddStep("sign out", () => ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 0 });
            AddUntilStep("private card removed", () => !overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any() && overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.Login));
            AddStep("other account", () => ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 43 });
            AddUntilStep("other recommendations", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddStep("other account saved", () => button(DiscoveryStrings.Saved).Action.Invoke());
            AddAssert("saved choices not shared", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.EmptySaved));
        }

        [Test]
        public void TestDownloadCountsAsPersistentLikeWithoutDuplicates()
        {
            int likedSet = 0;
            int requestsBeforeReopen = 0;
            AddStep("download current recommendation", () =>
            {
                button(DiscoveryStrings.Download).Action.Invoke();
                likedSet = overlay.DownloadedSets.Single();
            });
            AddAssert("download intent saved as one like even when transfer cannot start", () =>
            {
                string file = DiscoveryHistory.Filename(API.Endpoints.APIUrl, API.LocalUser.Value.Id, 0);
                var saved = DiscoveryHistory.Load(LocalStorage, file).Saved;
                return saved.Count == 1 && saved[0].OnlineID == likedSet;
            });
            AddUntilStep("recommendations advance", () => overlay.MediaRequests >= 2 && overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddStep("reopen saved choice", () => { requestsBeforeReopen = scoreRequests; overlay.Hide(); overlay.Show(); });
            AddUntilStep("top scores restored", () => scoreRequests > requestsBeforeReopen && button(DiscoveryStrings.Download).Enabled.Value);
            AddStep("open saved maps", () => button(DiscoveryStrings.Saved).Action.Invoke());
            AddStep("download saved map twice", () =>
            {
                button(DiscoveryStrings.Download).Action.Invoke();
                button(DiscoveryStrings.Download).Action.Invoke();
            });
            AddAssert("same map downloaded, no repeated like", () =>
            {
                string file = DiscoveryHistory.Filename(API.Endpoints.APIUrl, API.LocalUser.Value.Id, 0);
                return overlay.DownloadedSets.SequenceEqual(new[] { likedSet, likedSet, likedSet })
                       && DiscoveryHistory.Load(LocalStorage, file).Saved.Count == 1;
            });
        }

        [Test]
        public void TestToolbarAndRapidCardCancellation()
        {
            osu.Game.Overlays.Toolbar.ToolbarDiscoveryButton toolbar = null!;
            int before = 0;
            AddStep("load distinct toolbar button", () => Add(new DependencyProvidingContainer
            {
                CachedDependencies = new (Type, object)[] { (typeof(DiscoveryOverlay), overlay) },
                Width = 60, Height = osu.Game.Overlays.Toolbar.Toolbar.HEIGHT,
                Child = toolbar = new osu.Game.Overlays.Toolbar.ToolbarDiscoveryButton(),
            }));
            AddUntilStep("toolbar bound", () => toolbar.IsLoaded && toolbar.StateContainer == overlay && toolbar.TooltipMain == DiscoveryStrings.Title);
            AddStep("rapid skips", () =>
            {
                before = overlay.MediaRequests;
                for (int i = 0; i < 5; i++) button(DiscoveryStrings.Next).Action.Invoke();
            });
            AddUntilStep("only last card loaded", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddAssert("debounce avoids skipped media", () => overlay.MediaRequests <= before + 6 && overlay.ChildrenOfType<DrawableRuleset>().Count() == 1);
            AddStep("close through toolbar", () => toolbar.Action.Invoke());
            AddAssert("hidden preview stopped and removed", () => overlay.State.Value == Visibility.Hidden && !overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
        }

        [Test]
        public void TestAllStandardModes()
        {
            foreach (int mode in new[] { 1, 2, 3, 0 })
            {
                AddStep("select native mode " + mode, () =>
                {
                    testMode = mode;
                    Ruleset.Value = rulesets.GetRuleset(mode)!;
                });
                AddUntilStep("mode renderer loaded", () => overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any()
                    && preview.ChildrenOfType<DrawableRuleset>().Single().Ruleset.RulesetInfo.OnlineID == mode);
                AddUntilStep("mode clock follows audio", () => Math.Abs(preview.ChildrenOfType<DrawableRuleset>().Single().FrameStableClock.CurrentTime - preview.MapTime) < 250);
            }
        }

        [Test]
        public void TestAutomaticGameplayFromTopScoresAndReturnToTaste()
        {
            AddAssert("no duplicate ruleset selector or explanatory footer", () => !overlay.ChildrenOfType<OverlayRulesetSelector>().Any()
                && !overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.LocalOnly));
            AddStep("choose automatic gameplay", () => { overlay.ProvideSimilar = true; button(DiscoveryStrings.GameplayMode).Action.Invoke(); });
            AddUntilStep("top-score neighbours loaded", () => overlay.SimilaritySeeds.Contains(seed.OnlineID)
                && overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.GameplaySimilar));
            AddAssert("no hand-picked style or archive download", () => overlay.DownloadedSets.Count == 0
                && !overlay.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormDropdown<DiscoveryWorkMode>>().Any());
            AddStep("narrow gameplay layout", () => overlay.Width = 480);
            AddWaitStep("gameplay layout settled", 4);
            AddAssert("primary buttons fit viewport", () => new[] { DiscoveryStrings.Next, DiscoveryStrings.Save, DiscoveryStrings.Download }.All(text =>
                button(text).ScreenSpaceDrawQuad.BottomLeft.Y <= overlay.ScreenSpaceDrawQuad.BottomLeft.Y + 1));
            AddStep("back to taste", () => button(DiscoveryStrings.TasteMode).Action.Invoke());
            AddUntilStep("taste recommendations restored", () => overlay.HasVisibleCard && overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
        }

        [Test]
        public void TestSimilarityFailureUsesCatalogueWithoutHanging()
        {
            AddStep("similarity service down", () => { overlay.FailSimilarity = true; button(DiscoveryStrings.GameplayMode).Action.Invoke(); });
            AddUntilStep("catalogue card remains usable", () => overlay.HasVisibleCard && button(DiscoveryStrings.Save).Enabled.Value);
            AddAssert("fallback is not labelled gameplay similarity", () => !overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.GameplaySimilar));
        }

        [Test]
        public void TestFeedbackActionsIdentityAndOfflineRetry()
        {
            Guid shown = Guid.Empty;
            AddStep("remember impression", () => shown = feedbackEvents.Last(e => e.Action == "show").ImpressionId);
            AddStep("save card", () => button(DiscoveryStrings.Save).Action.Invoke());
            AddUntilStep("save event recorded", () => feedbackEvents.Any(e => e.Action == "save" && e.ImpressionId == shown));
            AddStep("skip next", () => button(DiscoveryStrings.Next).Action.Invoke());
            AddUntilStep("skip event recorded", () => feedbackEvents.Any(e => e.Action == "skip"));
            AddStep("download next", () => button(DiscoveryStrings.Download).Action.Invoke());
            AddUntilStep("download event, not save event", () => feedbackEvents.Any(e => e.Action == "download"));
            AddAssert("valid exact map and source", () => feedbackEvents.All(e => e.BeatmapId == e.BeatmapSetId * 10 && e.Ruleset == 0
                && e.EventId != Guid.Empty && e.SeedIds.Length <= 24));
            AddStep("offline skip", () => { failFeedback = true; button(DiscoveryStrings.Next).Action.Invoke(); });
            AddUntilStep("pending event saved", () => DiscoveryHistory.Load(LocalStorage, DiscoveryHistory.Filename(API.Endpoints.APIUrl, 42, 0)).PendingFeedback.Any());
            AddStep("reconnect and reopen", () => { failFeedback = false; overlay.Hide(); overlay.Show(); });
            AddUntilStep("outbox acknowledged", () => !DiscoveryHistory.Load(LocalStorage, DiscoveryHistory.Filename(API.Endpoints.APIUrl, 42, 0)).PendingFeedback.Any());
        }

        [Test]
        public void TestListenerCandidateHydratesAndReportsMusicSource()
        {
            AddStep("load listener recommendation", () => { provideTasteNeighbours = true; ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 46 }; });
            AddUntilStep("listener map shown", () => overlay.HasVisibleCard && overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.Listeners));
            AddAssert("authoritative map and honest feedback", () => overlay.ShownSets.Last().OnlineID == 200
                && feedbackEvents.Any(e => e.BeatmapSetId == 200 && e.Source == "mosu_listeners" && e.AlgorithmVersion == "music-v2"));
            AddAssert("no external matcher used", () => overlay.SimilaritySeeds.Count == 0);
        }

        [Test]
        public void TestDelayedListenerHydrationPrecedesCatalogueFallback()
        {
            int previousSearches = 0;
            AddStep("delay authoritative listener metadata", () =>
            {
                previousSearches = catalogueRequests;
                provideTasteNeighbours = delaySetLookup = true;
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 49 };
            });
            AddUntilStep("catalogue finished while listener pending", () => pendingSetLookup != null && catalogueRequests > previousSearches);
            AddWaitStep("allow catalogue callbacks", 5);
            AddAssert("catalogue does not win race", () => !overlay.HasVisibleCard);
            AddStep("listener metadata arrives", () => pendingSetLookup!.TriggerSuccess(makeSet(pendingSetLookup.ID)));
            AddUntilStep("listener recommendation shown", () => overlay.HasVisibleCard && overlay.ShownSets.Last().OnlineID == 200);
            AddAssert("music attribution retained", () => feedbackEvents.Any(e => e.BeatmapSetId == 200 && e.Source == "mosu_listeners"));
        }

        [Test]
        public void TestTasteNeverCallsGameplaySimilarityAndOldApiHasHonestFallback()
        {
            AddAssert("taste never calls external gameplay matcher", () => overlay.SimilaritySeeds.Count == 0);
            AddStep("API does not yet support taste", () =>
            {
                failTaste = true;
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 47 };
            });
            AddUntilStep("catalogue fallback still works", () => overlay.HasVisibleCard && overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddAssert("fallback not labelled musical match", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.Similar)
                && overlay.SimilaritySeeds.Count == 0);
        }

        [Test]
        public void TestStaleTasteResponseNeverCrossesAccountOrGeneration()
        {
            DiscoveryTasteRequest old = null!;
            AddStep("delay musical profile", () => { delayTaste = true; ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 48 }; });
            AddUntilStep("taste request waiting", () => pendingTaste != null && !overlay.HasVisibleCard);
            AddStep("switch owner and complete old response", () =>
            {
                old = pendingTaste!;
                delayTaste = false;
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 49 };
                old.TriggerSuccess(new DiscoveryTasteProfile { Candidates = new[] { new DiscoveryTasteCandidate { SetId = 999, Weight = 1, Reason = "listeners" } } });
            });
            AddUntilStep("new owner card loaded", () => overlay.HasVisibleCard && overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any());
            AddAssert("no old owner recommendation", () => overlay.ShownSets.All(s => s.OnlineID != 999));
        }

        [Test]
        public void TestResponsiveLayout()
        {
            foreach (float width in new[] { 1300f, 900f, 480f })
            {
                AddStep("width " + width, () => overlay.Width = width);
                AddWaitStep("layout settled", 4);
                AddAssert("visible buttons fit horizontal bounds", () => overlay.ChildrenOfType<ThemeLibraryButton>().Where(b => b.IsPresent).All(b =>
                    b.ScreenSpaceDrawQuad.TopRight.X <= overlay.ScreenSpaceDrawQuad.TopRight.X + 1));
                AddAssert("playfield is nonempty", () => preview.DrawWidth > 100 && preview.DrawHeight > 100);
                AddAssert("main actions visible without scrolling", () => new[] { DiscoveryStrings.Next, DiscoveryStrings.Save, DiscoveryStrings.Download,
                    DiscoveryStrings.Pause, DiscoveryStrings.Repeat, DiscoveryStrings.FullPreview }.All(text =>
                {
                    var bounds = button(text).ScreenSpaceDrawQuad;
                    return bounds.BottomLeft.Y <= overlay.ScreenSpaceDrawQuad.BottomLeft.Y + 1
                           && bounds.TopLeft.Y >= overlay.ScreenSpaceDrawQuad.TopLeft.Y;
                }));
            }
        }

        [Test]
        public void TestMediaFailureAndStaleScoreResponse()
        {
            AddStep("next uncached media fails", () => { overlay.FailMedia = true; button(DiscoveryStrings.Next).Action.Invoke(); button(DiscoveryStrings.Next).Action.Invoke(); });
            AddUntilStep("failure leaves actions usable", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == DiscoveryStrings.MediaFailed) && button(DiscoveryStrings.Save).Enabled.Value);
            AddStep("delay top scores on next identity", () => { delayScores = true; ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 45 }; });
            AddUntilStep("top request waiting", () => pending != null);
            AddStep("hide then return scores", () => { overlay.Hide(); pending!.TriggerSuccess(new List<SoloScoreInfo> { new SoloScoreInfo { Beatmap = seed } }); });
            AddAssert("stale response never starts gameplay", () => !overlay.ChildrenOfType<DiscoveryGameplayPreview>().Any() && overlay.State.Value == osu.Framework.Graphics.Containers.Visibility.Hidden);
        }

        private partial class TestDiscoveryOverlay : DiscoveryOverlay
        {
            internal bool AudioEnabled;
            internal bool ScoreAllAudio;
            internal IReadOnlyList<APIBeatmap>? AudioMaps;
            internal CancellationToken AudioToken;
            internal volatile Action<int, double>? AudioReady;
            internal TaskCompletionSource<IReadOnlyDictionary<int, double>>? AudioPending;
            internal osu.Framework.Localisation.LocalisableString AudioPhase = DiscoveryStrings.AudioAnalysing;
            protected override osu.Framework.Localisation.LocalisableString AudioRankingStatus => AudioPhase;
            internal IReadOnlyDictionary<int, double> AudioResult = new Dictionary<int, double>();
            internal int AudioSelectedSet;
            internal readonly List<int> AudioSeedIds = new List<int>();
            protected override bool AudioRankingEnabled => AudioEnabled;
            internal override Task<IReadOnlyDictionary<int, double>> RankAudioAsync(IReadOnlyList<APIBeatmap> seeds, IReadOnlyList<APIBeatmap> maps,
                DiscoverySeedSource source, CancellationToken token, Action<int, double> scoreReady)
            {
                AudioSeedIds.AddRange(seeds.Select(s => s.OnlineID));
                AudioMaps = maps;
                AudioToken = token;
                AudioReady = scoreReady;
                if (AudioPending is { } pending) return pending.Task.WaitAsync(token);
                if (ScoreAllAudio)
                {
                    var scores = maps.ToDictionary(m => m.OnlineBeatmapSetID, _ => .9);
                    foreach (var score in scores) scoreReady(score.Key, score.Value);
                    return Task.FromResult<IReadOnlyDictionary<int, double>>(scores);
                }
                if (AudioResult.ContainsKey(-1))
                {
                    var previous = ShownSets.LastOrDefault();
                    AudioSelectedSet = maps.First(m => m.BeatmapSet!.Artist != previous?.Artist && m.BeatmapSet.AuthorString != previous?.AuthorString).OnlineBeatmapSetID;
                    return Task.FromResult<IReadOnlyDictionary<int, double>>(new Dictionary<int, double> { [AudioSelectedSet] = .95 });
                }
                return Task.FromResult(AudioResult);
            }
            internal int MediaRequests;
            internal int InstalledChecks;
            internal bool CheckInstalled(APIBeatmapSet set) => base.IsInstalled(set);
            protected override bool IsInstalled(APIBeatmapSet set) { InstalledChecks++; return set.OnlineID == 102 || base.IsInstalled(set); }
            internal bool FailMedia;
            internal bool MissingAnchor;
            internal bool ProvideSimilar;
            internal bool FailSimilarity;
            internal readonly List<int> SimilaritySeeds = new List<int>();
            protected override Task<IReadOnlyList<DiscoveryNeighbour>> FindSimilarAsync(int seedId, CancellationToken token)
            {
                SimilaritySeeds.Add(seedId);
                if (FailSimilarity) return Task.FromException<IReadOnlyList<DiscoveryNeighbour>>(new System.Net.Http.HttpRequestException("Fixture unavailable"));
                return Task.FromResult<IReadOnlyList<DiscoveryNeighbour>>(ProvideSimilar
                    ? Enumerable.Range(200, 8).Select(id => new DiscoveryNeighbour(id * 10, id, 0.9, seedId)).ToArray()
                    : Array.Empty<DiscoveryNeighbour>());
            }
            internal readonly List<int> DownloadedSets = new List<int>();
            protected override bool DownloadSet(APIBeatmapSet set)
            {
                DownloadedSets.Add(set.OnlineID);
                return false; // No network transfer in fixtures; click intent still counts as a like.
            }
            internal readonly List<APIBeatmapSet> ShownSets = new List<APIBeatmapSet>();
            protected override Drawable CreateCover(APIBeatmapSet set)
            {
                ShownSets.Add(set);
                return new Container
                {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(0.16f, 0.25f, 0.29f, 1) },
                    new Circle { Size = new osuTK.Vector2(190), Position = new osuTK.Vector2(90, -20), Colour = new Colour4(0.32f, 0.56f, 0.57f, 1) },
                    new Circle { Size = new osuTK.Vector2(280), Position = new osuTK.Vector2(220, -80), Colour = new Colour4(0.22f, 0.39f, 0.43f, 1) },
                },
                };
            }

            protected override Task<(Beatmap Map, byte[]? Audio)> LoadPreviewAsync(APIBeatmap map, CancellationToken token)
            {
                MediaRequests++;
                if (FailMedia) throw new TaskCanceledException("Fixture internal transfer deadline (card itself is not cancelled)");
                if (Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_MEDIA") is string mediaRoot && map.RulesetID == 0)
                {
                    var selected = new APIBeatmap
                    {
                        OnlineID = int.Parse(Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_MAP") ?? "131891", System.Globalization.CultureInfo.InvariantCulture),
                        OnlineBeatmapSetID = int.Parse(Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_SET") ?? "41823", System.Globalization.CultureInfo.InvariantCulture), RulesetID = 0,
                    };
                    var realMap = DiscoveryMedia.Decode(System.IO.File.ReadAllBytes(System.IO.Path.Combine(mediaRoot, "map.osu")), selected);
                    if (MissingAnchor) realMap.Metadata.PreviewTime = -1;
                    return Task.FromResult<(Beatmap, byte[]?)>((realMap, System.IO.File.ReadAllBytes(System.IO.Path.Combine(mediaRoot, "clip"))));
                }
                string objects = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"{100 + i % 4 * 100},{100 + i % 3 * 80},{59000 + i * 300},1,0,0:0:0:0:"));
                string raw = $"osu file format v14\n[General]\nAudioFilename: preview.ogg\nPreviewTime: 60000\nMode: {map.RulesetID}\n[Metadata]\nTitle:Test\nArtist:Test\nCreator:Test\nVersion:Test\nBeatmapID:{map.OnlineID}\nBeatmapSetID:{map.OnlineBeatmapSetID}\n[Difficulty]\nHPDrainRate:5\nCircleSize:4\nOverallDifficulty:5\nApproachRate:7\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,344.827586,4,2,0,100,1,0\n[HitObjects]\n{objects}\n";
                var decoded = DiscoveryMedia.Decode(Encoding.UTF8.GetBytes(raw), map);
                if (MissingAnchor) decoded.Metadata.PreviewTime = -1;
                return Task.FromResult<(Beatmap, byte[]?)>((decoded, null));
            }
        }
    }
}
