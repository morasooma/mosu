// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers what a story gate does in every state a player's progress can be in, including the states
    /// that only happen because a story was rewritten under them.
    /// </summary>
    [TestFixture]
    public class StoryConditionTest
    {
        private static readonly Dictionary<string, long> nothing = new Dictionary<string, long>();

        private static Dictionary<string, long> flags(string flag, long value) =>
            new Dictionary<string, long> { [flag] = value };

        [Test]
        public void NoConditionAlwaysMatches()
        {
            Assert.That(StoryCondition.NONE.Matches(nothing), Is.True);
            Assert.That(StoryCondition.Read(null, 5, 9).Matches(nothing), Is.True,
                "bounds without a flag should not gate anything");
            Assert.That(StoryCondition.Read("  ", null, null).Matches(nothing), Is.True,
                "a blank flag name is not a gate");
        }

        /// <summary>
        /// Naming a flag and nothing else has to mean the obvious thing, or every gate in the world needs
        /// two fields filled in to work.
        /// </summary>
        [Test]
        public void NamingAFlagAloneMeansOnceItHasHappened()
        {
            StoryCondition condition = StoryCondition.Read("mora.met", null, null);

            Assert.That(condition.Matches(nothing), Is.False);
            Assert.That(condition.Matches(flags("mora.met", 1)), Is.True);
            Assert.That(condition.Matches(flags("mora.met", 7)), Is.True);
        }

        /// <summary>
        /// The whole reason a story can be rewritten: a flag nobody has ever set is zero, not an error.
        /// </summary>
        [Test]
        public void AnUnknownFlagReadsAsZero()
        {
            Assert.That(StoryCondition.Read("chapter.two", null, null).Matches(flags("chapter.one", 3)), Is.False);
            Assert.That(StoryCondition.Read("chapter.two", 0, null).Matches(nothing), Is.True,
                "a floor of zero is met by a player who has never touched the story");
        }

        /// <summary>
        /// An upper bound is what makes something belong to one chapter: present, then gone.
        /// </summary>
        [Test]
        public void AnUpperBoundHidesSomethingAgain()
        {
            StoryCondition chapterOneOnly = StoryCondition.Read("chapter", 1, 2);

            Assert.That(chapterOneOnly.Matches(nothing), Is.False);
            Assert.That(chapterOneOnly.Matches(flags("chapter", 1)), Is.True);
            Assert.That(chapterOneOnly.Matches(flags("chapter", 2)), Is.False);
            Assert.That(chapterOneOnly.Matches(flags("chapter", 99)), Is.False);
        }

        /// <summary>
        /// A range an author has inverted by mistake must hide the object, not crash and not show it
        /// always: a mistake should be visible as "my door never appears", which is findable.
        /// </summary>
        [Test]
        public void AnImpossibleRangeSimplyNeverMatches()
        {
            StoryCondition impossible = StoryCondition.Read("chapter", 5, 2);

            Assert.That(impossible.Matches(flags("chapter", 1)), Is.False);
            Assert.That(impossible.Matches(flags("chapter", 3)), Is.False);
            Assert.That(impossible.Matches(flags("chapter", 6)), Is.False);
        }

        /// <summary>
        /// Progress beyond anything the author wrote — an old save against a shortened story — keeps
        /// matching rather than falling through a hole.
        /// </summary>
        [Test]
        public void ProgressBeyondTheStoryStillMatchesAnOpenEndedGate()
        {
            Assert.That(StoryCondition.Read("chapter", 3, null).Matches(flags("chapter", 1_000_000)), Is.True);
        }
    }
}
