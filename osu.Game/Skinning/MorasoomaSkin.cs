using JetBrains.Annotations;
using Morasooma.Resources;
using osu.Framework.IO.Stores;
using osu.Game.Extensions;
using osu.Game.IO;

namespace osu.Game.Skinning
{
    public class MorasoomaSkin : LegacySkin
    {
        public static SkinInfo CreateInfo() => new SkinInfo
        {
            ID = Skinning.SkinInfo.MORASOOMA_SKIN,
            Name = "Morasooma",
            Creator = "pax / Morasooma (P1 White, MIT)",
            Protected = true,
            InstantiationInfo = typeof(MorasoomaSkin).GetInvariantInstantiationInfo(),
        };

        public MorasoomaSkin(IStorageResourceProvider resources)
            : this(CreateInfo(), resources)
        {
        }

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public MorasoomaSkin(SkinInfo skin, IStorageResourceProvider resources)
            : base(skin, resources, CreateResourceStore())
        {
        }

        internal static IResourceStore<byte[]> CreateResourceStore()
        {
            var assembly = typeof(ResourceAssembly).Assembly;
            var store = new ResourceStore<byte[]>();
            store.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(assembly), "Skins/Morasooma"));
            store.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(assembly), "SoundOverrides/Skins/Legacy"));
            return store;
        }
    }
}
