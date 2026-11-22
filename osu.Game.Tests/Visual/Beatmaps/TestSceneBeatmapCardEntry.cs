#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Tests.Visual.Components;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Visual.Beatmaps
{
    public partial class TestSceneBeatmapCardEntry : OsuManualInputManagerTestScene
    {
        private TestScenePreviewTrackManager.TestPreviewTrackManager previewTracks;

        private BeatmapCardEntry entry;
        private BasicScrollContainer scroll;
        private bool ownerVisible;
        private LargeTextureStore controlledCovers;

        private void createEntry(BeatmapCardSize size, Func<bool> admitFirstUpdate = null, bool useControlledCovers = false,
                                 Func<BeatmapCardEntry, bool, bool> retainHiddenContent = null, BeatmapCardPool pool = null)
        {
            ownerVisible = true;
            previewTracks = new TestScenePreviewTrackManager.TestPreviewTrackManager(new AudioAdjustments());
            if (useControlledCovers)
                controlledCovers = new LargeTextureStore(Dependencies.Get<IRenderer>(), new CoverUploads());
            var set = CreateAPIBeatmapSet(Ruleset.Value);
            if (useControlledCovers)
                set.Covers = new BeatmapSetOnlineCovers { List = "controlled-list", Card = "controlled-card" };
            using var store = pool != null ? new osu.Game.Overlays.BeatmapListing.BeatmapListingResultStore() : null;
            var cardEntry = store == null
                ? new BeatmapCardEntry(set, size, () => ownerVisible, admitFirstUpdate, retainHiddenContent)
                : new BeatmapCardEntry(store.Add(set), size, () => ownerVisible, admitFirstUpdate ?? (() => true),
                    retainHiddenContent ?? ((_, release) => release), pool);
            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = useControlledCovers
                    ? new (Type, object)[]
                    {
                        (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Blue)),
                        (typeof(PreviewTrackManager), previewTracks),
                        (typeof(LargeTextureStore), controlledCovers)
                    }
                    : new (Type, object)[]
                    {
                        (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Blue)),
                        (typeof(PreviewTrackManager), previewTracks)
                    },
                Children = new Drawable[]
                {
                    previewTracks,
                    scroll = new BasicScrollContainer
                    {
                        Size = new Vector2(400, 240),
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 1800,
                            Child = entry = cardEntry

                        }
                    }
                }
            };
            entry.Y = 20;
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        [TestCase(BeatmapCardSize.Nano)]
        public void TestReusedPooledCardSurvivesFormerEntryDisposal(BeatmapCardSize size)
        {
            BeatmapCardPool pool = null;
            BeatmapCard card = null;
            BeatmapCardEntry previousEntry = null;
            AddStep("create pooled entry", () =>
            {
                pool = new BeatmapCardPool(size);
                createEntry(size, useControlledCovers: true, pool: pool);
            });
            AddUntilStep("initial card loaded", () => entry.Content?.IsLoaded == true);
            AddStep("hide initial card", () => { card = (BeatmapCard)entry.Content; ownerVisible = false; });
            AddUntilStep("shell returned to pool", () => entry.Content == null);
            AddStep("reload already initialised shell", () => ownerVisible = true);
            AddUntilStep("same shell reattached", () => entry.DelayedLoadCompleted && ReferenceEquals(entry.Content, card));
            AddStep("hide reused shell", () => ownerVisible = false);
            AddUntilStep("reused shell returned to pool", () => entry.Content == null);
            AddStep("discard previous results", () =>
            {
                previousEntry = entry;
                scroll.Clear();
                Clear();
            });
            AddUntilStep("former wrapper disposed", () => isDisposed(previousEntry));
            AddAssert("pooled tree still alive", () => !isDisposed(card)
                && card.ChildrenOfType<BeatmapCardContent>().All(content => !isDisposed(content)));
            AddStep("replace search and restore pooled shell", () =>
            {
                controlledCovers.Dispose();
                createEntry(size, useControlledCovers: true, pool: pool);
            });
            AddUntilStep("new entry owns same live tree", () => entry.DelayedLoadCompleted && ReferenceEquals(entry.Content, card));
            AddAssert("new entry has correct model", () => ReferenceEquals(card.BeatmapSet, entry.BeatmapSet) && !isDisposed(card));
            AddStep("release resources", () => { Clear(); pool.Dispose(); controlledCovers.Dispose(); });
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        [TestCase(BeatmapCardSize.Nano)]
        public void TestScrollUnloadsAndRestoresCardWithoutChangingGeometry(BeatmapCardSize size)
        {
            BeatmapCard oldCard = null;
            Vector2 initialSize = default;
            AddStep("create entry", () =>
            {
                createEntry(size);
                initialSize = entry.Size;
            });
            AddUntilStep("card loaded", () => entry.Content?.IsLoaded == true);
            AddAssert("fixed geometry matches loaded card", () => entry.Size == initialSize && entry.Content.Size == initialSize);
            AddAssert("no second cover load delay", () => entry.Content.ChildrenOfType<DelayedLoadWrapper>().All(wrapper =>
                (double)typeof(DelayedLoadWrapper).GetField("timeBeforeLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wrapper)! == 0));
            AddStep("remember and scroll away", () =>
            {
                oldCard = (BeatmapCard)entry.Content;
                scroll.ScrollToEnd(false);
            });
            AddUntilStep("offscreen card unloaded", () => entry.Content == null);
            AddUntilStep("old card disposed", () => isDisposed(oldCard));
            AddAssert("entry retains geometry and data", () => entry.Size == initialSize && ReferenceEquals(entry.BeatmapSet, oldCard.BeatmapSet));
            AddStep("scroll back", () => scroll.ScrollToStart(false));
            AddUntilStep("new card loaded", () => entry.Content?.IsLoaded == true);
            AddAssert("same map new tree same geometry", () => !ReferenceEquals(oldCard, entry.Content)
                && ReferenceEquals(entry.BeatmapSet, ((BeatmapCard)entry.Content).BeatmapSet) && entry.Content.Size == initialSize);
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        [TestCase(BeatmapCardSize.Nano)]
        public void TestPoolReleasesModelAndCoverReferences(BeatmapCardSize size)
        {
            BeatmapCard card = null;
            BeatmapCardPool pool = null;
            WeakReference<osu.Game.Online.API.Requests.Responses.APIBeatmapSet> previousModel = null;
            AddStep("create covered card", () => createEntry(size, useControlledCovers: true));
            AddUntilStep("covers acquired", () => entry.Content?.IsLoaded == true && cachedCoverCount() > 0);
            AddStep("detach and return to pool", () =>
            {
                card = (BeatmapCard)entry.Content;
                previousModel = new(card.BeatmapSet);
                // Retain the shell, not the wrapper or its original model factory.
                var parent = (CompositeDrawable)card.Parent;
                typeof(CompositeDrawable).GetMethod("ClearInternal", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(parent, new object[] { false });
                pool = new BeatmapCardPool(size);
                Assert.That(pool.Return(card), Is.True);
                scroll.Clear();
                Clear();
                entry = null;
                scroll = null;
            });
            AddUntilStep("all old covers released", () => cachedCoverCount() == 0);
            AddWaitStep("drain old callbacks", 3);
            AddStep("diagnostic collection", () => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
            AddUntilStep("old model collectible", () =>
            {
                GC.Collect();
                return !previousModel.TryGetTarget(out _);
            });
            AddStep("reuse shell for another model", () =>
            {
                var model = CreateAPIBeatmapSet(Ruleset.Value);
                Assert.That(pool.Get(model, new Bindable<BeatmapSetFavouriteState>()), Is.SameAs(card));
            });
            AddStep("dispose pool", () => { card.Dispose(); pool.Dispose(); controlledCovers.Dispose(); });
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        [TestCase(BeatmapCardSize.Nano)]
        public void TestRecycledCardChangesModelAndKeepsFavouriteBindings(BeatmapCardSize size)
        {
            BeatmapCard recycled = null;
            osu.Game.Online.API.Requests.Responses.APIBeatmapSet replacement = null;
            Bindable<BeatmapSetFavouriteState> state = null;
            AddStep("load original card", () => createEntry(size, useControlledCovers: true));
            AddUntilStep("original card loaded", () => entry.Content?.IsLoaded == true);
            AddStep("rebind loaded shell", () =>
            {
                recycled = (BeatmapCard)entry.Content;
                replacement = CreateAPIBeatmapSet(Ruleset.Value);
                replacement.OnlineID = 9876543;
                replacement.Title = "Replacement title";
                replacement.Author.Username = "Replacement mapper";
                state = new Bindable<BeatmapSetFavouriteState>(new BeatmapSetFavouriteState(true, 47));
                recycled.ChangeBeatmapSet(replacement, state);
            });
            AddAssert("same shell new model", () => ReferenceEquals(entry.Content, recycled)
                && ReferenceEquals(recycled.BeatmapSet, replacement));
            AddAssert("button rebound", () => recycled.ChildrenOfType<FavouriteButton>().Single().Current.Value.Favourited
                && recycled.ChildrenOfType<FavouriteButton>().Single().Current.Value.FavouriteCount == 47);
            AddStep("change favourite after recycle", () => state.Value = new BeatmapSetFavouriteState(false, 46));
            AddAssert("internal bindings still connected", () => !recycled.ChildrenOfType<FavouriteButton>().Single().Current.Value.Favourited
                && recycled.ChildrenOfType<FavouriteButton>().Single().Current.Value.FavouriteCount == 46);
            AddAssert("title updated", () => recycled.ChildrenOfType<osu.Game.Graphics.Sprites.TruncatingSpriteText>()
                .Any(text => text.Text.ToString().Contains("Replacement title", StringComparison.Ordinal)));
            AddStep("release covers", () => { Clear(); controlledCovers.Dispose(); });
        }

        [Test]
        public void TestBoundedNeighbourhoodReusesTreeThenEvictsAndReloads()
        {
            BeatmapCard oldCard = null;
            bool retain = true;
            AddStep("create entry with retention policy", () => createEntry(BeatmapCardSize.Normal,
                retainHiddenContent: (_, release) => release || retain));
            AddUntilStep("card loaded", () => entry.Content?.IsLoaded == true);
            AddStep("remember and scroll away", () =>
            {
                oldCard = (BeatmapCard)entry.Content;
                scroll.ScrollToEnd(false);
            });
            AddWaitStep("briefly leave viewport", 2);
            AddStep("return within neighbourhood", () => scroll.ScrollToStart(false));
            AddAssert("same heavy tree reused", () => ReferenceEquals(entry.Content, oldCard));
            AddStep("evict while scrolling away", () =>
            {
                retain = false;
                scroll.ScrollToEnd(false);
            });
            AddUntilStep("budget evicts content", () => entry.Content == null);
            AddUntilStep("evicted tree disposed", () => isDisposed(oldCard));
            AddStep("return after eviction", () => scroll.ScrollToStart(false));
            AddUntilStep("visible card can load despite denied hidden retention", () => entry.Content?.IsLoaded == true
                && !ReferenceEquals(entry.Content, oldCard));
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        public void TestHiddenCardReleasesCoverTextureReferences(BeatmapCardSize size)
        {
            BeatmapCard oldCard = null;
            OnlineBeatmapSetCover[] savedCovers = null;
            Texture[] oldTextures = null;
            Vector2 geometry = default;
            AddStep("create card with local covers", () =>
            {
                createEntry(size, useControlledCovers: true);
                geometry = entry.Size;
            });
            AddUntilStep("both cover sprites loaded", () => entry.Content?.IsLoaded == true
                && entry.Content.ChildrenOfType<OnlineBeatmapSetCover>().Count(cover => cover.Texture != null) == 2);
            AddStep("remember original cover references", () =>
            {
                oldCard = (BeatmapCard)entry.Content;
                savedCovers = oldCard.ChildrenOfType<OnlineBeatmapSetCover>().ToArray();
                oldTextures = savedCovers.Select(cover => cover.Texture).ToArray();
            });
            AddAssert("both sources are refcounted and owned by card", () => savedCovers.Length == 2
                && oldTextures.All(texture => texture?.GetType().Name == "TextureWithRefCount") && cachedCoverCount() == 2);
            AddStep("hide owner", () => ownerVisible = false);
            AddUntilStep("content unloaded after framework timeout", () => entry.Content == null);
            AddUntilStep("old card and covers disposed", () => isDisposed(oldCard)
                && savedCovers.All(cover => cover.Texture == null));
            AddUntilStep("both source textures released from store", () => cachedCoverCount() == 0
                && oldTextures.All(texture => (bool)texture.GetType().GetProperty("IsDisposed")!.GetValue(texture)!));
            AddAssert("metadata and geometry intentionally remain", () => ReferenceEquals(entry.BeatmapSet, oldCard.BeatmapSet) && entry.Size == geometry);
            AddStep("reopen owner", () => ownerVisible = true);
            AddUntilStep("fresh heavy tree and covers loaded", () => entry.Content?.IsLoaded == true
                && !ReferenceEquals(entry.Content, oldCard)
                && entry.Content.ChildrenOfType<OnlineBeatmapSetCover>().Count(cover => cover.Texture != null) == 2);
            AddAssert("reopened geometry and metadata unchanged", () => entry.Content.Size == geometry
                && ReferenceEquals(((BeatmapCard)entry.Content).BeatmapSet, entry.BeatmapSet) && cachedCoverCount() == 2);
            AddStep("release local covers", () =>
            {
                Clear();
                controlledCovers.Dispose();
            });
        }

        [Test]
        public void TestHiddenReadyCardCannotPublishLate()
        {
            bool admit = false;
            BeatmapCard oldCard = null;
            AddStep("defer card initialization", () => createEntry(BeatmapCardSize.Normal, () => admit, useControlledCovers: true));
            AddUntilStep("background card ready", () => entry.Content?.LoadState == LoadState.Ready);
            AddStep("hide before first update", () =>
            {
                oldCard = (BeatmapCard)entry.Content;
                ownerVisible = false;
            });
            AddUntilStep("unadmitted card unloaded", () => entry.Content == null);
            AddUntilStep("old unadmitted card disposed", () => isDisposed(oldCard) && cachedCoverCount() == 0);
            AddStep("allow next initialization while hidden", () => admit = true);
            AddWaitStep("drain late callbacks", 3);
            AddAssert("no stale card published", () => entry.Content == null && !entry.DelayedLoadCompleted);
            AddStep("show owner", () => ownerVisible = true);
            AddUntilStep("fresh card initialized", () => entry.Content?.IsLoaded == true && !ReferenceEquals(entry.Content, oldCard));
            AddStep("release local covers", () =>
            {
                Clear();
                controlledCovers.Dispose();
            });
        }

        private int cachedCoverCount() => ((IDictionary)typeof(LargeTextureStore)
            .GetField("referenceCounts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controlledCovers)!).Count;

        private sealed class CoverUploads : IResourceStore<TextureUpload>
        {
            public TextureUpload Get(string name) => new TextureUpload(new Image<Rgba32>(16, 16));
            public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
            public Stream GetStream(string name) => null;
            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();
            public void Dispose() { }
        }

        [Test]
        public void TestOffscreenReadyCardDoesNotConsumeAdmission()
        {
            int attempts = 0;
            bool admit = false;
            AddStep("defer first update", () => createEntry(BeatmapCardSize.Normal, () =>
            {
                attempts++;
                return admit;
            }));
            AddUntilStep("background load ready", () => entry.Content?.LoadState == LoadState.Ready);
            AddUntilStep("visible entry attempted admission", () => attempts > 0);
            AddStep("scroll ready card away", () => scroll.ScrollToEnd(false));
            AddWaitStep("settle masking", 2);
            AddStep("reset attempts", () => attempts = 0);
            AddWaitStep("observe offscreen ready entry", 3);
            AddAssert("offscreen entry does not consume admission", () => attempts == 0);
            AddUntilStep("offscreen ready content unloaded", () => entry.Content == null);
            AddStep("permit initialization and scroll back", () =>
            {
                admit = true;
                scroll.ScrollToStart(false);
            });
            AddUntilStep("card restored and initialized", () => entry.Content?.IsLoaded == true);
        }

        [TestCase(BeatmapCardSize.Normal)]
        [TestCase(BeatmapCardSize.Extra)]
        public void TestExpandedDropdownRemainsUsableNearViewportEdge(BeatmapCardSize size)
        {
            BeatmapCard expandedCard = null;
            ExpandedContentScrollContainer dropdown = null;
            HoverHandlingContainer dropdownOwner = null;
            AddStep("create expandable card", () =>
            {
                createEntry(size);
                entry.Y = 100;
                entry.BeatmapSet.Beatmaps = Enumerable.Repeat(entry.BeatmapSet.Beatmaps.First(), 40).ToArray();
                scroll.Height = 600;
            });
            AddUntilStep("card loaded", () => entry.Content?.IsLoaded == true);
            AddStep("hover difficulties", () => InputManager.MoveMouseTo(entry.Content.ChildrenOfType<BeatmapCardExtraInfoRow>().Single()));
            AddUntilStep("dropdown loaded", () => entry.Content.ChildrenOfType<BeatmapCardDifficultyList>().Any(d => d.IsLoaded));
            AddWaitStep("finish expansion", 10);
            AddStep("hover dropdown before scrolling", () =>
            {
                expandedCard = (BeatmapCard)entry.Content;
                dropdown = expandedCard.ChildrenOfType<ExpandedContentScrollContainer>().Single();
                dropdownOwner = dropdown.FindClosestParent<HoverHandlingContainer>();
                // Avoid difficulty links/icons which may handle hover themselves.
                InputManager.MoveMouseTo(dropdown.ToScreenSpace(new Vector2(dropdown.DrawWidth - 25, 60)));
            });
            AddUntilStep("dropdown hovered before scrolling", () => dropdownOwner.IsHovered);
            AddStep("scroll body partly away while keeping dropdown hovered", () =>
            {
                // Existing cards are culled using their body bounds, not the
                // dropdown's bounds. Keep the body intersecting the viewport.
                scroll.ScrollTo(entry.Y + entry.Height - 20, false);
                InputManager.MoveMouseTo(scroll.ToScreenSpace(new Vector2(340, 60)));
            });
            AddUntilStep("dropdown hovered after scrolling", () => dropdownOwner.IsHovered);
            AddAssert("card body partially above viewport", () => entry.ScreenSpaceDrawQuad.TopLeft.Y < scroll.ScreenSpaceDrawQuad.TopLeft.Y
                && entry.ScreenSpaceDrawQuad.BottomRight.Y > scroll.ScreenSpaceDrawQuad.TopLeft.Y);
            AddWaitStep("past unload delay", 25);
            AddAssert("hovered dropdown retains card", () => ReferenceEquals(expandedCard, entry.Content) && !isDisposed(expandedCard));
            AddStep("move outside dropdown and scroll away", () =>
            {
                InputManager.MoveMouseTo(scroll.ScreenSpaceDrawQuad.BottomRight + new Vector2(20));
                scroll.ScrollToEnd(false);
            });
            AddUntilStep("unhovered card unloaded", () => entry.Content == null);
            AddUntilStep("old card disposed", () => isDisposed(expandedCard));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFavouriteStateSurvivesUnloadAndPendingRequestKeepsOwner(bool failRequest)
        {
            APIRequest pendingRequest = null;
            BeatmapCard oldCard = null;
            int initialFavourites = 0;
            AddStep("create entry and defer favourite response", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not PostBeatmapFavouriteRequest)
                        return false;
                    pendingRequest = request;
                    return true;
                };
                createEntry(BeatmapCardSize.Normal, retainHiddenContent: (_, release) => release);
                initialFavourites = entry.BeatmapSet.FavouriteCount;
            });
            AddUntilStep("card loaded", () => entry.Content?.IsLoaded == true);
            AddStep("hover card", () => InputManager.MoveMouseTo(entry));
            AddUntilStep("favourite button loaded", () => entry.Content.ChildrenOfType<FavouriteButton>().Single().IsLoaded);
            AddStep("favourite", () =>
            {
                oldCard = (BeatmapCard)entry.Content;
                oldCard.ChildrenOfType<FavouriteButton>().Single().TriggerClick();
            });
            AddUntilStep("request queued", () => pendingRequest != null);
            AddStep("hide owner", () => ownerVisible = false);
            AddWaitStep("past unload delay", 25);
            AddAssert("request still has live owner", () => ReferenceEquals(oldCard, entry.Content) && !isDisposed(oldCard));
            AddStep("complete favourite request", () =>
            {
                if (failRequest)
                    pendingRequest.TriggerFailure(new InvalidOperationException("Expected test failure"));
                else
                    pendingRequest.TriggerSuccess();
            });
            AddUntilStep("card unloaded after completion", () => entry.Content == null);
            AddUntilStep("old card disposed", () => isDisposed(oldCard));
            AddStep("show owner", () => ownerVisible = true);
            AddUntilStep("card restored", () => entry.Content?.IsLoaded == true);
            AddAssert("favourite retained", () => entry.Content.ChildrenOfType<FavouriteButton>().Single().Current.Value.Favourited == !failRequest);
            AddAssert("count retained", () => entry.Content.ChildrenOfType<FavouriteButton>().Single().Current.Value.FavouriteCount == initialFavourites + (failRequest ? 0 : 1));
        }

        [Test]
        public void TestScrollingDoesNotStopActivePreview()
        {
            BeatmapCard playingCard = null;
            AddStep("create entry", () => createEntry(BeatmapCardSize.Normal, retainHiddenContent: (_, release) => release));
            AddUntilStep("card loaded", () => entry.Content?.IsLoaded == true);
            AddStep("hover card", () => InputManager.MoveMouseTo(entry));
            AddUntilStep("play button loaded", () => entry.Content.ChildrenOfType<PlayButton>().Single().IsLoaded);
            AddStep("start silent virtual preview", () =>
            {
                playingCard = (BeatmapCard)entry.Content;
                playingCard.ChildrenOfType<PlayButton>().Single().Playing.Value = true;
            });
            AddUntilStep("virtual preview running", () => previewTracks.CurrentTrack?.IsRunning == true);
            AddStep("scroll away", () => scroll.ScrollToEnd(false));
            AddWaitStep("past unload delay", 25);
            AddAssert("playing card retained offscreen", () => ReferenceEquals(playingCard, entry.Content) && previewTracks.CurrentTrack?.IsRunning == true);
            AddStep("stop preview", () => previewTracks.CurrentTrack.Stop());
            AddUntilStep("stopped card unloaded", () => entry.Content == null);
            AddUntilStep("stopped card disposed", () => isDisposed(playingCard));
        }

        private static bool isDisposed(Drawable drawable) =>
            (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drawable)!;
    }
}
