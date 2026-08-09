// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class RulesetBeatmapSetOriginTest
    {
        [Test]
        public void TestMarkerMatchesLocalSource()
        {
            Guid sourceID = Guid.NewGuid();
            string filename = RulesetBeatmapSetOrigin.GetFilename(sourceID, -1, DodgeRuleset.ONLINE_ID);

            Assert.That(RulesetBeatmapSetOrigin.MatchesSource(filename, sourceID, -1, DodgeRuleset.ONLINE_ID), Is.True);
            Assert.That(RulesetBeatmapSetOrigin.MatchesSource(filename, Guid.NewGuid(), -1, DodgeRuleset.ONLINE_ID), Is.False);
        }

        [Test]
        public void TestMarkerMatchesReimportedOnlineSource()
        {
            Guid originalLocalID = Guid.NewGuid();
            string filename = RulesetBeatmapSetOrigin.GetFilename(originalLocalID, 1234, DodgeRuleset.ONLINE_ID);

            Assert.That(RulesetBeatmapSetOrigin.MatchesSource(filename, Guid.NewGuid(), 1234, DodgeRuleset.ONLINE_ID), Is.True);
            Assert.That(RulesetBeatmapSetOrigin.MatchesSource(filename, Guid.NewGuid(), 4321, DodgeRuleset.ONLINE_ID), Is.False);
        }

        [TestCase("audio.mp3", true)]
        [TestCase("background.jpg", true)]
        [TestCase("video.mp4", true)]
        [TestCase("storyboard.osb", true)]
        [TestCase("difficulty.osu", false)]
        [TestCase("difficulty.ruleset.json", false)]
        public void TestOnlyResourcesAreTransferred(string filename, bool expected)
        {
            Assert.That(RulesetBeatmapSetOrigin.IsTransferableResourceFilename(filename), Is.EqualTo(expected));
        }

        [Test]
        public void TestProvenanceRoundTrip()
        {
            var origin = new RulesetBeatmapSetOrigin
            {
                TargetRulesetOnlineID = DodgeRuleset.ONLINE_ID,
                SourceBeatmapSetLocalID = Guid.NewGuid(),
                SourceBeatmapSetOnlineID = 123,
                SourceBeatmapOnlineID = 456,
                SourceRulesetOnlineID = 0,
                OriginalAuthorOnlineID = 789,
                OriginalAuthorUsername = "mapper",
            };

            var result = JsonConvert.DeserializeObject<RulesetBeatmapSetOrigin>(JsonConvert.SerializeObject(origin))!;

            Assert.Multiple(() =>
            {
                Assert.That(result.Format, Is.EqualTo(RulesetBeatmapSetOrigin.FORMAT));
                Assert.That(result.Version, Is.EqualTo(RulesetBeatmapSetOrigin.VERSION));
                Assert.That(result.TargetRulesetOnlineID, Is.EqualTo(DodgeRuleset.ONLINE_ID));
                Assert.That(result.SourceBeatmapSetLocalID, Is.EqualTo(origin.SourceBeatmapSetLocalID));
                Assert.That(result.SourceBeatmapSetOnlineID, Is.EqualTo(123));
                Assert.That(result.SourceBeatmapOnlineID, Is.EqualTo(456));
                Assert.That(result.OriginalAuthorOnlineID, Is.EqualTo(789));
                Assert.That(result.OriginalAuthorUsername, Is.EqualTo("mapper"));
            });
        }
    }
}
