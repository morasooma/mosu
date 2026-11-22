using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Major surface or screen contexts in Morasooma that support scoped semantic color overrides.
    /// </summary>
    public enum ThemeSurfaceContext
    {
        SongSelect,
        ModSelect,
        Editor,
        BeatmapLibrary,
        Settings,
        InfiniteGlass,
        TopBar,
        Results,
        Footer,
        FooterBack,
        FooterMods,
        FooterRandom,
        FooterOptions,
    }

    /// <summary>
    /// Semantic color roles within a specific surface context.
    /// </summary>
    public enum SurfaceColourRole
    {
        SurfaceBackground,   // Primary container/panel surface
        SurfaceElevated,     // Elevated cards, columns, menus
        Accent,              // Local highlight / active item tint
        TextPrimary,         // Main titles, active labels
        TextSecondary,       // Subtitles, metadata, inactive icons
        ControlIdle,         // Base button/control surfaces, unselected tools
        ControlHover,        // Hover highlights, flyouts, secondary tabs
        ControlSelected,     // Active/selected tool buttons, table rows
        HeaderBackground,    // Header container / underlay
        Backdrop,            // Full-screen overlay dim / backdrop
    }

    /// <summary>
    /// Represents the persistent configuration of a custom color theme in Morasooma.
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class CustomThemeDefinition : IEquatable<CustomThemeDefinition>
    {
        public const int CURRENT_VERSION = 7;

        /// <summary>
        /// Schema version for migration compatibility.
        /// </summary>
        [JsonProperty("version")]
        public int Version { get; set; } = CURRENT_VERSION;

        /// <summary>
        /// The built-in base theme mode (<see cref="ThemeMode.Default"/>, <see cref="ThemeMode.Light"/>, or <see cref="ThemeMode.Dark"/>)
        /// used for fallback and contrast polarity.
        /// </summary>
        [JsonProperty("baseTheme")]
        public ThemeMode BaseTheme { get; set; } = ThemeMode.Default;

        /// <summary>
        /// Map of semantic <see cref="OverlayColour"/> roles to hex color codes (e.g. "#RRGGBB" or "#RRGGBBAA").
        /// </summary>
        [JsonProperty("overrides")]
        public Dictionary<OverlayColour, string> Overrides { get; set; } = new Dictionary<OverlayColour, string>();

        /// <summary>
        /// Scoped overrides per screen/surface context.
        /// </summary>
        [JsonProperty("contextOverrides", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<ThemeSurfaceContext, Dictionary<SurfaceColourRole, string>> ContextOverrides { get; set; }
            = new Dictionary<ThemeSurfaceContext, Dictionary<SurfaceColourRole, string>>();

        /// <summary>
        /// Cosmetic color overrides per ModType in the mod select overlay.
        /// </summary>
        [JsonProperty("modTypeOverrides", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<ModType, string> ModTypeOverrides { get; set; }
            = new Dictionary<ModType, string>();

        /// <summary>
        /// Sparse geometry and corner radius overrides for this theme.
        /// </summary>
        [JsonProperty("geometry")]
        public ThemeGeometryDefinition Geometry { get; set; } = new ThemeGeometryDefinition();

        [JsonProperty("interface", NullValueHandling = NullValueHandling.Ignore)]
        public ThemeInterfaceSettings? Interface { get; set; }

        /// <summary>
        /// Extension fields preserved for future features (e.g. geometry, radii, layout, profiles).
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Extensions { get; set; }

        public CustomThemeDefinition()
        {
        }

        public CustomThemeDefinition(ThemeMode baseTheme)
        {
            BaseTheme = baseTheme == ThemeMode.Custom ? ThemeMode.Default : baseTheme;
        }

        /// <summary>
        /// Tries to resolve a semantic color override.
        /// </summary>
        /// <param name="token">The semantic role.</param>
        /// <param name="colour">The resolved color if overridden and valid.</param>
        /// <returns>True if overridden and parsed successfully; false to inherit from base theme.</returns>
        public bool TryGetOverride(OverlayColour token, out Colour4 colour)
        {
            colour = default;

            if (Overrides.TryGetValue(token, out string? hex) && TryParseHex(hex, out colour))
                return true;

            return false;
        }

        /// <summary>
        /// Sets or replaces an override for a semantic role.
        /// </summary>
        public void SetOverride(OverlayColour token, Colour4 colour)
        {
            Overrides[token] = colour.ToHex();
        }

        /// <summary>
        /// Clears an override for a semantic role, reverting it to inherit from the base theme.
        /// </summary>
        public bool ClearOverride(OverlayColour token)
        {
            return Overrides.Remove(token);
        }

        /// <summary>
        /// Tries to resolve a context-specific color override.
        /// </summary>
        public bool TryGetContextOverride(ThemeSurfaceContext context, SurfaceColourRole role, out Colour4 colour)
        {
            colour = default;

            if (ContextOverrides != null &&
                ContextOverrides.TryGetValue(context, out var roleDict) &&
                roleDict != null &&
                roleDict.TryGetValue(role, out string? hex) &&
                TryParseHex(hex, out colour))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets or replaces a context-specific color override.
        /// </summary>
        public void SetContextOverride(ThemeSurfaceContext context, SurfaceColourRole role, Colour4 colour)
        {
            ContextOverrides ??= new Dictionary<ThemeSurfaceContext, Dictionary<SurfaceColourRole, string>>();

            if (!ContextOverrides.TryGetValue(context, out var roleDict) || roleDict == null)
            {
                roleDict = new Dictionary<SurfaceColourRole, string>();
                ContextOverrides[context] = roleDict;
            }

            roleDict[role] = colour.ToHex();
        }

        /// <summary>
        /// Clears a context-specific color override, reverting it to inherit from global tokens or base theme.
        /// </summary>
        public bool ClearContextOverride(ThemeSurfaceContext context, SurfaceColourRole role)
        {
            if (ContextOverrides != null && ContextOverrides.TryGetValue(context, out var roleDict) && roleDict != null)
            {
                bool removed = roleDict.Remove(role);
                if (roleDict.Count == 0)
                    ContextOverrides.Remove(context);
                return removed;
            }

            return false;
        }

        /// <summary>
        /// Clears all overrides for a specific surface context.
        /// </summary>
        public bool ClearContextOverrides(ThemeSurfaceContext context)
        {
            return ContextOverrides?.Remove(context) ?? false;
        }

        /// <summary>
        /// Clears all contextual color overrides for the footer strip and footer buttons.
        /// </summary>
        public void ClearFooterOverrides()
        {
            ClearContextOverrides(ThemeSurfaceContext.Footer);
            ClearContextOverrides(ThemeSurfaceContext.FooterBack);
            ClearContextOverrides(ThemeSurfaceContext.FooterMods);
            ClearContextOverrides(ThemeSurfaceContext.FooterRandom);
            ClearContextOverrides(ThemeSurfaceContext.FooterOptions);
        }

        /// <summary>
        /// Tries to resolve a cosmetic color override for a specific mod type.
        /// </summary>
        public bool TryGetModTypeOverride(ModType modType, out Colour4 colour)
        {
            colour = default;

            if (ModTypeOverrides != null && ModTypeOverrides.TryGetValue(modType, out string? hex) && TryParseHex(hex, out colour))
                return true;

            return false;
        }

        /// <summary>
        /// Sets or replaces a cosmetic color override for a specific mod type.
        /// </summary>
        public void SetModTypeOverride(ModType modType, Colour4 colour)
        {
            ModTypeOverrides ??= new Dictionary<ModType, string>();
            ModTypeOverrides[modType] = colour.ToHex();
        }

        /// <summary>
        /// Clears a cosmetic color override for a specific mod type, restoring default inheritance.
        /// </summary>
        public bool ClearModTypeOverride(ModType modType)
        {
            if (ModTypeOverrides == null)
                return false;

            return ModTypeOverrides.Remove(modType);
        }

        /// <summary>
        /// Clears all cosmetic mod type color overrides.
        /// </summary>
        public void ClearModTypeOverrides()
        {
            ModTypeOverrides?.Clear();
        }

        /// <summary>
        /// Clears all overrides (both global and context-specific), restoring complete inheritance from the base theme.
        /// Does not affect geometry settings.
        /// </summary>
        public void ClearAllOverrides()
        {
            Overrides.Clear();
            ContextOverrides?.Clear();
            ModTypeOverrides?.Clear();
        }

        /// <summary>
        /// Tries to resolve a custom corner radius override for the given role.
        /// </summary>
        public bool TryGetRadius(GeometryRole role, out float radius)
        {
            if (Geometry == null)
            {
                radius = default;
                return false;
            }

            return Geometry.TryGetRadius(role, out radius);
        }

        /// <summary>
        /// Sets or replaces an override for a semantic geometry role.
        /// </summary>
        public void SetRadius(GeometryRole role, float radius)
        {
            Geometry ??= new ThemeGeometryDefinition();
            Geometry.SetRadius(role, radius);
        }

        /// <summary>
        /// Clears an override for a semantic geometry role, reverting it to inherited default.
        /// </summary>
        public bool ClearRadius(GeometryRole role) => Geometry?.ClearRadius(role) ?? false;

        /// <summary>
        /// Clears all corner radius overrides without resetting the top-bar layout variant.
        /// </summary>
        public void ClearAllRadii() => Geometry?.ClearAllRadii();

        /// <summary>
        /// Visual shape and layout variant of the main top bar.
        /// Defaults to <see cref="TopBarVariant.Flat"/> if not configured.
        /// </summary>
        public TopBarVariant TopBarVariant
        {
            get => Geometry?.TopBarVariant ?? TopBarVariant.Flat;
            set
            {
                Geometry ??= new ThemeGeometryDefinition();
                Geometry.TopBarVariant = value;
            }
        }

        /// <summary>
        /// Whether subtle visual gradients are enabled on buttons and the bottom footer bar.
        /// Defaults to true (enabled). Setting to false enforces flat styling.
        /// </summary>
        public bool EnableGradients
        {
            get => Geometry?.EnableGradients ?? true;
            set
            {
                Geometry ??= new ThemeGeometryDefinition();
                Geometry.EnableGradients = value;
            }
        }

        /// <summary>
        /// Explicit corner radius for the top bar (used in rounded and floating styles).
        /// If null, inherits from the semantic panel radius.
        /// </summary>
        public float? TopBarRadius
        {
            get => Geometry?.TopBarRadius;
            set
            {
                Geometry ??= new ThemeGeometryDefinition();
                Geometry.TopBarRadius = value.HasValue ? ThemeGeometryDefinition.ClampTopBarRadius(value.Value) : null;
            }
        }

        /// <summary>
        /// Safely parses a hex color string without throwing exceptions.
        /// </summary>
        public static bool TryParseHex(string? hex, out Colour4 colour)
        {
            colour = default;

            if (string.IsNullOrWhiteSpace(hex))
                return false;

            try
            {
                colour = Colour4.FromHex(hex.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Serializes this definition to a single-line JSON string safe for storage.
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        /// <summary>
        /// Deserializes a JSON string into a <see cref="CustomThemeDefinition"/>.
        /// Returns a default instance if null, empty, corrupt, or invalid.
        /// </summary>
        public static CustomThemeDefinition FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new CustomThemeDefinition();

            try
            {
                var def = JsonConvert.DeserializeObject<CustomThemeDefinition>(json);

                if (def == null)
                    return new CustomThemeDefinition();

                // Upgrade in-memory version to CURRENT_VERSION so reserializing persists latest schema
                def.Version = CURRENT_VERSION;

                // Prevent recursive / self-referential base theme
                if (def.BaseTheme == ThemeMode.Custom)
                    def.BaseTheme = ThemeMode.Default;

                if (def.Overrides == null)
                    def.Overrides = new Dictionary<OverlayColour, string>();

                if (def.ContextOverrides == null)
                    def.ContextOverrides = new Dictionary<ThemeSurfaceContext, Dictionary<SurfaceColourRole, string>>();
                else
                {
                    // Clean up any null inner dictionaries from hand-edited or malformed JSON
                    List<ThemeSurfaceContext>? nullKeys = null;
                    foreach (var kv in def.ContextOverrides)
                    {
                        if (kv.Value == null)
                        {
                            nullKeys ??= new List<ThemeSurfaceContext>();
                            nullKeys.Add(kv.Key);
                        }
                    }

                    if (nullKeys != null)
                    {
                        foreach (var key in nullKeys)
                            def.ContextOverrides.Remove(key);
                    }
                }

                if (def.Geometry == null)
                    def.Geometry = new ThemeGeometryDefinition();

                if (def.ModTypeOverrides == null)
                    def.ModTypeOverrides = new Dictionary<ModType, string>();

                if (def.Interface?.IsValid() == false)
                    def.Interface = null;

                return def;
            }
            catch
            {
                // Fallback gracefully on corrupt or malformed JSON
                return new CustomThemeDefinition();
            }
        }

        /// <summary>
        /// Creates a deep clone of this definition.
        /// </summary>
        public CustomThemeDefinition DeepClone()
        {
            var clone = new CustomThemeDefinition
            {
                Version = Version,
                BaseTheme = BaseTheme,
                Overrides = new Dictionary<OverlayColour, string>(Overrides),
                Geometry = Geometry?.DeepClone() ?? new ThemeGeometryDefinition(),
                Interface = Interface?.DeepClone(),
                ContextOverrides = new Dictionary<ThemeSurfaceContext, Dictionary<SurfaceColourRole, string>>(),
            };

            if (ContextOverrides != null)
            {
                foreach (var kv in ContextOverrides)
                {
                    if (kv.Value != null)
                        clone.ContextOverrides[kv.Key] = new Dictionary<SurfaceColourRole, string>(kv.Value);
                }
            }

            if (ModTypeOverrides != null)
                clone.ModTypeOverrides = new Dictionary<ModType, string>(ModTypeOverrides);

            if (Extensions != null)
                clone.Extensions = new Dictionary<string, JToken>(Extensions);

            return clone;
        }

        public bool Equals(CustomThemeDefinition? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            if (Version != other.Version || BaseTheme != other.BaseTheme)
                return false;

            if (!EqualityComparer<ThemeGeometryDefinition>.Default.Equals(Geometry, other.Geometry))
                return false;

            if (JsonConvert.SerializeObject(Interface) != JsonConvert.SerializeObject(other.Interface))
                return false;

            if (Overrides.Count != other.Overrides.Count)
                return false;

            foreach (var kv in Overrides)
            {
                if (!other.Overrides.TryGetValue(kv.Key, out var otherHex) || kv.Value != otherHex)
                    return false;
            }

            if (ContextOverrides == null != (other.ContextOverrides == null))
                return false;

            if (ContextOverrides != null && other.ContextOverrides != null)
            {
                if (ContextOverrides.Count != other.ContextOverrides.Count)
                    return false;

                foreach (var outerKv in ContextOverrides)
                {
                    if (!other.ContextOverrides.TryGetValue(outerKv.Key, out var otherRoleDict))
                        return false;

                    if (outerKv.Value == null || otherRoleDict == null)
                    {
                        if (outerKv.Value != otherRoleDict)
                            return false;
                        continue;
                    }

                    if (outerKv.Value.Count != otherRoleDict.Count)
                        return false;

                    foreach (var innerKv in outerKv.Value)
                    {
                        if (!otherRoleDict.TryGetValue(innerKv.Key, out var otherHex) || innerKv.Value != otherHex)
                            return false;
                    }
                }
            }

            if (ModTypeOverrides == null != (other.ModTypeOverrides == null))
                return false;

            if (ModTypeOverrides != null && other.ModTypeOverrides != null)
            {
                if (ModTypeOverrides.Count != other.ModTypeOverrides.Count)
                    return false;

                foreach (var kv in ModTypeOverrides)
                {
                    if (!other.ModTypeOverrides.TryGetValue(kv.Key, out var otherHex) || kv.Value != otherHex)
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is CustomThemeDefinition other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Version);
            hash.Add((int)BaseTheme);
            hash.Add(Geometry);
            hash.Add(JsonConvert.SerializeObject(Interface));

            foreach (var kv in Overrides)
            {
                hash.Add(kv.Key);
                hash.Add(kv.Value);
            }

            if (ContextOverrides != null)
            {
                foreach (var outerKv in ContextOverrides)
                {
                    hash.Add(outerKv.Key);
                    if (outerKv.Value != null)
                    {
                        foreach (var innerKv in outerKv.Value)
                        {
                            hash.Add(innerKv.Key);
                            hash.Add(innerKv.Value);
                        }
                    }
                }
            }

            if (ModTypeOverrides != null)
            {
                foreach (var kv in ModTypeOverrides)
                {
                    hash.Add(kv.Key);
                    hash.Add(kv.Value);
                }
            }

            return hash.ToHashCode();
        }
    }
}
