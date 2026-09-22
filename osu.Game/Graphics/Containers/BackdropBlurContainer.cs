// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// Owns the stable framebuffer used by local glass surfaces.
    /// The scene remains parented to the same buffered container for its whole lifetime;
    /// buffering is bypassed when no visible surface consumes the backdrop.
    /// </summary>
    // Standalone public build note: Cursor effect presentation hosting (ICursorEffectPresentationHost)
    // and custom BufferedContainerView.DrawMode are optional Mosu framework extensions.
    // In standalone public builds using upstream ppy.osu.Framework, presentation hosting and view modes
    // fall back to standard upstream framework container behaviour.
    public partial class BackdropBlurContainer : Container, IBackdropBlurSource
    {
        private readonly BufferedContainer sceneBuffer;
        private readonly Container<Drawable> contextMenuLayer;
        private readonly Container<Drawable> popoverLayer;
        private readonly Container<Drawable> dropdownLayer;
        private readonly HashSet<ConsumerLease> consumers = new HashSet<ConsumerLease>();
        private readonly object consumersLock = new object();
        private bool bufferingEnabled;
        private double lastBlurStrength = -1;

        internal bool BufferingActive => bufferingEnabled;

        internal int RegisteredConsumers
        {
            get
            {
                lock (consumersLock)
                    return consumers.Count;
            }
        }

        public Drawable? SceneContent
        {
            get => sceneBuffer.Child;
            set => sceneBuffer.Child = value;
        }

        public BackdropBlurContainer()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                sceneBuffer = new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    DrawOriginal = true,
                    EffectPlacement = EffectPlacement.InFront,
                    EffectColour = Color4.Transparent,
                },
                popoverLayer = new Container
                {
                    Depth = -1,
                    RelativeSizeAxes = Axes.Both,
                },
                dropdownLayer = new Container
                {
                    Depth = -2,
                    RelativeSizeAxes = Axes.Both,
                },
                contextMenuLayer = new Container
                {
                    Depth = -3,
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        public BufferedContainerView<Drawable> CreateView() => sceneBuffer.CreateView().With(view =>
        {
            view.RelativeSizeAxes = Axes.Both;
            view.SynchronisedDrawQuad = true;
        });

        public IDisposable Acquire(Func<bool> isActive)
        {
            var lease = new ConsumerLease(this, isActive);

            lock (consumersLock)
                consumers.Add(lease);

            return lease;
        }

        protected override void Update()
        {
            base.Update();
            updateBufferingState();
        }

        private void updateBufferingState()
        {
            bool hasActiveConsumer = false;

            lock (consumersLock)
            {
                foreach (var consumer in consumers)
                {
                    if (consumer.IsActive)
                    {
                        hasActiveConsumer = true;
                        break;
                    }
                }
            }

            bool shouldBuffer = OverlayTransparency.Enabled.Value && hasActiveConsumer;
            bool wasBuffering = bufferingEnabled;
            bufferingEnabled = shouldBuffer;

            if (!shouldBuffer)
            {
                sceneBuffer.BlurSigma = Vector2.Zero;
                return;
            }

            double strength = OverlayTransparency.BlurStrength.Value;

            if (strength == lastBlurStrength)
                return;

            lastBlurStrength = strength;
            var target = new Vector2((float)(OverlayTransparency.MAX_BLUR_SIGMA * strength));

            if (!wasBuffering)
                sceneBuffer.BlurSigma = target;
            else
                sceneBuffer.BlurTo(target, 150, Easing.OutQuint);
        }

        private void release(ConsumerLease lease)
        {
            lock (consumersLock)
                consumers.Remove(lease);
        }

        private sealed class ConsumerLease : IDisposable
        {
            private BackdropBlurContainer? owner;
            private readonly Func<bool> isActive;

            public bool IsActive => isActive();

            public ConsumerLease(BackdropBlurContainer owner, Func<bool> isActive)
            {
                this.owner = owner;
                this.isActive = isActive;
            }

            public void Dispose()
            {
                owner?.release(this);
                owner = null;
            }
        }
    }

    public interface IBackdropBlurSource
    {
        BufferedContainerView<Drawable> CreateView();

        IDisposable Acquire(Func<bool> isActive);
    }
}
