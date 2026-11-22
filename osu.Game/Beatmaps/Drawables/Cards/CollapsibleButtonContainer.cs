// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Graphics;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public partial class CollapsibleButtonContainer : Container
    {
        public Bindable<bool> ShowDetails = new Bindable<bool>();
        public Bindable<BeatmapSetFavouriteState> FavouriteState = new Bindable<BeatmapSetFavouriteState>();

        private readonly BeatmapDownloadTracker downloadTracker;
        private readonly BeatmapCardContentBackground coverBackground;

        internal void ChangeBeatmapSet(APIBeatmapSet set)
        {
            ShowDetails.Value = false;
            coverBackground.ChangeBeatmapSet(set);
            foreach (var button in buttons)
            {
                switch (button)
                {
                    case FavouriteButton favourite: favourite.ChangeBeatmapSet(set); break;
                    case DownloadButton download: download.ChangeBeatmapSet(set); break;
                    case GoToBeatmapButton navigate: navigate.ChangeBeatmapSet(set); break;
                }
            }
        }
        private IBindable<Colour4> themeColour = null!;

        private float buttonsExpandedWidth;

        public float ButtonsExpandedWidth
        {
            get => buttonsExpandedWidth;
            set
            {
                buttonsExpandedWidth = value;
                if (IsLoaded)
                    updateState();
            }
        }

        private float buttonsCollapsedWidth;

        public float ButtonsCollapsedWidth
        {
            get => buttonsCollapsedWidth;
            set
            {
                buttonsCollapsedWidth = value;
                if (IsLoaded)
                    updateState();
            }
        }

        public IEnumerable<BeatmapCardIconButton> Buttons => buttons;

        protected override Container<Drawable> Content => mainContent;

        private readonly Container background;

        private readonly Container buttonArea;
        private readonly Container<BeatmapCardIconButton> buttons;

        private readonly Container mainArea;
        private readonly Container mainContent;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private IBindable<float>? cardRadius;

        public CollapsibleButtonContainer(APIBeatmapSet beatmapSet, bool allowNavigationToBeatmap = true, bool keepBackgroundLoaded = false,
                                          BeatmapDownloadTracker? sharedDownloadTracker = null)
        {
            // A card already owns a tracker. Share its state instead of registering
            // a second identical Realm notification and downloader subscription.
            downloadTracker = sharedDownloadTracker ?? new BeatmapDownloadTracker(beatmapSet);

            RelativeSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = BeatmapCard.CORNER_RADIUS;

            InternalChildren = new Drawable[]
            {
                background = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White
                    },
                },
                buttonArea = new Container
                {
                    Name = @"Right (button) area",
                    RelativeSizeAxes = Axes.Y,
                    Origin = Anchor.TopRight,
                    Anchor = Anchor.TopRight,
                    Child = buttons = new Container<BeatmapCardIconButton>
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new BeatmapCardIconButton[]
                        {
                            new FavouriteButton(beatmapSet)
                            {
                                Current = FavouriteState,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.Both,
                                Height = 0.5f,
                            },
                            new DownloadButton(beatmapSet)
                            {
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                State = { BindTarget = downloadTracker.State },
                                RelativeSizeAxes = Axes.Both,
                                Height = 0.5f,
                            },
                        }
                    }
                },
                mainArea = new Container
                {
                    Name = @"Main content",
                    RelativeSizeAxes = Axes.Y,
                    CornerRadius = 0,
                    Masking = false,
                    Children = new Drawable[]
                    {
                        coverBackground = new BeatmapCardContentBackground(beatmapSet, keepBackgroundLoaded)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Dimmed = { BindTarget = ShowDetails }
                        },
                        mainContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Horizontal = 10,
                                Vertical = 4
                            },
                        }
                    }
                }
            };

            if (sharedDownloadTracker == null)
                AddInternal(downloadTracker);

            buttons.Add(new GoToBeatmapButton(beatmapSet, allowNavigationToBeatmap)
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                State = { BindTarget = downloadTracker.State },
                RelativeSizeAxes = Axes.Both,
                Height = 0.5f,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background3);
            themeColour.BindValueChanged(_ => updateState());

            cardRadius = colourProvider.GetRadiusBindable(GeometryRole.Card, BeatmapCard.CORNER_RADIUS);
            cardRadius.BindValueChanged(r =>
            {
                CornerRadius = r.NewValue;
            }, true);

            downloadTracker.State.BindValueChanged(onDownloadStateChanged);
            ShowDetails.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);
        }

        protected override void Dispose(bool isDisposing)
        {
            downloadTracker.State.ValueChanged -= onDownloadStateChanged;
            base.Dispose(isDisposing);
        }

        private void onDownloadStateChanged(ValueChangedEvent<DownloadState> _) => updateState();

        private void updateState()
        {
            buttonArea.Width = buttonsExpandedWidth;

            float buttonAreaWidth = ShowDetails.Value ? ButtonsExpandedWidth : ButtonsCollapsedWidth;
            float mainAreaWidth = Width - buttonAreaWidth;

            mainArea.ResizeWidthTo(mainAreaWidth, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);

            // By limiting the width we avoid this box showing up as an outline around the drawables that are on top of it.
            float extraWidth = System.Math.Clamp(cardRadius?.Value ?? BeatmapCard.CORNER_RADIUS, 0f, 12f);
            background.ResizeWidthTo(buttonAreaWidth + extraWidth, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            if (ButtonsCollapsedWidth == 0)
                background.FadeTo(ShowDetails.Value ? 1 : 0, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);

            background.FadeColour(downloadTracker.State.Value == DownloadState.LocallyAvailable ? colours.Lime0 : colourProvider.Background3, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            buttons.FadeTo(ShowDetails.Value ? 1 : 0, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);

            foreach (var button in buttons)
            {
                button.IdleColour = downloadTracker.State.Value != DownloadState.LocallyAvailable ? colourProvider.Light1 : colourProvider.Background3;
                button.HoverColour = downloadTracker.State.Value != DownloadState.LocallyAvailable ? colourProvider.Content1 : colourProvider.Foreground1;
            }
        }
    }
}
