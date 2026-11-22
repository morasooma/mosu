using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Localisation;

namespace osu.Game.Screens.Select
{
    /// <summary>A fixed selection of whole sets. Never expands a matching difficulty to its entire set.</summary>
    public sealed class BeatmapGroupDeletion
    {
        public IReadOnlyList<Entry> Entries { get; }

        public BeatmapGroupDeletion(IEnumerable<BeatmapInfo> groupBeatmaps, IReadOnlyDictionary<Guid, BeatmapSetInfo>? fullSets = null)
        {
            var maps = groupBeatmaps.DistinctBy(b => b.ID).ToArray();
            var ids = maps.Select(b => b.ID).ToHashSet();
            Entries = maps.Where(b => b.BeatmapSet != null).Select(b => b.BeatmapSet!).DistinctBy(s => s.ID)
                          .OrderBy(s => s.Metadata.Artist).ThenBy(s => s.Metadata.Title)
                          .Select(s => new Entry(fullSets?.GetValueOrDefault(s.ID) ?? s, ids)).ToArray();
        }

        /// <summary>Revalidates the live database inside the same transaction as the existing soft delete.</summary>
        internal static int Delete(ModelManager<BeatmapSetInfo> manager, IEnumerable<Entry> entries, IReadOnlySet<Guid> currentGroup)
        {
            int deleted = 0;
            foreach (var entry in entries)
            {
                if (entry.CanDelete && !entry.Keep.Value && entry.Difficulties.Keys.All(currentGroup.Contains)
                    && manager.DeleteIf(entry.Set, entry.Matches))
                    deleted++;
            }
            return deleted;
        }

        public sealed class Entry
        {
            public BeatmapSetInfo Set { get; }
            public BindableBool Keep { get; } = new BindableBool();
            public LocalisableString Reason { get; }
            public bool CanDelete { get; }
            internal IReadOnlyDictionary<Guid, string> Difficulties { get; }
            private readonly string setHash;

            internal Entry(BeatmapSetInfo set, IReadOnlySet<Guid> groupIds)
            {
                Set = set;
                Difficulties = set.Beatmaps.ToDictionary(b => b.ID, b => b.Hash);
                setHash = set.Hash;
                Reason = set.Beatmaps.Any(b => !groupIds.Contains(b.ID)) ? BeatmapGroupDeletionStrings.Partial
                    : set.Protected ? BeatmapGroupDeletionStrings.Protected
                    : set.Beatmaps.Any(b => StablePathManager.IsStableBeatmap(b.ID) || b.IsExternalCatalogue) ? BeatmapGroupDeletionStrings.ReadOnly
                    : set.DeletePending ? BeatmapGroupDeletionStrings.Unavailable : default;
                CanDelete = Reason == default && Difficulties.Count > 0;
                if (!CanDelete)
                {
                    Keep.Value = true;
                    Keep.Disabled = true;
                }
            }

            internal bool Matches(BeatmapSetInfo current) => !current.Protected && !current.DeletePending && current.Hash == setHash
                && current.Beatmaps.Count == Difficulties.Count
                && current.Beatmaps.All(b => !StablePathManager.IsStableBeatmap(b.ID) && !b.IsExternalCatalogue
                    && Difficulties.TryGetValue(b.ID, out string? hash) && hash == b.Hash);
        }
    }
}
