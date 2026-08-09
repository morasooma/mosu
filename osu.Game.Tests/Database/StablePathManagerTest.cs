// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class StablePathManagerTest
    {
        [TearDown]
        public void TearDown() => StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        [Test]
        public void TestAvailabilityRequiresMatchingChecksumWhenProvided()
        {
            var set = new BeatmapSetInfo { OnlineID = 123 };
            set.Beatmaps.Add(new BeatmapInfo
            {
                OnlineID = 456,
                MD5Hash = "correct-checksum",
                BeatmapSet = set,
            });

            StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), new[] { set });

            Assert.Multiple(() =>
            {
                Assert.That(StablePathManager.IsBeatmapSetAvailableLocally(123), Is.True);
                Assert.That(StablePathManager.IsAvailableLocally(456), Is.True);
                Assert.That(StablePathManager.IsAvailableLocally(456, "CORRECT-CHECKSUM"), Is.True);
                Assert.That(StablePathManager.IsAvailableLocally(456, "different-checksum"), Is.False);
                Assert.That(StablePathManager.IsAvailableLocally(999), Is.False);
            });
        }
    }
}
