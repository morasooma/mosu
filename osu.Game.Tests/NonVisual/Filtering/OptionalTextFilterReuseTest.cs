using NUnit.Framework;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual.Filtering
{
    [TestFixture]
    public class OptionalTextFilterReuseTest
    {
        [Test]
        public void ChangingQueryDoesNotRetainPreviousMatcher()
        {
            var filter = new FilterCriteria.OptionalTextFilter { SearchTerm = "\"alpha\"" };
            Assert.That(filter.Matches("ALPHA beta"), Is.True);
            Assert.That(filter.Matches("alphabet"), Is.False);
            filter.SearchTerm = "\"beta\"";
            Assert.That(filter.Matches("alpha"), Is.False);
            Assert.That(filter.Matches("alpha beta"), Is.True);
            filter.SearchTerm = "alpha";
            Assert.That(filter.Matches("alphabet"), Is.True);
            filter.SearchTerm = "\"alpha\"!";
            Assert.That(filter.Matches("alpha beta"), Is.False);
            Assert.That(filter.Matches("ALPHA"), Is.True);
        }

        [TestCase("\"a+b\"", "a+b", true)]
        [TestCase("\"a+b\"", "aaab", false)]
        [TestCase("\"猫\"", "猫", true)]
        [TestCase("\"тест\"", "это ТЕСТ!", true)]
        [TestCase("\"\"", "anything", true)]
        public void PhraseSemanticsArePreserved(string query, string text, bool expected)
        {
            var filter = new FilterCriteria.OptionalTextFilter { SearchTerm = query };
            Assert.That(filter.Matches(text), Is.EqualTo(expected));
        }

        [Test]
        public void CopiedFilterCanBeChangedIndependently()
        {
            var original = new FilterCriteria.OptionalTextFilter { SearchTerm = "\"alpha\"" };
            var copy = original;
            copy.SearchTerm = "\"beta\"";
            Assert.That(original.Matches("alpha"), Is.True);
            Assert.That(copy.Matches("alpha"), Is.False);
            copy.ExcludeTerm = true;
            Assert.That(copy.Matches("beta"), Is.False);
            Assert.That(copy.Matches("alpha"), Is.True);
        }
    }
}
