using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using osu.Framework.Bindables;

namespace osu.Game.Overlays.Settings
{
    /// <summary>Only identities are persisted, never setting values or translated display names.</summary>
    public sealed class FavouriteSettingsStore : IDisposable
    {
        private readonly Bindable<string> config;
        private readonly List<string> ids = new List<string>();

        public IReadOnlyList<string> Ids => ids;
        public event Action? Changed;

        public FavouriteSettingsStore(Bindable<string> config)
        {
            this.config = config;
            config.BindValueChanged(_ => read(), true);
        }

        public bool Contains(string id) => ids.Contains(id, StringComparer.Ordinal);

        public void Toggle(string id)
        {
            if (!ids.Remove(id))
                ids.Add(id);
            config.Value = JsonSerializer.Serialize(ids);
        }

        private void read()
        {
            ids.Clear();
            try
            {
                ids.AddRange((JsonSerializer.Deserialize<string?[]>(config.Value) ?? Array.Empty<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.Ordinal));
            }
            catch (JsonException)
            {
                // An invalid/old config entry must not prevent opening settings.
            }
            Changed?.Invoke();
        }

        public void Dispose() => config.UnbindAll();
    }
}
