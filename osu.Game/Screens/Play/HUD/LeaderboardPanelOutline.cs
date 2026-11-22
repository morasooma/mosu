using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>A screen-space antialiased rounded stroke without an intermediate texture.</summary>
    internal partial class LeaderboardPanelOutline : Box
    {
        private float radius;
        private float thickness;

        public float Radius
        {
            get => radius;
            set
            {
                if (radius == value) return;
                radius = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        public float Thickness
        {
            get => thickness;
            set
            {
                if (thickness == value) return;
                thickness = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "MosuLeaderboardOutline");
        }

        protected override DrawNode CreateDrawNode() => new OutlineDrawNode(this);

        private class OutlineDrawNode : SpriteDrawNode
        {
            private OutlineParameters parameters;
            private IUniformBuffer<OutlineParameters>? buffer;

            public OutlineDrawNode(LeaderboardPanelOutline source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();
                var source = (LeaderboardPanelOutline)Source;
                parameters = new OutlineParameters
                {
                    Size = source.DrawSize,
                    Radius = source.Radius,
                    Thickness = source.Thickness,
                };
            }

            protected override void BindUniformResources(IShader shader, IRenderer renderer)
            {
                base.BindUniformResources(shader, renderer);
                buffer ??= renderer.CreateUniformBuffer<OutlineParameters>();
                buffer.Data = parameters;
                shader.BindUniformBlock("m_OutlineParameters", buffer);
            }

            protected override bool CanDrawOpaqueInterior => false;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                buffer?.Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct OutlineParameters
        {
            public UniformVector2 Size;
            public UniformFloat Radius;
            public UniformFloat Thickness;
        }
    }
}
