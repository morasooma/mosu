using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Graphics.Containers
{
    /// <summary>Clipped background-only blur, rendered beneath sharp song-select chrome.</summary>
    public partial class SongSelectBackdropSurface : CompositeDrawable
    {
        [Resolved(CanBeNull = true)]
        private ISongSelectBackdropBlurSource? source { get; set; }

        public Func<bool> IsActive { get; set; } = () => true;

        /// <summary>Match the owning card's geometry even when its performance mode disables parent masking.</summary>
        public Func<float>? GetCornerRadius { get; set; }

        private BufferedContainerView<Drawable>? view;
        private IDisposable? lease;
        private readonly Box dim = new Box { RelativeSizeAxes = Axes.Both };
        private (bool Enabled, double Amount)? appliedAppearance;
        internal int AppearanceUpdateCount { get; private set; }

        public SongSelectBackdropSurface()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
        }

        private bool active() => OverlayTransparency.SongSelectActive && IsActive()
                                 && IsAlive && IsPresent && DrawColourInfo.Colour.MaxAlpha > 0.0001f;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (source != null)
            {
                view = source.Source.CreateView();
                lease = source.Source.Acquire(active);
                AddInternal(view);
            }
            AddInternal(dim);
        }

        protected override void Update()
        {
            base.Update();
            if (GetCornerRadius != null)
                CornerRadius = GetCornerRadius();
            bool enabled = view != null && active();
            double amount = OverlayTransparency.DimAmount.Value;
            var appearance = (enabled, amount);
            if (appliedAppearance == appearance)
                return;
            appliedAppearance = appearance;
            AppearanceUpdateCount++;
            if (view != null)
                view.Alpha = enabled ? 1 : 0;
            dim.Colour = amount >= 0 ? Colour4.Black : Colour4.White;
            dim.Alpha = enabled ? (float)Math.Abs(amount) : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            lease?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
