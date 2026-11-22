using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [HeadlessTest]
    public partial class TestSceneAimAssistRankedIndicator : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private bool previousThirdParty;
        private bool previousStable;

        [SetUp]
        public void SaveServerFlags()
        {
            previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
            previousStable = MosuServerEnvironment.UsesStableProtocol;
        }

        [TearDown]
        public void RestoreServerFlags()
        {
            MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
            MosuServerEnvironment.UsesStableProtocol = previousStable;
        }

        [Test]
        public void TestFooterReflectsAlgorithmChangesAndRelaxCombination()
        {
            var mods = new Bindable<IReadOnlyList<Mod>>();
            var assist = new OsuModMosuAimAssist();
            var relax = new OsuModRelax();
            RankingInformationDisplay ranking = null!;
            ModSelectFooterContent footer = null!;
            AddStep("load footer with original ranked assist", () =>
            {
                MosuServerEnvironment.IsThirdPartyServer = false;
                MosuServerEnvironment.UsesStableProtocol = false;
                Beatmap.Value = CreateWorkingBeatmap(new OsuBeatmap());
                mods.Value = new Mod[] { assist };
                Child = footer = new ModSelectFooterContent(new ModSelectOverlay()) { State = { Value = Visibility.Visible } };
                footer.Beatmap.BindTo(Beatmap);
                footer.Ruleset.BindTo(Ruleset);
                footer.ActiveMods.BindTo(mods);
            });
            AddUntilStep("ranking display loaded", () => (ranking = footer.ChildrenOfType<RankingInformationDisplay>().SingleOrDefault()!)?.IsLoaded == true);
            AddAssert("Mosu alone ranked", () => ranking.Ranked.Value);
            AddStep("switch to Maple V2", () => assist.Algorithm.Value = MosuAimAssistAlgorithm.MapleV2);
            AddUntilStep("Maple V2 unranked", () => !ranking.Ranked.Value);
            AddStep("switch to Maple V3", () => assist.Algorithm.Value = MosuAimAssistAlgorithm.MapleV3);
            AddAssert("Maple V3 unranked", () => !ranking.Ranked.Value);
            AddStep("restore Mosu", () => assist.Algorithm.Value = MosuAimAssistAlgorithm.Mosu);
            AddUntilStep("Mosu ranked again", () => ranking.Ranked.Value);
            AddStep("add compatible Relax", () => mods.Value = new Mod[] { assist, relax });
            AddUntilStep("combination unranked", () => !ranking.Ranked.Value);
            AddStep("remove assist", () => mods.Value = new Mod[] { relax });
            AddUntilStep("Relax alone still ranked", () => ranking.Ranked.Value);
            AddStep("restore server flags", () =>
            {
                MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            });
        }
    }
}
