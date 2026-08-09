// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// Draws the default circular projectiles of one emitter as a contiguous FastCircle batch.
    /// The drawable spans the entire playfield and is never culled from projectile/player proximity.
    /// </summary>
    internal partial class DodgeEmitterBulletBatch : Drawable
    {
        private BulletRenderState[] states = Array.Empty<BulletRenderState>();
        private int stateCount;
        private IShader shader = null!;

        internal int StateCount => stateCount;

        internal int StorageCapacity => states.Length;

        internal Vector2? FirstPosition => stateCount == 0 ? null : states[0].Position;

        public override bool RemoveWhenNotAlive => false;

        public DodgeEmitterBulletBatch()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "FastCircle");
        }

        internal void BeginFrame() => stateCount = 0;

        internal void Add(Vector2 position, float diameter, Colour4 colour, float alpha)
        {
            if (alpha <= 0 || diameter <= 0)
                return;

            if (stateCount == states.Length)
                Array.Resize(ref states, Math.Max(16, states.Length * 2));

            states[stateCount++] = new BulletRenderState(
                position,
                diameter,
                colour.MultiplyAlpha(alpha).ToSRGB());
        }

        internal void EndFrame()
        {
            Alpha = stateCount > 0 ? 1 : 0;
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new BulletBatchDrawNode(this);

        private readonly record struct BulletRenderState(Vector2 Position, float Diameter, Color4 Colour);

        private class BulletBatchDrawNode : DrawNode
        {
            private readonly DodgeEmitterBulletBatch source;
            private BulletRenderState[] states = Array.Empty<BulletRenderState>();
            private int stateCount;
            private IShader shader = null!;
            private IVertexBatch<TexturedVertex2D>? quadBatch;

            public BulletBatchDrawNode(DodgeEmitterBulletBatch source)
                : base(source)
            {
                this.source = source;
            }

            public override void ApplyState()
            {
                base.ApplyState();

                if (states.Length < source.stateCount)
                    Array.Resize(ref states, source.states.Length);

                Array.Copy(source.states, states, source.stateCount);
                stateCount = source.stateCount;
                shader = source.shader;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (stateCount == 0 || !renderer.BindTexture(renderer.WhitePixel))
                    return;

                // One maximum-sized burst is 64 bullets. Larger overlaps can flush and reuse this buffer;
                // reserving for 1024 bullets on every emitter and every buffered draw node wastes tens of MiB.
                quadBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(DodgeEmitter.MAX_BULLET_COUNT * 4, 1);
                shader.Bind();

                for (int i = 0; i < stateCount; i++)
                    drawBullet(renderer, states[i]);

                shader.Unbind();
            }

            private void drawBullet(IRenderer renderer, BulletRenderState state)
            {
                float halfSize = state.Diameter / 2;
                var drawRectangle = new RectangleF(
                    state.Position - new Vector2(halfSize),
                    new Vector2(state.Diameter));
                Quad screenSpaceDrawQuad = Quad.FromRectangle(drawRectangle) * DrawInfo.Matrix;
                float screenDiameter = Math.Min(screenSpaceDrawQuad.Width, screenSpaceDrawQuad.Height);

                if (screenDiameter <= 0)
                    return;

                var textureRectangle = new Vector4(0, 0, state.Diameter, state.Diameter);
                var blend = new Vector2(state.Diameter / screenDiameter);

                quadBatch!.AddAction(new TexturedVertex2D(renderer)
                {
                    Position = screenSpaceDrawQuad.BottomLeft,
                    TexturePosition = new Vector2(0, state.Diameter),
                    TextureRect = textureRectangle,
                    BlendRange = blend,
                    Colour = state.Colour,
                });
                quadBatch.AddAction(new TexturedVertex2D(renderer)
                {
                    Position = screenSpaceDrawQuad.BottomRight,
                    TexturePosition = new Vector2(state.Diameter, state.Diameter),
                    TextureRect = textureRectangle,
                    BlendRange = blend,
                    Colour = state.Colour,
                });
                quadBatch.AddAction(new TexturedVertex2D(renderer)
                {
                    Position = screenSpaceDrawQuad.TopRight,
                    TexturePosition = new Vector2(state.Diameter, 0),
                    TextureRect = textureRectangle,
                    BlendRange = blend,
                    Colour = state.Colour,
                });
                quadBatch.AddAction(new TexturedVertex2D(renderer)
                {
                    Position = screenSpaceDrawQuad.TopLeft,
                    TexturePosition = Vector2.Zero,
                    TextureRect = textureRectangle,
                    BlendRange = blend,
                    Colour = state.Colour,
                });
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                quadBatch?.Dispose();
            }
        }
    }
}
