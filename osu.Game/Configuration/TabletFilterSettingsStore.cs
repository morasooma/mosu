// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Input.Handlers.Tablet;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Persists the fork's custom tablet filter settings (chatter exterminator, radial follow,
    /// bezier interpolator) in a fork-owned JSON file instead of the framework's
    /// <c>input.json</c>, so a restore from another client (or a config backup) can never
    /// overwrite the user's filter tuning.
    /// </summary>
    public class TabletFilterSettingsStore
    {
        public const string FILENAME = "mosu-tablet-filters.json";

        private readonly Storage storage;

        private readonly Dictionary<string, string> loaded = new Dictionary<string, string>();

        private readonly List<IFilterBindable> boundBindables = new List<IFilterBindable>();

        private ITabletHandler? cachedHandler;

        private TabletFilterSettingsStore(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>
        /// Loads persisted filter values into the handler's bindables and keeps the file updated
        /// whenever any of them changes. Returns null when no tablet handler exists.
        /// </summary>
        public static TabletFilterSettingsStore? AttachTo(Storage storage, ITabletHandler? handler)
        {
            if (handler == null)
                return null;

            var store = new TabletFilterSettingsStore(storage);
            store.load(handler);
            return store;
        }

        private void load(ITabletHandler handler)
        {
            cachedHandler = handler;

            try
            {
                if (storage.Exists(FILENAME))
                {
                    using (var stream = storage.GetStream(FILENAME, FileAccess.Read, FileMode.Open))
                    using (var reader = new StreamReader(stream))
                    {
                        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
                        if (deserialized != null)
                        {
                            foreach (var (key, value) in deserialized)
                                loaded[key] = value;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to read {FILENAME}; tablet filters will use defaults.");
                loaded.Clear();
            }

            boundBindables.AddRange(enumerateFilterBindables(handler));

            foreach (var bindable in boundBindables)
            {
                if (loaded.TryGetValue(bindable.Key, out string? value))
                    bindable.ValueFromString(value);

                bindable.ValueChanged += save;
            }

            // Ensure the file exists with current values even if nothing changed this session.
            save();
        }

        private void save()
        {
            if (cachedHandler == null)
                return;

            try
            {
                var snapshot = new Dictionary<string, string>();

                foreach (var bindable in boundBindables)
                    snapshot[bindable.Key] = bindable.ValueToString();

                using (var stream = storage.CreateFileSafely(FILENAME))
                using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(snapshot, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to write {FILENAME}; tablet filter changes may not persist.");
            }
        }

        private static IFilterBindable? tryCreateBindable<T>(ITabletHandler handler, string propertyName, IFormatProvider? formatProvider = null)
            where T : struct, IEquatable<T>
        {
            var prop = handler.GetType().GetProperty(propertyName);
            if (prop?.GetValue(handler) is Bindable<T> bindable)
                return new FilterBindable<T>(propertyName, bindable, formatProvider);

            return null;
        }

        private IEnumerable<IFilterBindable> enumerateFilterBindables(ITabletHandler handler)
        {
            if (tryCreateBindable<bool>(handler, "ChatterExterminatorEnabled") is { } chatterEnabled) yield return chatterEnabled;
            if (tryCreateBindable<float>(handler, "ChatterExterminationStrength", CultureInfo.InvariantCulture) is { } chatterStrength) yield return chatterStrength;
            if (tryCreateBindable<bool>(handler, "RadialFollowEnabled") is { } rfEnabled) yield return rfEnabled;
            if (tryCreateBindable<float>(handler, "RadialFollowOuterRadius", CultureInfo.InvariantCulture) is { } rfOuter) yield return rfOuter;
            if (tryCreateBindable<float>(handler, "RadialFollowInnerRadius", CultureInfo.InvariantCulture) is { } rfInner) yield return rfInner;
            if (tryCreateBindable<float>(handler, "RadialFollowSmoothingCoefficient", CultureInfo.InvariantCulture) is { } rfSmooth) yield return rfSmooth;
            if (tryCreateBindable<float>(handler, "RadialFollowSoftKneeScale", CultureInfo.InvariantCulture) is { } rfKnee) yield return rfKnee;
            if (tryCreateBindable<float>(handler, "RadialFollowSmoothingLeakCoefficient", CultureInfo.InvariantCulture) is { } rfLeak) yield return rfLeak;
            if (tryCreateBindable<bool>(handler, "BezierInterpolatorEnabled") is { } bezierEnabled) yield return bezierEnabled;
            if (tryCreateBindable<float>(handler, "BezierSmoothingFactor", CultureInfo.InvariantCulture) is { } bezierFactor) yield return bezierFactor;
        }

        private interface IFilterBindable
        {
            string Key { get; }

            event Action ValueChanged;

            string ValueToString();

            void ValueFromString(string value);
        }

        private sealed class FilterBindable<T> : IFilterBindable
            where T : struct, IEquatable<T>
        {
            private readonly Bindable<T> bindable;
            private readonly IFormatProvider? formatProvider;

            public FilterBindable(string key, Bindable<T> bindable, IFormatProvider? formatProvider = null)
            {
                Key = key;
                this.bindable = bindable;
                this.formatProvider = formatProvider;

                bindable.BindValueChanged(_ => ValueChanged?.Invoke());
            }

            public string Key { get; }

            public event Action? ValueChanged;

            public string ValueToString() => Convert.ToString(bindable.Value, formatProvider ?? CultureInfo.CurrentCulture)!;

            public void ValueFromString(string value)
            {
                try
                {
                    if (typeof(T) == typeof(bool))
                        bindable.Value = (T)(object)bool.Parse(value);
                    else if (typeof(T) == typeof(float))
                    {
                        bindable.Value = (T)(object)float.Parse(
                            value,
                            NumberStyles.Float | NumberStyles.AllowThousands,
                            formatProvider ?? CultureInfo.CurrentCulture);
                    }
                }
                catch
                {
                    // Malformed values fall back to the bindable's current default.
                }
            }
        }
    }
}
