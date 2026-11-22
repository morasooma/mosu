using System.Linq;

namespace osu.Game.Scoring.Render
{
    internal static class ReplayRenderFileName
    {
        public static string SanitizeTitle(string title)
        {
            // Linux may render onto NTFS with windows_names, so use portable filename rules.
            return string.Concat(title.Select(c => c < ' ' || "<>:\"/\\|?*".Contains(c) ? '_' : c));
        }
    }
}
