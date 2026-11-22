// Adapted from taziksfear’s MosuPp 0.4.0 tests.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class TazikPpBalanceTest
    {
        [Test]
        public void TestClientCannotLoadLabOverrides()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(TazikPpBalance).GetMethod("ConfigureForLab"), Is.Null);
                Assert.That(typeof(TazikPpBalance).GetProperty(nameof(TazikPpBalance.Current))!.GetSetMethod(true), Is.Null);
            });
        }
    }
}
