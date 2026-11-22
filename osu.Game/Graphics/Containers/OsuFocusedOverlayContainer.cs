// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;

namespace osu.Game.Graphics.Containers
{
    public abstract partial class OsuFocusedOverlayContainer : FocusedOverlayContainer, IPreviewTrackOwner, IKeyBindingHandler<GlobalAction>
    {
        protected readonly IBindable<OverlayActivation> OverlayActivationMode = new Bindable<OverlayActivation>(OverlayActivation.All);

        protected virtual string? PopInSampleName => @"UI/overlay-pop-in";
        protected virtual string? PopOutSampleName => @"UI/overlay-pop-out";
        protected virtual double PopInOutSampleBalance => 0;

        protected override bool BlockNonPositionalInput => true;

        /// <summary>
        /// Temporary to allow for overlays in the main screen content to not dim themselves.
        /// Should be eventually replaced by dimming which is aware of the target dim container (traverse parent for certain interface type?).
        /// </summary>
        protected virtual bool DimMainContent => true;

        /// <summary>
        /// A substantial opaque background, not a control, icon or the screen-wide dim.
        /// Overlays without one keep the default rendering path.
        /// </summary>
        protected virtual BackdropBlurSurface? OpaqueBackground => null;

        internal virtual bool CanUseOpaqueDrawPrepass => IsLoaded
                                                       && State.Value == Visibility.Visible
                                                       && !OverlayTransparency.Enabled.Value
                                                       && HasOpaqueBackground;

        protected virtual bool HasOpaqueBackground => IsOpaqueBackgroundReady(OpaqueBackground);

        protected bool IsOpaqueBackgroundReady(BackdropBlurSurface? background)
        {
            if (background?.IsLoaded != true || background.SurfaceColour.A != 1 || background.DrawSize.X <= 0 || background.DrawSize.Y <= 0)
                return false;

            // Check the actual background and its ancestor chain, not just the overlay's
            // local Alpha. Some overlays animate their content rather than themselves.
            for (Drawable? current = background; current != null; current = current.Parent)
            {
                if (!current.IsPresent || current.DrawColourInfo.Colour.MinAlpha != 1 || current.LatestTransformEndTime > current.Time.Current)
                    return false;

                if (current == this)
                    return true;
            }

            return false;
        }

        [Resolved]
        private IOverlayManager? overlayManager { get; set; }

        [Resolved]
        private PreviewTrackManager previewTrackManager { get; set; } = null!;

        private Sample? samplePopIn;
        private Sample? samplePopOut;
        private IBindable<osu.Game.Configuration.InterfaceAnimationMode>? animationMode;

        [BackgroundDependencyLoader]
        private void load(AudioManager? audio)
        {
            if (!string.IsNullOrEmpty(PopInSampleName))
                samplePopIn = audio?.Samples.Get(PopInSampleName);

            if (!string.IsNullOrEmpty(PopOutSampleName))
                samplePopOut = audio?.Samples.Get(PopOutSampleName);
        }

        protected override void LoadComplete()
        {
            if (overlayManager != null)
                OverlayActivationMode.BindTo(overlayManager.OverlayActivationMode);

            OverlayActivationMode.BindValueChanged(mode =>
            {
                if (mode.NewValue == OverlayActivation.Disabled)
                    State.Value = Visibility.Hidden;
            }, true);

            animationMode = InterfaceAnimation.Observe(this, true);
            base.LoadComplete();
        }

        /// <summary>
        /// Whether mouse input should be blocked screen-wide while this overlay is visible.
        /// Performing mouse actions outside of the valid extents will hide the overlay.
        /// </summary>
        public virtual bool BlockScreenWideMouse => BlockPositionalInput;

        // receive input outside our bounds so we can trigger a close event on ourselves.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => BlockScreenWideMouse || base.ReceivePositionalInputAt(screenSpacePos);

        private bool closeOnMouseUp;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            closeOnMouseUp = !base.ReceivePositionalInputAt(e.ScreenSpaceMousePosition);

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (closeOnMouseUp && !base.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                Hide();

            base.OnMouseUp(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // allow for controlling volume when alt is held.
            // mostly for compatibility with osu-stable.
            if (e.AltPressed) return false;

            return true;
        }

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.Back:
                    Hide();
                    return true;

                case GlobalAction.Select:
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override void UpdateState(ValueChangedEvent<Visibility> state)
        {
            bool didChange = state.NewValue != state.OldValue;

            switch (state.NewValue)
            {
                case Visibility.Visible:
                    if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                    {
                        // todo: visual/audible feedback that this operation could not complete.
                        State.Value = Visibility.Hidden;
                        return;
                    }

                    if (didChange && samplePopIn != null)
                    {
                        samplePopIn.Balance.Value = PopInOutSampleBalance;
                        samplePopIn.Play();
                    }

                    if (BlockScreenWideMouse && DimMainContent) overlayManager?.ShowBlockingOverlay(this);
                    break;

                case Visibility.Hidden:
                    if (didChange && samplePopOut != null)
                    {
                        samplePopOut.Balance.Value = PopInOutSampleBalance;
                        samplePopOut.Play();
                    }

                    if (BlockScreenWideMouse) overlayManager?.HideBlockingOverlay(this);
                    break;
            }

            base.UpdateState(state);

            if (InterfaceAnimation.Mode.Value == osu.Game.Configuration.InterfaceAnimationMode.Disabled)
            {
                // Only the overlay's own transition; never flush scheduled actions in its children.
                foreach (string member in new[] { "Alpha", "Position", "Scale" })
                    FinishTransforms(false, member);
            }
        }

        protected override void PopOut()
        {
            previewTrackManager.StopAnyPlaying(this);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            overlayManager?.HideBlockingOverlay(this);
        }
    }
}
