using System;
using System.Linq;
using System.Net;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.Sprites;
using osuTK.Input;
using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.Queue;
using osu.Game.Tests.Visual.OnlinePlay;
using static osu.Game.Screens.OnlinePlay.Matchmaking.Queue.RelaxPoolEditorScreen;

namespace osu.Game.Tests.Visual.Matchmaking
{
    public partial class TestSceneRankedPoolEditorScreen : OnlinePlayTestScene
    {
        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notificationOverlay = new NotificationOverlay();

        private RelaxPoolEditorScreen screen = null!;
        private ReplaceRelaxRankedPoolRequest? saved;

        private DrawableRoomPlaylist playlist => screen.ChildrenOfType<DrawableRoomPlaylist>().Single();
        private FormDropdown<PoolSortOrder> sort => screen.ChildrenOfType<FormDropdown<PoolSortOrder>>().Single();
        private FormCheckBox onlyMine => screen.ChildrenOfType<FormCheckBox>().Single();
        private Room room => (Room)typeof(RelaxPoolEditorScreen).GetField("room", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!;

        private void save() => screen.CreateFooterButtons().Single(button => button.Text.ToString() == "Save pool").TriggerClick();

        private void addMap(int id) => room.Playlist = room.Playlist.Append(new PlaylistItem(new APIBeatmap { OnlineID = id })).ToArray();

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("open shared pool", () =>
            {
                var pool = new RelaxRankedPoolResponse
                {
                    RulesetId = 4,
                    Revision = 12,
                    CanManage = true,
                    Beatmaps =
                    [
                        new() { BeatmapId = 11, Title = "Beta", Artist = "Zebra", StarRating = 5, AddedBy = API.LocalUser.Value.Id, AddedByUsername = "me", AddedAt = DateTimeOffset.UnixEpoch.AddDays(2) },
                        new() { BeatmapId = 22, Title = "Gamma", Artist = "Alpha", StarRating = 1, AddedBy = API.LocalUser.Value.Id + 1, AddedByUsername = "other", AddedAt = DateTimeOffset.UnixEpoch.AddDays(1) },
                        new() { BeatmapId = 33, Title = "Alpha", Artist = "Beta", StarRating = 3, AddedBy = API.LocalUser.Value.Id, AddedByUsername = "me", AddedAt = DateTimeOffset.UnixEpoch.AddDays(3) },
                    ]
                };
                saved = null;
                var api = (DummyAPIAccess)API;
                var fallback = api.HandleRequest;
                api.HandleRequest = request =>
                {
                    if (request is GetRelaxRankedPoolRequest get)
                    {
                        get.TriggerSuccess(pool);
                        return true;
                    }
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        saved = replace;
                        replace.TriggerSuccess(new ReplaceRelaxRankedPoolResponse { Accepted = 3 });
                        return true;
                    }
                    return fallback?.Invoke(request) ?? false;
                };
                LoadScreen(screen = new RelaxPoolEditorScreen(pool));
            });
            AddUntilStep("playlist ready", () => screen.IsLoaded && playlist.Items.Count == 3);
        }

        public override void TearDownSteps()
        {
            AddStep("discard test edits and pending requests", () =>
            {
                typeof(RelaxPoolEditorScreen).GetField("saving", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(screen, false);
                typeof(RelaxPoolEditorScreen).GetField("exitConfirmed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(screen, true);
            });
            base.TearDownSteps();
        }

        [TestCase(PoolSortOrder.StarsAscending, new[] { 22, 33, 11 })]
        [TestCase(PoolSortOrder.StarsDescending, new[] { 11, 33, 22 })]
        [TestCase(PoolSortOrder.NewestFirst, new[] { 33, 11, 22 })]
        [TestCase(PoolSortOrder.OldestFirst, new[] { 22, 11, 33 })]
        [TestCase(PoolSortOrder.Title, new[] { 33, 11, 22 })]
        [TestCase(PoolSortOrder.Artist, new[] { 22, 33, 11 })]
        public void TestSortOrder(PoolSortOrder order, int[] expected)
        {
            AddStep("choose sort", () => sort.Current.Value = order);
            AddAssert("visible order matches", () => playlist.Items.Select(item => item.Beatmap.OnlineID), () => Is.EqualTo(expected));
            AddStep("save pool", () => screen.CreateFooterButtons().Single(button => button.Text.ToString() == "Save pool").TriggerClick());
            AddUntilStep("all maps still saved", () => saved?.BeatmapIds.Order().SequenceEqual(new[] { 11, 22, 33 }) == true);
            AddAssert("sort retained after reload", () => sort.Current.Value == order);
        }

        [Test]
        public void TestConcurrentEditsAreMergedWithoutRestoringRemoteDeletions()
        {
            int attempts = 0;
            AddStep("other editor changes the pool", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        saved = replace;
                        if (++attempts == 1)
                            replace.TriggerFailure(new APIException("Pool changed", null, HttpStatusCode.Conflict));
                        else
                            replace.TriggerSuccess(new ReplaceRelaxRankedPoolResponse { Accepted = 3 });
                        return true;
                    }
                    if (request is GetRelaxRankedPoolRequest get)
                    {
                        get.TriggerSuccess(new RelaxRankedPoolResponse
                        {
                            Revision = attempts == 1 ? 13 : 14,
                            Beatmaps = (attempts == 1 ? new[] { 11, 22, 55 } : new[] { 11, 44, 55 })
                                .Select(id => new RelaxRankedPoolBeatmap { BeatmapId = id }).ToList(),
                        });
                        return true;
                    }
                    return false;
                };
                room.Playlist = room.Playlist.Where(item => item.Beatmap.OnlineID != 22).ToArray();
                addMap(44);
                save();
            });
            AddUntilStep("rebased save completed", () => attempts == 2 && playlist.Items.Count == 3);
            AddAssert("latest revision used", () => saved is { Revision: 13, RulesetId: 4 });
            AddAssert("local and remote changes preserved", () => saved!.BeatmapIds.Order(), () => Is.EqualTo(new[] { 11, 44, 55 }));
            AddAssert("merged pool displayed", () => playlist.Items.Select(item => item.Beatmap.OnlineID).Order(), () => Is.EqualTo(new[] { 11, 44, 55 }));
        }

        [Test]
        public void TestSaveAndExitWaitsForConflictResolution()
        {
            int attempts = 0;
            AddStep("save and exit with a stale revision", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        saved = replace;
                        if (++attempts == 1)
                            replace.TriggerFailure(new APIException("Pool changed", null, HttpStatusCode.Conflict));
                        else
                            replace.TriggerSuccess(new ReplaceRelaxRankedPoolResponse { Accepted = 5 });
                        return true;
                    }
                    if (request is GetRelaxRankedPoolRequest get)
                    {
                        get.TriggerSuccess(new RelaxRankedPoolResponse
                        {
                            Revision = 13,
                            Beatmaps = new[] { 11, 22, 33, 55 }.Select(id => new RelaxRankedPoolBeatmap { BeatmapId = id }).ToList(),
                        });
                        return true;
                    }
                    return false;
                };
                addMap(44);
                typeof(RelaxPoolEditorScreen).GetMethod("save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(screen, new object[] { true, 0 });
            });
            AddUntilStep("screen exited after retry", () => attempts == 2 && Stack.CurrentScreen == null);
            AddAssert("both additions saved before exit", () => saved!.BeatmapIds.Order(), () => Is.EqualTo(new[] { 11, 22, 33, 44, 55 }));
        }

        [Test]
        public void TestNonConflictFailureDoesNotReloadOrRetry()
        {
            int attempts = 0;
            int reloads = 0;
            AddStep("server rejects save", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        attempts++;
                        replace.TriggerFailure(new APIException("Forbidden", null, HttpStatusCode.Forbidden));
                        return true;
                    }
                    if (request is GetRelaxRankedPoolRequest)
                    {
                        reloads++;
                        return true;
                    }
                    return false;
                };
                addMap(44);
                save();
            });
            AddWaitStep("process rejection", 5);
            AddAssert("not retried or reloaded", () => attempts == 1 && reloads == 0);
            AddAssert("local changes kept", () => playlist.Items.Any(item => item.Beatmap.OnlineID == 44));
        }

        [Test]
        public void TestEditsDuringSaveAndRefreshRemainUnsaved()
        {
            ReplaceRelaxRankedPoolRequest? pendingSave = null;
            GetRelaxRankedPoolRequest? pendingReload = null;
            AddStep("delay network requests", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        pendingSave = replace;
                        saved = replace;
                        return true;
                    }
                    if (request is GetRelaxRankedPoolRequest get)
                    {
                        pendingReload = get;
                        return true;
                    }
                    return false;
                };
                save();
            });
            AddUntilStep("save pending", () => pendingSave != null);
            AddStep("add map during save", () =>
            {
                addMap(44);
                pendingSave!.TriggerSuccess(new ReplaceRelaxRankedPoolResponse { Accepted = 3 });
            });
            AddUntilStep("refresh pending", () => pendingReload != null);
            AddStep("edit again during refresh", () =>
            {
                addMap(66);
                room.Playlist = room.Playlist.Where(item => item.Beatmap.OnlineID != 22).ToArray();
                pendingReload!.TriggerSuccess(new RelaxRankedPoolResponse
                {
                    Revision = 14,
                    Beatmaps = new[] { 11, 22, 33, 55 }.Select(id => new RelaxRankedPoolBeatmap { BeatmapId = id }).ToList(),
                });
            });
            AddUntilStep("all edits survive refresh", () => playlist.Items.Select(item => item.Beatmap.OnlineID).Order().SequenceEqual(new[] { 11, 33, 44, 55, 66 }));
            AddStep("save preserved edits", save);
            AddUntilStep("new snapshot submitted", () => saved is { Revision: 14 } && saved.BeatmapIds.Order().SequenceEqual(new[] { 11, 33, 44, 55, 66 }));
        }

        [Test]
        public void TestRepeatedConflictsStopRetryingAndKeepLocalEdits()
        {
            int attempts = 0;
            AddStep("pool changes on every save", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        attempts++;
                        replace.TriggerFailure(new APIException("Pool changed", null, HttpStatusCode.Conflict));
                        return true;
                    }
                    if (request is GetRelaxRankedPoolRequest get)
                    {
                        get.TriggerSuccess(new RelaxRankedPoolResponse
                        {
                            Revision = 12 + attempts,
                            Beatmaps = new[] { 11, 22, 33, 55 }.Select(id => new RelaxRankedPoolBeatmap { BeatmapId = id }).ToList(),
                        });
                        return true;
                    }
                    return false;
                };
                addMap(44);
                save();
            });
            AddUntilStep("bounded retries finished", () => attempts == 4 && playlist.Items.Count == 5);
            AddWaitStep("no retry loop", 5);
            AddAssert("four attempts only", () => attempts == 4);
            AddAssert("local addition kept", () => playlist.Items.Any(item => item.Beatmap.OnlineID == 44));
            AddStep("manual save still possible", save);
            AddUntilStep("manual retry submitted", () => attempts == 8);
        }

        [Test]
        public void TestFailedConflictRefreshKeepsLocalChanges()
        {
            int attempts = 0;
            AddStep("refresh fails after conflict", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is ReplaceRelaxRankedPoolRequest replace)
                    {
                        attempts++;
                        saved = replace;
                        replace.TriggerFailure(new APIException("Pool changed", null, HttpStatusCode.Conflict));
                        return true;
                    }
                    if (request is GetRelaxRankedPoolRequest get)
                    {
                        get.TriggerFailure(new InvalidOperationException("Offline"));
                        return true;
                    }
                    return false;
                };
                addMap(44);
                save();
            });
            AddWaitStep("process failure", 5);
            AddAssert("local changes still present", () => playlist.Items.Any(item => item.Beatmap.OnlineID == 44));
            AddStep("try saving again", save);
            AddUntilStep("not stuck saving", () => attempts == 2);
            AddAssert("old revision retained on failed refresh", () => saved!.Revision == 12);
        }

        [Test]
        public void TestControlsAreClickable()
        {
            AddWaitStep("finish layout", 3);
            AddAssert("controls do not overlap", () => sort.ScreenSpaceDrawQuad.AABBFloat.Right <= onlyMine.ScreenSpaceDrawQuad.AABBFloat.Left);
            AddStep("open sorting with mouse", () =>
            {
                InputManager.MoveMouseTo(sort);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("sort menu open", () => sort.ChildrenOfType<Menu>().Any(menu => menu.State == MenuState.Open));
            AddStep("click newest first", () =>
            {
                InputManager.MoveMouseTo(sort.ChildrenOfType<OsuSpriteText>().Single(text => text.Text.ToString() == "Newest additions first"));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("newest selected by mouse", () => sort.Current.Value == PoolSortOrder.NewestFirst);
            AddStep("click added by me", () =>
            {
                InputManager.MoveMouseTo(onlyMine);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("mine filter applied by mouse", () => playlist.Items.Select(item => item.Beatmap.OnlineID).SequenceEqual(new[] { 33, 11 }));
        }

        [Test]
        public void TestOnlyMineCombinesWithSearchAndDoesNotRemoveHiddenMapsOnSave()
        {
            AddStep("newest first", () => sort.Current.Value = PoolSortOrder.NewestFirst);
            AddStep("only my additions", () => onlyMine.Current.Value = true);
            AddAssert("other contributor hidden", () => playlist.Items.Select(item => item.Beatmap.OnlineID), () => Is.EqualTo(new[] { 33, 11 }));
            AddStep("search my maps", () => screen.ChildrenOfType<BasicSearchTextBox>().Single().Current.Value = "Beta");
            AddAssert("search combines with contributor", () => playlist.Items.Select(item => item.Beatmap.OnlineID), () => Is.EqualTo(new[] { 33, 11 }));
            AddStep("search another contributor", () => screen.ChildrenOfType<BasicSearchTextBox>().Single().Current.Value = "Gamma");
            AddAssert("empty filtered list", () => playlist.Items.Count == 0);
            AddStep("save with empty view", () => screen.CreateFooterButtons().Single(button => button.Text.ToString() == "Save pool").TriggerClick());
            AddUntilStep("hidden maps not deleted", () => saved is { RulesetId: 4, Revision: 12 } && saved.BeatmapIds.Order().SequenceEqual(new[] { 11, 22, 33 }));
            AddStep("clear filters", () =>
            {
                onlyMine.Current.Value = false;
                screen.ChildrenOfType<BasicSearchTextBox>().Single().Current.Value = string.Empty;
            });
            AddUntilStep("full pool restored in chosen order", () => playlist.Items.Select(item => item.Beatmap.OnlineID).SequenceEqual(new[] { 33, 11, 22 }));
        }
    }
}
