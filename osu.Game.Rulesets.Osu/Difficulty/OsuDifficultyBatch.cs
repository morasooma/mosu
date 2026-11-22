using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    /// <summary>
    /// One-map, sequential calculation lifetime. TD/FL do not alter playable geometry;
    /// Hidden remains part of the key because it changes fade times used by opacity.
    /// Unknown/subclass/custom mods bypass sharing. Nothing is put in persistent caches.
    /// </summary>
    internal sealed class OsuDifficultyBatch
    {
        private readonly IWorkingBeatmap source;
        private readonly Dictionary<string, IBeatmap> prepared = new Dictionary<string, IBeatmap>();
        private readonly Queue<string> order = new Queue<string>();

        public OsuDifficultyBatch(IWorkingBeatmap source) => this.source = source;

        internal static bool SupportsSource(IWorkingBeatmap source)
            => source is WorkingBeatmap working && working.SupportsBatchPreparationReuse
               // Native typed inputs can be returned by the converter by reference. Applying
               // a later mod set would then mutate an earlier cached playable map.
               && source.Beatmap.HitObjects.All(obj => obj is not Objects.OsuHitObject);

        public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!mods.All(supported))
            {
                // A custom converter/mod may mutate shared paths or otherwise violate the
                // ordinary preparation contract. Do not retain maps across that boundary.
                prepared.Clear();
                order.Clear();
                return source.GetPlayableBeatmap(ruleset, mods, cancellationToken);
            }

            // Preserve mod order and every non-default setting (including custom rate and lock).
            string key = JsonConvert.SerializeObject(mods.Where(m => m is not OsuModTouchDevice && m is not OsuModFlashlight)
                                                         .Select(m => new { Type = m.GetType().FullName, Mod = new APIMod(m) }));
            if (prepared.TryGetValue(key, out var map))
                return map;
            map = source.GetPlayableBeatmap(ruleset, mods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (prepared.Count == 16)
                prepared.Remove(order.Dequeue());
            prepared.Add(key, map);
            order.Enqueue(key);
            return map;
        }

        internal static bool CanShare(IEnumerable<Mod> mods) => mods.All(supported);

        private static bool supported(Mod mod)
        {
            Type type = mod.GetType();
            return type == typeof(OsuModTouchDevice) || type == typeof(OsuModFlashlight)
                   || type == typeof(OsuModHidden) || type == typeof(OsuModClassic)
                   || type == typeof(OsuModDoubleTime) || type == typeof(OsuModHalfTime)
                   || type == typeof(OsuModNightcore) || type == typeof(OsuModDaycore)
                   || type == typeof(OsuModEasy) || type == typeof(OsuModHardRock)
                   || type == typeof(ModNoMod);
        }

    }
}
