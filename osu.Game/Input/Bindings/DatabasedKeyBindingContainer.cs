// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Game.Database;
using osu.Game.Performance.Debug;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Input.Bindings
{
    /// <summary>
    /// A KeyBindingInputManager with a database backing for custom overrides.
    /// </summary>
    /// <typeparam name="T">The type of the custom action.</typeparam>
    public partial class DatabasedKeyBindingContainer<T> : KeyBindingContainer<T>
        where T : struct
    {
        private readonly RulesetInfo ruleset;

        private readonly int? variant;

        private IDisposable realmSubscription;

        [Resolved]
        private RealmAccess realm { get; set; }

        public IEnumerable<IKeyBinding> CurrentKeyBindings => KeyBindings ?? Enumerable.Empty<IKeyBinding>();

        public override IEnumerable<IKeyBinding> DefaultKeyBindings => ruleset.CreateInstance().GetDefaultKeyBindings(variant ?? 0);

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="ruleset">A reference to identify the current <see cref="Ruleset"/>. Used to lookup mappings. Null for global mappings.</param>
        /// <param name="variant">An optional variant for the specified <see cref="Ruleset"/>. Used when a ruleset has more than one possible keyboard layouts.</param>
        /// <param name="simultaneousMode">Specify how to deal with multiple matches of <see cref="KeyCombination"/>s and <typeparamref name="T"/>s.</param>
        /// <param name="matchingMode">Specify how to deal with exact <see cref="KeyCombination"/> matches.</param>
        public DatabasedKeyBindingContainer(RulesetInfo ruleset = null, int? variant = null, SimultaneousBindingMode simultaneousMode = SimultaneousBindingMode.None, KeyCombinationMatchingMode matchingMode = KeyCombinationMatchingMode.Any)
            : base(simultaneousMode, matchingMode)
        {
            this.ruleset = ruleset;
            this.variant = variant;

            if (ruleset != null && variant == null)
                throw new InvalidOperationException($"{nameof(variant)} can not be null when a non-null {nameof(ruleset)} is provided.");
        }

        protected override void LoadComplete()
        {
            using var loading = PerformancePhaseLogger.Begin("KeyBindings.LoadComplete", detail: typeof(T).Name);
            using (PerformancePhaseLogger.Begin("KeyBindings.RegisterNotifications", detail: typeof(T).Name))
            {
                realmSubscription = realm.RegisterForNotifications(queryRealmKeyBindings, (sender, _) =>
                {
                    // The first fire of this is a bit redundant as this is being called in base.LoadComplete,
                    // but this is safest in case the subscription is restored after a context recycle.
                    ReloadMappings(sender.AsQueryable());
                });
            }

            using (PerformancePhaseLogger.Begin("KeyBindings.BaseLoadComplete", detail: typeof(T).Name))
                base.LoadComplete();
        }

        protected sealed override void ReloadMappings()
        {
            using var reload = PerformancePhaseLogger.Begin("KeyBindings.QueryMappings", detail: typeof(T).Name);
            ReloadMappings(queryRealmKeyBindings(realm.Realm));
        }

        private IQueryable<RealmKeyBinding> queryRealmKeyBindings(Realm realm)
        {
            string rulesetName = ruleset?.ShortName;
            return realm.All<RealmKeyBinding>()
                        .Where(b => b.RulesetName == rulesetName && b.Variant == variant);
        }

        protected virtual void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
        {
            using var reload = PerformancePhaseLogger.Begin("KeyBindings.BuildMappings", detail: typeof(T).Name);
            List<IKeyBinding> defaults;
            using (PerformancePhaseLogger.Begin("KeyBindings.GetDefaults", detail: typeof(T).Name))
                defaults = DefaultKeyBindings.ToList();

            List<RealmKeyBinding> detached;
            using (PerformancePhaseLogger.Begin("KeyBindings.DetachMappings", detail: typeof(T).Name))
                detached = realmKeyBindings.AsEnumerable().Select(RealmKeyBinding.CreateDetached).ToList();

            List<RealmKeyBinding> newBindings;
            using (PerformancePhaseLogger.Begin("KeyBindings.OrderMappings", detail: typeof(T).Name))
                newBindings = OrderByDefaultActionPriority(detached, defaults);

            // In the case no bindings were found in the database, presume this usage is for a non-databased ruleset.
            // This actually should never be required and can be removed if it is ever deemed to cause a problem.
            // See https://github.com/ppy/osu/issues/8805 for original reasoning, which is no longer valid as we use ShortName
            // for lookups these days.
            if (newBindings.Count == 0)
                KeyBindings = defaults;
            else
                KeyBindings = newBindings;
        }

        internal static List<RealmKeyBinding> OrderByDefaultActionPriority(IEnumerable<RealmKeyBinding> bindings, IReadOnlyList<IKeyBinding> defaults)
        {
            var defaultOrder = new Dictionary<int, int>();

            // Multiple default keys can map to the same action. Keep its first position, as FindIndex did.
            for (int i = 0; i < defaults.Count; i++)
                defaultOrder.TryAdd((int)defaults[i].Action, i);

            // Preserve default action priority for conflicting keys (for example song select vs music).
            // Unknown actions used to sort at -1; OrderBy retains their relative order.
            return bindings.OrderBy(b => defaultOrder.TryGetValue(b.ActionInt, out int index) ? index : -1).ToList();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }
    }
}
