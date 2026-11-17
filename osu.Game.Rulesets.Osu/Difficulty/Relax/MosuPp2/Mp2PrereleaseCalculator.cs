// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2;

/// <summary>Client adapter for the unchanged MosuPp 2 draft previously shown by the laboratory.</summary>
internal static class Mp2PrereleaseCalculator
{
    public const string Version = "2.0.0пререлиз";

    private static readonly ConditionalWeakTable<OsuDifficultyAttributes, PreparedMap> prepared = new();

    public static void Prepare(IBeatmap playable, Mod[] mods, IWorkingBeatmap working, OsuDifficultyAttributes attributes)
        => prepared.AddOrUpdate(attributes, new PreparedMap(playable, mods, working));

    public static void PrepareTimed(IBeatmap playable, Mod[] mods, IWorkingBeatmap working, IEnumerable<OsuDifficultyAttributes> attributes)
    {
        var map = new PreparedMap(playable, mods, working);
        foreach (var attribute in attributes)
            prepared.AddOrUpdate(attribute, map);
    }

    public static OsuPerformanceAttributes CalculatePerformance(ScoreInfo score, OsuDifficultyAttributes attributes)
    {
        if (!prepared.TryGetValue(attributes, out var map))
            throw new InvalidOperationException("Run matching difficulty preparation before MosuPp 2 prerelease performance.");

        int count = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;
        Mp2Difficulty model = map.Get(count);
        int misses = Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Miss));
        double? load = map.MissLoad(model, count, score, misses);
        double total = Mp2Model.Performance(model, Mp2Parameters.Default, score.Accuracy, misses, load);
        if (!double.IsFinite(total) || total < 0)
            throw new InvalidOperationException("MosuPp 2 prerelease produced invalid performance.");

        // The draft has cursor skills, not a tapping-speed component.
        return new OsuPerformanceAttributes { Total = total, Aim = total, EffectiveMissCount = load ?? misses };
    }

    private sealed class PreparedMap
    {
        private readonly IBeatmap playable;
        private readonly Mod[] mods;
        private readonly Lazy<IBeatmap> noMod;
        private readonly Dictionary<int, Mp2Difficulty> models = new();
        private readonly Queue<int> recency = new();

        public PreparedMap(IBeatmap playable, Mod[] mods, IWorkingBeatmap working)
        {
            this.playable = playable;
            this.mods = mods;
            noMod = new Lazy<IBeatmap>(() => working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo,
                mods.Where(m => m is OsuModRelax or OsuModMosuRelax).ToArray()));
        }

        public Mp2Difficulty Get(int count)
        {
            lock (models)
            {
                if (models.TryGetValue(count, out var result))
                    return result;
                var features = Mp2Features.Extract(prefix(playable, count), mods);
                var reference = mods.All(m => m is OsuModRelax or OsuModMosuRelax)
                    ? features : Mp2Features.Extract(prefix(noMod.Value, count), mods.Where(m => m is OsuModRelax or OsuModMosuRelax).ToArray());
                result = Mp2Model.Evaluate(features, Mp2Parameters.Default, noMod: reference);
                // A live counter visits many prefixes; retain only a bounded working set.
                if (models.Count >= 32)
                    models.Remove(recency.Dequeue());
                models.Add(count, result);
                recency.Enqueue(count);
                return result;
            }
        }

        public double? MissLoad(Mp2Difficulty model, int count, ScoreInfo score, int misses)
        {
            if (misses == 0 || score.HitEvents.Count == 0)
                return null;
            var events = score.HitEvents.Where(e => e.Result == HitResult.Miss).ToArray();
            if (events.Length != misses)
                return null;
            var notes = playable.HitObjects.Take(count).OfType<OsuHitObject>().Where(n => n is not Spinner).ToArray();
            var missed = new bool[notes.Length];
            foreach (var hit in events)
            {
                if (hit.HitObject is not OsuHitObject note)
                    return null;
                int[] candidates = Enumerable.Range(0, notes.Length).Where(i => notes[i].GetType() == note.GetType()
                    && notes[i].StartTime == note.StartTime && (notes[i].StackedPosition - note.StackedPosition).Length < 0.01).ToArray();
                if (candidates.Length != 1 || missed[candidates[0]])
                    return null;
                missed[candidates[0]] = true;
            }
            return Mp2Model.MissLoad(model, missed);
        }

        private static IBeatmap prefix(IBeatmap map, int count)
            => count >= map.HitObjects.Count ? map : new Beatmap<HitObject>
            {
                BeatmapInfo = map.BeatmapInfo.Clone(),
                Difficulty = map.Difficulty.Clone(),
                ControlPointInfo = map.ControlPointInfo,
                Breaks = map.Breaks,
                HitObjects = map.HitObjects.Take(Math.Max(0, count)).ToList(),
            };
    }
}
