// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Screens.Play
{
    public static class GameplaySceneAudit
    {
        /// <param name="TypeName">Runtime type name of the drawable.</param>
        /// <param name="TotalCount">Every instance reached by the walk.</param>
        /// <param name="AliveCount">Instances currently alive (within their lifetime).</param>
        /// <param name="PresentCount">
        /// Instances currently present. A non-present composite terminates both the update traversal and
        /// draw-node generation below itself, so this is the count that actually costs per-frame work.
        /// </param>
        /// <param name="MaskingCount">
        /// Present instances with masking enabled. Each pushes and pops a masking region while drawing, forcing
        /// a <c>SetMasking</c> batch flush and therefore an extra draw call.
        /// </param>
        /// <param name="AlwaysPresentInvisibleCount">
        /// Instances held in the traversal by <c>AlwaysPresent</c> while fully transparent -- paying update and
        /// draw-node cost with nothing visible to show for it.
        /// </param>
        public readonly record struct DrawableTypeCount(
            string TypeName,
            int TotalCount,
            int AliveCount,
            int PresentCount,
            int MaskingCount,
            int AlwaysPresentInvisibleCount);

        /// <summary>
        /// Column names matching the field order of <see cref="DrawableTypeCount"/>, for the <c>.scene.csv</c> header.
        /// </summary>
        public const string CSV_COLUMNS = "drawable_type,total_count,alive_count,present_count,masking_count,always_present_invisible_count";

        /// <summary>
        /// Guards against pathological recursion if the audit is ever pointed at a cyclic or proxy-heavy tree.
        /// </summary>
        private const int max_depth = 512;

        public static List<DrawableTypeCount> CountDrawableTypes(Drawable root)
        {
            var counts = new Dictionary<string, Counts>();
            enumerate(root, counts, 0);

            var results = new List<DrawableTypeCount>(counts.Count);

            foreach (var pair in counts)
            {
                results.Add(new DrawableTypeCount(
                    pair.Key,
                    pair.Value.Total,
                    pair.Value.Alive,
                    pair.Value.Present,
                    pair.Value.Masking,
                    pair.Value.AlwaysPresentInvisible));
            }

            results.Sort((a, b) => b.TotalCount.CompareTo(a.TotalCount));
            return results;
        }

        private static void enumerate(Drawable drawable, Dictionary<string, Counts> counts, int depth)
        {
            if (depth > max_depth)
                return;

            recordDrawable(drawable, counts);

            if (drawable is not CompositeDrawable composite)
                return;

            var children = getChildren(composite);

            for (int i = 0; i < children.Count; i++)
                enumerate(children[i], counts, depth + 1);
        }

        /// <summary>
        /// Returns the internal children of <paramref name="composite"/>.
        /// </summary>
        /// <remarks>
        /// This walk previously descended only into <see cref="Container"/>, via its public <c>Children</c>
        /// property. That skipped every plain <see cref="CompositeDrawable"/> -- which includes
        /// <c>DrawableHitObject</c> and most skin pieces -- leaving the busiest part of the gameplay scene
        /// invisible to the audit and its counts a large undercount.
        ///
        /// <c>CompositeDrawable.InternalChildren</c> is <c>protected internal</c> and osu.Game is not a friend
        /// assembly, so it is reached reflectively. The audit runs once per benchmark run, so the reflection cost
        /// is irrelevant, and this avoids widening framework visibility purely for diagnostics.
        /// </remarks>
        private static IReadOnlyList<Drawable> getChildren(CompositeDrawable composite)
        {
            // Container exposes the same list publicly; prefer it and skip reflection where possible.
            if (composite is Container container)
                return container.Children;

            if (internal_children_property == null)
                return Array.Empty<Drawable>();

            try
            {
                return internal_children_property.GetValue(composite) as IReadOnlyList<Drawable> ?? Array.Empty<Drawable>();
            }
            catch (Exception)
            {
                return Array.Empty<Drawable>();
            }
        }

        private static readonly PropertyInfo? internal_children_property =
            typeof(CompositeDrawable).GetProperty("InternalChildren", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void recordDrawable(Drawable drawable, Dictionary<string, Counts> counts)
        {
            string typeName = drawable.GetType().Name;

            counts.TryGetValue(typeName, out var entry);

            entry.Total++;

            if (drawable.IsAlive)
                entry.Alive++;

            if (drawable.IsPresent)
            {
                entry.Present++;

                if (drawable is CompositeDrawable { Masking: true })
                    entry.Masking++;

                if (drawable.AlwaysPresent && drawable.Alpha <= 0)
                    entry.AlwaysPresentInvisible++;
            }

            counts[typeName] = entry;
        }

        private struct Counts
        {
            public int Total;
            public int Alive;
            public int Present;
            public int Masking;
            public int AlwaysPresentInvisible;
        }
    }
}
