// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public partial class BeatmapCardContentBackground : CompositeDrawable
    {
        public BindableBool Dimmed { get; } = new BindableBool();

        private readonly Box background;
        private readonly UpdateableOnlineBeatmapSetCover cover;

        internal void ChangeBeatmapSet(IBeatmapSetOnlineInfo info)
        {
            Dimmed.Value = false;
            cover.ResetOnlineInfo(info);
        }
        private IBindable<Colour4> themeColour = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BeatmapCardContentBackground(IBeatmapSetOnlineInfo onlineInfo, bool keepLoaded = false)
        {
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                cover = new UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType.Card, keepLoaded ? 0 : 500, keepLoaded ? double.MaxValue : 1000, displayPlaceholder: false)
                {
                    RelativeSizeAxes = Axes.Both,
                    OnlineInfo = onlineInfo,
                    Colour = Colour4.Transparent
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Background2);
            // Initialise before the first draw. Scheduling a FadeColour from a
            // Box's default white here could expose a white card on first publication.
            background.Colour = Dimmed.Value ? colourProvider.Background4 : colourProvider.Background2;
            cover.Colour = ColourInfo.GradientHorizontal(Colour4.White.Opacity(0), Colour4.White.Opacity(0.2f));
            themeColour.BindValueChanged(_ => updateState());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Dimmed.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);
        }

        private void updateState() => Schedule(() =>
        {
            background.FadeColour(Dimmed.Value ? colourProvider.Background4 : colourProvider.Background2, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);

            var gradient = ColourInfo.GradientHorizontal(Colour4.White.Opacity(0), Colour4.White.Opacity(0.2f));
            cover.FadeColour(gradient, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
        });
    }
}
