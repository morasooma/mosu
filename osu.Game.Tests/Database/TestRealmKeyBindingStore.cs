// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public partial class TestRealmKeyBindingStore : RealmTest
    {
        [Test]
        public void TestDefaultsPopulationAndQuery()
        {
            RunTestWithRealm((realm, _) =>
            {
                Assert.That(queryCount(realm), Is.EqualTo(0));

                KeyBindingContainer testContainer = new TestKeyBindingContainer();

                var keyBindingStore = new RealmKeyBindingStore(realm, new ReadableKeyCombinationProvider());
                keyBindingStore.Register(testContainer, Enumerable.Empty<RulesetInfo>());

                Assert.That(queryCount(realm), Is.EqualTo(3));

                Assert.That(queryCount(realm, GlobalAction.Back), Is.EqualTo(1));
                Assert.That(queryCount(realm, GlobalAction.Select), Is.EqualTo(2));
            });
        }

        [Test]
        public void TestDefaultsPopulationRemovesExcess()
        {
            RunTestWithRealm((realm, _) =>
            {
                Assert.That(queryCount(realm), Is.EqualTo(0));

                KeyBindingContainer testContainer = new TestKeyBindingContainer();

                // Add some excess bindings for an action which only supports 1.
                realm.Write(r =>
                {
                    r.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.A)));
                    r.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.S)));
                    r.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.D)));
                });

                Assert.That(queryCount(realm, GlobalAction.Back), Is.EqualTo(3));

                var keyBindingStore = new RealmKeyBindingStore(realm, new ReadableKeyCombinationProvider());
                keyBindingStore.Register(testContainer, Enumerable.Empty<RulesetInfo>());

                Assert.That(queryCount(realm, GlobalAction.Back), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestMappingOrderPreservesDefaultActionPriority()
        {
            var defaults = new IKeyBinding[]
            {
                new KeyBinding(InputKey.A, GlobalAction.Select),
                new KeyBinding(InputKey.B, GlobalAction.Select),
                new KeyBinding(InputKey.C, GlobalAction.Back),
            };
            var bindings = new[]
            {
                new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.C)),
                new RealmKeyBinding((int)GlobalAction.Back + 10000, new KeyCombination(InputKey.D)),
                new RealmKeyBinding(GlobalAction.Select, new KeyCombination(InputKey.A)),
                new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.E)),
                new RealmKeyBinding(GlobalAction.Select, new KeyCombination(InputKey.B)),
            };

            var ordered = DatabasedKeyBindingContainer<GlobalAction>.OrderByDefaultActionPriority(bindings, defaults);

            // Unknown actions came first with FindIndex (-1), and ties kept the database's order.
            Assert.That(ordered, Is.EqualTo(new[] { bindings[1], bindings[2], bindings[4], bindings[0], bindings[3] }));
        }

        [Test]
        public void TestDetachedBindingCopiesAllPersistedFields()
        {
            RunTestWithRealm((realm, _) =>
            {
                var original = new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape), "osu", 0);
                realm.Write(r => r.Add(original));

                var detached = realm.Run(r => RealmKeyBinding.CreateDetached(r.All<RealmKeyBinding>().Single()));

                Assert.Multiple(() =>
                {
                    Assert.That(detached.IsManaged, Is.False);
                    Assert.That(detached.ID, Is.EqualTo(original.ID));
                    Assert.That(detached.RulesetName, Is.EqualTo(original.RulesetName));
                    Assert.That(detached.Variant, Is.EqualTo(original.Variant));
                    Assert.That(detached.ActionInt, Is.EqualTo(original.ActionInt));
                    Assert.That(detached.KeyCombinationString, Is.EqualTo(original.KeyCombinationString));
                });

                realm.Write(r => r.Find<RealmKeyBinding>(original.ID)!.KeyCombination = new KeyCombination(InputKey.BackSpace));
                Assert.That(detached.KeyCombination, Is.EqualTo(new KeyCombination(InputKey.Escape)));
            });
        }

        [Test]
        public void TestUpdateViaQueriedReference()
        {
            RunTestWithRealm((realm, _) =>
            {
                KeyBindingContainer testContainer = new TestKeyBindingContainer();

                var keyBindingStore = new RealmKeyBindingStore(realm, new ReadableKeyCombinationProvider());
                keyBindingStore.Register(testContainer, Enumerable.Empty<RulesetInfo>());

                realm.Run(outerRealm =>
                {
                    var backBinding = outerRealm.All<RealmKeyBinding>().Single(k => k.ActionInt == (int)GlobalAction.Back);

                    Assert.That(backBinding.KeyCombination.Keys, Is.EquivalentTo(new[] { InputKey.Escape }));

                    var tsr = ThreadSafeReference.Create(backBinding);

                    realm.Run(innerRealm =>
                    {
                        var binding = innerRealm.ResolveReference(tsr)!;
                        innerRealm.Write(() => binding.KeyCombination = new KeyCombination(InputKey.BackSpace));
                    });

                    Assert.That(backBinding.KeyCombination.Keys, Is.EquivalentTo(new[] { InputKey.BackSpace }));

                    // check still correct after re-query.
                    backBinding = outerRealm.All<RealmKeyBinding>().Single(k => k.ActionInt == (int)GlobalAction.Back);
                    Assert.That(backBinding.KeyCombination.Keys, Is.EquivalentTo(new[] { InputKey.BackSpace }));
                });
            });
        }

        private static int queryCount(RealmAccess realm, GlobalAction? match = null)
        {
            return realm.Run(r =>
            {
                var results = r.All<RealmKeyBinding>();
                if (match.HasValue)
                    results = results.Where(k => k.ActionInt == (int)match.Value);
                return results.Count();
            });
        }

        public partial class TestKeyBindingContainer : KeyBindingContainer
        {
            public override IEnumerable<IKeyBinding> DefaultKeyBindings =>
                new[]
                {
                    new KeyBinding(InputKey.Escape, GlobalAction.Back),
                    new KeyBinding(InputKey.Enter, GlobalAction.Select),
                    new KeyBinding(InputKey.Space, GlobalAction.Select),
                };
        }
    }
}
