// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public partial class BeatmapCardNano : BeatmapCard
    {
        protected override Drawable IdleContent => idleBottomContent;
        protected override Drawable DownloadInProgressContent => downloadProgressBar;

        public override float Width
        {
            get => base.Width;
            set
            {
                base.Width = value;

                if (LoadState >= LoadState.Ready)
                    buttonContainer.Width = value;
            }
        }

        public const float HEIGHT = 60;
        public new const float WIDTH = 300;

        [Cached]
        private readonly BeatmapCardContent content;

        private readonly bool keepCoversLoaded;
        private CollapsibleButtonContainer buttonContainer = null!;

        private FillFlowContainer idleBottomContent = null!;
        private BeatmapCardDownloadProgressBar downloadProgressBar = null!;
        private TruncatingSpriteText titleText = null!;
        private TruncatingSpriteText artistText = null!;
        private OsuSpriteText? mappedByText;
        private LinkFlowContainer mapperText = null!;
        private IBindable<Colour4> themeColour = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BeatmapCardNano(APIBeatmapSet beatmapSet, bool keepCoversLoaded = false)
            : base(beatmapSet, false)
        {
            this.keepCoversLoaded = keepCoversLoaded;
            content = new BeatmapCardContent(HEIGHT);

            Action = DefaultAction;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Width = WIDTH;
            Height = HEIGHT;

            Child = content.With(c =>
            {
                c.MainContent = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = HEIGHT,
                    Children = new Drawable[]
                    {
                        buttonContainer = new CollapsibleButtonContainer(BeatmapSet, keepBackgroundLoaded: keepCoversLoaded, sharedDownloadTracker: DownloadTracker)
                        {
                            Width = Width,
                            FavouriteState = { BindTarget = FavouriteState },
                            ButtonsCollapsedWidth = 5,
                            ButtonsExpandedWidth = 30,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        titleText = new TruncatingSpriteText
                                        {
                                            Text = new RomanisableString(BeatmapSet.TitleUnicode, BeatmapSet.Title),
                                            Font = OsuFont.Default.With(size: 19, weight: FontWeight.SemiBold),
                                            RelativeSizeAxes = Axes.X,
                                        },
                                        artistText = new TruncatingSpriteText
                                        {
                                            Text = createArtistText(),
                                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                                            RelativeSizeAxes = Axes.X,
                                        },
                                    }
                                },
                                new Container
                                {
                                    Name = @"Bottom content",
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    Children = new Drawable[]
                                    {
                                        idleBottomContent = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 3),
                                            AlwaysPresent = true,
                                            Children = new Drawable[]
                                            {
                                                mapperText = new LinkFlowContainer(s =>
                                                {
                                                    s.Shadow = false;
                                                    s.Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold);
                                                }).With(d =>
                                                {
                                                    d.AutoSizeAxes = Axes.Both;
                                                    d.Margin = new MarginPadding { Top = 2 };
                                                    d.AddText(global::osu.Game.Localisation.ClientTranslationStrings.J3221, t => { t.Colour = colourProvider.Content2; mappedByText = (OsuSpriteText)t; });
                                                    d.AddUserLink(BeatmapSet.Author);
                                                }),
                                            }
                                        },
                                        downloadProgressBar = new BeatmapCardDownloadProgressBar
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 6,
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            State = { BindTarget = DownloadTracker.State },
                                            Progress = { BindTarget = DownloadTracker.Progress }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                // Nano cards never expand; do not allocate an unreachable difficulty list.
                c.Expanded.BindTarget = Expanded;
            });

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                titleText.Colour = colourProvider.Content1;
                artistText.Colour = colourProvider.Content2;
                if (mappedByText != null)
                    mappedByText.Colour = colourProvider.Content2;
            }, true);
        }

        protected override void OnBeatmapSetChanged()
        {
            content.ResetModel();
            buttonContainer.ChangeBeatmapSet(BeatmapSet);
            titleText.Text = new RomanisableString(BeatmapSet.TitleUnicode, BeatmapSet.Title);
            artistText.Text = createArtistText();
            mapperText.Clear();
            mapperText.AddText(global::osu.Game.Localisation.ClientTranslationStrings.J3221, t => { t.Colour = colourProvider.Content2; mappedByText = (OsuSpriteText)t; });
            mapperText.AddUserLink(BeatmapSet.Author);
            UpdateState();
        }

        private LocalisableString createArtistText()
        {
            var romanisableArtist = new RomanisableString(BeatmapSet.ArtistUnicode, BeatmapSet.Artist);
            return BeatmapsetsStrings.ShowDetailsByArtist(romanisableArtist);
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            bool showDetails = IsHovered;

            buttonContainer.ShowDetails.Value = showDetails;
        }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                var items = base.ContextMenuItems.ToList();

                foreach (var button in buttonContainer.Buttons)
                {
                    if (button.Enabled.Value)
                        items.Add(new OsuMenuItem(button.TooltipText.ToSentence(), MenuItemType.Standard, () => button.TriggerClick()));
                }

                return items.ToArray();
            }
        }
    }
}
