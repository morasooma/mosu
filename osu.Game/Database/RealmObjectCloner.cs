using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Copies the persisted fields of the explicitly supported Realm models, without a
    /// general-purpose recursive mapper. Per-type limits retain the existing beatmap
    /// detachment behaviour; a hard limit also bounds future model changes.
    /// </summary>
    internal static class RealmObjectCloner
    {
        private const int max_depth = 32;
        private static readonly ConcurrentDictionary<Type, CloneProperty[]> properties = new ConcurrentDictionary<Type, CloneProperty[]>();
        private static readonly MethodInfo copy_list = typeof(RealmObjectCloner).GetMethod(nameof(copyList), BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static T Detach<T>(T source) where T : RealmObjectBase
            => (T)new CloneContext(source is BeatmapSetInfo).Clone(source, 0)!;

        internal static void CopyWritableProperties(RealmObjectBase source, RealmObjectBase destination, params string[] ignored)
        {
            // The write-back path intentionally leaves read-only collections unchanged.
            foreach (var entry in getProperties(source.GetType()))
            {
                var property = entry.Property;
                if (property.SetMethod?.IsPublic == true && !ignored.Contains(property.Name))
                    property.SetValue(destination, property.GetValue(source));
            }
        }

        private static CloneProperty[] getProperties(Type type) => properties.GetOrAdd(type, t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0
                        && !p.IsDefined(typeof(IgnoredAttribute)) && !p.IsDefined(typeof(BacklinkAttribute))
                        && !p.IsDefined(typeof(IgnoreDataMemberAttribute))
                        && (p.SetMethod?.IsPublic == true || isList(p.PropertyType)))
            .Select(p => new CloneProperty(p, isList(p.PropertyType)
                ? copy_list.MakeGenericMethod(p.PropertyType.GetGenericArguments()[0])
                           .CreateDelegate<Action<object, object, CloneContext, int>>()
                : null))
            .ToArray());

        // Resolve generic list dispatch once per persisted property. No MakeGenericMethod,
        // invocation argument array or boxed depth is needed for each cloned collection.
        private sealed record CloneProperty(PropertyInfo Property, Action<object, object, CloneContext, int>? CopyList);

        private static bool isList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>);

        private static RealmObjectBase create(RealmObjectBase source) => source switch
        {
            BeatmapSetInfo => new BeatmapSetInfo(),
            BeatmapInfo => new BeatmapInfo(),
            BeatmapMetadata => new BeatmapMetadata(),
            BeatmapDifficulty => new BeatmapDifficulty(),
            BeatmapUserSettings => new BeatmapUserSettings(),
            BeatmapCollection => new BeatmapCollection(),
            ScoreInfo => new ScoreInfo(),
            SkinInfo => new SkinInfo(),
            RealmKeyBinding binding => RealmKeyBinding.CreateDetached(binding),
            RulesetInfo => new RulesetInfo(),
            RealmUser => new RealmUser(),
            RealmFile => new RealmFile(),
            RealmNamedFileUsage usage => new RealmNamedFileUsage(new RealmFile(), usage.Filename),
            RealmOnlineAsset asset => new RealmOnlineAsset(new RealmFile(), asset.File.Filename),
            _ => throw new NotSupportedException($"No Realm clone defined for {source.GetType().FullName}.")
        };

        private static void copyList<T>(object source, object destination, CloneContext context, int depth)
        {
            var list = (IList<T>)destination;
            list.Clear();
            if (!typeof(RealmObjectBase).IsAssignableFrom(typeof(T)))
            {
                // Scalar Realm collections need no recursive cloning. Typed enumeration
                // also avoids boxing each value in score pause/statistic lists.
                foreach (var value in (IEnumerable<T>)source)
                    list.Add(value);
                return;
            }

            foreach (var value in (IEnumerable<T>)source)
            {
                var cloned = context.Clone(value, depth);
                // Depth-truncated Realm links are omitted rather than inserted as null
                // into Realm collections, which do not permit null object entries.
                if (cloned != null || value == null)
                    list.Add((T)cloned!);
            }
        }

        private sealed class CloneContext
        {
            private readonly bool setRoot;
            private readonly Dictionary<Type, int> typeDepth = new Dictionary<Type, int>();
            // Realm can return a different managed wrapper for the same row. Track active
            // beatmap ancestors by persisted identity, not CLR reference identity. Without
            // this, every sibling difficulty recursively copies its set and all its files.
            private readonly Dictionary<Guid, BeatmapSetInfo> activeSets = new Dictionary<Guid, BeatmapSetInfo>();
            private readonly Dictionary<Guid, BeatmapInfo> activeBeatmaps = new Dictionary<Guid, BeatmapInfo>();

            public CloneContext(bool setRoot)
            {
                this.setRoot = setRoot;
            }

            public object? Clone(object? source, int depth)
            {
                if (source is not RealmObjectBase realmObject)
                    return source;

                if (source is BeatmapSetInfo sourceSet && activeSets.TryGetValue(sourceSet.ID, out var ancestorSet))
                    return ancestorSet;
                if (source is BeatmapInfo sourceBeatmap && activeBeatmaps.TryGetValue(sourceBeatmap.ID, out var ancestorBeatmap))
                    return ancestorBeatmap;

                if (depth >= max_depth)
                    throw new InvalidOperationException("Realm object graph exceeds the safe clone depth.");

                var type = source.GetType();
                typeDepth.TryGetValue(type, out int count);
                int limit = source is BeatmapSetInfo ? 2 : source is BeatmapInfo ? (setRoot ? 1 : 2) : max_depth;
                if (count >= limit)
                    return null;

                typeDepth[type] = count + 1;
                try
                {
                    var result = create(realmObject);
                    if (source is BeatmapInfo remoteInfo)
                        ((BeatmapInfo)result).IsExternalCatalogue = remoteInfo.IsExternalCatalogue;
                    if (source is BeatmapSetInfo s)
                        activeSets.Add(s.ID, (BeatmapSetInfo)result);
                    else if (source is BeatmapInfo b)
                        activeBeatmaps.Add(b.ID, (BeatmapInfo)result);

                    foreach (var entry in getProperties(type))
                    {
                        var property = entry.Property;
                        if (setRoot && source is BeatmapSetInfo && property.Name == nameof(BeatmapSetInfo.Files))
                            continue;
                        if (setRoot && source is BeatmapInfo && property.Name == nameof(BeatmapInfo.BeatmapSet))
                            continue;

                        var value = property.GetValue(source);
                        if (entry.CopyList != null)
                        {
                            if (value != null)
                                entry.CopyList(value, property.GetValue(result)!, this, depth + 1);
                        }
                        else
                            property.SetValue(result, Clone(value, depth + 1));
                    }

                    if (result is BeatmapSetInfo set)
                    {
                        foreach (var beatmap in set.Beatmaps)
                            beatmap.BeatmapSet = set;
                    }
                    else if (result is BeatmapInfo beatmap && beatmap.BeatmapSet != null)
                    {
                        for (int i = 0; i < beatmap.BeatmapSet.Beatmaps.Count; i++)
                        {
                            if (beatmap.BeatmapSet.Beatmaps[i].Equals(beatmap))
                            {
                                beatmap.BeatmapSet.Beatmaps[i] = beatmap;
                                break;
                            }
                        }
                    }

                    return result;
                }
                finally
                {
                    typeDepth[type] = count;
                    if (source is BeatmapSetInfo s)
                        activeSets.Remove(s.ID);
                    else if (source is BeatmapInfo b)
                        activeBeatmaps.Remove(b.ID);
                }
            }
        }
    }
}
