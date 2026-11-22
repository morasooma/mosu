using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Scoring.Render;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens;
using osu.Game.Tests.Gameplay;
using osu.Game.Tests.Visual;
using osu.Game.Users.Drawables;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [HeadlessTest]
    public partial class ReplayRenderSnapshotIntegrationTest : OsuTestScene
    {
        [TestCase(BeatmapLeaderboardScope.Local)]
        [TestCase(BeatmapLeaderboardScope.Global)]
        [TestCase(BeatmapLeaderboardScope.Country)]
        [TestCase(BeatmapLeaderboardScope.Friend)]
        [TestCase(BeatmapLeaderboardScope.Team)]
        public void TestSelectedScopeFeedsOfflineGameplayProvider(BeatmapLeaderboardScope scope)
        {
            LeaderboardManager manager = null!;
            SoloGameplayLeaderboardProvider provider = null!;
            ReplayRenderSnapshot snapshot = null!;
            var state = TestGameplayState.Create(new OsuRuleset());
            var score = new ScoreInfo(Beatmap.Value.BeatmapInfo, Ruleset.Value)
            {
                User = new APIUser { Id = 22, Username = "Rendered player" },
            };
            state.Score.ScoreInfo.User = score.User;

            AddStep("create loaded leaderboard manager", () => LoadComponent(manager = new LeaderboardManager()));
            AddStep("select scope", () => manager.FetchWithCriteria(new LeaderboardCriteria(score.BeatmapInfo, score.Ruleset, scope, null)));
            AddStep("capture selected leaderboard", () =>
            {
                ((Bindable<LeaderboardScores?>)manager.Scores).Value = LeaderboardScores.Success(
                    [new ScoreInfo(score.BeatmapInfo, score.Ruleset)
                    {
                        User = new APIUser { Id = 33, Username = "Top player" },
                        TotalScore = 900000, MosuTotalScore = 900000, Position = 1,
                    }], 50, 1000, null);
                snapshot = ReplayRenderSnapshot.Capture(score, manager, API, Dependencies.Get<OsuConfigManager>());
                // The worker has no live leaderboard, only the transferred packet.
                ((Bindable<LeaderboardScores?>)manager.Scores).Value = null;
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies =
                    [
                        (typeof(ReplayRenderSnapshot), snapshot),
                        (typeof(GameplayState), state),
                        (typeof(LeaderboardManager), manager),
                    ],
                    Child = provider = new SoloGameplayLeaderboardProvider(),
                };
            });
            AddUntilStep("offline top loaded", () => provider?.Scores.Count == 2);
            AddAssert("scope preserved", () => snapshot.Scope, () => Is.EqualTo(scope));
            AddAssert("correct opponent", () => provider.Scores.Single(s => !s.Tracked).User.Username, () => Is.EqualTo("Top player"));
            AddAssert("correct replay player", () => provider.Scores.Single(s => s.Tracked).User.Username, () => Is.EqualTo("Rendered player"));
            AddAssert("unknown rank remains unknown", () => provider.Scores.Single(s => s.Tracked).Position.Value, () => Is.Null);
            AddStep("advance replay score", () => state.ScoreProcessor.TotalScore.Value = 950000);
            AddUntilStep("replay overtakes captured top", () => provider.Scores.Single(s => s.Tracked).DisplayOrder.Value == 1);
        }

        [Test]
        public void TestForeignBeatmapLeaderboardIsNotCaptured()
        {
            LeaderboardManager manager = null!;
            ReplayRenderSnapshot snapshot = null!;
            AddStep("create manager", () => LoadComponent(manager = new LeaderboardManager()));
            AddStep("select another beatmap", () => manager.FetchWithCriteria(new LeaderboardCriteria(new osu.Game.Beatmaps.BeatmapInfo(), Ruleset.Value, BeatmapLeaderboardScope.Global, null)));
            AddStep("capture", () =>
            {
                ((Bindable<LeaderboardScores?>)manager.Scores).Value = LeaderboardScores.Success([new ScoreInfo()], 50, 100, null);
                snapshot = ReplayRenderSnapshot.Capture(new ScoreInfo(Beatmap.Value.BeatmapInfo, Ruleset.Value), manager, API, Dependencies.Get<OsuConfigManager>());
            });
            AddAssert("unrelated top excluded", () => snapshot.TopScores, () => Is.Empty);
        }

        [Test]
        public void TestResultsUseTransferredTopWithoutApiRequests()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var score = new ScoreInfo(Beatmap.Value.BeatmapInfo, ruleset) { OnlineID = 123 };
            ResultsProbe results = null!;
            Task<ScoreInfo[]>? fetched = null;
            var snapshot = new ReplayRenderSnapshot
            {
                Scope = BeatmapLeaderboardScope.Country,
                TopScores =
                [
                    new ReplayRenderSnapshot.RenderScore { Id = score.ID, OnlineId = 123 },
                    new ReplayRenderSnapshot.RenderScore
                    {
                        Id = Guid.NewGuid(), OnlineId = 124, Position = 8, TotalScore = 900000,
                        User = new ReplayRenderSnapshot.RenderUser { Id = 33, Username = "Country top" },
                        RulesetId = ruleset.OnlineID, RulesetShortName = ruleset.ShortName,
                        RulesetInstantiationInfo = ruleset.InstantiationInfo,
                    },
                ],
            };
            AddStep("load render results", () =>
            {
                var stack = new OsuScreenStack { RelativeSizeAxes = Axes.Both };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = [(typeof(ReplayRenderSnapshot), snapshot)],
                    Child = stack,
                };
                stack.PushSynchronously(results = new ResultsProbe(score));
            });
            AddUntilStep("results loaded", () => results.IsLoaded);
            AddStep("fetch snapshot results", () => fetched = results.FetchSnapshotAsync());
            AddUntilStep("fetch completed", () => fetched?.IsCompletedSuccessfully == true);
            AddAssert("only opponent returned", () => fetched!.GetResultSafely().Length, () => Is.EqualTo(1));
            AddAssert("selected top returned", () => fetched!.GetResultSafely()[0].User.Username, () => Is.EqualTo("Country top"));
            AddAssert("original position preserved", () => fetched!.GetResultSafely()[0].Position, () => Is.EqualTo(8));
        }

        private sealed partial class ResultsProbe : RenderResultsScreen
        {
            public ResultsProbe(ScoreInfo score) : base(score) { }
            public Task<ScoreInfo[]> FetchSnapshotAsync() => base.FetchScores();
        }

        [Test]
        public void TestAvatarDecodesFromPacketWithoutNetworkOrOfflineEndpoint()
        {
            DrawableAvatar avatar = null!;
            Task? load = null;
            const string avatarUrl = "https://unreachable.invalid/avatar.png";
            var snapshot = new ReplayRenderSnapshot
            {
                Player = new ReplayRenderSnapshot.RenderUser { Id = 22, Username = "Player", AvatarUrl = avatarUrl },
            };
            using (var image = new Image<Rgba32>(7, 9))
            using (var stream = new MemoryStream())
            {
                image.SaveAsPng(stream);
                snapshot.Avatars[avatarUrl] = stream.ToArray();
            }

            AddStep("install transferred image bytes", () => Dependencies.Get<OnlineAssetCachingStore>().UseRenderSnapshot(snapshot.Avatars));
            AddStep("load real avatar with offline API", () =>
            {
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = [(typeof(ReplayRenderSnapshot), snapshot)],
                    Child = new AvatarLoader(snapshot.Player.ToUser(), task => load = task, value => avatar = value),
                };
            });
            AddUntilStep("avatar loaded", () => load?.IsCompleted == true && avatar?.IsLoaded == true);
            AddAssert("encoded snapshot decoded", () => avatar.Texture?.DisplayWidth, () => Is.EqualTo(7));
            AddAssert("not guest placeholder", () => avatar.Texture?.DisplayHeight, () => Is.EqualTo(9));
        }

        private sealed partial class AvatarLoader : osu.Framework.Graphics.Containers.Container
        {
            private readonly APIUser user;
            private readonly Action<Task> loading;
            private readonly Action<DrawableAvatar> loaded;

            public AvatarLoader(APIUser user, Action<Task> loading, Action<DrawableAvatar> loaded)
            {
                this.user = user;
                this.loading = loading;
                this.loaded = loaded;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                loading(LoadComponentAsync(new DrawableAvatar(user), avatar =>
                {
                    Add(avatar);
                    loaded(avatar);
                }));
            }
        }
    }
}
