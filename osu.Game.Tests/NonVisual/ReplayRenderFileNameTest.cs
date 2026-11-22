using NUnit.Framework;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayRenderFileNameTest
    {
        [TestCase("Koisuru*Uchuu Sensou!!", "Koisuru_Uchuu Sensou!!")]
        [TestCase("<>:\"/\\|?*", "_________")]
        [TestCase("a\0\n\rb", "a___b")]
        [TestCase("日本語 — Привет!", "日本語 — Привет!")]
        public void TestPortableTitle(string title, string expected)
        {
            Assert.That(ReplayRenderFileName.SanitizeTitle(title), Is.EqualTo(expected));
        }
    }
}
