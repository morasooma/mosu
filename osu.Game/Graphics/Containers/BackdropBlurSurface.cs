// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// A self-contained glass background. Only this drawable becomes translucent;
    /// palette colours and unrelated UI remain untouched.
    /// </summary>
    public partial class BackdropBlurSurface : CompositeDrawable
    {
        [Resolved(CanBeNull = true)]
        private IBackdropBlurSource? backdropSource { get; set; }

        private readonly Box surfaceTint;
        private readonly Box dimVeil;
        private BufferedContainerView<Drawable>? backdropView;
        private IDisposable? consumerLease;

        private Color4 surfaceColour = Color4.Black;

        public Color4 SurfaceColour
        {
            get => surfaceColour;
            set
            {
                surfaceColour = value;
                updateAppearance();
            }
        }

        public BackdropBlurSurface()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;

            surfaceTint = new Box { RelativeSizeAxes = Axes.Both };
            dimVeil = new Box { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (backdropSource != null)
            {
                backdropView = backdropSource.CreateView();
                consumerLease = backdropSource.Acquire(isActuallyVisible);
            }

            InternalChildren = backdropView == null
                ? new Drawable[] { surfaceTint, dimVeil }
                : new Drawable[] { backdropView, surfaceTint, dimVeil };

            updateAppearance();
        }

        protected override void Update()
        {
            base.Update();

            updateAppearance();
        }

        // IsPresent only accounts for this drawable's local alpha. The final draw colour also
        // includes every parent. This callback is evaluated by the always-updating source, so it
        // also works after a hidden parent has stopped updating its children.
        private bool isActuallyVisible() => IsAlive && IsPresent && DrawColourInfo.Colour.MaxAlpha > 0.0001f;

        private void updateAppearance()
        {
            bool glassActive = backdropView != null && OverlayTransparency.Enabled.Value && isActuallyVisible();

            if (backdropView != null)
                backdropView.Alpha = glassActive ? 1 : 0;

            surfaceTint.Colour = glassActive ? surfaceColour.Opacity(OverlayTransparency.SURFACE_ALPHA) : surfaceColour;

            double dimAmount = OverlayTransparency.DimAmount.Value;
            dimVeil.Colour = dimAmount >= 0 ? Color4.Black : Color4.White;
            dimVeil.Alpha = glassActive ? (float)Math.Abs(dimAmount) : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            consumerLease?.Dispose();
            consumerLease = null;
            base.Dispose(isDisposing);
        }
    }
}
