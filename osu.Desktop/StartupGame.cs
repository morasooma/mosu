using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources;
using osuTK.Graphics;

namespace osu.Desktop
{
    /// <summary>Game resources and styling without account/config migrations or Realm-backed services.</summary>
    public abstract partial class StartupGame : osu.Framework.Game
    {
        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(new OsuColour());
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));
            AddFont(Resources, "Fonts/Torus/Torus-Regular");
            AddFont(Resources, "Fonts/Inter/Inter-Regular");
            AddFont(Resources, "Fonts/Noto/Noto-Basic");
        }

        // OsuSpriteText depends on gameplay settings. Use the same font resources without
        // loading the real settings or writing into the folder the user is about to select.
#pragma warning disable RS0030
        protected partial class StartupButton : OsuButton
        {
            public StartupButton() : base(null)
            {
                CornerRadius = 12;
                BackgroundColour = new Color4(88, 58, 110, 255);
            }

            protected override bool ApplyGeometryRadius => false;

            protected override SpriteText CreateText() => new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = new FontUsage("Inter", size: 20),
                Colour = Color4.White,
            };
        }
    }
}
