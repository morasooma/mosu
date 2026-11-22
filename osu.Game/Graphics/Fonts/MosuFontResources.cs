using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Morasooma.Resources;
using osu.Framework.IO.Stores;

namespace osu.Game.Graphics.Fonts
{
    /// <summary>
    /// Keeps saved font names and Typeface values compatible while serving only
    /// licensed Morasooma bitmap assets. No upstream-font byte fallback.
    /// </summary>
    internal sealed class MosuFontResources : IResourceStore<byte[]>
    {
        private readonly ResourceStore<byte[]> source = new ResourceStore<byte[]>();

        public MosuFontResources(IResourceStore<byte[]>? overrides = null)
        {
            if (overrides != null) source.AddStore(overrides);
            source.AddStore(new DllResourceStore(typeof(ResourceAssembly).Assembly));
        }

        internal static readonly IReadOnlyDictionary<string, string> LegacyFaces = createLegacyFaces();

        private static Dictionary<string, string> createLegacyFaces()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string weight in new[] { "Light", "Regular", "SemiBold", "Bold" })
            {
                // Match visible stroke weight, not the unrelated family weight labels.
                string nunitoWeight = weight switch
                {
                    "Regular" => "Medium",
                    "SemiBold" => "Bold",
                    "Bold" => "Black",
                    _ => "Light",
                };
                string roundedWeight = weight switch
                {
                    "Regular" => "SemiBold",
                    "SemiBold" or "Bold" => "Bold",
                    _ => "Light",
                };
                result[$"Fonts/Torus/Torus-{weight}"] = $"Fonts/Nunito/Nunito-{nunitoWeight}";
                result[$"Fonts/Torus-Alternate/Torus-Alternate-{weight}"] = $"Fonts/MosuRoundedAlt/MosuRoundedAlt-{roundedWeight}";
            }
            result["Fonts/Venera/Venera-Light"] = "Fonts/Syncopate/Syncopate-Regular";
            result["Fonts/Venera/Venera-Bold"] = "Fonts/Michroma/Michroma-Regular";
            // Compatibility for the remaining legacy weight, never synthetic bold.
            result["Fonts/Venera/Venera-Black"] = "Fonts/Michroma/Michroma-Regular";
            return result;
        }

        internal static string Resolve(string name)
        {
            foreach (var pair in LegacyFaces)
            {
                if (name.StartsWith(pair.Key, StringComparison.Ordinal)
                    && name.Length > pair.Key.Length
                    && name[pair.Key.Length] is '.' or '_')
                    return pair.Value + name[pair.Key.Length..];
            }
            return name;
        }

        public byte[] Get(string name) => source.Get(Resolve(name));
        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => source.GetAsync(Resolve(name), cancellationToken);
        public Stream GetStream(string name) => source.GetStream(Resolve(name));
        public IEnumerable<string> GetAvailableResources() => source.GetAvailableResources();

        internal IEnumerable<string> GetFontAssets() => source.GetAvailableResources()
            .Where(name => name.StartsWith("Fonts/", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal))
            .Select(name => name[..^4])
            // Default fallback order: broad UI fonts, then symbols/CJK, not display digits.
            .OrderBy(name => name.StartsWith("Fonts/Nunito/", StringComparison.Ordinal) ? 0
                : name.StartsWith("Fonts/MosuRoundedAlt/", StringComparison.Ordinal) ? 1
                : name.StartsWith("Fonts/Inter/", StringComparison.Ordinal) ? 2
                : name.StartsWith("Fonts/Noto/", StringComparison.Ordinal) ? 3 : 4)
            .ThenBy(name => name.EndsWith("-Regular", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(name => name, StringComparer.Ordinal);

        public void Dispose() => source.Dispose();
    }
}
