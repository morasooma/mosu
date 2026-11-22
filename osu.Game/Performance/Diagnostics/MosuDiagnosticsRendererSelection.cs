using System.Collections.Generic;
using System.Linq;
using osu.Framework.Configuration;

namespace osu.Game.Performance.Diagnostics
{
    /// <summary>
    /// Selects explicit, platform-supported backends for comparison. The original backend runs last
    /// so a successful comparison finishes on the user's original renderer.
    /// </summary>
    public static class MosuDiagnosticsRendererSelection
    {
        public static IEnumerable<RendererType> GetComparisonRenderers(IEnumerable<RendererType> platformRenderers, RendererType originalRenderer)
            => platformRenderers
               .Where(renderer => renderer != RendererType.Automatic)
               .Distinct()
               .OrderBy(renderer => renderer == originalRenderer ? 1 : 0)
               .ThenBy(renderer => renderer);
    }
}
