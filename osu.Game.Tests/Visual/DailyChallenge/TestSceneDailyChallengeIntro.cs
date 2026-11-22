// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Screens;
using osu.Framework.Testing;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.Metadata;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Menu;
using osu.Game.Screens.OnlinePlay.DailyChallenge;
using osu.Game.Tests.Visual.Metadata;
using osu.Game.Tests.Visual.OnlinePlay;
using osuTK.Graphics;
using osuTK.Input;
using CreateRoomRequest = osu.Game.Online.Rooms.CreateRoomRequest;

namespace osu.Game.Tests.Visual.DailyChallenge
{
    public partial class TestSceneDailyChallengeIntro : OnlinePlayTestScene
    {
        [Cached(typeof(MetadataClient))]
        private TestMetadataClient metadataClient = new TestMetadataClient();

        [Cached(typeof(INotificationOverlay))]
        private NotificationOverlay notificationOverlay = new NotificationOverlay();

        private Room room = null!;
        private BlockingCoverStore coverStore = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<LargeTextureStore>(coverStore = new BlockingCoverStore(dependencies.Get<IRenderer>()));
            return dependencies;
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("disable remote downloads", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.AutomaticallyDownloadMissingBeatmaps, false);
                var api = (DummyAPIAccess)API;
                var fallback = api.HandleRequest;
                api.HandleRequest = request =>
                {
                    if (request is not osu.Game.Online.API.Requests.GetBeatmapsRequest lookup)
                        return fallback?.Invoke(request) ?? false;
                    lookup.TriggerSuccess(new osu.Game.Online.API.Requests.GetBeatmapsResponse { Beatmaps = room.Playlist.Select(item => item.Beatmap).Cast<osu.Game.Online.API.Requests.Responses.APIBeatmap>().ToList() });
                    return true;
                };
            });
        }

        public override void TearDownSteps()
        {
            AddStep("release cover load", () => coverStore.Release.Set());
            base.TearDownSteps();
        }

        [Test]
        public void TestSlowCoverDoesNotDelayDailyScreen()
        {
            DailyChallengeIntro intro = null!;
            var elapsed = new Stopwatch();
            startChallenge();
            AddStep("block cover and open intro", () =>
            {
                coverStore.Release.Reset();
                coverStore.Started.Reset();
                coverStore.Completed.Reset();
                ((osu.Game.Online.API.Requests.Responses.APIBeatmapSet)room.Playlist.Single().Beatmap.BeatmapSet!).Covers =
                    new osu.Game.Beatmaps.BeatmapSetOnlineCovers { Cover = "blocked-daily-cover" };
                elapsed.Restart();
                LoadScreen(intro = new DailyChallengeIntro(room));
            });
            AddUntilStep("cover request started", () => coverStore.Started.IsSet);
            AddUntilStep("daily opens while cover still pending", () => Stack.CurrentScreen is Screens.OnlinePlay.DailyChallenge.DailyChallenge daily && daily.IsLoaded);
            AddAssert("no cover wait added to intro", () => elapsed.Elapsed.TotalSeconds < 7);
            AddAssert("cover still blocked", () => !coverStore.Release.IsSet && !intro.ChildrenOfType<osu.Game.Beatmaps.Drawables.OnlineBeatmapSetCover>().Any());
            AddStep("complete late cover", () => coverStore.Release.Set());
            // The non-resumable intro may already be disposed when its artwork finally arrives.
            AddUntilStep("late cover finishes safely", () => coverStore.Completed.IsSet);
            AddAssert("daily remains current", () => Stack.CurrentScreen is Screens.OnlinePlay.DailyChallenge.DailyChallenge);
        }

        private sealed class BlockingCoverStore(IRenderer renderer) : LargeTextureStore(renderer)
        {
            public readonly ManualResetEventSlim Started = new();
            public readonly ManualResetEventSlim Release = new(true);
            public readonly ManualResetEventSlim Completed = new();

            public override Texture Get(string name, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                if (name == "blocked-daily-cover")
                {
                    Started.Set();
                    if (!Release.Wait(TimeSpan.FromSeconds(15)))
                        throw new TimeoutException("Test did not release the blocked daily cover.");
                    Completed.Set();
                }
                // Headless regression: never fetch remote artwork.
                return null!;
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(notificationOverlay);
            Add(metadataClient);

            // add button to observe for daily challenge changes and perform its logic.
            Add(new DailyChallengeButton(@"button-default-select", new Color4(102, 68, 204, 255), (_, _) => { }, 0, Key.D));
        }

        [Test]
        public void TestDailyChallenge()
        {
            startChallenge();
            AddStep("push screen", () => LoadScreen(new DailyChallengeIntro(room)));
        }

        [Test]
        public void TestPlayIntroOnceFlag()
        {
            startChallenge();
            AddStep("set intro played flag", () => Dependencies.Get<SessionStatics>().SetValue(Static.DailyChallengeIntroPlayed, true));

            startChallenge();

            AddAssert("intro played flag reset", () => Dependencies.Get<SessionStatics>().Get<bool>(Static.DailyChallengeIntroPlayed), () => Is.False);

            AddStep("push screen", () => LoadScreen(new DailyChallengeIntro(room)));
            AddUntilStep("intro played flag set", () => Dependencies.Get<SessionStatics>().Get<bool>(Static.DailyChallengeIntroPlayed), () => Is.True);
        }

        private void startChallenge()
        {
            AddStep("add room", () =>
            {
                API.Perform(new CreateRoomRequest(room = new Room
                {
                    Name = "Daily Challenge: June 4, 2024",
                    Playlist =
                    [
                        new PlaylistItem(CreateAPIBeatmap(new OsuRuleset().RulesetInfo))
                        {
                            RequiredMods = [new APIMod(new OsuModTraceable())],
                            AllowedMods = [new APIMod(new OsuModDoubleTime())]
                        }
                    ],
                    StartDate = DateTimeOffset.Now,
                    EndDate = DateTimeOffset.Now.AddHours(24),
                    Category = RoomCategory.DailyChallenge
                }));
            });
            AddStep("signal client", () => metadataClient.DailyChallengeUpdated(new DailyChallengeInfo { RoomID = room.RoomID!.Value }));
        }
    }
}
