// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Visualisation;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osu.Game.Performance;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    [DrawVisualiserHidden]
    public partial class CursorTrail : Drawable, IRequireHighFrequencyMousePosition
    {
        private const int max_sprites = 2048;

        /// <summary>
        /// An exponentiating factor to ease the trail fade.
        /// </summary>
        protected virtual float FadeExponent => 1.7f;

        /// <summary>
        /// The scale used on creation of a new trail part.
        /// </summary>
        public Vector2 NewPartScale { get; set; } = Vector2.One;

        /// <summary>
        /// The rotation (in degrees) to apply to trail parts when <see cref="AllowPartRotation"/> is <c>true</c>.
        /// </summary>
        public float PartRotation { get; set; }

        /// <summary>
        /// Whether to rotate trail parts based on the value of <see cref="PartRotation"/>.
        /// </summary>
        protected bool AllowPartRotation { get; set; }

        private Vector2 cursorScale = Vector2.One;

        public Vector2 CursorScale
        {
            get => cursorScale;
            set
            {
                cursorScale = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The trail part texture origin.
        /// </summary>
        protected Anchor TrailOrigin
        {
            get => trailOrigin;
            set
            {
                trailOrigin = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private readonly TrailPart[] parts = new TrailPart[max_sprites];
        private Anchor trailOrigin = Anchor.Centre;
        private int currentIndex;
        private IShader shader;
        private double timeOffset;
        private float time;

        /// <summary>
        /// Ring-buffer index of the oldest part that has not yet faded out.
        /// </summary>
        /// <remarks>
        /// Parts are appended in strictly increasing time order, so the parts that are still visible always form
        /// one contiguous ring segment ending just before <see cref="currentIndex"/>. Tracking that window lets
        /// both the per-frame draw node copy and the draw loop touch only the live parts instead of all
        /// <see cref="max_sprites"/> slots.
        /// </remarks>
        private int oldestLiveIndex;

        /// <summary>
        /// Number of parts in the live window starting at <see cref="oldestLiveIndex"/>.
        /// </summary>
        private int liveCount;

        public CursorTrail()
        {
            // as we are currently very dependent on having a running clock, let's make our own clock for the time being.
            Clock = new FramedClock();

            RelativeSizeAxes = Axes.Both;

            for (int i = 0; i < max_sprites; i++)
            {
                // -1 signals that the part is unusable, and should not be drawn
                parts[i].InvalidationID = -1;
            }
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            texture ??= renderer.WhitePixel;
            shader = shaders.Load(@"CursorTrail", FragmentShaderDescriptor.TEXTURE);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            resetTime();
        }

        private Texture texture;

        public Texture Texture
        {
            get => texture;
            set
            {
                if (texture == value)
                    return;

                texture = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The amount of time to fade the cursor trail pieces.
        /// </summary>
        protected virtual double FadeDuration => 300;

        public override bool IsPresent => true;

        protected override void Update()
        {
            base.Update();

            const int fade_clock_reset_threshold = 1000000;

            time = (float)((Time.Current - timeOffset) / FadeDuration);

            if (!MosuOptimisationToggles.CursorTrailLiveWindow)
            {
                Invalidate(Invalidation.DrawNode);

                if (time > fade_clock_reset_threshold)
                    resetTime();

                return;
            }

            int liveCountBefore = liveCount;

            retireFadedParts();

            // With nothing left to fade there is nothing for the draw node to reflect, so an idle cursor stops
            // invalidating (and re-uploading) entirely. The frame that empties the window still needs to publish.
            if (liveCount > 0 || liveCountBefore > 0)
                Invalidate(Invalidation.DrawNode);

            if (time > fade_clock_reset_threshold)
                resetTime();
        }

        /// <summary>
        /// Advances <see cref="oldestLiveIndex"/> past any parts that have fully faded out.
        /// </summary>
        private void retireFadedParts()
        {
            while (liveCount > 0)
            {
                ref TrailPart oldest = ref parts[oldestLiveIndex];

                if (oldest.InvalidationID != -1 && time - oldest.Time < 1)
                    break;

                oldestLiveIndex = (oldestLiveIndex + 1) % max_sprites;
                liveCount--;
            }
        }

        private void resetTime()
        {
            for (int i = 0; i < parts.Length; ++i)
            {
                parts[i].Time -= time;

                if (parts[i].InvalidationID != -1)
                    ++parts[i].InvalidationID;
            }

            time = 0;
            timeOffset = Time.Current;
        }

        /// <summary>
        /// Whether to interpolate mouse movements and add trail pieces at intermediate points.
        /// </summary>
        protected virtual bool InterpolateMovements => true;

        /// <summary>
        /// Whether offline render should spend extra simulation updates to feed this trail denser cursor motion.
        /// Trails that already gate their own sampling over time do not benefit from sub-frame replay stepping.
        /// </summary>
        public virtual bool RequiresHighFrequencyRenderSimulation => InterpolateMovements;

        protected virtual float IntervalMultiplier => 1.0f;
        protected virtual bool AvoidDrawingNearCursor => false;

        private Vector2? lastPosition;
        private InputResampler resampler = new InputResampler();

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            AddTrail(e.ScreenSpaceMousePosition);
            return base.OnMouseMove(e);
        }

        public void Reset()
        {
            lastPosition = null;
            resampler = new InputResampler();
            currentIndex = 0;
            oldestLiveIndex = 0;
            liveCount = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].InvalidationID = -1;
                parts[i].Time = 0;
                parts[i].Position = Vector2.Zero;
                parts[i].Scale = Vector2.One;
            }

            resetState();

            Invalidate(Invalidation.DrawNode);
        }

        protected virtual void resetState()
        {
        }

        public void UseExternalClock(IFrameBasedClock clock)
        {
            Clock = clock;
            ProcessCustomClock = false;
            resetTime();
        }

        protected void AddTrail(Vector2 position)
        {
            position = ToLocalSpace(position);

            if (InterpolateMovements)
            {
                if (!lastPosition.HasValue)
                {
                    lastPosition = position;
                    resampler.AddPosition(lastPosition.Value);
                    return;
                }

                foreach (Vector2 pos2 in resampler.AddPosition(position))
                {
                    Trace.Assert(lastPosition.HasValue);

                    Vector2 pos1 = lastPosition.Value;
                    Vector2 diff = pos2 - pos1;
                    float distance = diff.Length;
                    Vector2 direction = diff / distance;

                    float interval = Texture.DisplayWidth * CursorScale.X / 2.5f * IntervalMultiplier;
                    float stopAt = distance - (AvoidDrawingNearCursor ? interval : 0);

                    for (float d = interval; d < stopAt; d += interval)
                    {
                        lastPosition = pos1 + direction * d;
                        addPart(lastPosition.Value);
                    }
                }
            }
            else
            {
                lastPosition = position;
                addPart(lastPosition.Value);
            }
        }

        private void addPart(Vector2 localSpacePosition)
        {
            parts[currentIndex].Position = localSpacePosition;
            parts[currentIndex].Time = time + 1;
            parts[currentIndex].Scale = NewPartScale;
            ++parts[currentIndex].InvalidationID;

            currentIndex = (currentIndex + 1) % max_sprites;

            if (!MosuOptimisationToggles.CursorTrailLiveWindow)
                return;

            bool wasEmpty = liveCount == 0;

            if (liveCount < max_sprites)
                liveCount++;
            else
                // The buffer wrapped and this write overwrote the oldest live part; the window start follows it.
                oldestLiveIndex = currentIndex;

            // Update() skips invalidation while the window is empty. Parts can still be added after it has run
            // for the frame -- from input handling, or from LegacyCursorTrail's disjoint trail, which calls
            // AddTrail() after base.Update(). Invalidating on the transition out of empty covers that gap without
            // invalidating repeatedly while the trail is already live.
            if (wasEmpty)
                Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new TrailDrawNode(this);

        private struct TrailPart
        {
            public Vector2 Position;
            public float Time;
            public Vector2 Scale;
            public long InvalidationID;
        }

        private class TrailDrawNode : DrawNode
        {
            protected new CursorTrail Source => (CursorTrail)base.Source;

            private IShader shader;
            private Texture texture;

            private float time;
            private float fadeExponent;
            private float angle;
            private Vector2 cursorScale;

            private readonly TrailPart[] parts = new TrailPart[max_sprites];

            /// <summary>
            /// Number of entries at the start of <see cref="parts"/> that hold live trail parts.
            /// </summary>
            private int partCount;

            private Vector2 originPosition;

            private IVertexBatch<TexturedTrailVertex> vertexBatch;

            public TrailDrawNode(CursorTrail source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                time = Source.time;
                fadeExponent = Source.FadeExponent;
                angle = Source.AllowPartRotation ? float.DegreesToRadians(Source.PartRotation) : 0;
                cursorScale = Source.cursorScale;

                originPosition = Vector2.Zero;

                if (Source.TrailOrigin.HasFlag(Anchor.x1))
                    originPosition.X = 0.5f;
                else if (Source.TrailOrigin.HasFlag(Anchor.x2))
                    originPosition.X = 1f;

                if (Source.TrailOrigin.HasFlag(Anchor.y1))
                    originPosition.Y = 0.5f;
                else if (Source.TrailOrigin.HasFlag(Anchor.y2))
                    originPosition.Y = 1f;

                if (!MosuOptimisationToggles.CursorTrailLiveWindow)
                {
                    Source.parts.CopyTo(parts, 0);
                    partCount = max_sprites;
                    return;
                }

                // Compact the live ring segment to the start of the local array so Draw() can walk it linearly.
                int count = Source.liveCount;
                int start = Source.oldestLiveIndex;

                if (count > 0)
                {
                    int firstRun = Math.Min(count, max_sprites - start);
                    Array.Copy(Source.parts, start, parts, 0, firstRun);

                    if (count > firstRun)
                        Array.Copy(Source.parts, 0, parts, firstRun, count - firstRun);
                }

                partCount = count;
            }

            private IUniformBuffer<CursorTrailParameters> cursorTrailParameters;

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                vertexBatch ??= renderer.CreateQuadBatch<TexturedTrailVertex>(max_sprites, 1);

                cursorTrailParameters ??= renderer.CreateUniformBuffer<CursorTrailParameters>();
                cursorTrailParameters.Data = cursorTrailParameters.Data with
                {
                    FadeClock = time,
                    FadeExponent = fadeExponent
                };

                shader.Bind();
                shader.BindUniformBlock("m_CursorTrailParameters", cursorTrailParameters);

                texture.Bind();

                RectangleF textureRect = texture.GetTextureRect();

                renderer.PushLocalMatrix(DrawInfo.Matrix);

                float sin = MathF.Sin(angle);
                float cos = MathF.Cos(angle);

                for (int i = 0; i < partCount; i++)
                {
                    var part = parts[i];

                    if (part.InvalidationID == -1)
                        continue;

                    if (time - part.Time >= 1)
                        continue;

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X - texture.DisplayWidth * originPosition.X * part.Scale.X * cursorScale.X,
                                part.Position.Y + texture.DisplayHeight * (1 - originPosition.Y) * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.BottomLeft,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.BottomLeft.Linear,
                        Time = part.Time
                    });

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X + texture.DisplayWidth * (1 - originPosition.X) * part.Scale.X * cursorScale.X,
                                part.Position.Y + texture.DisplayHeight * (1 - originPosition.Y) * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.BottomRight,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.BottomRight.Linear,
                        Time = part.Time
                    });

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X + texture.DisplayWidth * (1 - originPosition.X) * part.Scale.X * cursorScale.X,
                                part.Position.Y - texture.DisplayHeight * originPosition.Y * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.TopRight,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.TopRight.Linear,
                        Time = part.Time
                    });

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X - texture.DisplayWidth * originPosition.X * part.Scale.X * cursorScale.X,
                                part.Position.Y - texture.DisplayHeight * originPosition.Y * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.TopLeft,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.TopLeft.Linear,
                        Time = part.Time
                    });
                }

                renderer.PopLocalMatrix();

                vertexBatch.Draw();
                shader.Unbind();
            }

            private static Vector2 rotateAround(Vector2 input, Vector2 origin, float sin, float cos)
            {
                // The common case is an unrotated trail (AllowPartRotation is false, or PartRotation is zero),
                // where the rotation below is an identity transform.
                if (sin == 0 && cos == 1)
                    return input;

                float xTranslated = input.X - origin.X;
                float yTranslated = input.Y - origin.Y;

                return new Vector2(xTranslated * cos - yTranslated * sin, xTranslated * sin + yTranslated * cos) + origin;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                vertexBatch?.Dispose();
                cursorTrailParameters?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct CursorTrailParameters
            {
                public UniformFloat FadeClock;
                public UniformFloat FadeExponent;
                private readonly UniformPadding8 pad1;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TexturedTrailVertex : IEquatable<TexturedTrailVertex>, IVertex
        {
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 Position;

            [VertexMember(4, VertexAttribPointerType.Float)]
            public Color4 Colour;

            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 TexturePosition;

            [VertexMember(4, VertexAttribPointerType.Float)]
            public Vector4 TextureRect;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float Time;

            public bool Equals(TexturedTrailVertex other)
            {
                return Position.Equals(other.Position)
                       && TexturePosition.Equals(other.TexturePosition)
                       && Colour.Equals(other.Colour)
                       && Time.Equals(other.Time);
            }
        }
    }
}
