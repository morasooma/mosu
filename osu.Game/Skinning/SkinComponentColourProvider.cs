using System;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Applies explicitly configured interface colours to HUD cosmetics, without replacing
    /// individual skin colours or inheriting the skin editor sidebar's surface palette.
    /// </summary>
    public class SkinComponentColourProvider
    {
        // Bound copies are weakly held by the global sources. Keep this provider in the
        // receiving drawable, not in a static event subscription which would retain skins.
        private readonly IBindable<ThemeMode> mode = OverlayColourProvider.CurrentTheme.GetBoundCopy();
        private readonly IBindable<CustomThemeDefinition> theme = OverlayColourProvider.CustomTheme.GetBoundCopy();

        public void Bind(Action<Action> schedule, Action update)
        {
            void changed()
            {
                // Theme configuration can also load on a background thread. Loaded
                // components (including previews) must only mutate on their update thread.
                if (ThreadSafety.IsUpdateThread)
                    update();
                else
                    schedule(update);
            }
            mode.BindValueChanged(_ => changed());
            theme.BindValueChanged(_ => changed());
            update();
        }

        public Colour4 GetColour(OverlayColour role, Colour4 fallback)
            => mode.Value == ThemeMode.Custom && theme.Value.TryGetOverride(role, out var colour) ? colour : fallback;

        /// <summary>Explicit per-element colours take precedence; never write palette values into skin settings.</summary>
        public Colour4 GetColour(OverlayColour role, BindableColour4 setting)
            => setting.IsDefault ? GetColour(role, setting.Value) : setting.Value;
    }
}
