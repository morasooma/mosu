// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Configuration
{
    /// <summary>
    /// A helper class for tracking changes to the settings of a set of <see cref="Mod"/>s.
    /// </summary>
    /// <remarks>
    /// Ensure to dispose when usage is finished.
    /// </remarks>
    public class ModSettingChangeTracker : IDisposable
    {
        /// <summary>
        /// Notifies that the setting of a <see cref="Mod"/> has changed.
        /// </summary>
        public Action<Mod> SettingChanged;

        private static readonly MethodInfo bind_value_changed_method = typeof(ModSettingChangeTracker).GetMethod(nameof(bindValueChanged), BindingFlags.Static | BindingFlags.NonPublic)!;

        private readonly List<IBindable> settings = new List<IBindable>();

        /// <summary>
        /// Creates a new <see cref="ModSettingChangeTracker"/> for a set of <see cref="Mod"/>s.
        /// </summary>
        /// <param name="mods">The set of <see cref="Mod"/>s whose settings need to be tracked.</param>
        public ModSettingChangeTracker(IEnumerable<Mod> mods)
        {
            foreach (var mod in mods)
            {
                foreach (var (_, property) in mod.GetSettingsSourceProperties())
                {
                    var setting = ((IBindable)property.GetValue(mod)!).GetBoundCopy();

                    bind_value_changed_method.MakeGenericMethod(getBindableValueType(setting)).Invoke(null, new object[]
                    {
                        setting,
                        new Action(() => SettingChanged?.Invoke(mod))
                    });

                    settings.Add(setting);
                }
            }
        }

        private static Type getBindableValueType(IBindable bindable)
            => bindable.GetType().GetInterfaces().Single(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IBindable<>)).GenericTypeArguments[0];

        private static void bindValueChanged<T>(IBindable setting, Action onChange)
            => ((IBindable<T>)setting).BindValueChanged(_ => onChange());

        public void Dispose()
        {
            SettingChanged = null;

            foreach (var r in settings)
                r.UnbindAll();
            settings.Clear();
        }
    }
}
