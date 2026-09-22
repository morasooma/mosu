// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class PinnedSkinsStoreTest
    {
        [Test]
        public void TestPinStatePersists()
        {
            using var storage = new TemporaryNativeStorage($@"pinned-skins-{Guid.NewGuid():N}");
            Guid skinId = Guid.NewGuid();
            var store = new PinnedSkinsStore(storage);
            int changeCount = 0;

            store.Changed += () => changeCount++;

            Assert.Multiple(() =>
            {
                Assert.That(store.IsPinned(skinId), Is.False);
                Assert.That(store.SetPinned(skinId, true), Is.True);
                Assert.That(store.SetPinned(skinId, true), Is.False);
                Assert.That(store.IsPinned(skinId), Is.True);
                Assert.That(changeCount, Is.EqualTo(1));
            });

            var reloadedStore = new PinnedSkinsStore(storage);
            Assert.That(reloadedStore.IsPinned(skinId), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(reloadedStore.SetPinned(skinId, false), Is.True);
                Assert.That(reloadedStore.IsPinned(skinId), Is.False);
                Assert.That(new PinnedSkinsStore(storage).IsPinned(skinId), Is.False);
            });
        }
    }
}
