using System.Linq;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Mods
{
    [TestFixture]
    public class ModVisibilityTest
    {
        [Test]
        public void TestMosuRelaxIsOnlyVisuallyHidden()
        {
            var relax = new OsuModMosuRelax();
            var hidden = new OsuModHidden();
            Mod[] mods = { relax, hidden };

            Assert.That(mods.AsVisible().AsOrdered().ToArray(), Is.EqualTo(new Mod[] { hidden }));
            Assert.That(mods, Is.EqualTo(new Mod[] { relax, hidden }));
            Assert.That(new APIMod(relax).ToMod(new OsuRuleset()), Is.TypeOf<OsuModMosuRelax>());
            Assert.That(new OsuRuleset().AllMods.OfType<OsuModMosuRelax>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TestOtherModsRemainVisible()
        {
            Mod[] mods = { new OsuModRelax(), new OsuModDoubleTime(), new OsuModHidden() };
            Assert.That(mods.AsVisible().ToArray(), Is.EqualTo(mods));
            Assert.That(new Mod[] { new OsuModMosuRelax() }.AsVisible(), Is.Empty);
            Assert.That(System.Array.Empty<Mod>().AsVisible(), Is.Empty);
        }
    }
}
