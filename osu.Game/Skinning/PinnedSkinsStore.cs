// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Stores the IDs of user-pinned skins outside Realm, avoiding a client database schema change.
    /// </summary>
    public class PinnedSkinsStore
    {
        private const string filename = @"pinned-skins.json";

        private readonly Storage storage;
        private readonly object syncLock = new object();
        private readonly HashSet<Guid> pinnedIds = new HashSet<Guid>();
        private bool loaded;

        public event Action? Changed;

        public PinnedSkinsStore(Storage baseStorage)
        {
            storage = baseStorage.GetStorageForDirectory(@"morasooma");
        }

        public bool IsPinned(Guid skinId)
        {
            ensureLoaded();

            lock (syncLock)
                return pinnedIds.Contains(skinId);
        }

        public bool SetPinned(Guid skinId, bool pinned)
        {
            ensureLoaded();

            Guid[] snapshot;

            lock (syncLock)
            {
                bool changed = pinned ? pinnedIds.Add(skinId) : pinnedIds.Remove(skinId);

                if (!changed)
                    return false;

                snapshot = pinnedIds.OrderBy(id => id).ToArray();
            }

            persist(snapshot);
            Changed?.Invoke();
            return true;
        }

        public IReadOnlyCollection<Guid> GetAllPinned()
        {
            ensureLoaded();

            lock (syncLock)
                return pinnedIds.ToArray();
        }

        private void ensureLoaded()
        {
            if (loaded)
                return;

            lock (syncLock)
            {
                if (loaded)
                    return;

                try
                {
                    if (storage.Exists(filename))
                    {
                        using var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open);
                        using var reader = new StreamReader(stream);

                        foreach (Guid id in JsonConvert.DeserializeObject<Guid[]>(reader.ReadToEnd()) ?? Array.Empty<Guid>())
                            pinnedIds.Add(id);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to load pinned skins; continuing with an empty list");
                    pinnedIds.Clear();
                }

                loaded = true;
            }
        }

        private void persist(IEnumerable<Guid> ids)
        {
            try
            {
                using var stream = storage.CreateFileSafely(filename);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonConvert.SerializeObject(ids));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to save pinned skins");
            }
        }
    }
}
