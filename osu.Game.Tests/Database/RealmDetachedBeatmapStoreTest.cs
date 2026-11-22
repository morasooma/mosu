using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RealmDetachedBeatmapStoreTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestRepeatedDisposal(bool hasStableReload)
        {
            var store = new RealmDetachedBeatmapStore();
            var subscription = new CountingSubscription();
            setField(store, "realmSubscription", subscription);

            CancellationTokenSource? cancellation = hasStableReload ? new CancellationTokenSource() : null;
            CancellationToken token = cancellation?.Token ?? CancellationToken.None;
            setField(store, "stableReloadCancellation", cancellation);

            Assert.DoesNotThrow(store.Dispose);
            Assert.That(subscription.DisposeCount, Is.EqualTo(1));
            Assert.That(token.IsCancellationRequested, Is.EqualTo(hasStableReload));
            Assert.DoesNotThrow(store.Dispose, "shutdown may dispose the store more than once");
            Assert.That(subscription.DisposeCount, Is.EqualTo(1), "subscriptions must only be released once");
        }

        private static void setField(RealmDetachedBeatmapStore store, string name, object? value)
            => typeof(RealmDetachedBeatmapStore).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(store, value);

        private sealed class CountingSubscription : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose() => DisposeCount++;
        }
    }
}
