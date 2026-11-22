using JetBrains.Annotations;
using Morasooma.Resources;
using osu.Framework.IO.Stores;
using osu.Game.Extensions;
using osu.Game.IO;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Built-in P1 White derivative. User and beatmap skins remain ahead of this fallback.
    /// Graphics and configuration come only from the reviewed generated bank; audio is
    /// the accepted Morasooma Legacy bank, never P1's third-party release recordings.
    /// </summary>
    public class MorasoomaSkin : LegacySkin
    {
        public static SkinInfo CreateInfo() => new SkinInfo
        {
            ID = Skinning.SkinInfo.MORASOOMA_SKIN,
            Name = "Morasooma",
            Creator = "pax / Morasooma (P1 White, MIT)",
            Protected = true,
            InstantiationInfo = typeof(MorasoomaSkin).GetInvariantInstantiationInfo()
        };

        public MorasoomaSkin(IStorageResourceProvider resources)
            : this(CreateInfo(), resources)
        {
        }

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public MorasoomaSkin(SkinInfo skin, IStorageResourceProvider resources)
            : base(skin, resources, CreateResourceStore(resources.Resources))
        {
        }

        internal static IResourceStore<byte[]> CreateResourceStore(IResourceStore<byte[]>? gameResources = null)
        {
            var store = new ResourceStore<byte[]>();
            if (gameResources != null)
            {
                store.AddStore(new NamespacedResourceStore<byte[]>(osu.Game.IO.GameResourcePackStore.Borrow(gameResources), "Skins/Morasooma"));
                store.AddStore(new NamespacedResourceStore<byte[]>(osu.Game.IO.GameResourcePackStore.Borrow(gameResources), "Skins/Legacy"));
            }
            store.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "Skins/Morasooma"));
            store.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "SoundOverrides/Skins/Legacy"));
            return store;
        }
    }
}
