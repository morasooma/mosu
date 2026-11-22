// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osuTK.Input;
using APIUser = osu.Game.Online.API.Requests.Responses.APIUser;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneBeatmapListingOverlay : OsuManualInputManagerTestScene
    {
        private readonly List<APIBeatmapSet> setsForResponse = new List<APIBeatmapSet>();

        private BeatmapListingOverlay overlay;

        private BeatmapListingSearchControl searchControl => overlay.ChildrenOfType<BeatmapListingSearchControl>().Single();

        private OsuConfigManager localConfig;

        private bool returnCursorOnResponse;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(localConfig = new OsuConfigManager(LocalStorage));
            // The base fixture replaces IAPIProvider, but its inherited catalogue
            // router still points at the real API. Keep these UI tests fully local.
            Dependencies.CacheAs<IBeatmapApiProvider>(new BeatmapApiProvider(API, null));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setup overlay", () =>
            {
                localConfig.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Auto);
                Child = overlay = new BeatmapListingOverlay { State = { Value = Visibility.Visible } };
                setsForResponse.Clear();
            });

            AddStep("initialize dummy", () =>
            {
                var api = (DummyAPIAccess)API;

                api.HandleRequest = req =>
                {
                    if (!(req is SearchBeatmapSetsRequest searchBeatmapSetsRequest)) return false;

                    searchBeatmapSetsRequest.TriggerSuccess(new SearchBeatmapSetsResponse
                    {
                        BeatmapSets = setsForResponse,
                        Cursor = returnCursorOnResponse ? new Cursor() : null,
                    });

                    return true;
                };

                // non-supporter user
                api.LocalUser.Value = new APIUser
                {
                    Username = "TestBot",
                    Id = API.LocalUser.Value.Id + 1,
                };
            });

            AddStep("reset size", () => localConfig.SetValue(OsuSetting.BeatmapListingCardSize, BeatmapCardSize.Normal));
        }


        [Test]
        public void TestInitialMirrorSortAndNumericQueryUseSupportedSort()
        {
            var requestedSorts = new List<SortCriteria>();
            AddUntilStep("filters ready", () => searchControl.FiltersReady);
            AddStep("record sorts and select Mirria", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    if (req is not SearchBeatmapSetsRequest search) return false;
                    requestedSorts.Add(search.SortCriteria);
                    search.TriggerSuccess(new SearchBeatmapSetsResponse { BeatmapSets = setsForResponse });
                    return true;
                };
                localConfig.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Mirria);
            });
            AddUntilStep("initial request", () => requestedSorts.Count > 0);
            AddAssert("only Updated is exposed", () => overlay.ChildrenOfType<osu.Framework.Graphics.UserInterface.TabItem<SortCriteria>>()
                .Select(t => t.Value).Distinct().SequenceEqual(new[] { SortCriteria.Updated }));
            AddStep("numeric query", () => { requestedSorts.Clear(); overlay.ShowWithSearch("star>6"); });
            AddUntilStep("numeric request", () => requestedSorts.Count > 0);
            AddAssert("never defaults to unsupported ranked or relevance", () => requestedSorts.All(s => s == SortCriteria.Updated));
        }

        [Test]
        public void TestCatalogueSelectorPersistsResetsCursorAndStaysInsideHeader()
        {
            var requests = new List<BeatmapCatalogueSource>();
            FormEnumDropdown<BeatmapCatalogueSource> selector = null;
            AddUntilStep("filters ready", () => searchControl.FiltersReady);
            AddStep("record catalogue choices", () =>
            {
                selector = overlay.ChildrenOfType<FormEnumDropdown<BeatmapCatalogueSource>>().Single();
                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    if (req is not SearchBeatmapSetsRequest search) return false;
                    requests.Add(search.CatalogueSource);
                    search.TriggerSuccess(new SearchBeatmapSetsResponse { BeatmapSets = setsForResponse, Cursor = null });
                    return true;
                };
                selector.Current.Value = BeatmapCatalogueSource.Mirria;
            });
            AddUntilStep("Mirria requested", () => requests.Contains(BeatmapCatalogueSource.Mirria));
            AddAssert("persisted", () => localConfig.Get<BeatmapCatalogueSource>(OsuSetting.BeatmapCatalogueSource) == BeatmapCatalogueSource.Mirria);
            AddAssert("cursor reset", () => overlay.ChildrenOfType<BeatmapListingFilterControl>().Single().CurrentPage == 0);
            AddAssert("selector above search without overlap", () => selector.ScreenSpaceDrawQuad.BottomLeft.Y <= searchControl.ScreenSpaceDrawQuad.TopLeft.Y);
            AddStep("open selector", () =>
            {
                InputManager.MoveMouseTo(selector);
                InputManager.Click(MouseButton.Left);
            });
            AddWaitStep("menu open", 3);
            AddStep("select Nekoha with mouse", () =>
            {
                var option = selector.ChildrenOfType<osu.Framework.Graphics.UserInterface.Dropdown<BeatmapCatalogueSource>.DropdownMenu.DrawableDropdownMenuItem>()
                    .Single(item => item.Item.Text.Value.ToString() == "Nekoha");
                InputManager.MoveMouseTo(option);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("Nekoha requested", () => requests.Contains(BeatmapCatalogueSource.Nekoha));
            AddStep("hide and reopen", () => { overlay.Hide(); overlay.Show(); });
            AddAssert("choice retained", () => selector.Current.Value == BeatmapCatalogueSource.Nekoha);
        }

        [TestCase(BeatmapCatalogueSource.OsuDirect, "osu.direct")]
        [TestCase(BeatmapCatalogueSource.Hinamizawa, "Hinamizawa")]
        public void TestAdditionalCatalogueCanBeChosenWithMouse(BeatmapCatalogueSource source, string label)
        {
            SearchBeatmapSetsRequest lastRequest = null;
            FormEnumDropdown<BeatmapCatalogueSource> selector = null;
            AddUntilStep("filters ready", () => searchControl.FiltersReady);
            AddStep("open catalogue selector", () =>
            {
                selector = overlay.ChildrenOfType<FormEnumDropdown<BeatmapCatalogueSource>>().Single();
                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    if (req is not SearchBeatmapSetsRequest search) return false;
                    lastRequest = search;
                    search.TriggerSuccess(new SearchBeatmapSetsResponse { BeatmapSets = setsForResponse });
                    return true;
                };
                InputManager.MoveMouseTo(selector);
                InputManager.Click(MouseButton.Left);
            });
            AddWaitStep("menu open", 3);
            AddStep("choose catalogue", () =>
            {
                var option = selector.ChildrenOfType<osu.Framework.Graphics.UserInterface.Dropdown<BeatmapCatalogueSource>.DropdownMenu.DrawableDropdownMenuItem>()
                    .Single(item => item.Item.Text.Value.ToString() == label);
                InputManager.MoveMouseTo(option);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("new source requested", () => lastRequest?.CatalogueSource == source);
            AddAssert("choice saved", () => localConfig.Get<BeatmapCatalogueSource>(OsuSetting.BeatmapCatalogueSource) == source);
            AddAssert("supported initial sort", () => lastRequest.SortCriteria == SortCriteria.Updated);
            AddAssert("play count sorting available", () => overlay.ChildrenOfType<osu.Framework.Graphics.UserInterface.TabItem<SortCriteria>>().Any(t => t.Value == SortCriteria.Plays));
        }

        [Test]
        public void TestFiltersAreStagedBeforeTheFirstRequestAndRetainedOnReopen()
        {
            bool allRequestsReady = true;
            bool partialPublicationObserved = false;
            int requests = 0;
            BeatmapSearchRulesetFilterRow[] originalRows = null;
            AddStep("replace with fresh overlay", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    if (req is not SearchBeatmapSetsRequest search)
                        return false;
                    requests++;
                    allRequestsReady &= searchControl.FiltersReady && searchControl.Ruleset.Value != null;
                    search.TriggerSuccess(new SearchBeatmapSetsResponse { BeatmapSets = new List<APIBeatmapSet>(), Cursor = null });
                    return true;
                };
                Child = overlay = new BeatmapListingOverlay { State = { Value = Visibility.Visible } };
                // Test-step polling can skip the entire staged admission at Unlimited FPS.
                // Observe each Update rather than infer batching from wall-clock samples.
                overlay.OnUpdate += _ =>
                {
                    int count = overlay.ChildrenOfType<BeatmapSearchFilterRow<SearchGenre>>().Count();
                    partialPublicationObserved |= count > 0 && !searchControl.FiltersReady;
                };
            });
            AddUntilStep("filters complete", () => searchControl.FiltersReady);
            AddUntilStep("first request completed", () => requests > 0);
            AddAssert("request never used incomplete defaults", () => allRequestsReady);
            AddAssert("not all rows attached together", () => partialPublicationObserved);
            AddStep("remember rows and hide", () =>
            {
                originalRows = overlay.ChildrenOfType<BeatmapSearchRulesetFilterRow>().ToArray();
                overlay.Hide();
            });
            AddWaitStep("hidden", 5);
            AddStep("reopen", () => overlay.Show());
            AddWaitStep("shown", 5);
            AddAssert("same rows retained", () => overlay.ChildrenOfType<BeatmapSearchRulesetFilterRow>().SequenceEqual(originalRows));
        }

        [Test]
        public void TestHeaderBlocksInitialiseAcrossFramesAndRemainLoadedOnReopen()
        {
            var loaded = new HashSet<BeatmapListingFilterControl.HeaderLoadBlock>();
            int maximumNewBlocks = 0;
            BeatmapListingFilterControl.HeaderLoadBlock[] original = null;
            AddStep("show a fresh overlay", () =>
            {
                Child = overlay = new BeatmapListingOverlay { State = { Value = Visibility.Visible } };
                overlay.OnUpdate += _ =>
                {
                    int newlyLoaded = 0;
                    foreach (var block in overlay.ChildrenOfType<BeatmapListingFilterControl.HeaderLoadBlock>())
                    {
                        if (block.IsLoaded && loaded.Add(block))
                            newlyLoaded++;
                    }
                    maximumNewBlocks = Math.Max(maximumNewBlocks, newlyLoaded);
                };
            });
            AddUntilStep("all header blocks loaded", () => loaded.Count == 3);
            AddAssert("at most one first update per frame", () => maximumNewBlocks == 1);
            AddUntilStep("filter defaults ready", () => searchControl.FiltersReady);
            AddStep("remember and reopen", () =>
            {
                original = loaded.ToArray();
                overlay.Hide();
                overlay.Show();
            });
            AddWaitStep("reopened", 3);
            AddAssert("same loaded blocks retained", () =>
            {
                var current = overlay.ChildrenOfType<BeatmapListingFilterControl.HeaderLoadBlock>().ToArray();
                return current.Length == 3 && current.All(block => block.IsLoaded && original.Contains(block));
            });
        }

        [Test]
        public void TestHeaderAdmissionDoesNotSuppressLoadedUpdates()
        {
            bool admit = false;
            int updates = 0;
            int previousUpdates = 0;
            BeatmapListingFilterControl.HeaderLoadBlock block = null;
            AddStep("add deferred block", () =>
            {
                var child = new Container();
                child.OnUpdate += _ => updates++;
                Add(block = new BeatmapListingFilterControl.HeaderLoadBlock(() => admit)
                {
                    AutoSizeAxes = osu.Framework.Graphics.Axes.None,
                    Height = 10,
                    Child = child
                });
            });
            AddWaitStep("admission denied", 3);
            AddAssert("not initialised prematurely", () => !block.IsLoaded && updates == 0);
            AddStep("admit", () => admit = true);
            AddUntilStep("block updating", () => block.IsLoaded && updates > 0);
            AddStep("deny later admissions", () => { admit = false; previousUpdates = updates; });
            AddWaitStep("ordinary updates", 3);
            AddAssert("already loaded subtree still updates", () => updates > previousUpdates);
            AddStep("remove block", () => Remove(block, true));
        }

        [Test]
        public void TestHiddenOverlayDefersSearchUntilShown()
        {
            int requests = 0;
            AddStep("count searches", () =>
            {
                var dodge = Dependencies.Get<RulesetStore>().GetRuleset(0).Clone();
                dodge.OnlineID = 10;
                dodge.ShortName = "dodge";
                searchControl.Ruleset.Value = dodge;
                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    if (req is not SearchBeatmapSetsRequest search)
                        return false;
                    requests++;
                    search.TriggerSuccess(new SearchBeatmapSetsResponse { BeatmapSets = new List<APIBeatmapSet>(), Cursor = null });
                    return true;
                };
                overlay.Hide();
            });
            AddWaitStep("finish hiding", 10);
            AddStep("reset counter and query hidden", () =>
            {
                requests = 0;
                overlay.ShowWithSearch("hidden-search");
                overlay.Hide();
            });
            AddWaitStep("past debounce", 10);
            AddAssert("no hidden requests", () => requests == 0);
            AddStep("show", () => overlay.Show());
            AddUntilStep("deferred query requested", () => requests > 0);
        }

        [Test]
        public void TestHideViaBack()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hide", () => InputManager.Key(Key.Escape));
            AddUntilStep("is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestOpenForDodgeRuleset()
        {
            RulesetInfo dodge = null;

            AddStep("create Dodge ruleset info", () =>
            {
                dodge = Dependencies.Get<RulesetStore>().GetRuleset(0).Clone();
                dodge.OnlineID = 10;
                dodge.Name = "Dodge";
                dodge.ShortName = "dodge";
            });
            AddStep("show for Dodge", () => overlay.ShowForRuleset(dodge));
            AddUntilStep("Dodge selected", () => searchControl.Ruleset.Value.OnlineID == 10);
            AddAssert("server-exclusive forced", () => searchControl.ServerExclusive.Value);
        }

        [Test]
        public void TestServerExclusiveToggleIsLockedOnlyForDodge()
        {
            FilterTabItem<SearchExtra> tab = null;
            AddUntilStep("filters ready", () => searchControl.FiltersReady);
            AddStep("select osu and clear filter", () =>
            {
                searchControl.Ruleset.Value = Dependencies.Get<RulesetStore>().GetRuleset(0);
                searchControl.Extra.Remove(SearchExtra.ServerExclusive);
                tab = searchControl.ChildrenOfType<FilterTabItem<SearchExtra>>().Single(t => t.Value == SearchExtra.ServerExclusive && t.GetType().Name == "ServerExclusiveTabItem");
            });
            AddStep("click to enable", () =>
            {
                InputManager.MoveMouseTo(tab);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("enabled", () => searchControl.ServerExclusive.Value);
            AddStep("click to disable", () => InputManager.Click(MouseButton.Left));
            AddAssert("disabled", () => !searchControl.ServerExclusive.Value);
            AddStep("select Dodge", () =>
            {
                var dodge = Dependencies.Get<RulesetStore>().GetRuleset(0).Clone();
                dodge.OnlineID = 10;
                dodge.ShortName = "dodge";
                searchControl.Ruleset.Value = dodge;
            });
            AddAssert("forced and locked", () => searchControl.ServerExclusive.Value && tab.Active.Disabled);
            AddStep("click locked tab", () =>
            {
                InputManager.MoveMouseTo(tab);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("still forced", () => searchControl.ServerExclusive.Value);
            AddStep("select another Dodge info", () => searchControl.Ruleset.Value = searchControl.Ruleset.Value.Clone());
            AddStep("switch back to osu", () => searchControl.Ruleset.Value = Dependencies.Get<RulesetStore>().GetRuleset(0));
            AddAssert("previous filter restored and unlocked", () => !searchControl.ServerExclusive.Value && !tab.Active.Disabled);
        }

        [Test]
        public void TestHideViaBackWithSearch()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("search something", () => overlay.ChildrenOfType<SearchTextBox>().First().Text = "search");

            AddStep("kill search", () => InputManager.Key(Key.Escape));

            AddAssert("search textbox empty", () => string.IsNullOrEmpty(overlay.ChildrenOfType<SearchTextBox>().First().Text));
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("hide", () => InputManager.Key(Key.Escape));
            AddUntilStep("is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestHideViaBackWithScrolledSearch()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));

            AddUntilStep("placeholder hidden", () => !overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any(d => d.IsPresent));

            AddStep("scroll to bottom", () => overlay.ChildrenOfType<OverlayScrollContainer>().First().ScrollToEnd());

            AddStep("kill search", () => InputManager.Key(Key.Escape));

            AddUntilStep("search textbox empty", () => string.IsNullOrEmpty(overlay.ChildrenOfType<SearchTextBox>().First().Text));
            AddUntilStep("is scrolled to top", () => overlay.ChildrenOfType<OverlayScrollContainer>().First().Current == 0);
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("hide", () => InputManager.Key(Key.Escape));
            AddUntilStep("is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestCorrectOldContentExpiration()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(100);

            AddStep("show more results", () => fetchFor(getManyBeatmaps(30).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(30);
        }

        [Test]
        public void TestCardSizeSwitching([Values] bool viaConfig)
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(100);

            setCardSize(BeatmapCardSize.Extra, viaConfig);
            assertAllCardsOfType<BeatmapCardExtra>(100);

            setCardSize(BeatmapCardSize.Normal, viaConfig);
            assertAllCardsOfType<BeatmapCardNormal>(100);

            AddStep("fetch for 0 beatmaps", () => fetchFor());
            AddUntilStep("placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault()?.IsPresent == true);

            setCardSize(BeatmapCardSize.Extra, viaConfig);
            AddAssert("placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault()?.IsPresent == true);
        }

        [Test]
        public void TestCardSizeChangeDuringPublicationPreservesAllResults()
        {
            bool changed = false;
            AddStep("publish and resize during admission", () =>
            {
                overlay.OnUpdate += _ =>
                {
                    int count = overlay.ChildrenOfType<BeatmapCardEntry>().Count();
                    if (!changed && count > 0 && count < 200)
                    {
                        changed = true;
                        localConfig.SetValue(OsuSetting.BeatmapListingCardSize, BeatmapCardSize.Extra);
                    }
                };
                fetchFor(getManyBeatmaps(200));
            });
            AddUntilStep("size changed during publication", () => changed);
            assertAllCardsOfType<BeatmapCardExtra>(200);
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        [TestCase(BeatmapCardSize.Nano)]
        public void TestResultOrderSurvivesPaginationAndSizeChange(BeatmapCardSize size)
        {
            APIBeatmapSet[] expected = null;
            AddStep("prepare ordered pages", () =>
            {
                expected = getManyBeatmaps(75);
                fetchFor(expected.Take(50).ToArray(), true);
            });
            assertAllCardsOfType<BeatmapCardNormal>(50);
            assertOrder(50);
            AddStep("prepare second page", () => setSearchResponse(expected.Skip(50).ToArray(), false));
            AddStep("scroll to next page", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd());
            assertAllCardsOfType<BeatmapCardNormal>(75);
            assertOrder(75);
            setCardSize(size, true);
            AddUntilStep("all entries resized", () => overlay.ChildrenOfType<BeatmapCardEntry>().Count(e => e.CardSize == size) == 75);
            assertOrder(75);
            AddStep("hide", () => overlay.Hide());
            AddUntilStep("heavy cards unloaded", () => !overlay.ChildrenOfType<BeatmapCard>().Any());
            AddStep("reopen at start", () =>
            {
                overlay.Show();
                overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToStart(false);
            });
            AddUntilStep("visible cards restored", () => overlay.ChildrenOfType<BeatmapCard>().Any(c => c.IsLoaded));
            assertOrder(75);

            void assertOrder(int count) => AddUntilStep("screen order matches response", () => overlay.ChildrenOfType<BeatmapCardEntry>()
                .OrderBy(e => e.Y).ThenBy(e => e.X).Select(e => e.BeatmapSet.OnlineID).SequenceEqual(expected.Take(count).Select(set => set.OnlineID)));
        }

        [Test]
        public void TestNewSearchCancelsUnpublishedCards()
        {
            bool replaced = false;
            AddStep("replace search during admission", () =>
            {
                overlay.OnUpdate += _ =>
                {
                    int count = overlay.ChildrenOfType<BeatmapCardEntry>().Count();
                    if (!replaced && count > 0 && count < 200)
                    {
                        replaced = true;
                        fetchFor(getManyBeatmaps(7));
                    }
                };
                fetchFor(getManyBeatmaps(200));
            });
            AddUntilStep("search replaced during publication", () => replaced);
            assertAllCardsOfType<BeatmapCardNormal>(7);
            AddWaitStep("old publication cannot return", 5);
            AddAssert("only current results", () => overlay.ChildrenOfType<BeatmapCardEntry>().Count() == 7);
        }

        [Test]
        public void TestReplacingResultsDisposesPublishedCards()
        {
            BeatmapCard[] oldCards = null;
            AddStep("load first results", () => fetchFor(getManyBeatmaps(50)));
            assertAllCardsOfType<BeatmapCardNormal>(50);
            AddStep("observe old card disposal", () =>
            {
                oldCards = overlay.ChildrenOfType<BeatmapCard>().ToArray();
            });
            AddStep("replace results", () => fetchFor(getManyBeatmaps(7)));
            assertAllCardsOfType<BeatmapCardNormal>(7);
            AddUntilStep("all previous cards disposed", () => oldCards.All(card => (bool)typeof(osu.Framework.Graphics.Drawable)
                .GetProperty("IsDisposed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(card)!));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestPlaceholderReleasesPreviousResultOwner(int placeholder)
        {
            BeatmapCardEntry[] oldEntries = null;
            WeakReference<BeatmapCardEntry>[] weakEntries = null;
            AddStep("publish results", () => fetchFor(getManyBeatmaps(50)));
            assertAllCardsOfType<BeatmapCardNormal>(50);
            AddStep("replace with placeholder", () =>
            {
                oldEntries = overlay.ChildrenOfType<BeatmapCardEntry>().ToArray();
                var result = placeholder switch
                {
                    0 => BeatmapListingFilterControl.SearchResult.ResultsReturned(new List<APIBeatmapSet>()),
                    1 => BeatmapListingFilterControl.SearchResult.ErrorReturned("Expected test error"),
                    _ => BeatmapListingFilterControl.SearchResult.SupporterOnlyFilters(new List<osu.Framework.Localisation.LocalisableString>())
                };
                typeof(BeatmapListingOverlay).GetMethod("onSearchFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(overlay, new object[] { result });
            });
            AddAssert("no stale result owner", () => typeof(BeatmapListingOverlay)
                .GetField("foundContent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(overlay) == null);
            AddUntilStep("previous entries disposed", () => oldEntries.All(entry => (bool)typeof(osu.Framework.Graphics.Drawable)
                .GetProperty("IsDisposed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(entry)!));
            AddStep("drop test's strong references", () =>
            {
                weakEntries = oldEntries.Select(entry => new WeakReference<BeatmapCardEntry>(entry)).ToArray();
                oldEntries = null;
            });
            AddWaitStep("drain disposal callbacks", 3);
            AddStep("diagnostic collection", () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
            AddAssert("old entries collectible", () => weakEntries.All(reference => !reference.TryGetTarget(out _)));
            AddStep("search again", () => fetchFor(getManyBeatmaps(7)));
            assertAllCardsOfType<BeatmapCardNormal>(7);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPlaceholderCancelsPendingPublication(bool duringPublication)
        {
            bool replaced = false;
            AddStep("settle initial search", () => fetchFor(getManyBeatmaps(7)));
            assertAllCardsOfType<BeatmapCardNormal>(7);
            AddStep("replace pending results with placeholder", () =>
            {
                void replace()
                {
                    typeof(BeatmapListingOverlay).GetMethod("replaceResultsAreaContent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(overlay, new object[] { new BeatmapListingOverlay.NotFoundDrawable() });
                    replaced = true;
                }
                if (duringPublication)
                {
                    overlay.OnUpdate += _ =>
                    {
                        int count = overlay.ChildrenOfType<BeatmapCardEntry>().Count();
                        if (!replaced && count > 0 && count != 7 && count < 200)
                            replace();
                    };
                }
                typeof(BeatmapListingOverlay).GetMethod("onSearchFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(overlay, new object[] { BeatmapListingFilterControl.SearchResult.ResultsReturned(getManyBeatmaps(200).ToList()) });
                if (!duringPublication)
                    replace();
            });
            AddUntilStep("placeholder installed", () => replaced);
            AddWaitStep("drain background publication", 10);
            AddAssert("old entries cannot return", () => !overlay.ChildrenOfType<BeatmapCardEntry>().Any()
                && overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any());
            AddAssert("no detached results retained", () => typeof(BeatmapListingOverlay)
                .GetField("foundContent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(overlay) == null);
            AddStep("new results still publish", () => fetchFor(getManyBeatmaps(7)));
            assertAllCardsOfType<BeatmapCardNormal>(7);
        }

        [Test]
        public void TestFastScrollBoundsRecentlyHiddenCardTrees()
        {
            bool finished = false;
            int hiddenCount = -1;
            int totalLoaded = 0;
            int visited = 0;
            AddStep("publish long list", () => fetchFor(getManyBeatmaps(300)));
            assertAllCardsOfType<BeatmapCardNormal>(300);
            AddStep("scroll through several viewports within unload timeout", () =>
            {
                var scroll = overlay.ChildrenOfType<OverlayScrollContainer>().Single();
                for (int i = 1; i <= 8; i++)
                {
                    int step = i;
                    Scheduler.AddDelayed(() =>
                    {
                        visited += overlay.ChildrenOfType<BeatmapCard>().Count(c => c.IsLoaded);
                        scroll.ScrollTo(step * 400, false);
                    }, i * 120);
                }
                Scheduler.AddDelayed(() =>
                {
                    var entries = overlay.ChildrenOfType<BeatmapCardEntry>().Where(e => e.Content?.IsLoaded == true).ToArray();
                    totalLoaded = entries.Length;
                    hiddenCount = entries.Count(e => !e.ComputeMaskingBounds().IntersectsWith(e.ScreenSpaceDrawQuad.AABBFloat));
                    finished = true;
                }, 1500);
            });
            AddUntilStep("scroll sequence completed", () => finished);
            AddAssert("real loaded cards visited", () => visited > 12 && totalLoaded > 0);
            AddAssert("hidden neighbourhood bounded before two second TTL", () => hiddenCount <= 12);
        }

        [Test]
        public void TestNoBeatmapsPlaceholder()
        {
            AddStep("fetch for 0 beatmaps", () => fetchFor());
            placeholderShown();

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(100);
            AddUntilStep("placeholder hidden", () => !overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any(d => d.IsPresent));

            AddStep("fetch for 0 beatmaps", () => fetchFor());
            placeholderShown();

            // fetch once more to ensure nothing happens in displaying placeholder again when it already is present.
            AddStep("fetch for 0 beatmaps again", () => fetchFor());
            placeholderShown();

            void placeholderShown() =>
                AddUntilStep("placeholder shown", () =>
                {
                    var notFoundDrawable = overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault();
                    return notFoundDrawable != null && notFoundDrawable.IsPresent && notFoundDrawable.Parent!.DrawHeight > 0;
                });
        }

        /// <summary>
        /// During pagination, the first beatmap of the second page may be a duplicate of the last beatmap from the previous page.
        /// This is currently the case with osu!web API due to ES relevance score's presence in the response cursor.
        /// See: https://github.com/ppy/osu-web/issues/9270
        /// </summary>
        [Test]
        public void TestDuplicatedBeatmapOnlyShowsOnce()
        {
            APIBeatmapSet beatmapSet = null;

            AddStep("show many results", () =>
            {
                beatmapSet = CreateAPIBeatmapSet(Ruleset.Value);
                beatmapSet.Title = "last beatmap of first page";

                fetchFor(getManyBeatmaps(49).Append(new APIBeatmapSet { Title = "last beatmap of first page", OnlineID = beatmapSet.OnlineID }).ToArray(), true);
            });
            assertAllCardsOfType<BeatmapCardNormal>(50);

            AddStep("set next page", () => setSearchResponse(getManyBeatmaps(49).Prepend(new APIBeatmapSet { Title = "this shouldn't show up", OnlineID = beatmapSet.OnlineID }).ToArray(), false));
            AddStep("scroll to end", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd());
            assertAllCardsOfType<BeatmapCardNormal>(99);

            AddAssert("beatmap not duplicated", () => overlay.ChildrenOfType<BeatmapCardEntry>().Count(c => c.BeatmapSet.Equals(beatmapSet)) == 1);
        }

        [Test]
        public void TestUserWithoutSupporterUsesSupporterOnlyFiltersWithoutResults()
        {
            AddStep("select official catalogue", () => localConfig.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Official));
            AddStep("fetch for 0 beatmaps", () => fetchFor());

            AddStep("set dummy as non-supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = false);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            notFoundPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();

            // both RankAchieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();
        }

        [Test]
        public void TestUserWithSupporterUsesSupporterOnlyFiltersWithoutResults()
        {
            AddStep("select official catalogue", () => localConfig.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Official));
            AddStep("fetch for 0 beatmaps", () => fetchFor());
            AddStep("set dummy as supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = true);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            notFoundPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            notFoundPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            notFoundPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();

            // both Rank Achieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            notFoundPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();
        }

        [Test]
        public void TestUserWithoutSupporterUsesSupporterOnlyFiltersWithResults()
        {
            AddStep("select official catalogue", () => localConfig.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Official));
            AddStep("fetch for 1 beatmap", () => fetchFor(CreateAPIBeatmapSet(Ruleset.Value)));

            noPlaceholderShown();

            AddStep("set dummy as non-supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = false);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            noPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();

            // both Rank Achieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();
        }

        [Test]
        public void TestUserWithSupporterUsesSupporterOnlyFiltersWithResults()
        {
            AddStep("select official catalogue", () => localConfig.SetValue(OsuSetting.BeatmapCatalogueSource, BeatmapCatalogueSource.Official));
            AddStep("fetch for 1 beatmap", () => fetchFor(CreateAPIBeatmapSet(Ruleset.Value)));

            noPlaceholderShown();

            AddStep("set dummy as supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = true);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            noPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            noPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            noPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();

            // both Rank Achieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            noPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();
        }

        [Test]
        public void TestVisibleCardInitializationIsSpreadAcrossFrames()
        {
            var initialized = new HashSet<BeatmapCard>();
            int maximumPerFrame = 0;
            int firstViewportCount = 0;
            AddStep("observe completed card initialization", () =>
            {
                overlay.OnUpdate += _ =>
                {
                    int added = 0;
                    foreach (var card in overlay.ChildrenOfType<BeatmapCard>().Where(c => c.IsLoaded))
                    {
                        if (initialized.Add(card))
                            added++;
                    }
                    maximumPerFrame = Math.Max(maximumPerFrame, added);
                };
                fetchFor(getManyBeatmaps(200));
            });
            AddUntilStep("first viewport initialized", () => initialized.Count > 0);
            AddWaitStep("settle visible viewport", 10);
            AddStep("scroll to next viewport", () =>
            {
                firstViewportCount = initialized.Count;
                overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollBy(450, false);
            });
            AddUntilStep("next viewport initialized", () => initialized.Count > firstViewportCount);
            AddUntilStep("at most one new heavy tree per frame", () => maximumPerFrame, () => Is.EqualTo(1));
        }

        [Test]
        public void TestOnlyVisibleCardTreesAreRetained()
        {
            AddStep("load large result page", () => fetchFor(getManyBeatmaps(200)));
            AddUntilStep("visible cards loaded", () => overlay.ChildrenOfType<BeatmapCard>().Any(c => c.IsLoaded));
            AddWaitStep("finish page publication", 5);
            AddAssert("offscreen cards have no heavy trees", () => overlay.ChildrenOfType<BeatmapCard>().Count() <= 36);
        }

        [Test]
        public void TestHiddenListingReleasesCardTrees()
        {
            AddStep("load results", () => fetchFor(getManyBeatmaps(50)));
            AddUntilStep("visible cards loaded", () => overlay.ChildrenOfType<BeatmapCard>().Any(c => c.IsLoaded));
            AddStep("hide library", () => overlay.Hide());
            AddUntilStep("hidden cards unloaded", () => !overlay.ChildrenOfType<BeatmapCard>().Any());
            AddStep("reopen library", () => overlay.Show());
            AddUntilStep("visible cards restored", () => overlay.ChildrenOfType<BeatmapCard>().Any(c => c.IsLoaded));
        }

        [Test]
        public void TestOffscreenCardSubtreesStopUpdating()
        {
            int visibleUpdates = 0;
            AddStep("load 100 cards", () => fetchFor(getManyBeatmaps(100)));
            assertAllCardsOfType<BeatmapCardNormal>(100);
            AddWaitStep("settle card layout", 5);
            AddStep("observe visible thumbnail", () =>
            {
                var card = overlay.ChildrenOfType<BeatmapCard>().OrderBy(c => c.ScreenSpaceDrawQuad.TopLeft.Y).First();
                card.ChildrenOfType<BeatmapCardThumbnail>().Single().OnUpdate += _ => visibleUpdates++;
            });
            AddWaitStep("observe steady updates", 5);
            AddAssert("visible thumbnail updated", () => visibleUpdates > 0);
            AddAssert("last offscreen entry has no card tree", () => overlay.ChildrenOfType<BeatmapCardEntry>()
                .OrderBy(c => c.ScreenSpaceDrawQuad.TopLeft.Y).Last().Content == null);
        }

        [Test]
        public void TestExpandedCardContentNotClipped()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show result with many difficulties", () =>
            {
                var beatmapSet = CreateAPIBeatmapSet(Ruleset.Value);
                beatmapSet.Beatmaps = Enumerable.Repeat(beatmapSet.Beatmaps.First(), 100).ToArray();
                fetchFor(beatmapSet);
            });
            assertAllCardsOfType<BeatmapCardNormal>(1);

            AddStep("hover extra info row", () =>
            {
                var difficultyArea = this.ChildrenOfType<BeatmapCardExtraInfoRow>().Single();
                InputManager.MoveMouseTo(difficultyArea);
            });
            AddUntilStep("wait for expanded", () => this.ChildrenOfType<BeatmapCardNormal>().Single().Expanded.Value);
            AddUntilStep("expanded difficulties loaded", () => this.ChildrenOfType<BeatmapCardDifficultyList>().Any(d => d.IsLoaded));
            AddWaitStep("expanded layout settled", 3);
            AddAssert("expanded content not clipped", () =>
            {
                var cardContainer = this.ChildrenOfType<ReverseChildIDFillFlowContainer<BeatmapCardEntry>>().Single().Parent;
                var expandedContent = this.ChildrenOfType<ExpandedContentScrollContainer>().Single();
                return expandedContent.ScreenSpaceDrawQuad.GetVertices().ToArray().All(v => cardContainer!.ScreenSpaceDrawQuad.Contains(v));
            });
        }

        private static int searchCount;

        private APIBeatmapSet[] getManyBeatmaps(int count) => Enumerable.Range(0, count).Select(_ => CreateAPIBeatmapSet(Ruleset.Value)).ToArray();

        private void fetchFor(params APIBeatmapSet[] beatmaps) => fetchFor(beatmaps, false);

        private void fetchFor(APIBeatmapSet[] beatmaps, bool hasNextPage)
        {
            setSearchResponse(beatmaps, hasNextPage);

            // trigger arbitrary change for fetching.
            searchControl.Query.Value = $"search {searchCount++}";
        }

        private void setSearchResponse(APIBeatmapSet[] beatmaps, bool hasNextPage)
        {
            setsForResponse.Clear();
            setsForResponse.AddRange(beatmaps);
            returnCursorOnResponse = hasNextPage;
        }

        private void setRankAchievedFilter(ScoreRank[] ranks)
        {
            AddStep($"set Rank Achieved filter to [{string.Join(',', ranks)}]", () =>
            {
                searchControl.Ranks.Clear();
                searchControl.Ranks.AddRange(ranks);
            });
        }

        private void setPlayedFilter(SearchPlayed played)
        {
            AddStep($"set Played filter to {played}", () => searchControl.Played.Value = played);
        }

        private void supporterRequiredPlaceholderShown()
        {
            AddUntilStep("\"supporter required\" placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.SupporterRequiredDrawable>().SingleOrDefault()?.IsPresent == true);
        }

        private void notFoundPlaceholderShown()
        {
            AddUntilStep("\"no maps found\" placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault()?.IsPresent == true);
        }

        private void noPlaceholderShown()
        {
            AddUntilStep("\"supporter required\" placeholder not shown", () => !overlay.ChildrenOfType<BeatmapListingOverlay.SupporterRequiredDrawable>().Any(d => d.IsPresent));
            AddUntilStep("\"no maps found\" placeholder not shown", () => !overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any(d => d.IsPresent));
        }

        private void setCardSize(BeatmapCardSize cardSize, bool viaConfig) => AddStep($"set card size to {cardSize}", () =>
        {
            if (viaConfig)
                localConfig.SetValue(OsuSetting.BeatmapListingCardSize, cardSize);
            else
                overlay.ChildrenOfType<BeatmapListingCardSizeTabControl>().Single().Current.Value = cardSize;
        });

        private void assertAllCardsOfType<T>(int expectedCount)
            where T : BeatmapCard =>
            AddUntilStep($"all loaded beatmap cards are {typeof(T)}", () =>
            {
                var entries = overlay.ChildrenOfType<BeatmapCardEntry>().ToArray();
                var cards = overlay.ChildrenOfType<BeatmapCard>().ToArray();
                var size = typeof(T) == typeof(BeatmapCardExtra) ? BeatmapCardSize.Extra
                    : typeof(T) == typeof(BeatmapCardNano) ? BeatmapCardSize.Nano : BeatmapCardSize.Normal;
                // Result cardinality includes offscreen layout/data entries. Heavy
                // cards are intentionally absent there, but every actual card must
                // still have the selected type and the visible ones must load.
                return entries.Length == expectedCount && entries.All(e => e.CardSize == size)
                       && cards.Any(card => card.IsLoaded) && cards.All(card => card.GetType() == typeof(T));
            });

        protected override void Dispose(bool isDisposing)
        {
            localConfig?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
