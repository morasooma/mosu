// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// Verifies that relax SR/PP cache entries are stored separately per Relax PP system,
    /// so switching systems never discards the other system's calculated data.
    /// </summary>
    [TestFixture]
    public class ForkDataStoreRelaxPpSystemTest
    {
        private TemporaryNativeStorage storage = null!;
        private ForkDataStore store = null!;
        private ForkRelaxPpSystem originalSystem;

        [SetUp]
        public void SetUp()
        {
            originalSystem = RelaxPpSystemSelection.Current;
            storage = new TemporaryNativeStorage($"fork-relax-pp-system-{Guid.NewGuid():N}");
            store = new ForkDataStore(storage);
        }

        [TearDown]
        public void TearDown()
        {
            RelaxPpSystemSelection.Current = originalSystem;
            store.Dispose();
            storage.Dispose();
        }

        [Test]
        public void RelaxDataIsStoredPerSystem()
        {
            Guid beatmapId = Guid.NewGuid();

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            store.SetRelaxData(beatmapId, 5.5, 400);

            Assert.Multiple(() =>
            {
                Assert.That(store.HasRelaxData(beatmapId), Is.True);
                Assert.That(store.GetRelaxPP(beatmapId), Is.EqualTo(400));
                Assert.That(store.GetRelaxStarRating(beatmapId), Is.EqualTo(5.5));
            });

            // The vanilla cache is still empty and must not expose the Mosu data.
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;

            Assert.Multiple(() =>
            {
                Assert.That(store.HasRelaxData(beatmapId), Is.False);
                Assert.That(store.GetRelaxPP(beatmapId), Is.EqualTo(0));
            });

            store.SetRelaxData(beatmapId, 4.5, 250);

            Assert.Multiple(() =>
            {
                Assert.That(store.HasRelaxData(beatmapId), Is.True);
                Assert.That(store.GetRelaxPP(beatmapId), Is.EqualTo(250));
            });

            // Switching back must not have lost the Mosu data.
            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;

            Assert.Multiple(() =>
            {
                Assert.That(store.HasRelaxData(beatmapId), Is.True);
                Assert.That(store.GetRelaxPP(beatmapId), Is.EqualTo(400));
                Assert.That(store.GetRelaxStarRating(beatmapId), Is.EqualTo(5.5));
            });
        }

        [Test]
        public void PerSystemDataSurvivesReload()
        {
            Guid beatmapId = Guid.NewGuid();

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;
            store.SetRelaxData(beatmapId, 5.5, 400);

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;
            store.SetRelaxData(beatmapId, 4.5, 250);

            store.Dispose();
            store = new ForkDataStore(storage);

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuRealistik;

            Assert.Multiple(() =>
            {
                Assert.That(store.HasRelaxData(beatmapId), Is.True);
                Assert.That(store.GetRelaxPP(beatmapId), Is.EqualTo(400));
            });

            RelaxPpSystemSelection.Current = ForkRelaxPpSystem.LazerVanilla;

            Assert.Multiple(() =>
            {
                Assert.That(store.HasRelaxData(beatmapId), Is.True);
                Assert.That(store.GetRelaxPP(beatmapId), Is.EqualTo(250));
                Assert.That(store.GetRelaxStarRating(beatmapId), Is.EqualTo(4.5));
            });
        }
    }
}
