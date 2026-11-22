using System.Globalization;
using Humanizer;
using NUnit.Framework;
using osu.Game.Overlays.Profile.Header;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ProfileContributionFormattingTest
    {
        [TestCase("en-US")]
        [TestCase("ru-RU")]
        [TestCase("de-DE")]
        public void TestMatchesOriginalQuantityFormatting(string culture)
        {
            using var scope = new CultureScope(culture);
            foreach (int count in new[] { int.MinValue, -2, -1, 0, 1, 2, 1000, int.MaxValue })
            {
                Assert.That(BottomHeaderContainer.FormatContributionCount(count, "forum post", "forum posts"),
                    Is.EqualTo("forum post".ToQuantity(count, "#,##0")));
                Assert.That(BottomHeaderContainer.FormatContributionCount(count, "comment", "comments"),
                    Is.EqualTo("comment".ToQuantity(count, "#,##0")));
            }
        }

        private sealed class CultureScope : System.IDisposable
        {
            private readonly CultureInfo previous = CultureInfo.CurrentCulture;
            public CultureScope(string culture) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            public void Dispose() => CultureInfo.CurrentCulture = previous;
        }
    }
}
