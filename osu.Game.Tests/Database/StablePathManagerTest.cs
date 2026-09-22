// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Framework.Extensions;
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
            string beatmapPath = Path.GetTempFileName();
            File.WriteAllText(beatmapPath, "original beatmap content");
            string checksum;
            using (var stream = File.OpenRead(beatmapPath))
                checksum = stream.ComputeMD5Hash();
            var set = new BeatmapSetInfo { OnlineID = 123 };
            var beatmap = new BeatmapInfo
            {
                OnlineID = 456,
                MD5Hash = checksum,
                BeatmapSet = set,
            };
            set.Beatmaps.Add(beatmap);

            StablePathManager.Replace(
                new Dictionary<Guid, string> { [beatmap.ID] = beatmapPath },
                new Dictionary<Guid, string>(),
                new[] { set });

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(StablePathManager.IsBeatmapSetAvailableLocally(123), Is.True);
                    Assert.That(StablePathManager.IsAvailableLocally(456), Is.True);
                    Assert.That(StablePathManager.IsAvailableLocally(456, checksum.ToUpperInvariant()), Is.True);
                    Assert.That(StablePathManager.IsAvailableLocally(456, "different-checksum"), Is.False);
                    Assert.That(StablePathManager.IsAvailableLocally(999), Is.False);
                    Assert.That(StablePathManager.GetBeatmapInfo(456, checksum)?.OnlineID, Is.EqualTo(456));
                    Assert.That(StablePathManager.GetBeatmapInfo(456, "different-checksum"), Is.Null);
                    Assert.That(StablePathManager.GetBeatmapInfo(999), Is.Null);
                });

                BeatmapInfo firstLookup = StablePathManager.GetBeatmapInfo(456, checksum)!;
                firstLookup.Status = BeatmapOnlineStatus.LocallyModified;

                Assert.That(StablePathManager.GetBeatmapInfo(456, checksum)?.Status, Is.Not.EqualTo(BeatmapOnlineStatus.LocallyModified),
                    "Stable snapshot metadata must not be mutable through a lookup result.");

                File.WriteAllText(beatmapPath, "changed beatmap content");

                Assert.That(StablePathManager.IsAvailableLocally(456, checksum), Is.False);
                Assert.That(StablePathManager.GetBeatmapInfo(456, checksum), Is.Null);

                File.Delete(beatmapPath);

                Assert.Multiple(() =>
                {
                    Assert.That(StablePathManager.IsBeatmapSetAvailableLocally(123), Is.False);
                    Assert.That(StablePathManager.IsAvailableLocally(456), Is.False);
                    Assert.That(StablePathManager.GetBeatmapInfo(456), Is.Null);
                });
            }
            finally
            {
                File.Delete(beatmapPath);
            }
        }
    }
}
