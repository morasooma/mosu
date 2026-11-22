using System;
using System.Collections.Generic;

namespace osu.Game.Skinning
{
    /// <summary>Fork-only component settings stored outside upstream layout files.</summary>
    public class SkinComponentSettingsOverride
    {
        public string Container { get; set; } = string.Empty;
        public string Ruleset { get; set; } = "global";
        public int[] Path { get; set; } = Array.Empty<int>();
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }
}
