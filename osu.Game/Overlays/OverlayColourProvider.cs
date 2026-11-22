// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Rulesets.Mods;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public class OverlayColourProvider
    {
        /// <summary>
        /// The global theme mode bindable. When set to <see cref="ThemeMode.Dark"/>,
        /// all colours produced by any <see cref="OverlayColourProvider"/> become monochrome (grayscale)
        /// with darker backgrounds.
        /// When set to <see cref="ThemeMode.Custom"/>, custom overrides are resolved from <see cref="CustomTheme"/>.
        /// </summary>
        public static readonly Bindable<ThemeMode> CurrentTheme = new Bindable<ThemeMode>(ThemeMode.Default);

        /// <summary>
        /// The active custom theme definition. When <see cref="CurrentTheme"/> is set to <see cref="ThemeMode.Custom"/>,
        /// colour providers resolve semantic overrides from here and fall back to <see cref="CustomThemeDefinition.BaseTheme"/>.
        /// </summary>
        public static readonly Bindable<CustomThemeDefinition> CustomTheme = new Bindable<CustomThemeDefinition>(new CustomThemeDefinition());

        private static readonly System.Collections.Generic.Dictionary<GeometryRole, BindableFloat> staticRadii = new System.Collections.Generic.Dictionary<GeometryRole, BindableFloat>
        {
            [GeometryRole.Button] = new BindableFloat(GetDefaultRadius(GeometryRole.Button)),
            [GeometryRole.Input] = new BindableFloat(GetDefaultRadius(GeometryRole.Input)),
            [GeometryRole.Card] = new BindableFloat(GetDefaultRadius(GeometryRole.Card)),
            [GeometryRole.Panel] = new BindableFloat(GetDefaultRadius(GeometryRole.Panel)),
            [GeometryRole.Popup] = new BindableFloat(GetDefaultRadius(GeometryRole.Popup)),
        };

        private static readonly Bindable<TopBarVariant> staticTopBarVariant = new Bindable<TopBarVariant>(TopBarVariant.Flat);
        private static readonly BindableFloat staticTopBarRadius = new BindableFloat(10f);
        private static readonly BindableBool buttonFooterGradients = new BindableBool(true);

        /// <summary>
        /// Whether subtle visual gradients are enabled on buttons and the bottom footer bar.
        /// Controlled via <see cref="CustomThemeDefinition.EnableGradients"/> (defaults to true).
        /// </summary>
        public static IBindable<bool> ButtonFooterGradients => buttonFooterGradients;

        static OverlayColourProvider()
        {
            CustomTheme.BindValueChanged(e =>
            {
                if (CurrentTheme.Value == ThemeMode.Custom && e.OldValue?.BaseTheme != e.NewValue?.BaseTheme)
                    CurrentTheme.TriggerChange(propagateToBindings: true);

                updateStaticRadii();
                updateStaticGradients();
            });

            CurrentTheme.BindValueChanged(_ =>
            {
                updateStaticRadii();
                updateStaticGradients();
            });
            updateStaticRadii();
            updateStaticGradients();
        }

        private static void updateStaticRadii()
        {
            foreach (var role in System.Enum.GetValues<GeometryRole>())
            {
                if (staticRadii.TryGetValue(role, out var bindable))
                    bindable.Value = CalculateEffectiveRadius(role);
            }

            updateStaticTopBar();
        }

        private static void updateStaticGradients()
        {
            if (CurrentTheme.Value != ThemeMode.Custom)
            {
                buttonFooterGradients.Value = true;
                return;
            }

            buttonFooterGradients.Value = CustomTheme.Value?.EnableGradients ?? true;
        }

        /// <summary>
        /// Attempts to get a contextual color override for the specified context and role.
        /// Returns true if custom theme is active and defines an override.
        /// </summary>
        public static bool TryGetCustomContextColour(ThemeSurfaceContext context, SurfaceColourRole role, out Colour4 colour)
        {
            colour = default;
            if (CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null)
                return CustomTheme.Value.TryGetContextOverride(context, role, out colour);

            return false;
        }

        /// <summary>
        /// Resolves a contextual color override if defined, otherwise returning the specified fallback color.
        /// </summary>
        public static Colour4 GetEffectiveContextColour(ThemeSurfaceContext context, SurfaceColourRole role, Colour4 fallback)
        {
            if (TryGetCustomContextColour(context, role, out var colour))
                return colour;

            return fallback;
        }

        /// <summary>
        /// Returns true if custom theme is active and defines an override for the given mod type.
        /// </summary>
        public static bool TryGetCustomModTypeColour(ModType modType, out Colour4 colour)
        {
            colour = default;

            if (CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null)
                return CustomTheme.Value.TryGetModTypeOverride(modType, out colour);

            return false;
        }

        /// <summary>
        /// Resolves the effective cosmetic colour for a mod type.
        /// If inside the mod selection UI context and overridden by custom theme, returns the custom colour;
        /// otherwise falls back to the default <see cref="OsuColour.ForModType"/>.
        /// </summary>
        public Color4 GetModTypeColour(ModType modType, OsuColour colours)
        {
            if (SurfaceContext == ThemeSurfaceContext.ModSelect && CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null)
            {
                if (CustomTheme.Value.TryGetModTypeOverride(modType, out var customColour))
                    return customColour;
            }

            return colours.ForModType(modType);
        }

        private static void updateStaticTopBar()
        {
            var custom = CustomTheme.Value;
            if (CurrentTheme.Value == ThemeMode.Custom && custom != null)
            {
                staticTopBarVariant.Value = custom.TopBarVariant;
                staticTopBarRadius.Value = custom.TopBarRadius ?? CalculateEffectiveRadius(GeometryRole.Panel, 10f);
            }
            else
            {
                staticTopBarVariant.Value = TopBarVariant.Flat;
                staticTopBarRadius.Value = CalculateEffectiveRadius(GeometryRole.Panel, 10f);
            }
        }

        /// <summary>
        /// Retrieves a weakly-bound copy of the top bar variant under current theme state.
        /// Updates live when custom theme changes or theme mode toggles.
        /// </summary>
        public static IBindable<TopBarVariant> GetTopBarVariant() => staticTopBarVariant.GetBoundCopy();

        /// <summary>
        /// Retrieves a weakly-bound copy of the effective top bar corner radius under current theme state.
        /// Updates live when custom theme changes or theme mode toggles.
        /// </summary>
        public static IBindable<float> GetTopBarRadius() => staticTopBarRadius.GetBoundCopy();

        /// <summary>
        /// Returns the default corner radius for a semantic geometry role.
        /// </summary>
        public static float GetDefaultRadius(GeometryRole role) => role switch
        {
            GeometryRole.Button => 5f,
            GeometryRole.Input => 5f,
            GeometryRole.Card => 8f,
            GeometryRole.Panel => 10f,
            GeometryRole.Popup => 6f,
            _ => 0f,
        };

        /// <summary>
        /// Calculates the effective corner radius for a role under current theme state.
        /// When custom theme is active and has an override for the role, the override is used.
        /// Otherwise <paramref name="fallbackDefault"/> or <see cref="GetDefaultRadius(GeometryRole)"/> is returned.
        /// </summary>
        public static float CalculateEffectiveRadius(GeometryRole role, float? fallbackDefault = null)
        {
            float def = fallbackDefault ?? GetDefaultRadius(role);

            if (CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null)
            {
                if (CustomTheme.Value.TryGetRadius(role, out float radius))
                    return radius;
            }

            return def;
        }

        /// <summary>
        /// Retrieves a weakly-bound copy of a corner radius for a semantic geometry role.
        /// The returned bindable updates live when custom theme geometry changes or theme mode toggles.
        /// </summary>
        public static IBindable<float> GetGeometryRadius(GeometryRole role)
        {
            if (staticRadii.TryGetValue(role, out var bindable))
                return bindable.GetBoundCopy();

            return new BindableFloat(GetDefaultRadius(role));
        }

        private class FallbackGeometryRadiusBindable : BindableFloat
        {
            // Retain strong references so weak bindings to static CustomTheme/CurrentTheme are not reclaimed on GC.
            private readonly IBindable<CustomThemeDefinition> customTheme;
            private readonly IBindable<ThemeMode> currentTheme;

            public FallbackGeometryRadiusBindable(GeometryRole role, float fallbackDefault)
                : base(CalculateEffectiveRadius(role, fallbackDefault))
            {
                void update() => Value = CalculateEffectiveRadius(role, fallbackDefault);

                customTheme = CustomTheme.GetBoundCopy();
                customTheme.BindValueChanged(_ => update());

                currentTheme = CurrentTheme.GetBoundCopy();
                currentTheme.BindValueChanged(_ => update());
            }
        }

        /// <summary>
        /// Retrieves a reactive corner radius bindable for a semantic geometry role with a custom fallback default.
        /// When no custom override is set, <paramref name="fallbackDefault"/> is returned.
        /// </summary>
        public static IBindable<float> GetGeometryRadius(GeometryRole role, float fallbackDefault)
        {
            if (System.Math.Abs(fallbackDefault - GetDefaultRadius(role)) < 0.001f)
                return GetGeometryRadius(role);

            return new FallbackGeometryRadiusBindable(role, fallbackDefault);
        }

        /// <summary>
        /// Retrieves a weakly-bound copy of a corner radius for a semantic geometry role from this provider.
        /// </summary>
        public IBindable<float> GetRadiusBindable(GeometryRole role) => GetGeometryRadius(role);

        /// <summary>
        /// Retrieves a reactive corner radius bindable with a custom fallback default from this provider.
        /// </summary>
        public IBindable<float> GetRadiusBindable(GeometryRole role, float fallbackDefault) => GetGeometryRadius(role, fallbackDefault);

        /// <summary>
        /// Returns the current effective corner radius for a semantic geometry role.
        /// </summary>
        public float GetRadius(GeometryRole role) => CalculateEffectiveRadius(role);

        private readonly IBindable<ThemeMode> currentTheme;
        private readonly IBindable<CustomThemeDefinition> customTheme;

        /// <summary>
        /// Indicates that palette values are currently being invalidated due to
        /// a theme change. UI controls can use this to skip interaction fades
        /// and apply the new palette in the same frame.
        /// </summary>
        public static bool IsThemeUpdate { get; private set; }

        public static double ThemeTransitionDuration(double normalDuration) => IsThemeUpdate ? 0 : osu.Game.Graphics.InterfaceAnimation.FadeDuration(normalDuration);

        private readonly BindableColour4 colour0 = new BindableColour4();
        private readonly BindableColour4 colour1 = new BindableColour4();
        private readonly BindableColour4 colour2 = new BindableColour4();
        private readonly BindableColour4 colour3 = new BindableColour4();
        private readonly BindableColour4 colour4 = new BindableColour4();
        private readonly BindableColour4 highlight1 = new BindableColour4();
        private readonly BindableColour4 content1 = new BindableColour4();
        private readonly BindableColour4 content2 = new BindableColour4();
        private readonly BindableColour4 light1 = new BindableColour4();
        private readonly BindableColour4 light2 = new BindableColour4();
        private readonly BindableColour4 light3 = new BindableColour4();
        private readonly BindableColour4 light4 = new BindableColour4();
        private readonly BindableColour4 dark1 = new BindableColour4();
        private readonly BindableColour4 dark2 = new BindableColour4();
        private readonly BindableColour4 dark3 = new BindableColour4();
        private readonly BindableColour4 dark4 = new BindableColour4();
        private readonly BindableColour4 dark5 = new BindableColour4();
        private readonly BindableColour4 dark6 = new BindableColour4();
        private readonly BindableColour4 foreground1 = new BindableColour4();
        private readonly BindableColour4 background1 = new BindableColour4();
        private readonly BindableColour4 background2 = new BindableColour4();
        private readonly BindableColour4 background3 = new BindableColour4();
        private readonly BindableColour4 background4 = new BindableColour4();
        private readonly BindableColour4 background5 = new BindableColour4();
        private readonly BindableColour4 background6 = new BindableColour4();

        /// <summary>
        /// The hue degree associated with the colour shades provided by this <see cref="OverlayColourProvider"/>.
        /// </summary>
        public int Hue { get; private set; }

        /// <summary>
        /// Optional surface context indicating which screen/surface this provider belongs to.
        /// When set, contextual custom theme overrides will take precedence.
        /// </summary>
        public ThemeSurfaceContext? SurfaceContext { get; }

        /// <summary>
        /// When true, accent colors (Highlight1, Colour0..Colour4) are derived strictly from this provider's <see cref="Hue"/>
        /// and are exempt from global/custom theme palette overrides. Use for user profiles where accent reflects user ProfileHue.
        /// </summary>
        public bool ExemptAccentFromCustomTheme { get; set; }

        private readonly Bindable<int> paletteVersion = new Bindable<int>();

        /// <summary>
        /// Fires whenever the complete palette of this provider is updated.
        /// Hold a bound copy or bind to this to perform global redraws of components
        /// that consume multiple colours without waiting for mouse hovers or nudges.
        /// </summary>
        public IBindable<int> PaletteVersion => paletteVersion.GetBoundCopy();

        public OverlayColourProvider(OverlayColourScheme colourScheme, ThemeSurfaceContext? surfaceContext = null)
            : this(colourScheme.GetHue(), surfaceContext)
        {
        }

        public OverlayColourProvider(int hue, ThemeSurfaceContext? surfaceContext = null)
        {
            Hue = hue;
            SurfaceContext = surfaceContext;

            // A bound copy is weakly held by the static source. Providers can
            // therefore be created by short-lived overlays without leaking
            // them through a global theme event.
            currentTheme = CurrentTheme.GetBoundCopy();
            currentTheme.BindValueChanged(_ => updateColours(), true);

            customTheme = CustomTheme.GetBoundCopy();
            customTheme.BindValueChanged(_ => updateColours());
        }

        // Note that the following five colours are also defined in `OsuColour` as `{colourScheme}{0,1,2,3,4}`.
        // The difference as to which should be used where comes down to context.
        // If the colour in question is supposed to always match the view in which it is displayed theme-wise, use `OverlayColourProvider`.
        // If the colour usage is special and in general differs from the surrounding view in choice of hue, use the `OsuColour` constants.
        public Color4 Colour0 => colour0.Value;
        public Color4 Colour1 => colour1.Value;
        public Color4 Colour2 => colour2.Value;
        public Color4 Colour3 => colour3.Value;
        public Color4 Colour4 => colour4.Value;

        public Color4 Highlight1 => highlight1.Value;
        public Color4 Content1 => content1.Value;
        public Color4 Content2 => content2.Value;
        public Color4 Light1 => light1.Value;
        public Color4 Light2 => light2.Value;
        public Color4 Light3 => light3.Value;
        public Color4 Light4 => light4.Value;
        public Color4 Dark1 => dark1.Value;
        public Color4 Dark2 => dark2.Value;
        public Color4 Dark3 => dark3.Value;
        public Color4 Dark4 => dark4.Value;
        public Color4 Dark5 => dark5.Value;
        public Color4 Dark6 => dark6.Value;
        public Color4 Foreground1 => foreground1.Value;
        public Color4 Background1 => background1.Value;
        public Color4 Background2 => background2.Value;
        public Color4 Background3 => background3.Value;
        public Color4 Background4 => background4.Value;
        public Color4 Background5 => background5.Value;
        public Color4 Background6 => background6.Value;

        /// <summary>
        /// Retrieves a weakly-bound copy of a palette colour. The copy changes
        /// whenever the global theme or this provider's hue changes.
        /// Hold the returned bindable for as long as the receiving drawable is
        /// alive, then bind its value to every derived colour/state that needs
        /// to update together.
        /// </summary>
        public IBindable<Framework.Graphics.Colour4> GetColourBindable(OverlayColour colour) => getBindable(colour).GetBoundCopy();

        /// <summary>
        /// Changes the <see cref="Hue"/> to a different degree.
        /// Note that this does not trigger any kind of signal to any drawable that received colours from here, all drawables need to be updated manually.
        /// </summary>
        /// <param name="colourScheme">The proposed colour scheme.</param>
        public void ChangeColourScheme(OverlayColourScheme colourScheme) => ChangeColourScheme(colourScheme.GetHue());

        /// <summary>
        /// Changes the <see cref="Hue"/> to a different degree.
        /// Note that this does not trigger any kind of signal to any drawable that received colours from here, all drawables need to be updated manually.
        /// </summary>
        /// <param name="hue">The proposed hue degree.</param>
        public void ChangeColourScheme(int hue)
        {
            if (Hue == hue)
                return;

            Hue = hue;
            updateColours();
        }

        /// <summary>
        /// Returns a colour as if <see cref="ThemeMode.Default"/> was active.
        /// Useful for UI surfaces that should remain dark even in <see cref="ThemeMode.Light"/>.
        /// </summary>
        public Color4 GetDefaultColour(float saturation, float lightness) =>
            Framework.Graphics.Colour4.FromHSL(Hue / 360f, saturation, lightness);

        /// <summary>
        /// Returns the inherited base colour for a semantic role under the active theme (or specified base theme),
        /// ignoring any custom override for that role.
        /// </summary>
        public Color4 GetInheritedColour(OverlayColour colour, ThemeMode? baseThemeOverride = null)
        {
            var (sat, light) = GetDefaultParametersFor(colour);
            var baseMode = baseThemeOverride ?? (CurrentTheme.Value == ThemeMode.Custom ? (CustomTheme.Value?.BaseTheme ?? ThemeMode.Default) : CurrentTheme.Value);
            return calculateBaseColour(baseMode, sat, light);
        }

        /// <summary>
        /// Returns the inherited colour for a contextual role under the active theme,
        /// falling back to the global semantic override if present, or to the base theme.
        /// </summary>
        public Color4 GetInheritedContextColour(ThemeSurfaceContext context, SurfaceColourRole role, ThemeMode? baseThemeOverride = null)
        {
            var token = MapRoleToTokenForContext(context, role);
            var custom = CustomTheme.Value;
            if (custom != null && custom.TryGetOverride(token, out var globalOverride))
                return globalOverride;

            var baseMode = baseThemeOverride ?? (CurrentTheme.Value == ThemeMode.Custom ? (CustomTheme.Value?.BaseTheme ?? ThemeMode.Default) : CurrentTheme.Value);
            var (sat, light) = GetDefaultParametersFor(token);
            int hue = GetDefaultHueForContext(context);
            return CalculateBaseColourForHue(hue, baseMode, sat, light, false);
        }

        /// <summary>
        /// Resolves the current color for a context role from this provider.
        /// </summary>
        public Color4 GetSurfaceColour(SurfaceColourRole role)
        {
            if (SurfaceContext.HasValue && CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null)
            {
                if (CustomTheme.Value.TryGetContextOverride(SurfaceContext.Value, role, out var colour))
                    return colour;
            }

            var context = SurfaceContext ?? ThemeSurfaceContext.SongSelect;
            var token = MapRoleToTokenForContext(context, role);
            return getBindable(token).Value;
        }

        /// <summary>
        /// Retrieves a reactive bindable for a context role from this provider.
        /// </summary>
        public IBindable<Framework.Graphics.Colour4> GetSurfaceColourBindable(SurfaceColourRole role)
        {
            var context = SurfaceContext ?? ThemeSurfaceContext.SongSelect;
            var token = MapRoleToTokenForContext(context, role);
            return GetColourBindable(token);
        }

        /// <summary>
        /// Maps an overlay colour token to its associated context role if one exists for the given context.
        /// </summary>
        public static SurfaceColourRole? MapTokenToRoleForContext(ThemeSurfaceContext context, OverlayColour token) => context switch
        {
            ThemeSurfaceContext.SongSelect => token switch
            {
                OverlayColour.Background3 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background5 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Background4 => SurfaceColourRole.ControlIdle,
                OverlayColour.Highlight1 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.ModSelect => token switch
            {
                OverlayColour.Background3 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background2 => SurfaceColourRole.ControlIdle,
                OverlayColour.Background4 => SurfaceColourRole.SurfaceElevated,
                // Incompatible mod panels use Background5 rather than Background4.
                // Resolve it through the same editable surface role, not the green base palette.
                OverlayColour.Background5 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Light4 => SurfaceColourRole.ControlHover,
                OverlayColour.Highlight1 => SurfaceColourRole.Accent,
                OverlayColour.Colour3 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                OverlayColour.Background6 => SurfaceColourRole.Backdrop,
                OverlayColour.Dark3 => SurfaceColourRole.HeaderBackground,
                OverlayColour.Dark4 => SurfaceColourRole.HeaderBackground,
                _ => null
            },
            ThemeSurfaceContext.Editor => token switch
            {
                OverlayColour.Background6 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background5 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Background4 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Background3 => SurfaceColourRole.ControlIdle,
                OverlayColour.Background2 => SurfaceColourRole.ControlHover,
                OverlayColour.Background1 => SurfaceColourRole.ControlSelected,
                OverlayColour.Light3 => SurfaceColourRole.Accent,
                OverlayColour.Colour3 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.BeatmapLibrary => token switch
            {
                OverlayColour.Background2 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Highlight1 => SurfaceColourRole.Accent,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.Settings => token switch
            {
                OverlayColour.Background4 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background5 => SurfaceColourRole.SurfaceElevated,
                _ => null
            },
            ThemeSurfaceContext.InfiniteGlass => token switch
            {
                OverlayColour.Background4 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Highlight1 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.TopBar => token switch
            {
                OverlayColour.Background6 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background5 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background4 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Light4 => SurfaceColourRole.ControlHover,
                OverlayColour.Colour3 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.Results => token switch
            {
                OverlayColour.Background5 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background4 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Background3 => SurfaceColourRole.ControlIdle,
                OverlayColour.Light4 => SurfaceColourRole.ControlHover,
                OverlayColour.Highlight1 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.Footer => token switch
            {
                OverlayColour.Background5 => SurfaceColourRole.SurfaceBackground,
                OverlayColour.Background4 => SurfaceColourRole.SurfaceElevated,
                OverlayColour.Highlight1 => SurfaceColourRole.Accent,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Content2 => SurfaceColourRole.TextSecondary,
                _ => null
            },
            ThemeSurfaceContext.FooterBack or ThemeSurfaceContext.FooterMods or ThemeSurfaceContext.FooterRandom or ThemeSurfaceContext.FooterOptions => token switch
            {
                OverlayColour.Background3 => SurfaceColourRole.ControlIdle,
                OverlayColour.Background2 => SurfaceColourRole.ControlHover,
                OverlayColour.Highlight1 => SurfaceColourRole.ControlSelected,
                OverlayColour.Content1 => SurfaceColourRole.TextPrimary,
                OverlayColour.Light4 => SurfaceColourRole.Accent,
                _ => null
            },
            _ => null
        };

        /// <summary>
        /// Maps a context role to the default underlying OverlayColour token for that context.
        /// </summary>
        public static OverlayColour MapRoleToTokenForContext(ThemeSurfaceContext context, SurfaceColourRole role) => context switch
        {
            ThemeSurfaceContext.SongSelect => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background3,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background5,
                SurfaceColourRole.ControlIdle => OverlayColour.Background4,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background3
            },
            ThemeSurfaceContext.ModSelect => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background3,
                SurfaceColourRole.ControlIdle => OverlayColour.Background2,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background4,
                SurfaceColourRole.ControlHover => OverlayColour.Light4,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                SurfaceColourRole.Backdrop => OverlayColour.Background6,
                SurfaceColourRole.HeaderBackground => OverlayColour.Dark3,
                _ => OverlayColour.Background3
            },
            ThemeSurfaceContext.Editor => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background6,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background5,
                SurfaceColourRole.ControlIdle => OverlayColour.Background3,
                SurfaceColourRole.ControlHover => OverlayColour.Background2,
                SurfaceColourRole.ControlSelected => OverlayColour.Background1,
                SurfaceColourRole.Accent => OverlayColour.Colour3,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background3
            },
            ThemeSurfaceContext.BeatmapLibrary => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background2,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background3
            },
            ThemeSurfaceContext.Settings => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background4,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background5,
                _ => OverlayColour.Background3
            },
            ThemeSurfaceContext.InfiniteGlass => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background4,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background3
            },
            ThemeSurfaceContext.TopBar => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background5,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background4,
                SurfaceColourRole.ControlHover => OverlayColour.Light4,
                SurfaceColourRole.Accent => OverlayColour.Colour3,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background5
            },
            ThemeSurfaceContext.Results => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background5,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background4,
                SurfaceColourRole.ControlIdle => OverlayColour.Background3,
                SurfaceColourRole.ControlHover => OverlayColour.Light4,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background5
            },
            ThemeSurfaceContext.Footer => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background5,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background4,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                _ => OverlayColour.Background5
            },
            ThemeSurfaceContext.FooterBack or ThemeSurfaceContext.FooterMods or ThemeSurfaceContext.FooterRandom or ThemeSurfaceContext.FooterOptions => role switch
            {
                SurfaceColourRole.ControlIdle => OverlayColour.Background3,
                SurfaceColourRole.ControlHover => OverlayColour.Background2,
                SurfaceColourRole.ControlSelected => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                _ => OverlayColour.Background3
            },
            _ => role switch
            {
                SurfaceColourRole.SurfaceBackground => OverlayColour.Background3,
                SurfaceColourRole.SurfaceElevated => OverlayColour.Background4,
                SurfaceColourRole.Accent => OverlayColour.Highlight1,
                SurfaceColourRole.TextPrimary => OverlayColour.Content1,
                SurfaceColourRole.TextSecondary => OverlayColour.Content2,
                SurfaceColourRole.Backdrop => OverlayColour.Background6,
                SurfaceColourRole.HeaderBackground => OverlayColour.Dark3,
                _ => OverlayColour.Background3
            }
        };

        /// <summary>
        /// Returns the default saturation and lightness parameters for a semantic <see cref="OverlayColour"/> role.
        /// </summary>
        public static (float saturation, float lightness) GetDefaultParametersFor(OverlayColour colour) => colour switch
        {
            OverlayColour.Colour0 => (1f, 0.8f),
            OverlayColour.Colour1 => (1f, 0.7f),
            OverlayColour.Colour2 => (0.8f, 0.6f),
            OverlayColour.Colour3 => (0.6f, 0.5f),
            OverlayColour.Colour4 => (0.4f, 0.3f),
            OverlayColour.Highlight1 => (1f, 0.7f),
            OverlayColour.Content1 => (0.4f, 1f),
            OverlayColour.Content2 => (0.4f, 0.9f),
            OverlayColour.Light1 => (0.4f, 0.8f),
            OverlayColour.Light2 => (0.4f, 0.75f),
            OverlayColour.Light3 => (0.4f, 0.7f),
            OverlayColour.Light4 => (0.4f, 0.5f),
            OverlayColour.Dark1 => (0.2f, 0.35f),
            OverlayColour.Dark2 => (0.2f, 0.3f),
            OverlayColour.Dark3 => (0.2f, 0.25f),
            OverlayColour.Dark4 => (0.2f, 0.2f),
            OverlayColour.Dark5 => (0.2f, 0.15f),
            OverlayColour.Dark6 => (0.2f, 0.1f),
            OverlayColour.Foreground1 => (0.1f, 0.6f),
            OverlayColour.Background1 => (0.1f, 0.4f),
            OverlayColour.Background2 => (0.1f, 0.3f),
            OverlayColour.Background3 => (0.1f, 0.25f),
            OverlayColour.Background4 => (0.1f, 0.2f),
            OverlayColour.Background5 => (0.1f, 0.15f),
            OverlayColour.Background6 => (0.1f, 0.1f),
            _ => (0f, 0f),
        };

        /// <summary>
        /// Returns true when the global theme is set to <see cref="ThemeMode.Light"/>
        /// or a custom theme derived from <see cref="ThemeMode.Light"/>.
        /// </summary>
        public static bool IsLightTheme =>
            CurrentTheme.Value == ThemeMode.Light ||
            (CurrentTheme.Value == ThemeMode.Custom && (CustomTheme.Value?.BaseTheme ?? ThemeMode.Default) == ThemeMode.Light);

        /// <summary>
        /// Returns true when the global theme is set to <see cref="ThemeMode.Dark"/>
        /// or a custom theme derived from <see cref="ThemeMode.Dark"/>.
        /// </summary>
        public static bool IsDarkTheme =>
            CurrentTheme.Value == ThemeMode.Dark ||
            (CurrentTheme.Value == ThemeMode.Custom && (CustomTheme.Value?.BaseTheme ?? ThemeMode.Default) == ThemeMode.Dark);

        /// <summary>
        /// When <c>true</c>, colours ignore <see cref="ThemeMode.Light"/> adjustments and stay as <see cref="ThemeMode.Default"/>.
        /// Use for UI drawn on dark surfaces (song select panels over beatmap backgrounds).
        /// </summary>
        public bool IgnoreLightTheme
        {
            get => ignoreLightTheme;
            set
            {
                if (ignoreLightTheme == value)
                    return;

                ignoreLightTheme = value;
                updateColours();
            }
        }

        private bool ignoreLightTheme;

        private BindableColour4 getBindable(OverlayColour colour) => colour switch
        {
            OverlayColour.Colour0 => colour0,
            OverlayColour.Colour1 => colour1,
            OverlayColour.Colour2 => colour2,
            OverlayColour.Colour3 => colour3,
            OverlayColour.Colour4 => colour4,
            OverlayColour.Highlight1 => highlight1,
            OverlayColour.Content1 => content1,
            OverlayColour.Content2 => content2,
            OverlayColour.Light1 => light1,
            OverlayColour.Light2 => light2,
            OverlayColour.Light3 => light3,
            OverlayColour.Light4 => light4,
            OverlayColour.Dark1 => dark1,
            OverlayColour.Dark2 => dark2,
            OverlayColour.Dark3 => dark3,
            OverlayColour.Dark4 => dark4,
            OverlayColour.Dark5 => dark5,
            OverlayColour.Dark6 => dark6,
            OverlayColour.Foreground1 => foreground1,
            OverlayColour.Background1 => background1,
            OverlayColour.Background2 => background2,
            OverlayColour.Background3 => background3,
            OverlayColour.Background4 => background4,
            OverlayColour.Background5 => background5,
            OverlayColour.Background6 => background6,
            _ => throw new System.ArgumentOutOfRangeException(nameof(colour), colour, null),
        };

        private void updateColours()
        {
            bool wasThemeUpdate = IsThemeUpdate;
            IsThemeUpdate = true;

            try
            {
                // Palette entries are interdependent in most consumers (for
                // example a Content1 callback also applies Background3 and
                // Content2). Publish them only after the complete palette has
                // been calculated so no callback can see a half-updated theme.
                using (Bindable<Framework.Graphics.Colour4>.BeginValueChangeBatch())
                {
                    colour0.Value = getColour(OverlayColour.Colour0, 1, 0.8f);
                    colour1.Value = getColour(OverlayColour.Colour1, 1, 0.7f);
                    colour2.Value = getColour(OverlayColour.Colour2, 0.8f, 0.6f);
                    colour3.Value = getColour(OverlayColour.Colour3, 0.6f, 0.5f);
                    colour4.Value = getColour(OverlayColour.Colour4, 0.4f, 0.3f);
                    highlight1.Value = getColour(OverlayColour.Highlight1, 1, 0.7f);
                    content1.Value = getColour(OverlayColour.Content1, 0.4f, 1);
                    content2.Value = getColour(OverlayColour.Content2, 0.4f, 0.9f);
                    light1.Value = getColour(OverlayColour.Light1, 0.4f, 0.8f);
                    light2.Value = getColour(OverlayColour.Light2, 0.4f, 0.75f);
                    light3.Value = getColour(OverlayColour.Light3, 0.4f, 0.7f);
                    light4.Value = getColour(OverlayColour.Light4, 0.4f, 0.5f);
                    dark1.Value = getColour(OverlayColour.Dark1, 0.2f, 0.35f);
                    dark2.Value = getColour(OverlayColour.Dark2, 0.2f, 0.3f);
                    dark3.Value = getColour(OverlayColour.Dark3, 0.2f, 0.25f);
                    dark4.Value = getColour(OverlayColour.Dark4, 0.2f, 0.2f);
                    dark5.Value = getColour(OverlayColour.Dark5, 0.2f, 0.15f);
                    dark6.Value = getColour(OverlayColour.Dark6, 0.2f, 0.1f);
                    foreground1.Value = getColour(OverlayColour.Foreground1, 0.1f, 0.6f);
                    background1.Value = getColour(OverlayColour.Background1, 0.1f, 0.4f);
                    background2.Value = getColour(OverlayColour.Background2, 0.1f, 0.3f);
                    background3.Value = getColour(OverlayColour.Background3, 0.1f, 0.25f);
                    background4.Value = getColour(OverlayColour.Background4, 0.1f, 0.2f);
                    background5.Value = getColour(OverlayColour.Background5, 0.1f, 0.15f);
                    background6.Value = getColour(OverlayColour.Background6, 0.1f, 0.1f);
                }

                colour0.TriggerChange(propagateToBindings: true);
                colour1.TriggerChange(propagateToBindings: true);
                colour2.TriggerChange(propagateToBindings: true);
                colour3.TriggerChange(propagateToBindings: true);
                colour4.TriggerChange(propagateToBindings: true);
                highlight1.TriggerChange(propagateToBindings: true);
                content1.TriggerChange(propagateToBindings: true);
                content2.TriggerChange(propagateToBindings: true);
                light1.TriggerChange(propagateToBindings: true);
                light2.TriggerChange(propagateToBindings: true);
                light3.TriggerChange(propagateToBindings: true);
                light4.TriggerChange(propagateToBindings: true);
                dark1.TriggerChange(propagateToBindings: true);
                dark2.TriggerChange(propagateToBindings: true);
                dark3.TriggerChange(propagateToBindings: true);
                dark4.TriggerChange(propagateToBindings: true);
                dark5.TriggerChange(propagateToBindings: true);
                dark6.TriggerChange(propagateToBindings: true);
                foreground1.TriggerChange(propagateToBindings: true);
                background1.TriggerChange(propagateToBindings: true);
                background2.TriggerChange(propagateToBindings: true);
                background3.TriggerChange(propagateToBindings: true);
                background4.TriggerChange(propagateToBindings: true);
                background5.TriggerChange(propagateToBindings: true);
                background6.TriggerChange(propagateToBindings: true);

                paletteVersion.Value++;
            }
            finally
            {
                IsThemeUpdate = wasThemeUpdate;
            }
        }

        private Color4 getColour(OverlayColour colour, float saturation, float lightness)
        {
            if (ExemptAccentFromCustomTheme && isAccentToken(colour))
                return calculateBaseColour(CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null ? CustomTheme.Value.BaseTheme : CurrentTheme.Value, saturation, lightness);

            if (CurrentTheme.Value == ThemeMode.Custom && CustomTheme.Value != null)
            {
                if (SurfaceContext.HasValue)
                {
                    var role = MapTokenToRoleForContext(SurfaceContext.Value, colour);
                    if (role.HasValue && CustomTheme.Value.TryGetContextOverride(SurfaceContext.Value, role.Value, out var contextColour))
                        return contextColour;
                }

                if (CustomTheme.Value.TryGetOverride(colour, out var overrideColour))
                    return overrideColour;

                return calculateBaseColour(CustomTheme.Value.BaseTheme, saturation, lightness);
            }

            return calculateBaseColour(CurrentTheme.Value, saturation, lightness);
        }

        private static bool isAccentToken(OverlayColour colour) => colour switch
        {
            OverlayColour.Highlight1 => true,
            OverlayColour.Colour0 => true,
            OverlayColour.Colour1 => true,
            OverlayColour.Colour2 => true,
            OverlayColour.Colour3 => true,
            OverlayColour.Colour4 => true,
            _ => false,
        };

        public static int GetDefaultHueForContext(ThemeSurfaceContext context) => context switch
        {
            ThemeSurfaceContext.ModSelect => OverlayColourScheme.Green.GetHue(),
            ThemeSurfaceContext.Editor => OverlayColourScheme.Aquamarine.GetHue(),
            ThemeSurfaceContext.Results => OverlayColourScheme.Aquamarine.GetHue(),
            ThemeSurfaceContext.SongSelect => OverlayColourScheme.Blue.GetHue(),
            ThemeSurfaceContext.InfiniteGlass => OverlayColourScheme.Blue.GetHue(),
            ThemeSurfaceContext.BeatmapLibrary => OverlayColourScheme.Purple.GetHue(),
            ThemeSurfaceContext.Settings => OverlayColourScheme.Purple.GetHue(),
            ThemeSurfaceContext.TopBar => OverlayColourScheme.Purple.GetHue(),
            ThemeSurfaceContext.Footer => OverlayColourScheme.Blue.GetHue(),
            ThemeSurfaceContext.FooterBack => 330,
            ThemeSurfaceContext.FooterMods => 85,
            ThemeSurfaceContext.FooterRandom => 200,
            ThemeSurfaceContext.FooterOptions => 280,
            _ => OverlayColourScheme.Purple.GetHue()
        };

        public static Color4 CalculateBaseColourForHue(int hue, ThemeMode baseTheme, float saturation, float lightness, bool ignoreLightTheme = false)
        {
            if (ignoreLightTheme && baseTheme == ThemeMode.Light)
                return Framework.Graphics.Colour4.FromHSL(hue / 360f, saturation, lightness);

            if (baseTheme == ThemeMode.Dark)
            {
                saturation = 0;
                if (lightness < 0.5f)
                    lightness *= 0.3f;
            }
            else if (baseTheme == ThemeMode.Light)
            {
                // Backgrounds (low lightness in default) → light/white surfaces
                // Content/text (high lightness in default) → dark readable text
                if (lightness < 0.5f)
                    lightness = 0.91f - lightness * 0.35f;  // 0.1→0.875, 0.2→0.84, 0.3→0.805, 0.4→0.77
                else
                    lightness = (1f - lightness) * 0.55f;   // 0.5→0.275, 0.7→0.165, 0.8→0.11, 1.0→0.0
            }

            return Framework.Graphics.Colour4.FromHSL(hue / 360f, saturation, lightness);
        }

        private Color4 calculateBaseColour(ThemeMode baseTheme, float saturation, float lightness)
            => CalculateBaseColourForHue(Hue, baseTheme, saturation, lightness, IgnoreLightTheme);
    }
}
