using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Models;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    [HeadlessTest]
    public partial class TestSceneUserRankRefresh : OsuTestScene
    {
        private LocalUserStatisticsProvider statisticsProvider = null!;
        private UserRankPanel panel = null!;
        private UserProfileOverlay profile = null!;
        private UserStatisticsWatcher watcher = null!;

        [Resolved]
        private SpectatorClient spectatorClient { get; set; } = null!;

        private readonly Dictionary<string, UserStatistics> serverStatistics = new Dictionary<string, UserStatistics>();
        private bool previousSpecialRulesets;
        private bool previousThirdParty;
        private bool previousStable;

        [SetUp]
        public void SaveEnvironment()
        {
            previousSpecialRulesets = MosuServerEnvironment.SupportsSpecialRulesets;
            previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
            previousStable = MosuServerEnvironment.UsesStableProtocol;
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            MosuServerEnvironment.SupportsSpecialRulesets = previousSpecialRulesets;
            MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
            MosuServerEnvironment.UsesStableProtocol = previousStable;
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("configure local server responses", () =>
            {
                MosuServerEnvironment.SupportsSpecialRulesets = true;
                MosuServerEnvironment.IsThirdPartyServer = false;
                MosuServerEnvironment.UsesStableProtocol = false;
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                SelectedMods.Value = Array.Empty<Mod>();
                serverStatistics.Clear();
                serverStatistics["osu"] = new UserStatistics { GlobalRank = 22, CountryRank = 4 };
                serverStatistics["taiko"] = new UserStatistics { GlobalRank = 30, CountryRank = 6 };
                serverStatistics["osurx"] = new UserStatistics { GlobalRank = 7, CountryRank = 2 };
                var api = (DummyAPIAccess)API;
                api.LocalUser.Value = new APIUser { Id = 42, Username = "rank fixture" };
                api.HandleRequest = request =>
                {
                    if (request is not GetUserRequest userRequest)
                        return false;

                    userRequest.TriggerSuccess(new APIUser
                    {
                        Id = 42,
                        Username = "rank fixture",
                        PlayMode = userRequest.Ruleset?.ShortName ?? "osu",
                        Statistics = serverStatistics.GetValueOrDefault(userRequest.Ruleset?.ShortName ?? "osu") ?? new UserStatistics(),
                        ProfileOrder = Array.Empty<string>(),
                        Kudosu = new APIUser.KudosuCount()
                    });
                    return true;
                };
                statisticsProvider = new LocalUserStatisticsProvider();
                profile = new UserProfileOverlay();
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(LocalUserStatisticsProvider), statisticsProvider),
                        (typeof(UserProfileOverlay), profile),
                        (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink))
                    },
                    Children = new Drawable[]
                    {
                        statisticsProvider,
                        watcher = new UserStatisticsWatcher(statisticsProvider),
                        panel = new UserRankPanel(api.LocalUser.Value) { Width = 300 },
                        profile
                    }
                };
            });
            assertPanelRanks(22, 4);
        }

        [Test]
        public void TestModeSwitchRefetchesRanksAndReturningDoesNotUseSessionSnapshot()
        {
            AddStep("change taiko rank on server", () => serverStatistics["taiko"] = new UserStatistics { GlobalRank = 12, CountryRank = 3 });
            AddStep("switch to taiko", () => Ruleset.Value = new TaikoRuleset().RulesetInfo);
            assertPanelRanks(12, 3);
            AddStep("change osu rank on server", () => serverStatistics["osu"] = new UserStatistics { GlobalRank = 18, CountryRank = 2 });
            AddStep("switch back to osu", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
            assertPanelRanks(18, 2);
        }

        [Test]
        public void TestRelaxSelectionRefetchesCorrectStatistics()
        {
            AddStep("change relax rank on server", () => serverStatistics["osurx"] = new UserStatistics { GlobalRank = 5, CountryRank = 1 });
            AddStep("enable relax", () => SelectedMods.Value = new Mod[] { new OsuModRelax() });
            assertPanelRanks(5, 1);
            AddStep("disable relax", () => SelectedMods.Value = Array.Empty<Mod>());
            assertPanelRanks(22, 4);
        }

        [Test]
        public void TestDisposingPanelDoesNotUnbindGameSelection()
        {
            IBindable<RulesetInfo> observer = null!;
            IBindable<IReadOnlyList<Mod>> modsObserver = null!;
            AddStep("bind game selection observers", () =>
            {
                observer = Ruleset.GetBoundCopy();
                modsObserver = SelectedMods.GetBoundCopy();
            });
            AddStep("dispose rank panel", () => ((Container)panel.Parent!).Remove(panel, true));
            AddStep("change game selection", () =>
            {
                Ruleset.Value = new TaikoRuleset().RulesetInfo;
                SelectedMods.Value = new Mod[] { new OsuModRelax() };
            });
            AddAssert("ruleset remains bound", () => observer.Value.ShortName == "taiko");
            AddAssert("mods remain bound", () => modsObserver.Value.Count == 1);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReopeningProfileRefreshesRanks(bool thirdPartyServer)
        {
            AddStep("select server type", () => MosuServerEnvironment.IsThirdPartyServer = thirdPartyServer);
            AddStep("open profile", () => profile.ShowUser(API.LocalUser.Value));
            AddUntilStep("initial profile ready", () => profile.Header.User.Value?.User.Statistics.GlobalRank == 22);
            AddStep("hide profile and change server rank", () =>
            {
                profile.Hide();
                serverStatistics["osu"] = new UserStatistics { GlobalRank = 18, CountryRank = 2 };
            });
            AddStep("reopen same profile", () => profile.ShowUser(API.LocalUser.Value));
            AddUntilStep("new profile ranks displayed", () => profile.Header.User.Value?.User.Statistics is { GlobalRank: 18, CountryRank: 2 });
        }

        [Test]
        public void TestStatisticsUpdateRefreshesOpenProfileAndPanel()
        {
            AddStep("open profile", () => profile.ShowUser(API.LocalUser.Value));
            AddUntilStep("initial profile ready", () => profile.Header.User.Value?.User.Statistics.GlobalRank == 22);
            AddStep("watch submitted score", () => watcher.RegisterForStatisticsUpdateAfter(
                new ScoreInfo(Beatmap.Value.BeatmapInfo, Ruleset.Value, new RealmUser()) { OnlineID = 12345 }));
            AddStep("processed score changes server statistics", () =>
            {
                serverStatistics["osu"] = new UserStatistics { GlobalRank = 18, CountryRank = 2 };
                ((ISpectatorClient)spectatorClient).UserScoreProcessed(42, 12345);
            });
            assertPanelRanks(18, 2);
            AddUntilStep("open profile refreshed", () => profile.Header.User.Value?.User.Statistics is { GlobalRank: 18, CountryRank: 2 });
        }

        private void assertPanelRanks(int global, int country)
        {
            AddUntilStep($"global rank is {global}", () => panel.ChildrenOfType<GlobalRankDisplay>().Single()
                .ChildrenOfType<ProfileValueDisplay>().Any(display => display.Content.Text.ToString() == $"#{global}"));
            AddAssert($"country rank is {country}", () => panel.ChildrenOfType<ProfileValueDisplay>().Any(display => display.Content.Text.ToString() == $"#{country}"));
        }
    }
}
