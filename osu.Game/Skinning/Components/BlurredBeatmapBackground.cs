using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays;
using osuTK;
using osu.Game.Screens.Play;

namespace osu.Game.Skinning.Components
{
    /// <summary>
    /// A cached, locally blurred window onto the background underneath this skin element.
    /// </summary>
    public partial class BlurredBeatmapBackground : CompositeDrawable, ISerialisableDrawable, IMorasoomaSkinComponent
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BackgroundBlur))]
        public BindableBool BlurEnabled { get; } = new BindableBool(true);

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BlurStrength))]
        public BindableFloat BlurStrength { get; } = new BindableFloat(12) { MinValue = 0, MaxValue = 40, Precision = 0.5f };

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BackgroundTint), SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat TintOpacity { get; } = new BindableFloat(0) { MinValue = 0, MaxValue = 1, Precision = 0.01f };

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BackgroundDim), SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat Dim { get; } = new BindableFloat(0) { MinValue = 0, MaxValue = 1, Precision = 0.01f };

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BackgroundOpacity), SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public BindableFloat Opacity { get; } = new BindableFloat(1) { MinValue = 0, MaxValue = 1, Precision = 0.01f };

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BackgroundFullImage))]
        public BindableBool ShowFullImage { get; } = new BindableBool(false);

        [Resolved]
        private IBackgroundSceneSource? backgroundSource { get; set; }

        [Resolved]
        private GameplaySkinUnderlay? underlay { get; set; }
        private Drawable? underlayProxy;

        private const float blur_sigma = 12;
        private readonly RegionBuffer buffer;
        private readonly BackgroundSample sample;
        private readonly Box fallback;
        private readonly Box tint;
        private readonly Box dim;
        private readonly Container appearance;
        private readonly SkinComponentColourProvider themeColours = new SkinComponentColourProvider();
        private Sprite? previousSource;
        private Texture? previousTexture;
        private Quad? previousSourceQuad;
        private Quad? previousPanelQuad;
        private ColourInfo? previousColour;

        internal Colour4 TintColour => tint.Colour.TopLeft;
        internal int CaptureRevision { get; private set; }
        internal Vector2 CaptureSize => buffer.DrawSize;
        internal Quad SampleQuad => sample.ScreenSpaceDrawQuad;
        internal bool HasBackground => sample.Texture != null;

        public BlurredBeatmapBackground()
        {
            Size = new Vector2(400, 160);
            Masking = true;
            CornerRadius = 8;

            InternalChild = appearance = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    fallback = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black },
                    buffer = new RegionBuffer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FrameBufferScale = new Vector2(0.5f),
                        BlurSigma = new Vector2(blur_sigma * 0.5f),
                        Child = sample = new BackgroundSample(),
                    },
                    tint = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black,
                        Alpha = 0,
                    },
                    dim = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black, Alpha = 0 },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (underlay != null)
                underlay.Add(underlayProxy = CreateProxy());
            // Tint lives outside the cached capture: changing theme never reruns the blur.
            themeColours.Bind(action => Scheduler.Add(action), () => tint.Colour = themeColours.GetColour(OverlayColour.Background3, Colour4.Black));
            TintOpacity.BindValueChanged(value => tint.Alpha = value.NewValue, true);
            Dim.BindValueChanged(value => dim.Alpha = value.NewValue, true);
            Opacity.BindValueChanged(value => appearance.Alpha = value.NewValue, true);
            BlurEnabled.BindValueChanged(_ => previousPanelQuad = null);
            BlurStrength.BindValueChanged(_ => previousPanelQuad = null);
            ShowFullImage.BindValueChanged(_ => previousPanelQuad = null);
        }

        protected override void Update()
        {
            base.Update();

            var source = backgroundSource?.BackgroundSprite;
            var texture = source?.Texture;
            Quad? sourceQuad = source?.ScreenSpaceDrawQuad;
            var panelQuad = ScreenSpaceDrawQuad;
            ColourInfo? colour = source?.DrawColourInfo.Colour;

            // This is the whole steady-state path: no texture loading, framebuffer
            // capture, blur pass, allocation or property writes until something changes.
            if (source == previousSource && texture == previousTexture && Nullable.Equals(sourceQuad, previousSourceQuad)
                && previousPanelQuad.HasValue && panelQuad.Equals(previousPanelQuad.Value) && Nullable.Equals(colour, previousColour))
                return;

            if (texture != previousTexture)
                sample.Texture = texture == null ? null : new Texture(texture) { ScaleAdjust = texture.ScaleAdjust };
            // Do not composite a black masked layer beneath the image's antialiased edge.
            fallback.Alpha = texture == null ? 1 : 0;

            previousSource = source;
            previousTexture = texture;
            previousSourceQuad = sourceQuad;
            previousPanelQuad = panelQuad;
            previousColour = colour;

            // Capture only this region plus the Gaussian kernel's margin, never the
            // whole screen. Sampling uses the real background's screen-space quad,
            // including its crop, parallax, viewport and scale; it is not fitted here.
            float sigma = BlurEnabled.Value ? BlurStrength.Value : 0;
            float bufferScale = sigma > 0 ? 0.5f : 1;
            buffer.FrameBufferScale = new Vector2(bufferScale);
            buffer.BlurSigma = new Vector2(sigma * bufferScale);
            buffer.Size = DrawSize + new Vector2(sigma * 6);
            sample.Colour = colour ?? Colour4.White;
            if (ShowFullImage.Value && texture != null)
            {
                // Cover the entire panel without letterboxes. Preserve aspect ratio,
                // cropping the excess rather than leaving transparent/black strips.
                Vector2 imageSize = new Vector2(texture.DisplayWidth, texture.DisplayHeight);
                float scale = Math.Max(DrawWidth / imageSize.X, DrawHeight / imageSize.Y);
                Vector2 fittedSize = imageSize * scale;
                sample.SetQuad(ToScreenSpace(new RectangleF((DrawSize - fittedSize) / 2, fittedSize)));
            }
            else
                sample.SetQuad(sourceQuad ?? panelQuad);
            sample.SetCaptureBounds(buffer.ScreenSpaceDrawQuad.AABBFloat.Inflate(2));
            buffer.ForceRedraw();
            CaptureRevision++;
        }

        protected override void Dispose(bool isDisposing)
        {
            var proxy = underlayProxy;
            var target = underlay;
            if (proxy != null && target != null)
                target.RemoveProxy(proxy);
            base.Dispose(isDisposing);
        }

        private partial class RegionBuffer : BufferedContainer
        {
            public RegionBuffer()
                : base(cachedFrameBuffer: true)
            {
            }

            // Keep geometry checks and texture safety replacements live even when
            // rendering the cached image. This does not invalidate the framebuffer.
            protected override bool RequiresChildrenUpdate => true;
        }

        private partial class BackgroundSample : Sprite
        {
            private Quad quad;
            private RectangleF captureBounds;

            public BackgroundSample()
            {
                Size = Vector2.One;
            }

            public override Quad ScreenSpaceDrawQuad => quad;

            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;

            protected override DrawNode CreateDrawNode() => new SampleDrawNode(this);

            private class SampleDrawNode : SpriteDrawNode
            {
                private Quad sourceQuad;
                private RectangleF captureBounds;

                public SampleDrawNode(Sprite source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();
                    sourceQuad = ((BackgroundSample)Source).quad;
                    captureBounds = ((BackgroundSample)Source).captureBounds;
                }

                protected override void Blit(IRenderer renderer)
                {
                    // Extend the actual edge texels across the kernel margin and any
                    // area beyond the source. Nine quads also work for atlas textures:
                    // no out-of-range UVs can pick up transparent atlas padding.
                    Vector2 xAxis = sourceQuad.TopRight - sourceQuad.TopLeft;
                    Vector2 yAxis = sourceQuad.BottomLeft - sourceQuad.TopLeft;
                    float determinant = xAxis.X * yAxis.Y - xAxis.Y * yAxis.X;
                    if (Math.Abs(determinant) < 0.0001f)
                        return;

                    float minX = 0, minY = 0, maxX = 1, maxY = 1;
                    foreach (var corner in Quad.FromRectangle(captureBounds).GetVertices())
                    {
                        Vector2 point = corner - sourceQuad.TopLeft;
                        float x = (point.X * yAxis.Y - point.Y * yAxis.X) / determinant;
                        float y = (xAxis.X * point.Y - xAxis.Y * point.X) / determinant;
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }

                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                    {
                        float left = column == 0 ? minX : column - 1;
                        float right = column == 2 ? maxX : column;
                        float top = row == 0 ? minY : row - 1;
                        float bottom = row == 2 ? maxY : row;
                        if (right <= left || bottom <= top)
                            continue;

                        var drawQuad = new Quad(
                            sourceQuad.TopLeft + xAxis * left + yAxis * top,
                            sourceQuad.TopLeft + xAxis * right + yAxis * top,
                            sourceQuad.TopLeft + xAxis * left + yAxis * bottom,
                            sourceQuad.TopLeft + xAxis * right + yAxis * bottom);
                        float width = Texture.DisplayWidth;
                        float height = Texture.DisplayHeight;
                        float halfTexel = 0.5f / Texture.ScaleAdjust;
                        float u = column == 0 ? halfTexel : column == 2 ? width - halfTexel : 0;
                        float v = row == 0 ? halfTexel : row == 2 ? height - halfTexel : 0;
                        renderer.DrawQuad(Texture, drawQuad, DrawColourInfo.Colour,
                            textureCoords: new RectangleF(u, v, column == 1 ? width : 0, row == 1 ? height : 0));
                    }
                }

                // Sprite's opaque-interior shortcut uses its own layout quad, not
                // the sampled source quad. Always use the regular textured blit.
                protected override bool CanDrawOpaqueInterior => false;
            }

            public void SetCaptureBounds(RectangleF value)
            {
                captureBounds = value;
                Invalidate(Invalidation.DrawNode);
            }

            public void SetQuad(Quad value)
            {
                quad = value;
                Invalidate(Invalidation.DrawNode);
            }
        }
    }
}
