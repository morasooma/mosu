using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Skinning
{
    /// <summary>Per-element rounding of the element's actual surfaces, never its entire unmasked layout.</summary>
    public sealed class SkinComponentGeometry
    {
        internal const string SERIALISATION_KEY = "mosu_corner_radius";
        private static readonly ConditionalWeakTable<Drawable, SkinComponentGeometry> settings = new ConditionalWeakTable<Drawable, SkinComponentGeometry>();
        private static readonly Func<CompositeDrawable, IReadOnlyList<Drawable>> getChildren = typeof(CompositeDrawable)
            .GetProperty("InternalChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetGetMethod(true)!.CreateDelegate<Func<CompositeDrawable, IReadOnlyList<Drawable>>>();
        private static readonly Action<CompositeDrawable, float> setRadius = typeof(CompositeDrawable)
            .GetProperty(nameof(CompositeDrawable.CornerRadius))!.GetSetMethod(true)!.CreateDelegate<Action<CompositeDrawable, float>>();

        public static bool HasNativeSetting(Drawable drawable) => drawable is Components.BoxElement or Screens.Play.HUD.PlayerAvatar;
        public static SkinComponentGeometry For(Drawable drawable) => settings.GetValue(drawable, d => new SkinComponentGeometry((CompositeDrawable)d));
        internal static float? GetOverride(Drawable drawable) => settings.TryGetValue(drawable, out var geometry) ? geometry.radiusOverride : null;

        internal static float? GetInheritedOverride(Drawable drawable)
        {
            Drawable? owner = drawable;
            while (owner != null && owner is not ISerialisableDrawable) owner = owner.Parent;
            return owner == null ? null : GetOverride(owner);
        }

        internal static void Restore(Drawable drawable, float? radius)
        {
            if (drawable is not CompositeDrawable || HasNativeSetting(drawable)) return;
            if (radius.HasValue)
            {
                if (!float.IsFinite(radius.Value)) return;
                var geometry = For(drawable);
                geometry.radiusOverride = Math.Clamp(radius.Value, 0, 200);
                geometry.restoring = true;
                geometry.Radius.Value = geometry.radiusOverride.Value;
                geometry.restoring = false;
                if (geometry.component.IsLoaded) geometry.apply();
            }
            else if (settings.TryGetValue(drawable, out var geometry)) geometry.Reset();
        }

        public BindableFloat Radius { get; }
        private readonly CompositeDrawable component;
        private float? radiusOverride;
        private bool restoring;
        private double nextDiscovery;
        private readonly Dictionary<CompositeDrawable, float> surfaces = new Dictionary<CompositeDrawable, float>();

        private SkinComponentGeometry(CompositeDrawable component)
        {
            this.component = component;
            Radius = new BindableFloat(Math.Clamp(component.CornerRadius, 0, 200)) { MinValue = 0, MaxValue = 200, Precision = 0.5f };
            Radius.BindValueChanged(e =>
            {
                if (restoring) return;
                // Zero is an explicit square override, even for originally unmasked layouts.
                radiusOverride = e.NewValue;
                if (component.IsLoaded) apply();
            });
            if (!component.IsLoaded)
                component.OnLoadComplete += _ =>
                {
                    restoring = true;
                    Radius.Default = Math.Clamp(component.CornerRadius, 0, 200);
                    Radius.Value = radiusOverride ?? Radius.Default;
                    restoring = false;
                    apply();
                };
            component.OnUpdate += _ =>
            {
                if (radiusOverride.HasValue && Environment.TickCount64 >= nextDiscovery)
                {
                    discover(component);
                    nextDiscovery = Environment.TickCount64 + 250;
                }
            };
        }

        public void Reset()
        {
            restoring = true;
            radiusOverride = null;
            Radius.SetDefault();
            restoring = false;
            foreach (var entry in surfaces) setRadius(entry.Key, entry.Value);
        }

        private void apply()
        {
            if (radiusOverride.HasValue) discover(component);
        }

        private void discover(CompositeDrawable target)
        {
            if (target != component && target is ISerialisableDrawable) return;
            // CircularContainer overwrites its radius during draw-node generation. The
            // editable key bars use SkinComponentRoundedContainer instead. Avatars/icons
            // that are deliberately circular are not flattened by unrelated settings.
            // A scroll viewport is a structural clip, not a decorative surface.
            // Rounding it cuts corners off unrelated rows at the ends of the list.
            // Still walk its descendants so each actual row receives its own radius.
            if (target.Masking && target is not IScrollContainer && target is not CircularContainer && target is not SkinComponentRoundedContainer && !surfaces.ContainsKey(target))
            {
                surfaces.Add(target, target.CornerRadius);
                target.OnUpdate += _ =>
                {
                    if (radiusOverride.HasValue) setRadius(target, radiusOverride.Value);
                };
            }
            if (surfaces.ContainsKey(target) && radiusOverride.HasValue) setRadius(target, radiusOverride.Value);
            var children = getChildren(target);
            for (int i = 0; i < children.Count; i++)
                if (children[i] is CompositeDrawable composite) discover(composite);
        }
    }
}
