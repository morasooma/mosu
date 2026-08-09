// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class APIBeatmapMetadataSourceTest
    {
        [Test]
        public void TestMetadataSearchRejectsDifferentMapOfSameSong()
        {
            var local = createLocalBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            var differentMap = createOnlineBeatmap(length: 101000, totalObjects: 600, durationObjects: 143);

            Assert.That(APIBeatmapMetadataSource.matchBeatmapByStructure(local, new[] { differentMap }), Is.Null);
        }

        [Test]
        public void TestMetadataSearchFindsSameMapAfterMapperRename()
        {
            var local = createLocalBeatmap(length: 257432, totalObjects: 1487, durationObjects: 398);
            var renamedMapperMap = createOnlineBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);

            Assert.That(APIBeatmapMetadataSource.matchBeatmapByStructure(local, new[] { renamedMapperMap }), Is.SameAs(renamedMapperMap));
        }

        [Test]
        public void TestMetadataSearchRejectsAmbiguousStructuralMatches()
        {
            var local = createLocalBeatmap(length: 257432, totalObjects: 1487, durationObjects: 398);
            local.DifficultyName = "Local difficulty name";

            var first = createOnlineBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            first.DifficultyName = "First";
            var second = createOnlineBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            second.DifficultyName = "Second";

            Assert.That(APIBeatmapMetadataSource.matchBeatmapByStructure(local, new[] { first, second }), Is.Null);
        }

        [Test]
        public void TestOnlineIdLookupRejectsDifferentDifficultyChecksum()
        {
            var local = createLocalBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            local.MD5Hash = "7eb3bf3585bc3cb9a10887450c5dcaf8";

            var differentDifficulty = createOnlineBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            differentDifficulty.Checksum = "efbfa738e713166e06bc8bd52bdb97df";

            Assert.That(APIBeatmapMetadataSource.onlineIdLookupMatchesBeatmap(local, differentDifficulty), Is.False);
        }

        [Test]
        public void TestOnlineIdLookupAcceptsMatchingDifficultyChecksum()
        {
            var local = createLocalBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            local.MD5Hash = "7eb3bf3585bc3cb9a10887450c5dcaf8";

            var matchingDifficulty = createOnlineBeatmap(length: 257000, totalObjects: 1487, durationObjects: 398);
            matchingDifficulty.Checksum = "7EB3BF3585BC3CB9A10887450C5DCAF8";

            Assert.That(APIBeatmapMetadataSource.onlineIdLookupMatchesBeatmap(local, matchingDifficulty), Is.True);
        }

        private static BeatmapInfo createLocalBeatmap(double length, int totalObjects, int durationObjects) => new BeatmapInfo
        {
            DifficultyName = "Insane",
            Length = length,
            BPM = 192,
            TotalObjectCount = totalObjects,
            EndTimeObjectCount = durationObjects,
            Difficulty = new BeatmapDifficulty
            {
                DrainRate = 6,
                CircleSize = 4,
                ApproachRate = 9.7f,
                OverallDifficulty = 9.7f,
            },
        };

        private static APIBeatmap createOnlineBeatmap(double length, int totalObjects, int durationObjects) => new APIBeatmap
        {
            OnlineID = 1234,
            RulesetID = 0,
            DifficultyName = "Insane",
            Length = length,
            BPM = 192,
            CircleCount = totalObjects - durationObjects,
            SliderCount = durationObjects,
            DrainRate = 6,
            CircleSize = 4,
            ApproachRate = 9.7f,
            OverallDifficulty = 9.7f,
        };
    }
}
