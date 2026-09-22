// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// A thin polyline which draws directly into a reusable quad batch.
    /// Unlike framework Path it does not allocate and redraw a framebuffer when its consumed prefix changes.
    /// </summary>
    internal partial class DodgeUnbufferedPath : Drawable
    {
        private Vector2[] vertices = Array.Empty<Vector2>();
        private int vertexCount;
        private IShader shader = null!;
        private Texture texture = null!;

        public DodgeUnbufferedPath()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            texture = renderer.WhitePixel;
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        internal void SetGeometry(
            Vector2 start,
            Vector2 controlEnd,
            float minimumProgress,
            float maximumProgress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            DodgeMovementEasing movementEasing)
        {
            int segmentCount = Math.Max(16,
                (int)Math.Ceiling((maximumProgress - minimumProgress) * Math.Max(1, Math.Abs(waveCycles)) * 24));
            int required = segmentCount + 1;

            if (vertices.Length < required)
                Array.Resize(ref vertices, Math.Max(required, vertices.Length * 2));

            for (int i = 0; i <= segmentCount; i++)
            {
                float progress = minimumProgress + (maximumProgress - minimumProgress) * i / segmentCount;
                vertices[i] = DodgeTrajectory.PositionAtProgress(
                    start,
                    controlEnd,
                    progress,
                    movementType,
                    waveAmplitude,
                    waveCycles,
                    wavePhase,
                    movementEasing);
            }

            vertexCount = required;
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new UnbufferedPathDrawNode(this);

        private class UnbufferedPathDrawNode : DrawNode
        {
            private readonly DodgeUnbufferedPath source;
            private Vector2[] vertices = Array.Empty<Vector2>();
            private int vertexCount;
            private IShader shader = null!;
            private Texture texture = null!;

            public UnbufferedPathDrawNode(DodgeUnbufferedPath source)
                : base(source)
            {
                this.source = source;
            }

            public override void ApplyState()
            {
                base.ApplyState();

                if (vertices.Length < source.vertexCount)
                    Array.Resize(ref vertices, source.vertices.Length);

                Array.Copy(source.vertices, vertices, source.vertexCount);
                vertexCount = source.vertexCount;
                shader = source.shader;
                texture = source.texture;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (vertexCount < 2)
                    return;

                shader.Bind();

                for (int i = 1; i < vertexCount; i++)
                    drawSegment(renderer, vertices[i - 1], vertices[i]);

                shader.Unbind();
            }

            private void drawSegment(IRenderer renderer, Vector2 start, Vector2 end)
            {
                Vector2 direction = end - start;

                if (direction.LengthSquared == 0)
                    return;

                Vector2 normal = new Vector2(-direction.Y, direction.X);
                normal.Normalize();
                normal *= 0.5f;

                var localQuad = new Quad(
                    start - normal,
                    end - normal,
                    start + normal,
                    end + normal);
                renderer.DrawQuad(
                    texture,
                    localQuad * DrawInfo.Matrix,
                    DrawColourInfo.Colour);
            }
        }
    }
}
