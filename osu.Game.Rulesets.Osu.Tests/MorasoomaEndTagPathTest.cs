using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class MorasoomaEndTagPathTest
    {
        [Test]
        public void TestDefaultKeepsOriginalGeometry()
        {
            var path = new MorasoomaEndTagPath("MORASOOMA");
            Assert.That(path.StrokeCount, Is.EqualTo(9));
            Assert.That(path.GetPosition(0, 0), Is.EqualTo(new Vector2(39, 235)));
            Assert.That(path.GetPosition(0, 1), Is.EqualTo(new Vector2(81, 235)));
            Assert.That(path.GetPosition(8, 0), Is.EqualTo(new Vector2(431, 235)));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("日本語😀")]
        public void TestEmptyOrUnsupportedTextFallsBack(string text)
            => assertSamePath(new MorasoomaEndTagPath(text), new MorasoomaEndTagPath("MORASOOMA"));

        [Test]
        public void TestCaseAndUnsupportedCharacters()
            => assertSamePath(new MorasoomaEndTagPath("  a😀bc  "), new MorasoomaEndTagPath("ABC"));

        [Test]
        public void TestCustomTextChangesPath()
        {
            var path = new MorasoomaEndTagPath("HI");
            Assert.That(path.StrokeCount, Is.EqualTo(6));
            Assert.That(path.GetPosition(0, 0), Is.Not.EqualTo(new MorasoomaEndTagPath("MORASOOMA").GetPosition(0, 0)));
        }

        [Test]
        public void TestLengthIsLimitedEvenWhenLoadedFromConfig()
            => assertSamePath(new MorasoomaEndTagPath(new string('W', 10000)), new MorasoomaEndTagPath(new string('W', MorasoomaEndTagPath.MAX_LENGTH)));

        [Test]
        public void TestSpacesSeparateLettersWithoutSmoke()
        {
            var path = new MorasoomaEndTagPath("M M");
            Assert.That(path.StrokeCount, Is.EqualTo(2));
            Assert.That(path.GetPosition(1, 0).X - path.GetPosition(0, 0).X, Is.EqualTo(98));
        }

        [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
        [TestCase("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ")]
        [TestCase("0123456789-_.!?@#+=()[]/\\:;*'\"<>,")]
        public void TestSupportedGlyphsAndLayoutBounds(string alphabet)
        {
            foreach (char c in alphabet)
            {
                var glyph = new MorasoomaEndTagPath(c.ToString());
                Assert.That(glyph.StrokeCount, Is.InRange(1, 6), c.ToString());
                Assert.That(glyph.StrokeCount, Is.Not.EqualTo(9), $"{c} must not fall back");
            }
            var path = new MorasoomaEndTagPath(new string(alphabet.First(), 16));
            for (int stroke = 0; stroke < path.StrokeCount; stroke++)
            {
                for (int sample = 0; sample <= 100; sample++)
                {
                    Vector2 point = path.GetPosition(stroke, sample / 100f);
                    Assert.That(float.IsFinite(point.X) && float.IsFinite(point.Y), Is.True);
                    Assert.That(point.X, Is.InRange(20f - .001f, OsuPlayfield.BASE_SIZE.X - 20f + .001f));
                    Assert.That(point.Y, Is.InRange(149f, 235f));
                }
            }
        }

        private static void assertSamePath(MorasoomaEndTagPath actual, MorasoomaEndTagPath expected)
        {
            Assert.That(actual.StrokeCount, Is.EqualTo(expected.StrokeCount));
            for (int stroke = 0; stroke < actual.StrokeCount; stroke++)
            {
                for (int sample = 0; sample <= 10; sample++)
                    Assert.That(actual.GetPosition(stroke, sample / 10f), Is.EqualTo(expected.GetPosition(stroke, sample / 10f)));
            }
        }
    }
}
