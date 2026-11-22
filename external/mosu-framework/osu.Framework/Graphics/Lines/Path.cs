// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Textures;
using osuTK;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Allocation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Caching;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Layout;
using osu.Framework.Logging;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Lines
{
    public partial class Path : Drawable, IBufferedDrawable
    {
        public IShader TextureShader { get; private set; }
        private IShader pathShader;

        private static readonly bool use_blit_discard = Environment.GetEnvironmentVariable("MOSU_PATH_BLIT_DISCARD") == "1";
        private static readonly bool use_tight_blit = Environment.GetEnvironmentVariable("MOSU_PATH_TIGHT_BLIT") == "1";

        // Performance capture can start or stop while paths remain alive. The draw-thread
        // flag is shared across all instances, but has no per-path work while disabled.
        public static volatile bool TraceSlowRendering;

        [Resolved]
        private IRenderer renderer { get; set; }

        public Path()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, use_blit_discard ? "PathDiscard" : "Path");
            pathShader = shaders.Load("PathPrepass", "PathPrepass");
        }

        private readonly List<Vector2> vertices = new List<Vector2>();

        public IReadOnlyList<Vector2> Vertices
        {
            get => vertices;
            set
            {
                vertices.Clear();
                vertices.AddRange(value);

                vertexBoundsCache.Invalidate();
                segmentsCache.Invalidate();

                Invalidate(Invalidation.DrawSize);
            }
        }

        private float pathRadius = 10f;

        /// <summary>
        /// How wide this path is on each side of the line.
        /// </summary>
        /// <remarks>
        /// The actual width of the path is twice the PathRadius.
        /// </remarks>
        public virtual float PathRadius
        {
            get => pathRadius;
            set
            {
                if (pathRadius == value) return;

                pathRadius = value;

                vertexBoundsCache.Invalidate();
                segmentsCache.Invalidate();

                Invalidate(Invalidation.DrawSize);
            }
        }

        public override Axes RelativeSizeAxes
        {
            get => base.RelativeSizeAxes;
            set
            {
                if ((AutoSizeAxes & value) != 0)
                    throw new InvalidOperationException("No axis can be relatively sized and automatically sized at the same time.");

                base.RelativeSizeAxes = value;
            }
        }

        private Axes autoSizeAxes;

        /// <summary>
        /// Controls which <see cref="Axes"/> are automatically sized w.r.t. the bounds of the vertices.
        /// It is not allowed to manually set <see cref="Size"/> (or <see cref="Width"/> / <see cref="Height"/>)
        /// on any <see cref="Axes"/> which are automatically sized.
        /// </summary>
        public virtual Axes AutoSizeAxes
        {
            get => autoSizeAxes;
            set
            {
                if (value == autoSizeAxes)
                    return;

                if ((RelativeSizeAxes & value) != 0)
                    throw new InvalidOperationException("No axis can be relatively sized and automatically sized at the same time.");

                autoSizeAxes = value;
                OnSizingChanged();
            }
        }

        public override float Width
        {
            get
            {
                if (AutoSizeAxes.HasFlagFast(Axes.X))
                    return base.Width = vertexBounds.Width;

                return base.Width;
            }
            set
            {
                if ((AutoSizeAxes & Axes.X) != 0)
                    throw new InvalidOperationException($"The width of a {nameof(Path)} with {nameof(AutoSizeAxes)} can not be set manually.");

                base.Width = value;
            }
        }

        public override float Height
        {
            get
            {
                if (AutoSizeAxes.HasFlagFast(Axes.Y))
                    return base.Height = vertexBounds.Height;

                return base.Height;
            }
            set
            {
                if ((AutoSizeAxes & Axes.Y) != 0)
                    throw new InvalidOperationException($"The height of a {nameof(Path)} with {nameof(AutoSizeAxes)} can not be set manually.");

                base.Height = value;
            }
        }

        public override Vector2 Size
        {
            get
            {
                if (AutoSizeAxes != Axes.None)
                    return base.Size = vertexBounds.Size;

                return base.Size;
            }
            set
            {
                if ((AutoSizeAxes & Axes.Both) != 0)
                    throw new InvalidOperationException($"The Size of a {nameof(Path)} with {nameof(AutoSizeAxes)} can not be set manually.");

                base.Size = value;
            }
        }

        private readonly Cached<RectangleF> vertexBoundsCache = new Cached<RectangleF>();

        private RectangleF vertexBounds
        {
            get
            {
                if (vertexBoundsCache.IsValid)
                    return vertexBoundsCache.Value;

                if (vertices.Count > 0)
                {
                    float minX = 0;
                    float minY = 0;
                    float maxX = 0;
                    float maxY = 0;

                    foreach (var v in vertices)
                    {
                        minX = Math.Min(minX, v.X - PathRadius);
                        minY = Math.Min(minY, v.Y - PathRadius);
                        maxX = Math.Max(maxX, v.X + PathRadius);
                        maxY = Math.Max(maxY, v.Y + PathRadius);
                    }

                    return vertexBoundsCache.Value = new RectangleF(minX, minY, maxX - minX, maxY - minY);
                }

                return vertexBoundsCache.Value = new RectangleF(0, 0, 0, 0);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            var localPos = ToLocalSpace(screenSpacePos);
            float pathRadiusSquared = PathRadius * PathRadius;

            foreach (var t in segments)
            {
                if (t.DistanceSquaredToPoint(localPos) <= pathRadiusSquared)
                    return true;
            }

            return false;
        }

        public Vector2 PositionInBoundingBox(Vector2 pos) => pos - vertexBounds.TopLeft;

        public void ClearVertices()
        {
            if (vertices.Count == 0)
                return;

            vertices.Clear();

            vertexBoundsCache.Invalidate();
            segmentsCache.Invalidate();

            Invalidate(Invalidation.DrawSize);
        }

        public void AddVertex(Vector2 pos)
        {
            vertices.Add(pos);

            vertexBoundsCache.Invalidate();
            segmentsCache.Invalidate();

            Invalidate(Invalidation.DrawSize);
        }

        public void ReplaceVertex(int index, Vector2 pos)
        {
            vertices[index] = pos;

            vertexBoundsCache.Invalidate();
            segmentsCache.Invalidate();

            Invalidate(Invalidation.DrawSize);
        }

        private readonly List<Line> segmentsBacking = new List<Line>();
        private readonly Cached segmentsCache = new Cached();
        private List<Line> segments => segmentsCache.IsValid ? segmentsBacking : generateSegments();

        private List<Line> generateSegments()
        {
            segmentsBacking.Clear();

            if (vertices.Count > 1)
            {
                Vector2 offset = vertexBounds.TopLeft;
                for (int i = 0; i < vertices.Count - 1; ++i)
                    segmentsBacking.Add(new Line(vertices[i] - offset, vertices[i + 1] - offset));
            }

            segmentsCache.Validate();
            return segmentsBacking;
        }

        private Texture texture;

        protected Texture Texture
        {
            get => texture ?? renderer?.WhitePixel;
            set
            {
                if (texture == value)
                    return;

                texture?.Dispose();
                texture = value;

                Invalidate(Invalidation.DrawNode);
            }
        }

        public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;

        public Vector2 FrameBufferScale { get; } = Vector2.One;

        // The path should not receive the true colour to avoid colour doubling when the frame-buffer is rendered to the back-buffer.
        public override DrawColourInfo DrawColourInfo => new DrawColourInfo(Color4.White, base.DrawColourInfo.Blending);

        private static readonly Color4 background_colour = new Color4(0, 0, 0, 0);

        /// <summary>
        /// The background colour to be used for the frame buffer this path is rendered to.
        /// </summary>
        public Color4 BackgroundColour => background_colour;

        public long PathInvalidationID { get; private set; }

        protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
        {
            bool result = base.OnInvalidate(invalidation, source);

            // Colour is being applied to the buffer instead of the actual drawable, thus removing the need to redraw the path on colour invalidation.
            invalidation &= ~Invalidation.Colour;

            if (invalidation != Invalidation.None)
                PathInvalidationID++;

            return result;
        }

        private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData(TexturePixelFormat.R16Float, clipToRootNode: true);

        protected override DrawNode CreateDrawNode() => new PathBufferedDrawNode(this, new PathDrawNode(this), sharedData);

        private class PathBufferedDrawNode : BufferedDrawNode
        {
            protected new Path Source => (Path)base.Source;

            public PathBufferedDrawNode(Path source, PathDrawNode child, BufferedDrawNodeSharedData sharedData)
                : base(source, child, sharedData)
            {
            }

            private long pathInvalidationID = -1;
            private Texture texture;
            private Vector4 textureRect;
            private IUniformBuffer<PathTextureParameters> parametersBuffer;

            public override void ApplyState()
            {
                base.ApplyState();
                pathInvalidationID = Source.PathInvalidationID;
                texture = Source.Texture;

                var rect = texture.GetTextureRect();
                textureRect = new Vector4(rect.Left, rect.Top, rect.Width, rect.Height);
            }

            protected override void BindUniformResources(IShader shader, IRenderer renderer)
            {
                base.BindUniformResources(shader, renderer);

                parametersBuffer ??= renderer.CreateUniformBuffer<PathTextureParameters>();
                parametersBuffer.Data = new PathTextureParameters
                {
                    TexRect1 = textureRect,
                };
                shader.BindUniformBlock("m_PathTextureParameters", parametersBuffer);

                texture?.Bind(1);
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct PathTextureParameters
            {
                public UniformVector4 TexRect1;
            }

            protected override long GetDrawVersion() => pathInvalidationID;

            private const int tile_size = 64;
            private const float tile_inflation_radii = 2.5f;
            private readonly List<RectangleF> blitTiles = new List<RectangleF>();
            private long blitTilesVersion = -1;

            protected override void DrawContents(IRenderer renderer)
            {
                long start = TraceSlowRendering ? Stopwatch.GetTimestamp() : 0;
                try
                {
                    drawContents(renderer);
                }
                finally
                {
                    if (TraceSlowRendering)
                        logSlowPathStep("composite", start);
                }
            }

            private void drawContents(IRenderer renderer)
            {
                if (!use_tight_blit)
                {
                    base.DrawContents(renderer);
                    return;
                }

                if (blitTilesVersion != SharedData.DrawVersion)
                {
                    computeBlitTiles();
                    blitTilesVersion = SharedData.DrawVersion;
                }

                if (blitTiles.Count == 0)
                {
                    base.DrawContents(renderer);
                    return;
                }

                var frameBufferTexture = SharedData.MainBuffer.Texture;
                foreach (var tile in blitTiles)
                {
                    var screenRect = new RectangleF(DrawRectangle.X + tile.X, DrawRectangle.Y + tile.Y, tile.Width, tile.Height);
                    // Keep the shader's texture bounds at the full framebuffer, but sample only this
                    // pixel-space sub-area. DrawQuad normalises textureCoords through GetTextureRect().
                    renderer.DrawQuad(frameBufferTexture, Quad.FromRectangle(screenRect), DrawColourInfo.Colour, textureCoords: tile);
                }
            }

            private void logSlowPathStep(string step, long started)
            {
                double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (ms >= 8)
                    Logger.Log($"MOSU_SLOW_PATH_RENDER step={step} elapsed_ms={ms:F2} path_segments={((PathDrawNode)Child).CapturedSegments.Count} "
                               + $"size={DrawRectangle.Width:F0}x{DrawRectangle.Height:F0} tight_blit={use_tight_blit}", LoggingTarget.Performance);
            }

            private void computeBlitTiles()
            {
                blitTiles.Clear();
                var child = (PathDrawNode)Child;
                var capturedSegments = child.CapturedSegments;
                if (capturedSegments.Count == 0)
                    return;

                int cols = (int)Math.Ceiling(DrawRectangle.Width / tile_size);
                int rows = (int)Math.Ceiling(DrawRectangle.Height / tile_size);
                if (cols <= 0 || rows <= 0 || cols * rows > 4096)
                    return;

                bool[] marked = new bool[cols * rows];
                var matrix = DrawInfo.Matrix;
                float scale = MathF.Sqrt(Math.Abs(matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21));
                float inflate = tile_inflation_radii * child.CapturedRadius * scale;

                foreach (var segment in capturedSegments)
                {
                    Vector2 start = Vector2Extensions.Transform(segment.StartPoint, matrix);
                    Vector2 end = Vector2Extensions.Transform(segment.EndPoint, matrix);
                    float minX = Math.Min(start.X, end.X) - inflate - DrawRectangle.X;
                    float maxX = Math.Max(start.X, end.X) + inflate - DrawRectangle.X;
                    float minY = Math.Min(start.Y, end.Y) - inflate - DrawRectangle.Y;
                    float maxY = Math.Max(start.Y, end.Y) + inflate - DrawRectangle.Y;
                    if (maxX < 0 || minX >= DrawRectangle.Width || maxY < 0 || minY >= DrawRectangle.Height)
                        continue;

                    int col0 = Math.Clamp((int)MathF.Floor(minX / tile_size), 0, cols - 1);
                    int col1 = Math.Clamp((int)MathF.Floor(maxX / tile_size), 0, cols - 1);
                    int row0 = Math.Clamp((int)MathF.Floor(minY / tile_size), 0, rows - 1);
                    int row1 = Math.Clamp((int)MathF.Floor(maxY / tile_size), 0, rows - 1);

                    for (int r = row0; r <= row1; r++)
                    {
                        for (int c = col0; c <= col1; c++)
                            marked[r * cols + c] = true;
                    }
                }

                for (int r = 0; r < rows; r++)
                {
                    float tileHeight = Math.Min(tile_size, DrawRectangle.Height - r * tile_size);
                    for (int c = 0; c < cols; c++)
                    {
                        if (!marked[r * cols + c])
                            continue;

                        int runStart = c;
                        while (c < cols && marked[r * cols + c])
                            c++;

                        float x = runStart * tile_size;
                        float tileWidth = Math.Min((c - runStart) * tile_size, DrawRectangle.Width - x);
                        blitTiles.Add(new RectangleF(x, r * tile_size, tileWidth, tileHeight));
                    }
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            texture?.Dispose();
            texture = null;

            sharedData.Dispose();
        }
    }
}
