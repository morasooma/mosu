using NUnit.Framework;
using osu.Game.Rulesets;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BundledRulesetPolicyTest
    {
        [Test]
        public void BundledRulesetsActuallyLoad()
        {
            using var store = new AssemblyRulesetStore();
            Assert.Multiple(() =>
            {
                Assert.That(store.GetRuleset(0), Is.Not.Null);
                Assert.That(store.GetRuleset(1), Is.Not.Null);
                Assert.That(store.GetRuleset(2), Is.Not.Null);
                Assert.That(store.GetRuleset(3), Is.Not.Null);
            });
        }

        [TestCase("osu.Game.Rulesets.Osu")]
        [TestCase("osu.Game.Rulesets.Taiko")]
        [TestCase("osu.Game.Rulesets.Catch")]
        [TestCase("osu.Game.Rulesets.Mania")]
        [TestCase("osu.Game.Rulesets.Dodge")]
        public void BundledRulesetsAreAllowed(string name)
            => Assert.That(RulesetStore.IsBundledRulesetAssemblyName(name), Is.True);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("osu.Game.Rulesets.SecurityProbe")]
        [TestCase("osu.Game.Rulesets.Osu.Tests")]
        [TestCase("osu.Game.Rulesets.Osu.Evil")]
        [TestCase("osu.game.rulesets.osu")]
        public void ExternalAssembliesAreNotAllowed(string? name)
            => Assert.That(RulesetStore.IsBundledRulesetAssemblyName(name), Is.False);
    }
}
