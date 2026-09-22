// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Platform;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Utils;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Net
{
    /// <summary>
    /// A Dodge World kept in local storage, used by the visual tests and offline previews.
    /// </summary>
    /// <remarks>
    /// Always editable and never rewarded: there is no account state or revision to coordinate with.
    /// </remarks>
    internal sealed class LocalDodgeWorldSession : DodgeWorldSession
    {
        private readonly Storage layoutStorage;
        private readonly Storage textureStorage;

        public LocalDodgeWorldSession(Storage layoutStorage, Storage textureStorage)
        {
            this.layoutStorage = layoutStorage;
            this.textureStorage = textureStorage;
        }

        public override void Load(Action<DodgeWorldDocument> onLoaded)
        {
            SetCanEdit(true);
            SetAvailability(DodgeWorldAvailability.Ready);
            onLoaded(readDocument());
        }

        private DodgeWorldDocument readDocument()
        {
            string path = layoutStorage.GetFullPath(DodgeWorldSerializer.FILENAME);

            if (File.Exists(path))
            {
                try
                {
                    DodgeWorldDocument? stored = DodgeWorldSerializer.Deserialize(File.ReadAllText(path));

                    if (stored?.IsSupported == true)
                        return stored;
                }
                catch (Exception)
                {
                    // An unreadable local file is not worth surfacing; fall through to the legacy
                    // format and then to a fresh world.
                }
            }

            return readLegacyDocument() ?? DefaultWorld.CreateDocument();
        }

        /// <summary>
        /// Rebuilds a world from the retired single-room layout, if one was left behind.
        /// </summary>
        /// <remarks>
        /// The legacy file only stored entity placements, so they are laid over the default room:
        /// a record replaces the default entity sharing its id, and is appended otherwise.
        /// </remarks>
        private DodgeWorldDocument? readLegacyDocument()
        {
            string path = layoutStorage.GetFullPath(LegacyRoomLayout.FILENAME);

            if (!File.Exists(path))
                return null;

            try
            {
                LegacyRoomLayout? layout = JsonConvert.DeserializeObject<LegacyRoomLayout>(File.ReadAllText(path));

                if (layout?.Version != LegacyRoomLayout.SUPPORTED_VERSION)
                    return null;

                DodgeWorldDocument document = DefaultWorld.CreateDocument();
                RoomDefinition room = document.Rooms.Single();

                foreach (EntityRecord legacy in layout.Entities)
                {
                    int existing = room.Entities.FindIndex(entity => entity.Id == legacy.Id);

                    if (existing >= 0)
                        room.Entities[existing] = legacy;
                    else
                        room.Entities.Add(legacy);
                }

                return document;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override void Publish(DodgeWorldDocument document, Action<PublishOutcome> onCompleted)
        {
            try
            {
                File.WriteAllText(layoutStorage.GetFullPath(DodgeWorldSerializer.FILENAME),
                    DodgeWorldSerializer.Serialize(document, indented: true));
                onCompleted(PublishOutcome.Published);
            }
            catch (Exception)
            {
                onCompleted(PublishOutcome.Failed);
            }
        }

        /// <summary>
        /// Warps open for free here and travel is never charged: a local world has no coin balance to
        /// spend, so enforcing a price would only block the author from walking their own world.
        /// </summary>
        public override void UnlockWarp(string roomId, string entityId, Action<WarpOutcome> onCompleted)
        {
            var key = new WarpKey(roomId, entityId);

            if (UnlockedWarps.Value.Contains(key))
            {
                onCompleted(WarpOutcome.AlreadyUnlocked);
                return;
            }

            SetUnlockedWarps(UnlockedWarps.Value.Append(key).ToArray());
            onCompleted(WarpOutcome.Paid);
        }

        public override void TravelToWarp(string roomId, string entityId, Action<WarpOutcome> onCompleted) =>
            onCompleted(UnlockedWarps.Value.Contains(new WarpKey(roomId, entityId))
                ? WarpOutcome.Paid
                : WarpOutcome.NotUnlocked);

        public override void StoreTexture(TextureImport upload, Action<string> onStored, Action onFailed)
        {
            if (!SupportedExtensions.IMAGE_EXTENSIONS.Contains(upload.Extension))
            {
                onFailed();
                return;
            }

            try
            {
                string storedName = $"{upload.Purpose.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{upload.Extension}";

                using (Stream destination = textureStorage.GetStream(storedName, FileAccess.Write, FileMode.Create))
                    destination.Write(upload.Content, 0, upload.Content.Length);

                onStored(storedName);
            }
            catch (Exception)
            {
                onFailed();
            }
        }
    }
}
