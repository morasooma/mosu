using System;
using System.IO;
using System.Linq;

namespace osu.Game.IO
{
    public static class LibraryLocationValidator
    {
        /// <summary>
        /// Detects stable installations, including paths inside them, without modifying the directory.
        /// </summary>
        public static bool IsInsideStableInstallation(string path)
        {
            for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory != null; directory = directory.Parent)
            {
                if (!directory.Exists)
                    continue;

                if (directory.EnumerateFiles().Any(file =>
                        file.Name.Equals("osu!.exe", StringComparison.OrdinalIgnoreCase)
                        || file.Name.Equals("osu!.db", StringComparison.OrdinalIgnoreCase)
                        || (file.Name.StartsWith("osu!.", StringComparison.OrdinalIgnoreCase)
                            && file.Name.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))))
                    return true;
            }

            return false;
        }
    }
}
