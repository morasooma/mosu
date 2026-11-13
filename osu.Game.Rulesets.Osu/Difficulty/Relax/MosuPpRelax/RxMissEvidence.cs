// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

/// <summary>Authoritative gameplay evidence for the approved contextual miss rule; absent/incomplete evidence stays flat.</summary>
internal sealed class RxMissEvidence
{
    private readonly Dictionary<HitObject, int> indices = new();
    private readonly Lazy<double[]> loads;
    private readonly Dictionary<(double Time, Type Type), List<(OsuHitObject Note, int Index)>> signatures = new();

    public RxMissEvidence(IBeatmap playable, Mod[] mods)
    {
        int index = 0;
        foreach (var note in playable.HitObjects.OfType<OsuHitObject>().Where(n => n is not Spinner))
        {
            add(note, index++);
        }
        loads = new Lazy<double[]>(() => Mp2Model.NoteLoads(Mp2Model.Evaluate(Mp2Features.Extract(playable, mods), Mp2Parameters.Default)));
    }

    private void add(HitObject obj, int index)
    {
        indices[obj] = index;
        if (obj is OsuHitObject note)
        {
            var key = (note.StartTime, note.GetType());
            if (!signatures.TryGetValue(key, out var list)) signatures[key] = list = new();
            list.Add((note, index));
        }
        foreach (var nested in obj.NestedHitObjects) add(nested, index);
    }

    public double? MissLoad(IReadOnlyList<HitEvent> events, int expectedMisses)
    {
        if (expectedMisses <= 0 || events.Count == 0) return null;
        var missed = events.Where(e => e.Result == HitResult.Miss).ToArray();
        if (missed.Length != expectedMisses) return null;
        var evidence = new List<(int Index, double Distance)>();
        var seen = new HashSet<int>();
        foreach (var hit in missed)
        {
            if (hit.HitObject is not OsuHitObject note || hit.Position is not { } position
                || !float.IsFinite(position.X) || !float.IsFinite(position.Y)) return null;
            if (!indices.TryGetValue(note, out int index))
            {
                // Gameplay and difficulty can use separate instances of the same prepared map.
                if (!signatures.TryGetValue((note.StartTime, note.GetType()), out var candidates)) return null;
                var matches = candidates.Where(x => (x.Note.StackedPosition - note.StackedPosition).Length < 0.01
                    && Math.Abs(x.Note.Radius - note.Radius) < 0.001).Select(x => x.Index).Distinct().ToArray();
                if (matches.Length != 1) return null;
                index = matches[0];
            }
            if (!seen.Add(index)) return null;
            double radius = note.Radius;
            if (!double.IsFinite(radius) || radius <= 0) return null;
            double outside = Math.Max(0, (position - note.StackedPosition).Length / radius - 1);
            evidence.Add((index, outside));
        }
        double[] noteLoads = loads.Value;
        if (evidence.Any(e => e.Index >= noteLoads.Length)) return null;
        return evidence.Sum(e => noteLoads[e.Index] * RxMissWeighting.DistanceFactor(e.Distance));
    }
}
