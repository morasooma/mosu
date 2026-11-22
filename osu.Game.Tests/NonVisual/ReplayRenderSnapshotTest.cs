using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Scoring.Render;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Users;
using osu.Game.Users.Drawables;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class ReplayRenderSnapshotTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestSnapshotRoundTripRetainsPresentationWithoutAccountState(bool stable)
        {
            bool previousStable = MosuServerEnvironment.UsesStableProtocol;
            try
            {
                MosuServerEnvironment.UsesStableProtocol = stable;
                using var api = new DummyAPIAccess();
                api.Endpoints.APIUrl = "https://morasooma.net/api/v2";
                var score = new ScoreInfo(ruleset: new OsuRuleset().RulesetInfo)
                {
                    User = new APIUser { Id = 25, Username = "Player", CountryCode = CountryCode.RU, AvatarUrl = "https://morasooma.net/file/avatar.png" },
                    OnlineID = 1234, Position = 15, TotalScore = 750000, MosuTotalScore = 800000, LegacyTotalScore = 9876543,
                    Accuracy = 0.98, Combo = 120, MaxCombo = 350, PP = 200, Rank = ScoreRank.A,
                    Date = DateTimeOffset.UtcNow, Mods = [new OsuModHidden()],
                    Statistics = { [HitResult.Great] = 200 }, MaximumStatistics = { [HitResult.Great] = 210 },
                };
                var snapshot = new ReplayRenderSnapshot
                {
                    Player = ReplayRenderSnapshot.RenderUser.From(score.User, api),
                    Scope = BeatmapLeaderboardScope.Friend, Sorting = LeaderboardSortMode.Accuracy,
                    ExactMods = score.APIMods, ShowLeaderboard = true, ScoreDisplayMode = ScoringMode.Classic,
                    TopScores = [ReplayRenderSnapshot.RenderScore.From(score, api)],
                    UserScore = ReplayRenderSnapshot.RenderScore.From(score, api),
                    ScoresRequested = 50, TotalScores = 1000,
                    Avatars = { [score.User.AvatarUrl] = [1, 2, 3] },
                };
                using var storage = new TemporaryNativeStorage($"render-snapshot-{Guid.NewGuid():N}");
                string path = storage.GetFullPath("snapshot.json");
                snapshot.Write(path);
                var restored = ReplayRenderSnapshot.Read(path);
                var scores = restored.CreateLeaderboardScores();
                var restoredScore = scores.TopScores.Single();
                string json = File.ReadAllText(path);

                Assert.Multiple(() =>
                {
                    Assert.That(restored.Scope, Is.EqualTo(BeatmapLeaderboardScope.Friend));
                    Assert.That(restored.Sorting, Is.EqualTo(LeaderboardSortMode.Accuracy));
                    Assert.That(restored.ExactMods!.Single().Acronym, Is.EqualTo("HD"));
                    Assert.That(restored.ScoreDisplayMode, Is.EqualTo(ScoringMode.Classic));
                    Assert.That(restored.ShowLeaderboard, Is.True);
                    Assert.That(scores.IsPartial, Is.True);
                    Assert.That(scores.AllScores.Count(), Is.EqualTo(1), "own best score is not duplicated");
                    Assert.That(restoredScore.User.Username, Is.EqualTo("Player"));
                    Assert.That(restoredScore.User.AvatarUrl, Is.EqualTo(score.User.AvatarUrl));
                    Assert.That(restoredScore.User.CountryCode, Is.EqualTo(CountryCode.RU));
                    Assert.That(restoredScore.Position, Is.EqualTo(15));
                    Assert.That(restoredScore.MaxCombo, Is.EqualTo(350));
                    Assert.That(restoredScore.Accuracy, Is.EqualTo(0.98));
                    Assert.That(restoredScore.Statistics[HitResult.Great], Is.EqualTo(200));
                    Assert.That(restoredScore.Mods.Single(), Is.TypeOf<OsuModHidden>());
                    Assert.That(restoredScore.GetDisplayScore(ScoringMode.Classic), Is.EqualTo(score.GetDisplayScore(ScoringMode.Classic)));
                    Assert.That(restored.Avatars[score.User.AvatarUrl], Is.EqualTo(new byte[] { 1, 2, 3 }));
                    Assert.That(json, Does.Not.Contain("AccessToken"));
                    Assert.That(json, Does.Not.Contain("RealmUser"));
                });
            }
            finally
            {
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            }
        }

        [TestCase(ScalingMode.Off)]
        [TestCase(ScalingMode.Everything)]
        [TestCase(ScalingMode.ExcludeOverlays)]
        [TestCase(ScalingMode.Gameplay)]
        public void TestCurrentLayoutRoundTripsWithoutSavingParentConfig(ScalingMode mode)
        {
            using var parentStorage = new TemporaryNativeStorage($"render-layout-parent-{Guid.NewGuid():N}");
            using var childStorage = new TemporaryNativeStorage($"render-layout-child-{Guid.NewGuid():N}");
            using var parent = new OsuConfigManager(parentStorage);
            using var child = new OsuConfigManager(childStorage);
            parent.DisableSaving();
            child.DisableSaving();
            parent.SetValue(OsuSetting.Scaling, mode);
            parent.SetValue(OsuSetting.ScalingSizeX, 0.65f);
            parent.SetValue(OsuSetting.ScalingSizeY, 0.75f);
            parent.SetValue(OsuSetting.ScalingPositionX, 0.3f);
            parent.SetValue(OsuSetting.ScalingPositionY, 0.7f);
            parent.SetValue(OsuSetting.UIScale, 0.9f);
            parent.SetValue(OsuSetting.ForkDisableInterfaceShear, true);
            using var api = new DummyAPIAccess();
            var snapshot = ReplayRenderSnapshot.Capture(new ScoreInfo(ruleset: new OsuRuleset().RulesetInfo), null, api, parent);
            string path = childStorage.GetFullPath("snapshot.json");
            snapshot.Write(path);
            ReplayRenderSnapshot.Read(path).Layout!.Apply(child);
            Assert.Multiple(() =>
            {
                Assert.That(child.Get<ScalingMode>(OsuSetting.Scaling), Is.EqualTo(mode));
                Assert.That(child.Get<float>(OsuSetting.ScalingSizeX), Is.EqualTo(0.65f));
                Assert.That(child.Get<float>(OsuSetting.ScalingSizeY), Is.EqualTo(0.75f));
                Assert.That(child.Get<float>(OsuSetting.ScalingPositionX), Is.EqualTo(0.3f));
                Assert.That(child.Get<float>(OsuSetting.ScalingPositionY), Is.EqualTo(0.7f));
                Assert.That(child.Get<float>(OsuSetting.UIScale), Is.EqualTo(0.9f));
                Assert.That(child.Get<bool>(OsuSetting.ForkDisableInterfaceShear), Is.True);
                Assert.That(parent.Save(), Is.False);
                Assert.That(child.Save(), Is.False);
            });
        }

        [Test]
        public void TestSnapshotDoesNotShareScoreStatisticsOrMods()
        {
            using var api = new DummyAPIAccess();
            var score = new ScoreInfo(ruleset: new OsuRuleset().RulesetInfo)
            {
                Mods = [new OsuModHidden()], Statistics = { [HitResult.Great] = 10 },
            };
            var captured = ReplayRenderSnapshot.RenderScore.From(score, api);
            score.Statistics[HitResult.Great] = 999;
            score.APIMods[0].Acronym = "HR";
            Assert.That(captured.Statistics[HitResult.Great], Is.EqualTo(10));
            Assert.That(captured.Mods.Single().Acronym, Is.EqualTo("HD"));
        }

        [TestCase(false, "https://old.example/avatar.png", "https://morasooma.net/api/v2/users/25/avatar")]
        [TestCase(false, "https://morasooma.net/file/avatar.png", "https://morasooma.net/file/avatar.png")]
        [TestCase(true, "https://other.example/avatar.png", "https://other.example/avatar.png")]
        [TestCase(true, "", "https://morasooma.net/api/v2/users/25/avatar")]
        public void TestAvatarSnapshotUsesSameLookupAsClient(bool thirdParty, string original, string expected)
        {
            Assert.That(DrawableAvatar.GetAvatarLookup(new APIUser { Id = 25, AvatarUrl = original },
                "https://morasooma.net/api/v2", thirdParty), Is.EqualTo(expected));
        }
    }
}
