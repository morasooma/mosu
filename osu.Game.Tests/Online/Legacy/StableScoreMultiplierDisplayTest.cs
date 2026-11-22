using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Utils;

namespace osu.Game.Tests.Online.Legacy
{
    [TestFixture]
    [NonParallelizable]
    public class StableScoreMultiplierDisplayTest
    {
        private bool previousStable;

        [SetUp]
        public void SetUp()
        {
            previousStable = MosuServerEnvironment.UsesStableProtocol;
            MosuServerEnvironment.UsesStableProtocol = true;
        }

        [TearDown]
        public void TearDown() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestClassicIsNeutralForAllStableModes(int mode)
        {
            var ruleset = createRuleset(mode);
            var beatmap = new BeatmapInfo { Ruleset = ruleset.RulesetInfo };
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, new[] { ruleset.CreateMod<ModClassic>()! }), Is.EqualTo(1));
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, Array.Empty<Mod>()), Is.EqualTo(1));
        }

        [TestCase(0, "HD", 1.06)]
        [TestCase(0, "HR", 1.06)]
        [TestCase(0, "DT", 1.12)]
        [TestCase(0, "NC", 1.12)]
        [TestCase(0, "SO", 0.9)]
        [TestCase(0, "RX", 0)]
        [TestCase(1, "DT", 1.12)]
        [TestCase(1, "HR", 1.06)]
        [TestCase(1, "HT", 0.3)]
        [TestCase(2, "HR", 1.12)]
        [TestCase(2, "DT", 1.06)]
        [TestCase(2, "HT", 0.3)]
        [TestCase(3, "DT", 1)]
        [TestCase(3, "HD", 1)]
        [TestCase(3, "HT", 0.5)]
        [TestCase(3, "EZ", 0.5)]
        public void TestStableModMultipliers(int mode, string acronym, double expected)
        {
            var ruleset = createRuleset(mode);
            var beatmap = new BeatmapInfo { Ruleset = ruleset.RulesetInfo };
            Mod[] mods = { ruleset.CreateMod<ModClassic>()!, ruleset.CreateModFromAcronym(acronym)! };
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, mods), Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void TestCombinationDoesNotIncludeClassicPenaltyOrDuplicateMods()
        {
            var ruleset = new OsuRuleset();
            var beatmap = new BeatmapInfo { Ruleset = ruleset.RulesetInfo };
            Mod[] mods = { new OsuModClassic(), new OsuModHidden(), new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime() };
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, mods), Is.EqualTo(1.06 * 1.06 * 1.12).Within(1e-12));
        }

        [TestCase(true, "5K", 1)]
        [TestCase(false, "5K", 0.9)]
        [TestCase(false, "3K", 0.86)]
        public void TestManiaKeyModUsesSourceMapMetadata(bool native, string acronym, double expected)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new BeatmapInfo
            {
                Ruleset = native ? ruleset.RulesetInfo : new OsuRuleset().RulesetInfo,
                Difficulty = new BeatmapDifficulty { CircleSize = 4, OverallDifficulty = 4 },
                TotalObjectCount = 100,
                EndTimeObjectCount = 80,
            };
            Mod[] mods = { new ManiaModClassic(), ruleset.CreateModFromAcronym(acronym)! };
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, mods), Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void TestLazerMultipliersAreUnchanged()
        {
            MosuServerEnvironment.UsesStableProtocol = false;
            var ruleset = new OsuRuleset();
            var beatmap = new BeatmapInfo { Ruleset = ruleset.RulesetInfo };
            Mod[] mods = { new OsuModClassic(), new OsuModHidden(), new OsuModDoubleTime() };
            double expected = ruleset.CreateScoreMultiplierCalculator(new ScoreMultiplierContext(beatmap.Difficulty)).CalculateFor(mods);
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, mods), Is.EqualTo(expected));
            Assert.That(ModUtils.CalculateScoreMultiplierForDisplay(ruleset, beatmap, new[] { new OsuModClassic() }), Is.EqualTo(0.985));
        }

        private static Ruleset createRuleset(int mode) => mode switch
        {
            0 => new OsuRuleset(),
            1 => new TaikoRuleset(),
            2 => new CatchRuleset(),
            _ => new ManiaRuleset(),
        };
    }
}
