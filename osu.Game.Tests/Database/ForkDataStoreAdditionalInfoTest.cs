// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class ForkDataStoreAdditionalInfoTest
    {
        [Test]
        public void TestModKeyIncludesSettings()
        {
            string standard = ForkDataStore.CreateModsKey(new[] { new OsuModDoubleTime() });
            string customRate = ForkDataStore.CreateModsKey(new[] { new OsuModDoubleTime { SpeedChange = { Value = 1.25 } } });

            Assert.That(standard, Is.Not.EqualTo("nomod"));
            Assert.That(customRate, Is.Not.EqualTo(standard));
            Assert.That(ForkDataStore.CreateModsKey(Array.Empty<OsuModDoubleTime>()), Is.EqualTo("nomod"));
        }

        [Test]
        public void TestSeasonalBackgroundPersists()
        {
            using var storage = new TemporaryNativeStorage($"fork-seasonal-background-{Guid.NewGuid():N}");
            byte[] data = [1, 2, 3, 4];
            string hash = Convert.ToHexStringLower(SHA256.HashData(data));

            using (var store = new ForkDataStore(storage))
            {
                store.SetSeasonalBackground(hash, data);
                Assert.That(store.GetSeasonalBackground(hash), Is.EqualTo(data));
                Assert.That(store.GetSeasonalBackground(new string('0', 64)), Is.Null);
            }

            using (var reloaded = new ForkDataStore(storage))
                Assert.That(reloaded.GetSeasonalBackground(hash), Is.EqualTo(data));
        }

        [Test]
        public void TestAdditionalInfoPersistsAndRejectsStaleEntries()
        {
            using var storage = new TemporaryNativeStorage($"fork-additional-info-{Guid.NewGuid():N}");
            Guid id = Guid.NewGuid();
            var standard = new ForkDataStore.AdditionalInfoData("checksum-a", 42, 5.2, 123, "Combo: 123x | PP: 321 pp (Aim: 200pp)");
            var dodge = new ForkDataStore.AdditionalInfoData("checksum-a", 4, 6.0, 456, "Combo: 456x | PP: 99 pp");

            using (var store = new ForkDataStore(storage))
            {
                store.SetAdditionalInfo(id, "osu", "nomod", standard);
                store.SetAdditionalInfo(id, "dodge", "nomod", dodge);
                Assert.That(store.TryGetAdditionalInfo(id, "osu", "nomod", "checksum-a", 42, out var loaded), Is.True);
                Assert.That(loaded, Is.EqualTo(standard));
            }

            using (var store = new ForkDataStore(storage))
            {
                Assert.That(store.TryGetAdditionalInfo(id, "osu", "nomod", "checksum-a", 42, out var loaded), Is.True);
                Assert.That(loaded, Is.EqualTo(standard));
                Assert.That(store.TryGetAdditionalInfo(id, "dodge", "nomod", "checksum-a", 4, out loaded), Is.True);
                Assert.That(loaded, Is.EqualTo(dodge));
                Assert.That(store.TryGetAdditionalInfo(id, "osu", "DT", "checksum-a", 42, out _), Is.False);
                Assert.That(store.TryGetAdditionalInfo(id, "osu", "nomod", "checksum-b", 42, out _), Is.False);
                Assert.That(store.TryGetAdditionalInfo(id, "osu", "nomod", "checksum-a", 43, out _), Is.False);
                Assert.That(store.TryGetAdditionalInfo(id, "taiko", "nomod", "checksum-a", 42, out _), Is.False);
            }
        }
    }
}
