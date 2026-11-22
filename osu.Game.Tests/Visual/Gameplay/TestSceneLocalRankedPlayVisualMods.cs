using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneLocalRankedPlayVisualMods : PlayerTestScene
    {
        private string acronym = "HD";
        private bool mania;

        protected override bool HasCustomSteps => true;
        protected override Ruleset CreatePlayerRuleset() => mania ? new ManiaRuleset() : new OsuRuleset();

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new LocalVisualPlayer(acronym);

        [TestCase("HD", false)]
        [TestCase("TC", false)]
        [TestCase("HD", true)]
        [TestCase("FI", true)]
        [TestCase("CO", true)]
        public void LocalModDoesNotEnterScoreOrGameplayMods(string mod, bool useMania)
        {
            CreateTest(() => AddStep("set visual preference", () =>
            {
                acronym = mod;
                mania = useMania;
            }));
            AddAssert("renderer has local mod", () => Player.DrawableRuleset.Mods.Any(m => m.Acronym == mod));
            AddAssert("global mods omit local mod", () => Player.Mods.Value.All(m => m.Acronym != mod));
            AddAssert("score processor mods omit local mod", () => Player.ScoreProcessor.Mods.Value.All(m => m.Acronym != mod));
            AddAssert("score and gameplay state omit local mod", () => ((LocalVisualPlayer)Player).IsIsolated);
        }

        private partial class LocalVisualPlayer : TestPlayer
        {
            private readonly string acronym;

            public LocalVisualPlayer(string acronym) => this.acronym = acronym;

            public bool IsIsolated => Score.ScoreInfo.Mods.All(m => m.Acronym != acronym)
                                      && GameplayState.Mods.All(m => m.Acronym != acronym);

            protected override Mod[] CreateLocalVisualMods(Ruleset currentRuleset, IReadOnlyList<Mod> gameplayMods)
                => new LocalRankedPlayVisualMods(acronym).Resolve(currentRuleset, gameplayMods);
        }
    }
}
