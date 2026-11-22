using System.Collections.Generic;

namespace osu.Framework.Platform.SDL3
{
    internal static class SDL3DisplaySelection
    {
        // Display indices are positions in a changing SDL list, not persistent identities.
        public static Display? Resolve(Display requested, IReadOnlyList<Display> available, int? windowDisplayIndex)
        {
            foreach (var display in available)
            {
                if (display.Name == requested.Name && display.Bounds == requested.Bounds)
                    return display;
            }

            if (!string.IsNullOrEmpty(requested.Name))
            {
                foreach (var display in available)
                {
                    if (display.Name == requested.Name)
                        return display;
                }
            }

            foreach (var display in available)
            {
                if (display.Index == windowDisplayIndex)
                    return display;
            }

            return available.Count > 0 ? available[0] : null;
        }
    }
}
