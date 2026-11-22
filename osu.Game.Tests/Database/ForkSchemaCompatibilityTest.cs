// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class ForkSchemaCompatibilityTest
    {
        [Test]
        public void TestDodgeDifficultyPersistsWithCalculatorVersion()
        {
            using var storage = new TemporaryNativeStorage($"fork-dodge-difficulty-{Guid.NewGuid():N}");
            Guid beatmapId = Guid.NewGuid();

            using (var forkDataStore = new ForkDataStore(storage))
            {
                forkDataStore.SetDodgeDifficulty(beatmapId, 6.42, 123);
                Assert.That(forkDataStore.HasDodgeDifficulty(beatmapId, 123), Is.True);
                Assert.That(forkDataStore.HasDodgeDifficulty(beatmapId, 124), Is.False);

                var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = RulesetInfo.DODGE_MODE_SHORTNAME })
                {
                    ID = beatmapId,
                    StarRating = -1,
                };

                Assert.That(BeatmapDifficultyCache.GetInitialStarRating(beatmap), Is.EqualTo(6.42));
            }

            using (var reloaded = new ForkDataStore(storage))
            {
                Assert.That(reloaded.GetDodgeDifficulty(beatmapId).StarRating, Is.EqualTo(6.42));
                Assert.That(reloaded.GetDodgeDifficulty(beatmapId).DifficultyVersion, Is.EqualTo(123));
            }
        }

        [Test]
        public void TestFullDodgeDifficultyAttributesPersistWithChecksum()
        {
            using var storage = new TemporaryNativeStorage($"fork-dodge-attributes-{Guid.NewGuid():N}");
            Guid beatmapId = Guid.NewGuid();
            const string checksum = "0123456789abcdef0123456789abcdef";
            var attributes = new DifficultyAttributes
            {
                StarRating = 4.26,
                MaxCombo = 518,
            };

            using (var forkDataStore = new ForkDataStore(storage))
            {
                forkDataStore.SetDodgeDifficulty(beatmapId, checksum, attributes, 456);

                Assert.Multiple(() =>
                {
                    Assert.That(forkDataStore.HasDodgeDifficultyAttributes(beatmapId, 456, checksum), Is.True);
                    Assert.That(forkDataStore.HasDodgeDifficultyAttributes(beatmapId, 455, checksum), Is.False);
                    Assert.That(forkDataStore.HasDodgeDifficultyAttributes(beatmapId, 456, "different"), Is.False);
                    Assert.That(forkDataStore.GetDodgeDifficulty(beatmapId).AttributesJson, Does.Contain("\"max_combo\":518"));
                });
            }

            using (var reloaded = new ForkDataStore(storage))
            {
                ForkDataStore.DodgeDifficultyData persisted = reloaded.GetDodgeDifficulty(beatmapId);

                Assert.Multiple(() =>
                {
                    Assert.That(reloaded.HasDodgeDifficultyAttributes(beatmapId, 456, checksum), Is.True);
                    Assert.That(persisted.StarRating, Is.EqualTo(4.26));
                    Assert.That(persisted.BeatmapChecksum, Is.EqualTo(checksum));
                    Assert.That(persisted.HasFullAttributes, Is.True);
                });
            }
        }

    }
}
