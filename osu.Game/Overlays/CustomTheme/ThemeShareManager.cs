using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>
    /// Manages secure export, validation, and import of portable theme files.
    /// Enforces size limits, strict schema validation, hex color checks, and safe radius clamping.
    /// Never mutates settings or state directly before confirmation.
    /// </summary>
    public static class ThemeShareManager
    {
        /// <summary>
        /// Maximum allowable theme JSON file size in bytes (256 KB).
        /// </summary>
        public const int MAX_THEME_FILE_SIZE_BYTES = 256 * 1024;

        private static readonly Regex hex_colour_regex = new Regex(@"^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

        /// <summary>
        /// Exports the given custom theme to the user's export storage safely.
        /// Automatically generates a non-colliding filename and wraps the theme in a <see cref="PortableThemePackage"/>.
        /// </summary>
        public static bool TryExportTheme(CustomThemeDefinition theme, Storage exportStorage, string? requestedName, out string savedFileName, out string? errorMessage)
        {
            savedFileName = string.Empty;
            errorMessage = null;

            if (exportStorage == null)
            {
                errorMessage = "Export storage is unavailable.";
                return false;
            }

            try
            {
                string safeThemeName = sanitizeThemeName(requestedName);
                string safeName = sanitizeFileName(safeThemeName);
                if (string.IsNullOrWhiteSpace(safeName))
                    safeName = "morasooma-theme";
                if (safeName.Length > 48)
                    safeName = safeName.Substring(0, 48).TrimEnd('_');

                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseFileName = $"{safeName}_{timeStamp}.json";
                string finalFileName = baseFileName;

                int counter = 1;
                while (exportStorage.Exists(finalFileName))
                {
                    finalFileName = $"{safeName}_{timeStamp}_{counter}.json";
                    counter++;
                }

                var package = new PortableThemePackage
                {
                    Format = PortableThemePackage.FORMAT_MAGIC,
                    FormatVersion = PortableThemePackage.CURRENT_FORMAT_VERSION,
                    Name = safeThemeName,
                    Theme = theme.DeepClone()
                };

                // Unknown extension fields are not part of the public theme format. Do not
                // accidentally ship arbitrary data preserved in the local configuration.
                package.Theme.Extensions = null;
                if (package.Theme.Geometry != null)
                    package.Theme.Geometry.Extensions = null;

                // Ensure exported theme version is current
                package.Theme.Version = CustomThemeDefinition.CURRENT_VERSION;

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 16,
                };

                string json = JsonConvert.SerializeObject(package, settings);

                using (var stream = exportStorage.GetStream(finalFileName, FileAccess.Write, FileMode.CreateNew))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }

                savedFileName = finalFileName;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Strictly validates and parses a theme JSON stream into a <see cref="CustomThemeDefinition"/>.
        /// Accepts both <see cref="PortableThemePackage"/> envelopes and raw <see cref="CustomThemeDefinition"/> JSON.
        /// Enforces payload size limits, schema version constraints, valid enum values, hex color formats, and geometry limits.
        /// Does NOT mutate configuration or global state.
        /// </summary>
        public static bool ValidateAndImport(Stream stream, out CustomThemeDefinition? importedTheme, out string themeName, out LocalisableString errorMessage)
        {
            importedTheme = null;
            themeName = "Custom Theme";
            errorMessage = default;

            if (stream == null)
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3925;
                return false;
            }

            string json;
            try
            {
                // Limit bytes rather than UTF-16 characters and read to EOF: a single
                // StreamReader.Read() can return only its first internal buffer.
                using var buffer = new MemoryStream();
                byte[] chunk = new byte[8192];
                int count;
                while (buffer.Length <= MAX_THEME_FILE_SIZE_BYTES &&
                       (count = stream.Read(chunk, 0, Math.Min(chunk.Length, MAX_THEME_FILE_SIZE_BYTES + 1 - (int)buffer.Length))) > 0)
                {
                    buffer.Write(chunk, 0, count);
                }

                if (buffer.Length > MAX_THEME_FILE_SIZE_BYTES)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3926(MAX_THEME_FILE_SIZE_BYTES / 1024);
                    return false;
                }

                json = new UTF8Encoding(false, true).GetString(buffer.ToArray());
            }
            catch (Exception ex) when (ex is IOException || ex is DecoderFallbackException || ex is NotSupportedException)
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3927(ex.Message);
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3928;
                return false;
            }

            JObject rootObject;
            try
            {
                var token = JToken.Parse(json);
                if (token is not JObject jObj)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3929;
                    return false;
                }
                rootObject = jObj;
            }
            catch (Exception ex)
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3930(ex.Message);
                return false;
            }

            JToken? themeToken;

            // Check if wrapped in PortableThemePackage
            if (rootObject.TryGetValue("format", StringComparison.OrdinalIgnoreCase, out var formatToken))
            {
                // Untrusted metadata may be an array/object or have invalid numeric types.
                // Validate before accessing it; Value<T>() can throw outside the JSON parser.
                if (formatToken?.Type != JTokenType.String || formatToken.Value<string>() != PortableThemePackage.FORMAT_MAGIC)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3931;
                    return false;
                }

                var packageVersionToken = rootObject["formatVersion"];
                if (packageVersionToken?.Type != JTokenType.Integer ||
                    !int.TryParse(packageVersionToken.ToString(), out int packageVersion) || packageVersion < 1 ||
                    packageVersion > PortableThemePackage.CURRENT_FORMAT_VERSION)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3932;
                    return false;
                }

                var nameToken = rootObject["name"];
                if (nameToken != null && nameToken.Type != JTokenType.String && nameToken.Type != JTokenType.Null)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3933;
                    return false;
                }

                themeName = sanitizeThemeName(nameToken?.Value<string>());
                themeToken = rootObject["theme"];

                if (themeToken is not JObject)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3934;
                    return false;
                }
            }
            else if (rootObject.ContainsKey("version") && (rootObject.ContainsKey("overrides") || rootObject.ContainsKey("baseTheme") || rootObject.ContainsKey("geometry")))
            {
                // Raw CustomThemeDefinition JSON
                themeToken = rootObject;
                themeName = "Imported Theme";
            }
            else
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3935;
                return false;
            }

            // Deserialize theme payload with strict settings
            CustomThemeDefinition theme;
            try
            {
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = 16,
                });
                var deserialized = themeToken.ToObject<CustomThemeDefinition>(serializer);
                if (deserialized == null)
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3936;
                    return false;
                }
                theme = deserialized;
            }
            catch (Exception ex)
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3937(ex.Message);
                return false;
            }

            // Schema version invariant: do not silently convert unknown future schema to current
            if (theme.Version > CustomThemeDefinition.CURRENT_VERSION)
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3938(theme.Version, CustomThemeDefinition.CURRENT_VERSION);
                return false;
            }

            // Ensure BaseTheme is safe and not recursive
            if (theme.BaseTheme == ThemeMode.Custom || !Enum.IsDefined(typeof(ThemeMode), theme.BaseTheme))
                theme.BaseTheme = ThemeMode.Default;

            if (theme.Interface?.IsValid() == false)
            {
                errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.J3939;
                return false;
            }

            // Validate global Overrides
            theme.Overrides ??= new Dictionary<OverlayColour, string>();
            foreach (var kv in theme.Overrides)
            {
                if (!Enum.IsDefined(typeof(OverlayColour), kv.Key))
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3940(kv.Key);
                    return false;
                }

                if (string.IsNullOrEmpty(kv.Value) || !hex_colour_regex.IsMatch(kv.Value))
                {
                    errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3941(kv.Key, kv.Value);
                    return false;
                }
            }

            // Validate ContextOverrides
            if (theme.ContextOverrides != null)
            {
                foreach (var contextKv in theme.ContextOverrides)
                {
                    if (!Enum.IsDefined(typeof(ThemeSurfaceContext), contextKv.Key))
                    {
                        errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3942(contextKv.Key);
                        return false;
                    }

                    if (contextKv.Value == null)
                        continue;

                    foreach (var roleKv in contextKv.Value)
                    {
                        if (!Enum.IsDefined(typeof(SurfaceColourRole), roleKv.Key))
                        {
                            errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3943(roleKv.Key, contextKv.Key);
                            return false;
                        }

                        if (string.IsNullOrEmpty(roleKv.Value) || !hex_colour_regex.IsMatch(roleKv.Value))
                        {
                            errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3944(contextKv.Key, roleKv.Key, roleKv.Value);
                            return false;
                        }
                    }
                }
            }

            // Validate ModTypeOverrides
            if (theme.ModTypeOverrides != null)
            {
                foreach (var kv in theme.ModTypeOverrides)
                {
                    if (!Enum.IsDefined(typeof(ModType), kv.Key))
                    {
                        errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3945(kv.Key);
                        return false;
                    }

                    if (string.IsNullOrEmpty(kv.Value) || !hex_colour_regex.IsMatch(kv.Value))
                    {
                        errorMessage = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3946(kv.Key, kv.Value);
                        return false;
                    }
                }
            }

            // Validate and clamp Geometry
            if (theme.Geometry == null)
            {
                theme.Geometry = new ThemeGeometryDefinition();
            }
            else
            {
                // Clamp all radii safely to prevent NaN, Infinity, negative, or extreme values
                if (theme.Geometry.ButtonRadius.HasValue)
                    theme.Geometry.ButtonRadius = sanitizeRadius(GeometryRole.Button, theme.Geometry.ButtonRadius.Value);

                if (theme.Geometry.InputRadius.HasValue)
                    theme.Geometry.InputRadius = sanitizeRadius(GeometryRole.Input, theme.Geometry.InputRadius.Value);

                if (theme.Geometry.CardRadius.HasValue)
                    theme.Geometry.CardRadius = sanitizeRadius(GeometryRole.Card, theme.Geometry.CardRadius.Value);

                if (theme.Geometry.PanelRadius.HasValue)
                    theme.Geometry.PanelRadius = sanitizeRadius(GeometryRole.Panel, theme.Geometry.PanelRadius.Value);

                if (theme.Geometry.PopupRadius.HasValue)
                    theme.Geometry.PopupRadius = sanitizeRadius(GeometryRole.Popup, theme.Geometry.PopupRadius.Value);

                if (theme.Geometry.TopBarRadius.HasValue)
                    theme.Geometry.TopBarRadius = sanitizeTopBarRadius(theme.Geometry.TopBarRadius.Value);

                if (theme.Geometry.TopBarVariant.HasValue && !Enum.IsDefined(typeof(TopBarVariant), theme.Geometry.TopBarVariant.Value))
                    theme.Geometry.TopBarVariant = TopBarVariant.Flat;
            }

            // Do not retain unrecognised extension data from an untrusted file; this could
            // otherwise be re-exported to another user as arbitrary non-theme content.
            theme.Extensions = null;
            theme.Geometry.Extensions = null;

            // Upgrade version to CURRENT_VERSION for in-memory use
            theme.Version = CustomThemeDefinition.CURRENT_VERSION;

            importedTheme = theme;
            return true;
        }

        private static float sanitizeRadius(GeometryRole role, float val)
        {
            if (float.IsNaN(val) || float.IsInfinity(val) || val < 0)
                return 0;

            return ThemeGeometryDefinition.ClampRadius(role, val);
        }

        private static float sanitizeTopBarRadius(float val)
        {
            if (float.IsNaN(val) || float.IsInfinity(val) || val < 0)
                return 0;

            return ThemeGeometryDefinition.ClampTopBarRadius(val);
        }

        private static string sanitizeThemeName(string? rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Custom Theme";

            var clean = new string(rawName.Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(clean))
                return "Custom Theme";

            if (clean.Length > 64)
                clean = clean.Substring(0, 64).TrimEnd();

            return clean;
        }

        private static string sanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            char[] invalids = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalids.Contains(c) ? '_' : c).ToArray()).Trim();
        }
    }
}
