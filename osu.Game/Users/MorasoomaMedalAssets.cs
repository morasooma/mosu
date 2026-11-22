using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Stores;

namespace osu.Game.Users
{
    /// <summary>
    /// Resolves reviewed medals to bundled art, including historical notifications with ppy URLs.
    /// Unknown/custom medals keep their explicit server-provided cover; no upstream fallback is invented.
    /// </summary>
    public static class MorasoomaMedalAssets
    {
        private static readonly Lazy<HashSet<string>> slugs = new Lazy<HashSet<string>>(() =>
        {
            using var resources = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var manifest = JObject.Parse(Encoding.UTF8.GetString(resources.Get("medal-manifest.json")));
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var medal in (JArray)manifest["medals"]!)
                result.Add((string)medal["slug"]!);
            return result;
        });

        public static string? Resolve(string? slug, string? coverUrl = null)
        {
            if (!string.IsNullOrEmpty(slug) && slugs.Value.Contains(slug))
                return $"Medals/{slug}@2x.png";

            return string.IsNullOrWhiteSpace(coverUrl) ? null : coverUrl;
        }
    }
}
