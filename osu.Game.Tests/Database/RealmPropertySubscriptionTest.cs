using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RealmPropertySubscriptionTest : RealmTest
    {
        [Test]
        public void TestRepeatedPropertySubscriptionsAndContextReset()
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = Resources.TestResources.CreateTestBeatmapSetInfo();
                realm.Write(r => r.Add(set));
                Guid id = set.Beatmaps[0].ID;
                Expression<Func<BeatmapUserSettings, double>> lookup = settings => settings.Offset;

                using (realm.SubscribeToPropertyChanged(r => r.Find<BeatmapInfo>(id)?.UserSettings, lookup, _ => { }))
                {
                }

                var values = new List<double>();
                using var subscription = realm.SubscribeToPropertyChanged(r => r.Find<BeatmapInfo>(id)?.UserSettings, lookup, values.Add);
                Assert.That(values, Is.EqualTo(new[] { 0d }));

                realm.Write(r => r.Find<BeatmapInfo>(id)!.UserSettings.Offset = 12.5);
                realm.Run(r => r.Refresh());
                Assert.That(values.Last(), Is.EqualTo(12.5));

                long allocated = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 200; i++)
                {
                    using (realm.SubscribeToPropertyChanged(r => r.Find<BeatmapInfo>(id)?.UserSettings, lookup, _ => { }))
                    {
                    }
                }
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                watch.Stop();
                TestContext.Progress.WriteLine($"REALM_PROPERTY_SUBSCRIPTION repeats=200 allocated_bytes={bytes} wall_ms={watch.Elapsed.TotalMilliseconds:F3}");
                // The unchanged Realm query/listener/registration paths still allocate.
                // Retained baseline was 1,344,520 bytes; leave headroom above the measured
                // ~0.53 MB final path while rejecting return to per-subscription compilation.
                Assert.That(bytes, Is.LessThan(800_000), "repeated subscriptions must reuse the property getter");

                for (int i = 0; i < 3; i++)
                {
                    using (realm.BlockAllOperations("property getter reuse"))
                    {
                    }
                    realm.Run(r => r.Refresh());
                    Assert.That(values.Last(), Is.EqualTo(12.5));
                }
                Assert.That(values, Has.Count.EqualTo(5), "initial, write, and three restored initial values");

                subscription.Dispose();
                realm.Write(r => r.Find<BeatmapInfo>(id)!.UserSettings.Offset = -8);
                realm.Run(r => r.Refresh());
                Assert.That(values, Has.Count.EqualTo(5));
                using (realm.BlockAllOperations("disposed property listener"))
                {
                }
                realm.Run(r => r.Refresh());
                Assert.That(values, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public void TestMissingModelDoesNotNotify()
        {
            RunTestWithRealm((realm, _) =>
            {
                int notifications = 0;
                using var subscription = realm.SubscribeToPropertyChanged<BeatmapInfo, double>(
                    r => r.Find<BeatmapInfo>(Guid.Empty), map => map.StarRating, _ => notifications++);
                realm.Run(r => r.Refresh());
                Assert.That(notifications, Is.Zero);
            });
        }

        [Test]
        public void TestAccessorReuseAndInheritedGetterFallback()
        {
            Expression<Func<BeatmapUserSettings, double>> offset = settings => settings.Offset;
            var member = ((MemberExpression)offset.Body).Member;
            var getter = RealmPropertyAccessor<BeatmapUserSettings, double>.Get(member, offset);
            Assert.That(RealmPropertyAccessor<BeatmapUserSettings, double>.Get(member, settings => settings.Offset), Is.SameAs(getter));
            Assert.That(getter(new BeatmapUserSettings { Offset = 3.25 }), Is.EqualTo(3.25));
            Assert.That(getter.Method.Name, Is.EqualTo("get_Offset"), "use the existing getter, not a dynamic expression method");

            Expression<Func<BeatmapInfo, bool>> inherited = map => map.IsManaged;
            member = ((MemberExpression)inherited.Body).Member;
            var inheritedGetter = RealmPropertyAccessor<BeatmapInfo, bool>.Get(member, inherited);
            Assert.That(inheritedGetter(new BeatmapInfo()), Is.False);
            Assert.That(RealmPropertyAccessor<BeatmapInfo, bool>.Get(member, inherited), Is.SameAs(inheritedGetter));
        }

        [Test]
        public void TestUnsupportedExpressionsStillFailOnRegistration()
        {
            RunTestWithRealm((realm, _) =>
            {
                Assert.Throws<ArgumentException>(() => realm.SubscribeToPropertyChanged<BeatmapInfo, double>(
                    _ => null, map => map.UserSettings.Offset, _ => { }));
                Assert.Throws<ArgumentException>(() => realm.SubscribeToPropertyChanged<BeatmapInfo, double>(
                    _ => null, map => map.StarRating + 1, _ => { }));
            });
        }

    }
}
