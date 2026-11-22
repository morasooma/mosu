using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using LegacyFooterButtonMods = osu.Game.Screens.SelectLegacy.FooterButtonMods;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneRankedStarLimit : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Cached]
        private readonly BeatmapDifficultyCache difficultyCache = new BeatmapDifficultyCache();

        private FooterButtonMods footer = null!;
        private LegacyFooterButtonMods legacyFooter = null!;
        private ModSelectFooterContent modFooter = null!;
        private bool previousThirdParty;
        private bool previousStable;
        private string previousApiUrl = null!;

        [SetUp]
        public void SaveFlags()
        {
            previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
            previousStable = MosuServerEnvironment.UsesStableProtocol;
            previousApiUrl = MosuServerEnvironment.ActiveApiUrl;
        }

        [TearDown]
        public void RestoreFlags()
        {
            MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
            MosuServerEnvironment.UsesStableProtocol = previousStable;
            MosuServerEnvironment.ActiveApiUrl = previousApiUrl;
        }

        [Test]
        public void TestAllIndicatorsFollowCalculatedStarsAndRelax()
        {
            AddStep("load all three indicators", () =>
            {
                MosuServerEnvironment.ActiveApiUrl = MosuServerEnvironment.PublicServerUrl;
                MosuServerEnvironment.IsThirdPartyServer = false;
                MosuServerEnvironment.UsesStableProtocol = false;
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                SelectedMods.Value = Array.Empty<Mod>();
                Beatmap.Value = createMap(11);
                Add(difficultyCache);
                Dependencies.CacheAs<IBindable<WorkingBeatmap>>(Beatmap);
                Add(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        footer = new FooterButtonMods(new ModSelectOverlay()) { Mods = { BindTarget = SelectedMods }, Ruleset = { BindTarget = Ruleset } },
                        legacyFooter = new LegacyFooterButtonMods { Current = { BindTarget = SelectedMods } },
                        modFooter = new ModSelectFooterContent(new ModSelectOverlay())
                        {
                            State = { Value = Visibility.Visible },
                            Beatmap = { BindTarget = Beatmap },
                            Ruleset = { BindTarget = Ruleset },
                            ActiveMods = { BindTarget = SelectedMods },
                        },
                    },
                });
            });
            assertRanked(true);
            AddStep("map above normal limit without mods", () => Beatmap.Value = createMap(13));
            assertRanked(false);
            AddUntilStep("no empty mod-bar space", () =>
            {
                var badge = footer.ChildrenOfType<FooterButtonMods.UnrankedBadge>().Single();
                return badge.Margin.Left == 0 && Math.Abs(footer.DrawWidth - Math.Max(116, badge.DrawWidth)) < 0.1f;
            });
            AddStep("foreign server above limit", () =>
            {
                MosuServerEnvironment.ActiveApiUrl = "https://another-server.example";
                Beatmap.Value = createMap(20);
            });
            assertRanked(true);
            AddStep("return to Mosu proxy", () =>
            {
                MosuServerEnvironment.ActiveApiUrl = MosuServerEnvironment.SecondaryProxyServerUrl;
                Beatmap.Value = createMap(13);
            });
            assertRanked(false);
            AddStep("enable Relax", () => SelectedMods.Value = new Mod[] { new OsuModRelax() });
            assertRanked(true);
            AddUntilStep("mod bar keeps its normal width", () => footer.DrawWidth == 116);
            AddStep("map above Relax limit", () => Beatmap.Value = createMap(17));
            assertRanked(false);
            AddUntilStep("badge beside populated mod bar", () =>
            {
                var badge = footer.ChildrenOfType<FooterButtonMods.UnrankedBadge>().Single();
                return badge.Margin.Left == 121 && Math.Abs(footer.DrawWidth - (121 + badge.DrawWidth)) < 0.1f;
            });
            AddStep("back to exactly Relax limit", () => Beatmap.Value = createMap(16));
            assertRanked(true);
            AddStep("remove Relax", () => SelectedMods.Value = Array.Empty<Mod>());
            assertRanked(false);
            AddStep("switch to lower difficulty", () => Beatmap.Value = createMap(11));
            assertRanked(true);
            AddStep("simulate modded difficulty completing above limit", () => publishStars(12.5));
            assertRanked(false);
            AddStep("simulate rate reduction completing at limit", () => publishStars(12));
            assertRanked(true);
            AddStep("unranked mod stays unranked below limit", () => SelectedMods.Value = new Mod[] { new OsuModAutopilot() });
            assertRanked(false);
        }

        private WorkingBeatmap createMap(double stars)
        {
            var map = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
            map.BeatmapInfo.ID = Guid.NewGuid();
            map.BeatmapInfo.MD5Hash = Guid.NewGuid().ToString("N");
            map.BeatmapInfo.IsExternalCatalogue = true;
            map.BeatmapInfo.StarRating = stars;
            return map;
        }

        private void publishStars(double stars)
        {
            // Publish into the actual cache subscriptions, as an asynchronous modded calculation would.
            foreach (object owner in new object[] { footer, legacyFooter, modFooter.ChildrenOfType<BeatmapAttributesDisplay>().Single() })
            {
                var field = owner.GetType().GetField("starDifficulty", BindingFlags.NonPublic | BindingFlags.Instance)!;
                ((Bindable<StarDifficulty>)field.GetValue(owner)!).Value = new StarDifficulty(stars, 1);
            }
        }

        private void assertRanked(bool ranked)
        {
            AddUntilStep($"mod overlay ranked={ranked}", () => modFooter.ChildrenOfType<RankingInformationDisplay>().SingleOrDefault()?.Ranked.Value == ranked);
            AddUntilStep($"modern badge ranked={ranked}", () => footer.ChildrenOfType<FooterButtonMods.UnrankedBadge>().SingleOrDefault()?.Alpha == (ranked ? 0 : 1));
            AddUntilStep($"legacy badge ranked={ranked}", () => legacyBadge().Alpha == (ranked ? 0 : 1));
        }

        private Container legacyBadge() => (Container)typeof(LegacyFooterButtonMods)
            .GetProperty("UnrankedBadge", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(legacyFooter)!;
    }
}
