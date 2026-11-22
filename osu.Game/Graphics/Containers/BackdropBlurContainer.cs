// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Layout;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
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
    [Cached(typeof(ICursorEffectPresentationHost))]
    public partial class BackdropBlurContainer : Container, IBackdropBlurSource, ICursorEffectPresentationHost
    {
        private readonly BufferedContainer sceneBuffer;
        private readonly BufferedContainer effectBuffer;
        internal const float EFFECT_RESOLUTION_SCALE = 0.5f;
        internal Vector2 EffectFrameBufferScale => effectBuffer.FrameBufferScale;
        internal Vector2 OriginalFrameBufferScale => sceneBuffer.FrameBufferScale;
        internal int BackgroundRefreshCount => ((LiveBackgroundBuffer)sceneBuffer).RefreshCount;
        internal Vector2 EffectBlurSigma => effectBuffer.BlurSigma;
        internal bool EffectActive => effectBuffer.Alpha > 0;
        private readonly Container<Drawable> contextMenuLayer;
        private readonly Container<Drawable> popoverLayer;
        private readonly Container<Drawable> dropdownLayer;
        private readonly HashSet<ConsumerLease> consumers = new HashSet<ConsumerLease>();
        private ConsumerLease? lastActiveConsumer;
        private readonly HashSet<PresentationLease> presentations = new HashSet<PresentationLease>();
        private readonly object consumersLock = new object();
        private readonly object presentationsLock = new object();
        private double lastBlurStrength = -1;
        private float nextPresentationDepth;
        private double nextEffectRefresh;

        internal bool BufferingActive => sceneBuffer.BufferingEnabled;
        public Func<bool> CaptureOriginal { get; set; } = () => false;
        public Func<bool> AllowEffects { get; set; } = () => true;

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

        public BackdropBlurContainer(bool backgroundOnly = false)
        {
            RelativeSizeAxes = Axes.Both;

            // Keep async texture loading alive independently from framebuffer refresh.
            sceneBuffer = new LiveBackgroundBuffer(backgroundOnly)
                {
                    RelativeSizeAxes = Axes.Both,
                    BufferingEnabled = false,
                    DrawOriginal = true,
                    EffectPlacement = EffectPlacement.InFront,
                    // The source displays its sharp main buffer. Effect-only views intentionally
                    // ignore this colour and display the raw blurred buffer inside glass surfaces.
                    EffectColour = Color4.Transparent,
                    // The original buffer is also displayed as the sharp background.
                    // Downsampling it degrades every image, not just the glass effect.
                    FrameBufferScale = Vector2.One,
                };

            // The effect samples the full-size original at reduced resolution. Its
            // synchronised view must keep updating after initial layout and toolbar
            // padding changes; ordinary cached containers pause that view's updates.
            effectBuffer = new LiveBackgroundBuffer(backgroundOnly)
                {
                    RelativeSizeAxes = Axes.Both,
                    FrameBufferScale = new Vector2(EFFECT_RESOLUTION_SCALE),
                    Alpha = 0,
                    DrawOriginal = false,
                    EffectColour = Color4.Transparent,
                    // Render the already captured sharp image, not the scene a second time.
                    Child = sceneBuffer.CreateView().With(view =>
                    {
                        view.RelativeSizeAxes = Axes.Both;
                        view.SynchronisedDrawQuad = true;
                        view.DrawMode = BufferedContainerViewDrawMode.Original;
                    }),
                };

            InternalChildren = new Drawable[]
            {
                sceneBuffer,
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

            effectBuffer.Depth = -0.5f;
            AddInternal(effectBuffer);
        }

        Container<Drawable> ICursorEffectPresentationHost.ContextMenuLayer => contextMenuLayer;

        Container<Drawable> ICursorEffectPresentationHost.PopoverLayer => popoverLayer;

        Container<Drawable> ICursorEffectPresentationHost.DropdownLayer => dropdownLayer;

        IDisposable ICursorEffectPresentationHost.Register(Drawable drawable, Func<bool> ownerIsActive, Action hide)
        {
            var lease = new PresentationLease(this, drawable, ownerIsActive, hide);

            lock (presentationsLock)
                presentations.Add(lease);

            return lease;
        }

        void ICursorEffectPresentationHost.BringToFront(Drawable drawable)
        {
            if (drawable.Parent == contextMenuLayer)
                contextMenuLayer.ChangeChildDepth(drawable, --nextPresentationDepth);
            else if (drawable.Parent == popoverLayer)
                popoverLayer.ChangeChildDepth(drawable, --nextPresentationDepth);
            else if (drawable.Parent == dropdownLayer)
                dropdownLayer.ChangeChildDepth(drawable, --nextPresentationDepth);
        }

        public BufferedContainerView<Drawable> CreateOriginalView() => sceneBuffer.CreateView().With(view =>
        {
            view.RelativeSizeAxes = Axes.Both;
            view.SynchronisedDrawQuad = true;
            view.DrawMode = BufferedContainerViewDrawMode.Original;
        });

        public BufferedContainerView<Drawable> CreateView() => effectBuffer.CreateView().With(view =>
        {
            view.RelativeSizeAxes = Axes.Both;
            view.SynchronisedDrawQuad = true;
            view.DrawMode = BufferedContainerViewDrawMode.Effect;
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
            updatePresentationState();
            updateBufferingState();
            if (sceneBuffer.UsingCachedFrameBuffer && sceneBuffer.BufferingEnabled && effectBuffer.IsPresent && Time.Current >= nextEffectRefresh)
            {
                nextEffectRefresh = Time.Current + 1000.0 / 30;
                effectBuffer.ForceRedraw();
            }
        }

        private void updatePresentationState()
        {
            lock (presentationsLock)
            {
                foreach (var presentation in presentations)
                    presentation.Update();
            }
        }

        private void updateBufferingState()
        {
            bool hasActiveConsumer = false;

            // Do not walk every surface (including inherited draw-colour checks)
            // when this source explicitly forbids effects.
            if (AllowEffects())
            {
                lock (consumersLock)
                {
                    // The active surface is often behind hundreds of hidden menus.
                    // Recheck it every update, but only rescan the others when it stops
                    // consuming the backdrop. This never suppresses drawable updates.
                    var previousActive = lastActiveConsumer;
                    if (previousActive?.IsActive == true && ReferenceEquals(lastActiveConsumer, previousActive))
                        hasActiveConsumer = true;
                    else
                    {
                        lastActiveConsumer = null;
                        foreach (var consumer in consumers)
                        {
                            if (ReferenceEquals(consumer, previousActive) || !consumer.IsActive || !consumers.Contains(consumer))
                                continue;

                            lastActiveConsumer = consumer;
                            hasActiveConsumer = true;
                            break;
                        }
                    }
                }
            }
            // Consumers gate their own feature toggle (overlay or carousel glass).
            bool shouldBuffer = CaptureOriginal() || hasActiveConsumer;
            bool wasBuffering = sceneBuffer.BufferingEnabled;
            sceneBuffer.BufferingEnabled = shouldBuffer;
            if (!ReferenceEquals(effectBuffer, sceneBuffer))
                effectBuffer.Alpha = hasActiveConsumer ? 1 : 0;

            if (!shouldBuffer)
                return;

            double strength = hasActiveConsumer ? OverlayTransparency.BlurStrength.Value : 0;

            if (wasBuffering && strength == lastBlurStrength)
                return;

            lastBlurStrength = strength;
            // Sigma is in texture pixels. Background-only sources retain a full-size
            // original; general overlay effects use a separate half-size buffer.
            float effectScale = ReferenceEquals(effectBuffer, sceneBuffer) ? 1f : EFFECT_RESOLUTION_SCALE;
            var target = new Vector2((float)(OverlayTransparency.MAX_BLUR_SIGMA * strength * effectScale));

            if (!wasBuffering)
                effectBuffer.BlurSigma = target;
            else
                effectBuffer.BlurTo(target, 150, Easing.OutQuint);
        }

        private void release(ConsumerLease lease)
        {
            lock (consumersLock)
            {
                consumers.Remove(lease);
                if (ReferenceEquals(lastActiveConsumer, lease))
                    lastActiveConsumer = null;
            }
        }

        private void release(PresentationLease lease)
        {
            lock (presentationsLock)
                presentations.Remove(lease);
        }

        private partial class LiveBackgroundBuffer : BufferedContainer
        {
            private double nextRefresh;
            internal int RefreshCount { get; private set; }

            public LiveBackgroundBuffer(bool cached) : base(cachedFrameBuffer: cached)
            {
            }

            // BufferedContainer's default cache also pauses descendants. Background
            // loading, scheduled callbacks and transitions must continue every update.
            protected override bool RequiresChildrenUpdate => true;

            protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
            {
                // Descendants keep loading/animating, but may not bypass the capture budget.
                if (UsingCachedFrameBuffer && source == InvalidationSource.Child)
                    invalidation &= ~Invalidation.DrawNode;
                return base.OnInvalidate(invalidation, source);
            }

            protected override void Update()
            {
                base.Update();
                if (!UsingCachedFrameBuffer || !BufferingEnabled || Time.Current < nextRefresh)
                    return;

                // Bound the expensive full-screen capture + two blur passes, regardless
                // of the game's uncapped frame rate. No refresh work in gameplay.
                nextRefresh = Time.Current + 1000.0 / 30;
                RefreshCount++;
                ForceRedraw();
            }
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

        private sealed class PresentationLease : IDisposable
        {
            private BackdropBlurContainer? owner;
            private readonly Drawable drawable;
            private readonly Func<bool> ownerIsActive;
            private readonly Action hide;
            private bool ownerWasInactive;

            public PresentationLease(BackdropBlurContainer owner, Drawable drawable, Func<bool> ownerIsActive, Action hide)
            {
                this.owner = owner;
                this.drawable = drawable;
                this.ownerIsActive = ownerIsActive;
                this.hide = hide;
            }

            public void Update()
            {
                bool inactive = !drawable.IsAlive || !ownerIsActive();

                if (inactive && !ownerWasInactive)
                    hide();

                ownerWasInactive = inactive;
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
