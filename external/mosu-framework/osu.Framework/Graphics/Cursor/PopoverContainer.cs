// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osuTK;

namespace osu.Framework.Graphics.Cursor
{
    public partial class PopoverContainer : Container
    {
        private readonly Container content;
        private readonly Container dismissOnMouseDownContainer;

        [Resolved(CanBeNull = true)]
        private ICursorEffectPresentationHost? presentationHost { get; set; }

        private IDisposable? presentationLease;

        /// <summary>
        /// The target drawable which is providing the popover for display. <c>null</c> if no popover is displayed.
        /// </summary>
        public IHasPopover? CurrentTarget { get; private set; }

        /// <summary>
        /// The current popover being displayed. <c>null</c> if no popover is displayed.
        /// </summary>
        public Popover? CurrentPopover { get; private set; }

        protected override Container<Drawable> Content => content;

        public PopoverContainer()
        {
            InternalChildren = new Drawable[]
            {
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                dismissOnMouseDownContainer = new DismissOnMouseDownContainer(() => SetTarget(null))
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (presentationHost is Drawable hostDrawable && !this.IsRootedAt(hostDrawable))
                presentationHost = null;

            if (presentationHost == null)
                return;

            RemoveInternal(dismissOnMouseDownContainer, false);
            presentationHost.PopoverLayer.Add(dismissOnMouseDownContainer);
            presentationLease = presentationHost.Register(dismissOnMouseDownContainer, isActuallyVisible, () => SetTarget(null));
        }

        /// <summary>
        /// Sets the target drawable for this <see cref="PopoverContainer"/> to <paramref name="newTarget"/>.
        /// </summary>
        /// <remarks>
        /// After calling this method, the previous popover shown in this <see cref="PopoverContainer"/> will be hidden.
        /// This method can be called with a <see langword="null"/> argument to hide the currently-visible popover.
        /// </remarks>
        /// <returns><see langword="true"/> if a new popover was shown, <see langword="false"/> otherwise.</returns>
        internal bool SetTarget(IHasPopover? newTarget)
        {
            if (CurrentPopover != null)
            {
                CurrentPopover.Owner = null;
                CurrentPopover.Hide();
                CurrentPopover.Expire();
            }

            CurrentTarget = newTarget;

            var newPopover = CurrentTarget?.GetPopover();
            if (newPopover == null)
                return false;

            newPopover.Owner = this;
            dismissOnMouseDownContainer.Add(CurrentPopover = newPopover);
            presentationHost?.BringToFront(dismissOnMouseDownContainer);
            CurrentPopover.Show();
            return true;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if ((CurrentTarget as Drawable)?.FindClosestParent<PopoverContainer>() != this || CurrentTarget?.IsPresent != true)
            {
                SetTarget(null);
                return;
            }

            updatePopoverPositioning();
        }

        protected override void OnSizingChanged()
        {
            base.OnSizingChanged();

            // reset to none to prevent exceptions
            content.RelativeSizeAxes = Axes.None;
            content.AutoSizeAxes = Axes.None;

            // in addition to using this.RelativeSizeAxes, sets RelativeSizeAxes on every axis that is neither relative size nor auto size
            content.RelativeSizeAxes = Axes.Both & ~AutoSizeAxes;
            content.AutoSizeAxes = AutoSizeAxes;
        }

        private void updatePopoverPositioning()
        {
            if (CurrentTarget == null || CurrentPopover == null)
                return;

            var presentationSpace = presentationHost?.PopoverLayer ?? this;
            var targetLocalQuad = presentationSpace.ToLocalSpace(CurrentTarget.ScreenSpaceDrawQuad);

            Anchor bestAnchor = Anchor.Centre;
            float biggestArea = 0;

            float totalSize = Math.Max(presentationSpace.DrawSize.X * presentationSpace.DrawSize.Y, 1);

            foreach (var anchor in CurrentPopover.AllowableAnchors)
            {
                // Compute how much free space is available on this side of the target.
                var availableSize = availableSizeAroundTargetForAnchor(targetLocalQuad, anchor, presentationSpace.DrawSize);
                float area = availableSize.X * availableSize.Y / totalSize;

                // If the free space is insufficient for the popover to fit in, do not consider this anchor further.
                if (availableSize.X < CurrentPopover.BoundingBoxContainer.DrawWidth || availableSize.Y < CurrentPopover.BoundingBoxContainer.DrawHeight)
                    continue;

                // The heuristic used to find the "best" anchor is the biggest area of free space available in the popover container
                // on the side of the anchor.
                if (Precision.DefinitelyBigger(area, biggestArea, 0.01f))
                {
                    biggestArea = area;
                    bestAnchor = anchor;
                }
            }

            CurrentPopover.PopoverAnchor = bestAnchor.Opposite();

            var positionOnQuad = bestAnchor.PositionOnQuad(targetLocalQuad);
            CurrentPopover.Position = new Vector2(positionOnQuad.X - (presentationHost == null ? Padding.Left : 0), positionOnQuad.Y - (presentationHost == null ? Padding.Top : 0));

            // While the side has been chosen to maximise the area of free space available, that doesn't mean that the popover's body
            // will still fit in its entirety in the default configuration.
            // To avoid this, offset the popover so that it fits in the bounds of this container.
            var adjustment = new Vector2();

            var popoverContentLocalQuad = presentationSpace.ToLocalSpace(CurrentPopover.Body.ScreenSpaceDrawQuad);
            if (popoverContentLocalQuad.TopLeft.X < 0)
                adjustment.X = -popoverContentLocalQuad.TopLeft.X;
            else if (popoverContentLocalQuad.BottomRight.X > presentationSpace.DrawWidth)
                adjustment.X = presentationSpace.DrawWidth - popoverContentLocalQuad.BottomRight.X;
            if (popoverContentLocalQuad.TopLeft.Y < 0)
                adjustment.Y = -popoverContentLocalQuad.TopLeft.Y;
            else if (popoverContentLocalQuad.BottomRight.Y > presentationSpace.DrawHeight)
                adjustment.Y = presentationSpace.DrawHeight - popoverContentLocalQuad.BottomRight.Y;

            CurrentPopover.Position += adjustment;

            // Even if the popover was moved, the arrow should stay fixed in place and point at the target's centre.
            // In such a case, apply a counter-adjustment to the arrow position.
            // The reason why just the body isn't moved is that the popover's autosize does not play well with that
            // (setting X/Y on the body can lead BoundingBox to be larger than it actually needs to be, causing 1-frame-errors)
            CurrentPopover.Arrow.Position = -adjustment;
        }

        /// <summary>
        /// Computes the available size around the <paramref name="targetLocalQuad"/> on the side of it indicated by <paramref name="anchor"/>
        /// </summary>
        private static Vector2 availableSizeAroundTargetForAnchor(Quad targetLocalQuad, Anchor anchor, Vector2 presentationSize)
        {
            Vector2 availableSize = new Vector2();

            // left anchor = area to the left of the quad, right anchor = area to the right of the quad.
            // for horizontal centre assume we have the whole quad width to work with.
            if (anchor.HasFlagFast(Anchor.x0))
                availableSize.X = MathF.Max(0, targetLocalQuad.TopLeft.X);
            else if (anchor.HasFlagFast(Anchor.x2))
                availableSize.X = MathF.Max(0, presentationSize.X - targetLocalQuad.BottomRight.X);
            else
                availableSize.X = presentationSize.X;

            // top anchor = area above quad, bottom anchor = area below quad.
            // for vertical centre assume we have the whole quad height to work with.
            if (anchor.HasFlagFast(Anchor.y0))
                availableSize.Y = MathF.Max(0, targetLocalQuad.TopLeft.Y);
            else if (anchor.HasFlagFast(Anchor.y2))
                availableSize.Y = MathF.Max(0, presentationSize.Y - targetLocalQuad.BottomRight.Y);
            else
                availableSize.Y = presentationSize.Y;

            // the final size is the intersection of the X/Y areas.
            return availableSize;
        }

        private bool isActuallyVisible() => IsAlive && IsPresent && DrawColourInfo.Colour.MaxAlpha > 0.0001f;

        protected override void Dispose(bool isDisposing)
        {
            presentationLease?.Dispose();
            presentationLease = null;

            if (presentationHost != null && dismissOnMouseDownContainer.Parent == presentationHost.PopoverLayer)
                dismissOnMouseDownContainer.Expire();

            base.Dispose(isDisposing);
        }

        private partial class DismissOnMouseDownContainer : Container
        {
            private readonly Action dismiss;

            public DismissOnMouseDownContainer(Action dismiss)
            {
                this.dismiss = dismiss;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                dismiss();
                return false;
            }
        }
    }
}
