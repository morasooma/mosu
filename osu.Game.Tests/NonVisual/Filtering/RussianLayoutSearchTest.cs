using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual.Filtering
{
    [TestFixture]
    public class RussianLayoutSearchTest
    {
        [TestCase("фшь ыдщз", "aim slop")]
        [TestCase("ФШЬ ЫДЩЗ", "AIM SLOP")]
        [TestCase("aim ыдщз 123", "aim slop 123")]
        [TestCase("ёхъжэбю", "`[];',.")]
        [TestCase("ЁХЪЖЭБЮ", "~{}:\"<>")]
        public void TestConversion(string query, string expected)
        {
            Assert.That(RussianKeyboardLayoutSearch.Convert(query), Is.EqualTo(expected));
        }

        [Test]
        public void TestLatinNeedsNoAlternative()
        {
            Assert.That(RussianKeyboardLayoutSearch.Convert("aim slop 123"), Is.Null);
        }

        [TestCase("фшь", "aim slop", true)]
        [TestCase("фшь", "фшь ыдщз", true)]
        [TestCase("песня", "русская песня", true)]
        [TestCase("\"фшь ыдщз\"", "an aim slop map", true)]
        [TestCase("\"фшь\"", "aimless", false)]
        [TestCase("\"фшь ыдщз\"!", "aim slop", true)]
        [TestCase("\"фшь ыдщз\"!", "an aim slop map", false)]
        [TestCase("фшь", "nothing", false)]
        public void TestMatches(string query, string value, bool expected)
        {
            var filter = new FilterCriteria.OptionalTextFilter { SearchTerm = query, UseRussianLayout = true };
            Assert.That(filter.Matches(value), Is.EqualTo(expected));
            filter.ExcludeTerm = true;
            Assert.That(filter.Matches(value), Is.EqualTo(!expected));
        }

        [Test]
        public void TestToggleAndReuse()
        {
            var filter = new FilterCriteria.OptionalTextFilter { SearchTerm = "\"фшь\"" };
            Assert.That(filter.Matches("aim"), Is.False);
            filter.UseRussianLayout = true;
            Assert.That(filter.Matches("aim"), Is.True);
            var copy = filter;
            filter.SearchTerm = "\"ыдщз\"!";
            Assert.That(filter.Matches("slop"), Is.True);
            Assert.That(filter.Matches("aim"), Is.False);
            Assert.That(copy.Matches("aim"), Is.True);
            filter.UseRussianLayout = false;
            Assert.That(filter.Matches("slop"), Is.False);
            Assert.That(filter.Matches("ыдщз"), Is.True);
        }

        [Test]
        public void TestSettingDefaultAndPersistence()
        {
            using var storage = new TemporaryNativeStorage($"layout-search-{Guid.NewGuid():N}");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<bool>(OsuSetting.ForkRussianLayoutSearch), Is.True);
                config.SetValue(OsuSetting.ForkRussianLayoutSearch, false);
                config.Save();
            }

            using var reopened = new OsuConfigManager(storage);
            Assert.That(reopened.Get<bool>(OsuSetting.ForkRussianLayoutSearch), Is.False);
        }

        [Test]
        public void TestParsedCriteria()
        {
            var criteria = new FilterCriteria();
            const string query = "фшь ыдщз artist=фшь title=ыдщз creator=фшь source=ыдщз diff=фшь tag=ыдщз star>4";
            FilterQueryParser.ApplyQueries(criteria, query);
            criteria.EnableRussianLayoutSearch();

            Assert.That(criteria.SearchTerms.All(t => t.UseRussianLayout), Is.True);
            Assert.That(criteria.SearchTerms[0].Matches("aim"), Is.True);
            Assert.That(criteria.SearchTerms[1].Matches("slop"), Is.True);
            Assert.That(criteria.Artist.Matches("aim"), Is.True);
            Assert.That(criteria.Title.Matches("slop"), Is.True);
            Assert.That(criteria.Creator.Matches("aim"), Is.True);
            Assert.That(criteria.Source.Matches("slop"), Is.True);
            Assert.That(criteria.DifficultyName.Matches("aim"), Is.True);
            Assert.That(criteria.UserTags.Single().Matches("slop"), Is.True);
            Assert.That(criteria.StarDifficulty.Min, Is.EqualTo(4));
            Assert.That(criteria.SearchText, Does.Contain("фшь ыдщз"));
        }
    }
}
