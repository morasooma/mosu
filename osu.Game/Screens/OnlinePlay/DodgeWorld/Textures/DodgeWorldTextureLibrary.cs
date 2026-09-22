// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osu.Game.Utils;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Textures
{
    /// <summary>
    /// Owns every texture the world can reference: the browser of available images, the stores they
    /// are resolved through, and the sword skin currently in use.
    /// </summary>
    internal sealed partial class DodgeWorldTextureLibrary : CompositeDrawable
    {
        /// <summary>
        /// Raised when the set of available textures changes, so the browser can be rebuilt.
        /// </summary>
        public event Action? Changed;

        public string? SelectedAssetId { get; private set; }

        public WeaponSkin ActiveSkin { get; private set; } = WeaponSkin.CreateFallback();

        /// <summary>
        /// Whether the active skin failed to resolve and the bundled sword is standing in for it.
        /// </summary>
        public bool ActiveSkinUsesFallback { get; private set; } = true;

        public IReadOnlyDictionary<string, TextureLibraryEntry> Entries => entries;

        private readonly Dictionary<string, TextureLibraryEntry> entries = new Dictionary<string, TextureLibraryEntry>();

        /// <summary>
        /// One store per way of loading an image, since filtering and wrapping are both fixed at load
        /// time. A repeating texture cannot live in a shared atlas, which is why tiling needs its own.
        /// </summary>
        private readonly Dictionary<TextureVariant, TextureStore> stores = new Dictionary<TextureVariant, TextureStore>();

        /// <summary>
        /// In-flight and completed loads, keyed by variant and path so that one image resolved two
        /// ways does not collide.
        /// </summary>
        private readonly Dictionary<string, Task<Texture?>> loads = new Dictionary<string, Task<Texture?>>();

        private readonly Storage textureStorage;
        private readonly LargeTextureStore bundledTextures;

        private Texture? activeWeaponTexture;

        /// <summary>
        /// The combination of filtering and wrapping an image is loaded with.
        /// </summary>
        private readonly record struct TextureVariant(bool Smoothing, bool Repeating);

        public DodgeWorldTextureLibrary(Storage textureStorage, LargeTextureStore bundledTextures)
        {
            this.textureStorage = textureStorage;
            this.bundledTextures = bundledTextures;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, OsuConfigManager config)
        {
            foreach (bool smoothing in new[] { true, false })
            {
                foreach (bool repeating in new[] { false, true })
                {
                    var store = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(new StorageBackedResourceStore(textureStorage)),
                        false, smoothing ? TextureFilteringMode.Linear : TextureFilteringMode.Nearest, scaleAdjust: 1);

                    // Textures referenced by URL are resolved through the trusted-domain store, so a
                    // world cannot make the client fetch from an arbitrary host.
                    store.AddTextureSource(host.CreateTextureLoaderStore(new TrustedDomainOnlineStore(config)));

                    stores[new TextureVariant(smoothing, repeating)] = store;
                }
            }

            entries[DodgeWorldTexture.BUNDLED_SWORD_ASSET_ID] = new TextureLibraryEntry(
                DodgeWorldTexture.BUNDLED_SWORD_ASSET_ID, "Стандартный меч", null, true);

            foreach (string path in textureStorage.GetFiles(string.Empty))
            {
                if (SupportedExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(path).ToLowerInvariant()))
                    entries[path] = new TextureLibraryEntry(path, Path.GetFileName(path), path, false);
            }

            SelectedAssetId = DodgeWorldTexture.BUNDLED_SWORD_ASSET_ID;
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds every texture a loaded world references, so they appear in the browser even though
        /// they were never imported on this machine.
        /// </summary>
        public void RegisterFromDocument(DodgeWorldDocument document)
        {
            foreach (WeaponSkin skin in document.WeaponSkins ?? Enumerable.Empty<WeaponSkin>())
            {
                if (!string.IsNullOrWhiteSpace(skin.Texture))
                    entries[skin.Texture] = new TextureLibraryEntry(skin.Texture, skin.DisplayName ?? skin.Id, skin.Texture, false);
            }

            foreach (EntityRecord entity in document.Rooms.SelectMany(room => room.Entities))
            {
                if (!string.IsNullOrWhiteSpace(entity.Texture))
                {
                    entries[entity.Texture] = new TextureLibraryEntry(entity.Texture, entity.DisplayName ?? "Текстура комнаты", entity.Texture, false);
                    prewarm(entity);
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Starts loading an image the world will want, before anything asks for it.
        /// </summary>
        /// <remarks>
        /// A room's objects ask for their images when the room is entered, and a mob is fought from the
        /// first frame — so an image still being read off the disk meant the placeholder was what the
        /// player saw first. The loads are cached by path and variant, so the entity's own request finds
        /// this one already finished instead of starting again.
        /// <para>
        /// Loaded the way the entity that referenced it will draw it: filtering and wrapping are fixed
        /// when an image is read, so warming the wrong variant would warm the wrong image.
        /// </para>
        /// </remarks>
        private void prewarm(EntityRecord entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Texture) || prewarmed.Count >= maximum_prewarmed)
                return;

            bool tiling = entity.TextureFill == (int)SurfaceFillMode.Tile;
            var variant = new TextureVariant(entity.TextureSmoothing ?? true, tiling);

            if (!prewarmed.Add($"{variant}|{entity.Texture}"))
                return;

            loadAsync(variant, entity.Texture);
        }

        private readonly HashSet<string> prewarmed = new HashSet<string>();

        /// <summary>
        /// How many images are read ahead of being asked for.
        /// </summary>
        /// <remarks>
        /// Each load takes a thread of its own — <see cref="loadAsync"/> has to, because the store may
        /// block — so reading a whole large world ahead would cost more than the flash it saves. The rest
        /// still load when their room is entered, as they always did.
        /// </remarks>
        private const int maximum_prewarmed = 24;

        public void Register(string path, string displayName)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            entries[path] = new TextureLibraryEntry(path, displayName, path, false);
            Changed?.Invoke();
        }

        public void Select(string assetId)
        {
            if (entries.ContainsKey(assetId))
                SelectedAssetId = assetId;
        }

        public TextureLibraryEntry? Selected =>
            SelectedAssetId != null && entries.TryGetValue(SelectedAssetId, out TextureLibraryEntry? entry) ? entry : null;

        /// <summary>
        /// Resolves and applies an entity's configured texture, respecting its smoothing and tiling.
        /// </summary>
        public void LoadEntityTexture(ITexturedEntity entity, Func<ITexturedEntity, bool> stillPresent)
        {
            if (string.IsNullOrEmpty(entity.TexturePath))
            {
                entity.SetTexture(null);
                return;
            }

            string path = entity.TexturePath;
            var variant = new TextureVariant(entity.TextureSmoothing, entity.TextureRepeats);

            entity.SetTexture(null);

            loadAsync(variant, path).ContinueWith(task => Schedule(() =>
            {
                // The entity may have been deleted, or pointed at something else, while loading.
                if (!task.IsCompletedSuccessfully || !stillPresent(entity) || entity.TexturePath != path)
                    return;

                entity.SetTexture(task.GetResultSafely());
            }), TaskScheduler.Default);
        }

        /// <summary>
        /// Loads a preview for a browser card.
        /// </summary>
        public void LoadPreview(TextureLibraryCard card, TextureLibraryEntry entry)
        {
            if (entry.BuiltIn || entry.Path == null)
            {
                card.SetTexture(bundledTextures.Get(DodgeWorldTexture.FALLBACK_WEAPON));
                return;
            }

            loadAsync(new TextureVariant(true, false), entry.Path).ContinueWith(task => Schedule(() =>
            {
                if (task.IsCompletedSuccessfully && card.IsAlive)
                    card.SetTexture(task.GetResultSafely());
            }), TaskScheduler.Default);
        }

        /// <summary>
        /// Creates a texture reference owned by the next sword drawable.
        /// </summary>
        /// <remarks>
        /// <see cref="LargeTextureStore"/> returns reference-counted wrappers and a <see cref="Sprite"/>
        /// disposes the wrapper assigned to it. The bundled sword must therefore be retrieved once per
        /// drawable; sharing one wrapper lets the first expired swing invalidate every other swing.
        /// Custom textures come from a regular <see cref="TextureStore"/> and may be shared safely.
        /// </remarks>
        public Texture? GetWeaponTexture()
        {
            if (!ActiveSkinUsesFallback && DodgeWorldTexture.IsUsable(activeWeaponTexture))
                return activeWeaponTexture;

            if (!ActiveSkinUsesFallback)
            {
                activeWeaponTexture = reloadCustomWeaponTexture();

                if (DodgeWorldTexture.IsUsable(activeWeaponTexture))
                    return activeWeaponTexture;

                ActiveSkinUsesFallback = true;
            }

            // Do not cache or share this wrapper. Ownership is transferred to the Sprite created
            // by SwordSwing, which disposes it when the animation expires.
            return bundledTextures.Get(DodgeWorldTexture.FALLBACK_WEAPON);
        }

        /// <summary>
        /// The skin to draw with, which is the fallback whenever the configured one is unavailable.
        /// </summary>
        public WeaponSkin GetRenderSkin() => ActiveSkinUsesFallback ? WeaponSkin.CreateFallback() : ActiveSkin;

        public void SetActiveSkin(WeaponSkin? skin)
        {
            ActiveSkin = skin ?? WeaponSkin.CreateFallback();
            activeWeaponTexture = null;
            ActiveSkinUsesFallback = true;

            if (string.IsNullOrWhiteSpace(ActiveSkin.Texture))
                return;

            string texturePath = ActiveSkin.Texture;
            string loadingSkinId = ActiveSkin.Id;

            loadAsync(new TextureVariant(true, false), texturePath).ContinueWith(task => Schedule(() =>
            {
                // Another skin may have been selected while this one was loading.
                if (!task.IsCompletedSuccessfully || ActiveSkin.Id != loadingSkinId || ActiveSkin.Texture != texturePath)
                    return;

                Texture? loaded = task.GetResultSafely();
                ActiveSkinUsesFallback = !DodgeWorldTexture.IsUsable(loaded);
                activeWeaponTexture = ActiveSkinUsesFallback ? null : loaded;
            }), TaskScheduler.Default);
        }

        private Texture? reloadCustomWeaponTexture()
        {
            if (string.IsNullOrWhiteSpace(ActiveSkin.Texture))
                return null;

            Texture? custom = stores[new TextureVariant(true, false)].Get(ActiveSkin.Texture);

            if (DodgeWorldTexture.IsUsable(custom))
                return custom;

            Logger.Log($"DodgeWorld: weapon skin '{ActiveSkin.Id}' is unavailable, using bundled sword.",
                LoggingTarget.Information, LogLevel.Important);
            return null;
        }

        /// <summary>
        /// Resolves a path through the store for <paramref name="variant"/>, once per pair.
        /// </summary>
        /// <remarks>
        /// Uses a dedicated long-running task because <see cref="TextureStore.Get(string)"/> may block
        /// waiting on another lookup of the same key, which osu!framework permits only off the thread
        /// pool.
        /// </remarks>
        private Task<Texture?> loadAsync(TextureVariant variant, string path)
        {
            TextureStore store = stores[variant];
            string key = $"{(variant.Smoothing ? 'L' : 'N')}{(variant.Repeating ? 'R' : 'C')}|{path}";

            lock (loads)
            {
                if (loads.TryGetValue(key, out Task<Texture?>? existing))
                    return existing;

                WrapMode wrap = variant.Repeating ? WrapMode.Repeat : WrapMode.None;

                Task<Texture?> created = Task.Factory.StartNew(
                    () => (Texture?)store.Get(path, wrap, wrap),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                loads[key] = created;
                return created;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            foreach (TextureStore store in stores.Values)
                store.Dispose();
        }
    }
}
