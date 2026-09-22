// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ScoreInfoTest
    {
        [Test]
        public void TestDeepClone()
        {
            var score = new ScoreInfo();

            score.Statistics.Add(HitResult.Good, 10);
            score.Rank = ScoreRank.B;

            var scoreCopy = score.DeepClone();

            score.Statistics[HitResult.Good]++;
            score.Rank = ScoreRank.X;
            score.RealmUser.Username = "test";

            Assert.That(scoreCopy.Statistics[HitResult.Good], Is.EqualTo(10));
            Assert.That(score.Statistics[HitResult.Good], Is.EqualTo(11));

            Assert.That(scoreCopy.Rank, Is.EqualTo(ScoreRank.B));
            Assert.That(score.Rank, Is.EqualTo(ScoreRank.X));

            Assert.That(scoreCopy.RealmUser.Username, Is.Empty);
            Assert.That(score.RealmUser.Username, Is.EqualTo("test"));
        }

        [Test]
        public void TestModsInitiallyEmpty()
        {
            var score = new ScoreInfo();

            Assert.That(score.Mods, Is.Empty);
            Assert.That(score.APIMods, Is.Empty);
            Assert.That(score.ModsJson, Is.Empty);
        }

        [Test]
        public void TestInvalidTagCoopReplayMetadataIsIgnored()
        {
            var score = new ScoreInfo { TagCoopReplayJson = "{invalid" };

            Assert.That(score.TagCoopReplay, Is.Null);
            Assert.That(score.TagCoopReplay, Is.Null);
        }

        [Test]
        public void TestTagCoopReplayFramesAddedAfterInitialisationArePersistedByClone()
        {
            var score = new ScoreInfo
            {
                TagCoopReplay = new TagCoopReplayMetadata
                {
                    Players =
                    [
                        new TagCoopReplayPlayer { UserID = 1, Username = "first" },
                        new TagCoopReplayPlayer { UserID = 2, Username = "second" },
                    ]
                }
            };

            score.TagCoopReplay.Frames.Add(new TagCoopReplayFrame
            {
                UserID = 2,
                Sequence = 5,
                GameplayTime = 1234,
                X = 0.25f,
                Y = 0.75f,
            });

            ScoreInfo clone = score.DeepClone();
            var restored = new ScoreInfo { TagCoopReplayJson = clone.TagCoopReplayJson };

            Assert.That(restored.TagCoopReplay?.Players.Select(player => player.Username), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(restored.TagCoopReplay?.Frames, Has.Count.EqualTo(1));
            Assert.That(restored.TagCoopReplay?.Frames[0].UserID, Is.EqualTo(2));
        }

        [Test]
        public void TestModsUpdatedCorrectly()
        {
            var score = new ScoreInfo
            {
                Mods = new Mod[] { new ManiaModClassic() },
                Ruleset = new ManiaRuleset().RulesetInfo,
            };

            Assert.That(score.Mods, Contains.Item(new ManiaModClassic()));
            Assert.That(score.APIMods, Contains.Item(new APIMod(new ManiaModClassic())));
            Assert.That(score.ModsJson, Contains.Substring("CL"));

            score.APIMods = new[] { new APIMod(new ManiaModDoubleTime()) };

            Assert.That(score.Mods, Contains.Item(new ManiaModDoubleTime()));
            Assert.That(score.APIMods, Contains.Item(new APIMod(new ManiaModDoubleTime())));
            Assert.That(score.ModsJson, Contains.Substring("DT"));

            score.Mods = new Mod[] { new ManiaModClassic() };

            Assert.That(score.Mods, Contains.Item(new ManiaModClassic()));
            Assert.That(score.APIMods, Contains.Item(new APIMod(new ManiaModClassic())));
            Assert.That(score.ModsJson, Contains.Substring("CL"));
        }
    }
}
