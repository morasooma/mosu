// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Diagnostics;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using System;
using System.Runtime.InteropServices;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.OpenGL.Buffers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Utils;
using osu.Framework.Statistics;

namespace osu.Framework.Graphics.Containers
{
    public partial class BufferedContainer<T>
    {
        private class BufferedContainerDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            protected new BufferedContainer<T> Source => (BufferedContainer<T>)base.Source;

            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            private bool drawOriginal;
            private ColourInfo effectColour;
            private BlendingParameters effectBlending;
            private EffectPlacement effectPlacement;

            private Vector2 blurSigma;
            private Vector2I blurRadius;
            private float blurRotation;
            private float grayscaleStrength;
            private bool bufferingEnabled;

            private long updateVersion;
            private IShader blurShader;
            private IShader grayscaleShader;

            public BufferedContainerDrawNode(BufferedContainer<T> source, BufferedContainerDrawNodeSharedData sharedData)
                : base(source, new CompositeDrawableDrawNode(source), sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                updateVersion = Source.updateVersion;

                effectColour = Source.EffectColour;
                effectBlending = Source.DrawEffectBlending;
                effectPlacement = Source.EffectPlacement;

                drawOriginal = Source.DrawOriginal;
                blurSigma = Source.BlurSigma;
                blurRadius = new Vector2I(Blur.KernelSize(blurSigma.X), Blur.KernelSize(blurSigma.Y));
                blurRotation = Source.BlurRotation;
                grayscaleStrength = Source.GrayscaleStrength;
                bufferingEnabled = Source.BufferingEnabled;

                blurShader = Source.blurShader;
                grayscaleShader = Source.grayscaleShader;
            }

            protected override bool BufferingEnabled => bufferingEnabled;

            protected override long GetDrawVersion() => updateVersion;

            protected override void PopulateContents(IRenderer renderer)
            {
                using var operation = StutterTraceEventSource.Log.Measure("Render.BufferedContents");
                base.PopulateContents(renderer);

                if (blurRadius.X > 0 || blurRadius.Y > 0 || grayscaleStrength > 0)
                {
                    renderer.PushScissorState(false);

                    if (blurRadius.X > 0) drawBlurredFrameBuffer(renderer, blurRadius.X, blurSigma.X, blurRotation);
                    if (blurRadius.Y > 0) drawBlurredFrameBuffer(renderer, blurRadius.Y, blurSigma.Y, blurRotation + 90);
                    if (grayscaleStrength > 0) drawGrayscaleFrameBuffer(renderer, grayscaleStrength);

                    renderer.PopScissorState();
                }
            }

            protected override void DrawContents(IRenderer renderer)
            {
                if (drawOriginal && effectPlacement == EffectPlacement.InFront)
                    base.DrawContents(renderer);

                renderer.SetBlend(effectBlending);

                ColourInfo finalEffectColour = DrawColourInfo.Colour;
                finalEffectColour.ApplyChild(effectColour);

                // Capture/effect generation above is still needed by views even when its
                // own composite is invisible. Only skip a colour/depth/stencil no-op.
                if (finalEffectColour.TopLeft.Alpha != 0 || finalEffectColour.TopRight.Alpha != 0
                    || finalEffectColour.BottomLeft.Alpha != 0 || finalEffectColour.BottomRight.Alpha != 0
                    || (effectBlending != BlendingParameters.Mixture && effectBlending != BlendingParameters.Additive)
                    || renderer.CurrentMaskingInfo.BorderThickness != 0
                    || renderer.CurrentDepthInfo.WriteDepth || renderer.CurrentStencilInfo.StencilTest)
                    renderer.DrawFrameBuffer(SharedData.CurrentEffectBuffer, DrawRectangle, finalEffectColour);

                if (drawOriginal && effectPlacement == EffectPlacement.Behind)
                    base.DrawContents(renderer);
            }

            private IUniformBuffer<BlurParameters> blurParametersBuffer;
            private IUniformBuffer<GrayscaleParameters> grayscaleParametersBuffer;

            private void drawBlurredFrameBuffer(IRenderer renderer, int kernelRadius, float sigma, float blurRotation)
            {
                using var operation = StutterTraceEventSource.Log.Measure("Render.BlurPass", kernelRadius);
                blurParametersBuffer ??= renderer.CreateUniformBuffer<BlurParameters>();

                IFrameBuffer current = SharedData.CurrentEffectBuffer;
                IFrameBuffer target = SharedData.GetNextEffectBuffer();

                renderer.SetBlend(BlendingParameters.None);
                bool inspect = StutterTraceEventSource.FramebufferProbe && StutterTraceEventSource.Log.IsEnabled();
                long bindStart = inspect ? Stopwatch.GetTimestamp() : 0;
                Vector2 oldSize = inspect ? target.Size : default;

                using (BindFrameBuffer(target))
                {
                    if (inspect && (oldSize != target.Size || Stopwatch.GetElapsedTime(bindStart).TotalMilliseconds >= 1))
                    {
                        var elapsed = Stopwatch.GetElapsedTime(bindStart);
                        StutterTraceEventSource.Log.Marker("Render.BlurTargetBind",
                            FormattableString.Invariant($"owner={Source.GetType().Name};parent={Source.Parent?.GetType().Name};old={oldSize.X}x{oldSize.Y};new={target.Size.X}x{target.Size.Y};kernel={kernelRadius};angle={blurRotation};wall_ms={elapsed.TotalMilliseconds:F3}"), bindStart);
                    }

                    float radians = float.DegreesToRadians(blurRotation);
                    Vector2? retainedStorageSize = current is GLFrameBuffer { HasRetainedTextureArea: true } gl ? gl.StorageSize : null;

                    blurParametersBuffer.Data = blurParametersBuffer.Data with
                    {
                        Radius = kernelRadius,
                        Sigma = sigma,
                        TexSize = retainedStorageSize ?? current.Size,
                        Direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians)),
                        ClampRetainedArea = retainedStorageSize.HasValue
                    };

                    blurShader.BindUniformBlock("m_BlurParameters", blurParametersBuffer);
                    blurShader.Bind();
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    blurShader.Unbind();
                }
            }

            private void drawGrayscaleFrameBuffer(IRenderer renderer, float strength)
            {
                grayscaleParametersBuffer ??= renderer.CreateUniformBuffer<GrayscaleParameters>();

                IFrameBuffer current = SharedData.CurrentEffectBuffer;
                IFrameBuffer target = SharedData.GetNextEffectBuffer();

                renderer.SetBlend(BlendingParameters.None);

                using (BindFrameBuffer(target))
                {
                    grayscaleParametersBuffer.Data = grayscaleParametersBuffer.Data with
                    {
                        Strength = strength
                    };

                    grayscaleShader.BindUniformBlock("m_GrayscaleParameters", grayscaleParametersBuffer);
                    grayscaleShader.Bind();
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    grayscaleShader.Unbind();
                }
            }

            public List<DrawNode> Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                blurParametersBuffer?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct BlurParameters
            {
                public UniformVector2 TexSize;
                public UniformInt Radius;
                public UniformFloat Sigma;
                public UniformVector2 Direction;
                public UniformBool ClampRetainedArea;
                private readonly UniformPadding4 pad1;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct GrayscaleParameters
            {
                public UniformFloat Strength;
                private readonly UniformPadding12 pad1;
            }
        }

        private class BufferedContainerDrawNodeSharedData : BufferedDrawNodeSharedData
        {
            public BufferedContainerDrawNodeSharedData(TexturePixelFormat mainBufferTextureFormat, RenderBufferFormat[] mainBufferFormats, bool pixelSnapping, bool clipToRootNode)
                : base(2, mainBufferTextureFormat, mainBufferFormats, pixelSnapping, clipToRootNode)
            {
            }
        }
    }
}
