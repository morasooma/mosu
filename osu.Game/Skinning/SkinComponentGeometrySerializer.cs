using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets;

namespace osu.Game.Skinning
{
    /// <summary>Persists fork geometry in mosu-skin-settings.json; layout snapshots still carry it for undo and clipboard.</summary>
    public static class SkinComponentGeometrySerializer
    {
        public static void Store(MosuSkinSettings settings, GlobalSkinnableContainers container, RulesetInfo? ruleset, IReadOnlyList<SerialisedDrawableInfo> infos)
        {
            Clear(settings, container, ruleset);
            storeRecursive(settings, container, ruleset, infos, Array.Empty<int>());
        }

        public static void Clear(MosuSkinSettings settings, GlobalSkinnableContainers container, RulesetInfo? ruleset)
        {
            settings.ComponentGeometry.RemoveAll(o => o.Container == container.ToString() && o.Ruleset == (ruleset?.ShortName ?? "global"));
            settings.ComponentSettings.RemoveAll(o => o.Container == container.ToString() && o.Ruleset == (ruleset?.ShortName ?? "global"));
        }

        private static void storeRecursive(MosuSkinSettings settings, GlobalSkinnableContainers container, RulesetInfo? ruleset,
                                           IReadOnlyList<SerialisedDrawableInfo> infos, int[] prefix)
        {
            for (int i = 0; i < infos.Count; i++)
            {
                var path = prefix.Append(i).ToArray();
                // Font overrides use the existing mosu-skin-fonts.json sidecar.
                infos[i].Settings.Remove(SkinComponentFont.SERIALISATION_KEY);
                if (infos[i].Settings.Remove(SkinComponentGeometry.SERIALISATION_KEY, out var value))
                    settings.ComponentGeometry.Add(new SkinComponentGeometryOverride
                    {
                        Container = container.ToString(),
                        Ruleset = ruleset?.ShortName ?? "global",
                        Path = path,
                        Radius = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture),
                    });
                var custom = infos[i].Settings.Where(kv => kv.Key.StartsWith("mosu_leaderboard_", StringComparison.Ordinal)).ToDictionary(kv => kv.Key, kv => kv.Value);
                if (custom.Count > 0)
                {
                    settings.ComponentSettings.Add(new SkinComponentSettingsOverride
                    {
                        Container = container.ToString(), Ruleset = ruleset?.ShortName ?? "global", Path = path, Settings = custom,
                    });
                    foreach (string key in custom.Keys) infos[i].Settings.Remove(key);
                }
                storeRecursive(settings, container, ruleset, infos[i].Children, path);
            }
        }

        public static void Apply(MosuSkinSettings settings, GlobalSkinnableContainers container, RulesetInfo? ruleset, IReadOnlyList<Drawable> drawables)
            => applyRecursive(settings, container, ruleset, drawables, Array.Empty<int>());

        private static void applyRecursive(MosuSkinSettings settings, GlobalSkinnableContainers container, RulesetInfo? ruleset,
                                           IReadOnlyList<Drawable> drawables, int[] prefix)
        {
            for (int i = 0; i < drawables.Count; i++)
            {
                var path = prefix.Append(i).ToArray();
                var geometry = settings.ComponentGeometry.FirstOrDefault(o => o.Container == container.ToString()
                    && o.Ruleset == (ruleset?.ShortName ?? "global") && o.Path != null && o.Path.SequenceEqual(path));
                if (geometry != null) SkinComponentGeometry.Restore(drawables[i], geometry.Radius);
                var custom = settings.ComponentSettings.FirstOrDefault(o => o.Container == container.ToString()
                    && o.Ruleset == (ruleset?.ShortName ?? "global") && o.Path != null && o.Path.SequenceEqual(path));
                if (custom != null)
                {
                    var info = drawables[i].CreateSerialisedInfo();
                    foreach (var entry in custom.Settings.Where(kv => kv.Key.StartsWith("mosu_leaderboard_", StringComparison.Ordinal)))
                        info.Settings[entry.Key] = entry.Value;
                    drawables[i].ApplySerialisedInfo(info);
                }
                if (drawables[i] is Container composite)
                    applyRecursive(settings, container, ruleset, composite.Children.OfType<ISerialisableDrawable>().Cast<Drawable>().ToArray(), path);
            }
        }
    }

    public class SkinComponentGeometryOverride
    {
        public string Container { get; set; } = string.Empty;
        public string Ruleset { get; set; } = "global";
        public int[] Path { get; set; } = Array.Empty<int>();
        public float Radius { get; set; }
    }
}
