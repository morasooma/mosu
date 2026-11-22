using System.Runtime.CompilerServices;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Skinning
{
    /// <summary>Per-component font family; empty selection preserves the original skin font.</summary>
    public sealed class SkinComponentFont
    {
        internal const string SERIALISATION_KEY = "mosu_font_family";
        private static readonly ConditionalWeakTable<Drawable, SkinComponentFont> settings = new ConditionalWeakTable<Drawable, SkinComponentFont>();
        public Bindable<string> Family { get; } = new Bindable<string>(string.Empty);
        public static SkinComponentFont For(Drawable drawable) => settings.GetValue(drawable, _ => new SkinComponentFont());
        internal static string? GetOverride(Drawable drawable) => settings.TryGetValue(drawable, out var font) && !string.IsNullOrEmpty(font.Family.Value) ? font.Family.Value : null;
        internal static void Restore(Drawable drawable, string? family)
        {
            if (drawable is FontAdjustableSkinComponent) return;
            if (family != null && !ThemeInterfaceSettings.IsSafeFontName(family)) return;
            if (family != null || settings.TryGetValue(drawable, out _)) For(drawable).Family.Value = family ?? string.Empty;
        }
    }
}
