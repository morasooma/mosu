using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Tracks a range selection alongside the primary group used by the timing settings.
    /// </summary>
    public class ControlPointSelection
    {
        public BindableList<ControlPointGroup> SelectedGroups { get; } = new BindableList<ControlPointGroup>();

        private readonly Bindable<ControlPointGroup?> primary;
        private ControlPointGroup? anchor;
        private bool updating;

        public ControlPointSelection(Bindable<ControlPointGroup?> primary)
        {
            this.primary = primary;
            primary.BindValueChanged(change =>
            {
                if (!updating)
                    replace(change.NewValue == null ? Array.Empty<ControlPointGroup>() : new[] { change.NewValue }, change.NewValue, change.NewValue);
            }, true);
        }

        public void Select(ControlPointGroup group, IEnumerable<ControlPointGroup> groups, bool range = false, bool toggle = false)
        {
            var available = groups.ToArray();
            if (range && anchor != null && available.Contains(anchor))
            {
                double start = Math.Min(anchor.Time, group.Time);
                double end = Math.Max(anchor.Time, group.Time);
                var selected = available.Where(g => g.Time >= start && g.Time <= end);
                replace(toggle ? SelectedGroups.Concat(selected).Distinct().ToArray() : selected.ToArray(), group, anchor);
            }
            else if (toggle)
            {
                var selected = SelectedGroups.ToList();
                if (!selected.Remove(group))
                    selected.Add(group);

                replace(selected, selected.Contains(group) ? group : selected.LastOrDefault(), group);
            }
            else
                replace(new[] { group }, group, group);
        }

        public void Clear() => replace(Array.Empty<ControlPointGroup>(), null, null);

        public void Prune(IEnumerable<ControlPointGroup> groups)
        {
            // Equality compares contents; undo may replace groups with equal but detached instances.
            var available = groups.ToArray();
            var remaining = SelectedGroups.Where(g => available.Any(a => ReferenceEquals(a, g))).ToArray();
            if (remaining.Length != SelectedGroups.Count)
                replace(remaining, remaining.FirstOrDefault(g => ReferenceEquals(g, primary.Value)) ?? remaining.LastOrDefault(), null);
        }

        public void ChangeTime(EditorBeatmap beatmap, double time, bool adjustHitObjects)
        {
            var current = primary.Value;
            if (current == null || !double.IsFinite(time) || time == current.Time)
                return;

            var original = SelectedGroups.ToArray();
            int primaryIndex = Array.IndexOf(original, current);
            int anchorIndex = anchor == null ? -1 : Array.IndexOf(original, anchor);
            if (primaryIndex < 0)
                return;

            bool hasTiming = original.Any(g => g.ControlPoints.OfType<TimingControlPoint>().Any());
            beatmap.BeginChange();
            try
            {
                var moved = TimingSectionAdjustments.MoveControlPointGroups(beatmap, original, time - current.Time, adjustHitObjects);
                replace(moved, moved[primaryIndex], anchorIndex >= 0 ? moved[anchorIndex] : moved[primaryIndex]);
                if (adjustHitObjects && hasTiming)
                    beatmap.UpdateAllHitObjects();
            }
            finally
            {
                beatmap.EndChange();
            }
        }

        private void replace(IEnumerable<ControlPointGroup> groups, ControlPointGroup? current, ControlPointGroup? newAnchor)
        {
            var snapshot = groups.ToArray();
            updating = true;
            try
            {
                SelectedGroups.Clear();
                SelectedGroups.AddRange(snapshot);
                anchor = newAnchor;
                primary.Value = current;
            }
            finally
            {
                updating = false;
            }
        }
    }
}
