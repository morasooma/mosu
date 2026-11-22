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
using osu.Game.Beatmaps.Drawables.Cards.Statistics;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapSet;
using osu.Game.Overlays.CustomTheme;
using osuTK;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public partial class BeatmapCardExtra : BeatmapCard
    {
        protected override Drawable IdleContent => idleBottomContent;
        protected override Drawable DownloadInProgressContent => downloadProgressBar;

        public const float HEIGHT = 112;

        [Cached]
        private readonly BeatmapCardContent content;

        private readonly bool keepCoversLoaded;
        private BeatmapCardThumbnail thumbnail = null!;
        private CollapsibleButtonContainer buttonContainer = null!;

        private TruncatingSpriteText titleText = null!;
        private TruncatingSpriteText artistText = null!;
        private TruncatingSpriteText sourceText = null!;
        private OsuSpriteText? mappedByText;
        private IBindable<Colour4> themeColour = null!;

        private FillFlowContainer leftIconArea = null!;
        private FillFlowContainer titleBadgeArea = null!;
        private GridContainer artistContainer = null!;
        private LinkFlowContainer mapperText = null!;
        private BeatmapCardExtraInfoRow extraInfo = null!;
        private GridContainer statisticsContainer = null!;

        private FillFlowContainer idleBottomContent = null!;
        private BeatmapCardDownloadProgressBar downloadProgressBar = null!;
        private IBindable<float>? cardRadius;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BeatmapCardExtra(APIBeatmapSet beatmapSet, bool allowExpansion = true, bool keepCoversLoaded = false)
            : base(beatmapSet, allowExpansion)
        {
            this.keepCoversLoaded = keepCoversLoaded;
            content = new BeatmapCardContent(HEIGHT);

            Action = DefaultAction;
        }

        [BackgroundDependencyLoader(true)]
        private void load(BeatmapSetOverlay? beatmapSetOverlay)
        {
            Width = WIDTH;
            Height = HEIGHT;

            cardRadius = colourProvider?.GetRadiusBindable(GeometryRole.Card, CORNER_RADIUS)
                ?? OverlayColourProvider.GetGeometryRadius(GeometryRole.Card, CORNER_RADIUS);
            cardRadius.BindValueChanged(r =>
            {
                float safeMargin = System.Math.Max(4f, (r.NewValue - 8f) * 0.25f + 4f);
                if (leftIconArea != null)
                    leftIconArea.Margin = new MarginPadding { Top = safeMargin, Left = safeMargin };
            });

            Child = content.With(c =>
            {
                c.MainContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        thumbnail = new BeatmapCardThumbnail(BeatmapSet, BeatmapSet, keepCoversLoaded)
                        {
                            Name = @"Left (icon) area",
                            Size = new Vector2(HEIGHT),
                            Padding = new MarginPadding { Right = CORNER_RADIUS },
                            Child = leftIconArea = new FillFlowContainer
                            {
                                Margin = new MarginPadding { Top = System.Math.Max(4f, (cardRadius.Value - 8f) * 0.25f + 4f), Left = System.Math.Max(4f, (cardRadius.Value - 8f) * 0.25f + 4f) },
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(1)
                            }
                        },
                        buttonContainer = new CollapsibleButtonContainer(BeatmapSet, keepBackgroundLoaded: keepCoversLoaded, sharedDownloadTracker: DownloadTracker)
                        {
                            X = HEIGHT - CORNER_RADIUS,
                            Width = WIDTH - HEIGHT + CORNER_RADIUS,
                            FavouriteState = { BindTarget = FavouriteState },
                            ButtonsCollapsedWidth = CORNER_RADIUS,
                            ButtonsExpandedWidth = 24,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension(),
                                                new Dimension(GridSizeMode.AutoSize)
                                            },
                                            RowDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.AutoSize)
                                            },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    titleText = new TruncatingSpriteText
                                                    {
                                                        Text = new RomanisableString(BeatmapSet.TitleUnicode, BeatmapSet.Title),
                                                        Font = OsuFont.Default.With(size: 18f, weight: FontWeight.SemiBold),
                                                        RelativeSizeAxes = Axes.X,
                                                    },
                                                    titleBadgeArea = new FillFlowContainer
                                                    {
                                                        Anchor = Anchor.BottomRight,
                                                        Origin = Anchor.BottomRight,
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                    }
                                                }
                                            }
                                        },
                                        artistContainer = new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension(),
                                                new Dimension(GridSizeMode.AutoSize)
                                            },
                                            RowDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.AutoSize)
                                            },
                                            Content = new[]
                                            {
                                                new[]
                                                {
                                                    artistText = new TruncatingSpriteText
                                                    {
                                                        Text = createArtistText(),
                                                        Font = OsuFont.Default.With(size: 14f, weight: FontWeight.SemiBold),
                                                        RelativeSizeAxes = Axes.X,
                                                    },
                                                    Empty()
                                                },
                                            }
                                        },
                                        sourceText = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = BeatmapSet.Source,
                                            Shadow = false,
                                            Font = OsuFont.GetFont(size: 11f, weight: FontWeight.SemiBold),
                                            Colour = colourProvider.Content2
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
                                            Spacing = new Vector2(0, 2),
                                            AlwaysPresent = true,
                                            Children = new Drawable[]
                                            {
                                                mapperText = new LinkFlowContainer(s =>
                                                {
                                                    s.Shadow = false;
                                                    s.Font = OsuFont.GetFont(size: 11f, weight: FontWeight.SemiBold);
                                                }).With(d =>
                                                {
                                                    d.AutoSizeAxes = Axes.Both;
                                                    d.Margin = new MarginPadding { Top = 1 };
                                                    d.AddText(global::osu.Game.Localisation.ClientTranslationStrings.J3221, t => { t.Colour = colourProvider.Content2; mappedByText = (OsuSpriteText)t; });
                                                    d.AddUserLink(BeatmapSet.Author);
                                                }),
                                                statisticsContainer = new GridContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    RowDimensions = new[]
                                                    {
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension(GridSizeMode.AutoSize)
                                                    },
                                                    ColumnDimensions = new[]
                                                    {
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension()
                                                    },
                                                    Content = new[]
                                                    {
                                                        new Drawable[3],
                                                        new Drawable[3]
                                                    }
                                                },
                                                extraInfo = new BeatmapCardExtraInfoRow(BeatmapSet)
                                            }
                                        },
                                        downloadProgressBar = new BeatmapCardDownloadProgressBar
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 5,
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
                c.ExpandedContentFactory = () => new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 8, Vertical = 10 },
                    Child = new BeatmapCardDifficultyList(BeatmapSet)
                };
                c.Expanded.BindTarget = Expanded;
            });

            updateBadges();
            createStatistics();

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                titleText.Colour = colourProvider.Content1;
                artistText.Colour = colourProvider.Content2;
                sourceText.Colour = colourProvider.Content2;
                if (mappedByText != null)
                    mappedByText.Colour = colourProvider.Content2;
            }, true);
        }

        protected override void OnBeatmapSetChanged()
        {
            content.ResetModel();
            thumbnail.ChangeBeatmapSet(BeatmapSet, BeatmapSet);
            buttonContainer.ChangeBeatmapSet(BeatmapSet);
            titleText.Text = new RomanisableString(BeatmapSet.TitleUnicode, BeatmapSet.Title);
            artistText.Text = createArtistText();
            sourceText.Text = BeatmapSet.Source;
            mapperText.Clear();
            mapperText.AddText(global::osu.Game.Localisation.ClientTranslationStrings.J3221, t => { t.Colour = colourProvider.Content2; mappedByText = (OsuSpriteText)t; });
            mapperText.AddUserLink(BeatmapSet.Author);
            extraInfo.ChangeBeatmapSet(BeatmapSet);
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                {
                    retireCell(statisticsContainer.Content[row][col]);
                    statisticsContainer.Content[row][col] = null!;
                }
            createStatistics();
            content.ExpandedContentFactory = () => new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 8, Vertical = 10 },
                Child = new BeatmapCardDifficultyList(BeatmapSet)
            };
            updateBadges();
            UpdateState();
        }

        private void updateBadges()
        {
            leftIconArea.Clear();
            titleBadgeArea.Clear();
            retireCell(artistContainer.Content[0][1]);
            artistContainer.Content[0][1] = Empty();
            if (BeatmapSet.HasVideo)
                leftIconArea.Add(new VideoIconPill());

            if (BeatmapSet.HasStoryboard)
                leftIconArea.Add(new StoryboardIconPill());

            if (BeatmapSet.FeaturedInSpotlight)
            {
                titleBadgeArea.Add(new SpotlightBeatmapBadge
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Left = 4 }
                });
            }

            if (BeatmapSet.IsServerExclusive())
            {
                titleBadgeArea.Add(new ServerExclusiveBeatmapBadge
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Left = 4 }
                });
            }

            if (BeatmapSet.HasExplicitContent)
            {
                titleBadgeArea.Add(new ExplicitContentBeatmapBadge
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Left = 4 }
                });
            }

            if (BeatmapSet.TrackId != null)
            {
                artistContainer.Content[0][1] = new FeaturedArtistBeatmapBadge
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Left = 4 }
                };
            }

        }

        private void retireCell(Drawable? drawable)
        {
            if (drawable == null) return;
            if (drawable.Parent is Container parent)
                parent.Remove(drawable, false);
            drawable.Dispose();
        }

        private LocalisableString createArtistText()
        {
            var romanisableArtist = new RomanisableString(BeatmapSet.ArtistUnicode, BeatmapSet.Artist);
            return BeatmapsetsStrings.ShowDetailsByArtist(romanisableArtist);
        }

        private void createStatistics()
        {
            BeatmapCardStatistic withMargin(BeatmapCardStatistic original)
            {
                original.Margin = new MarginPadding { Right = 8 };
                return original;
            }

            var hypesStatistic = HypesStatistic.CreateFor(BeatmapSet);
            if (hypesStatistic != null)
                statisticsContainer.Content[0][0] = withMargin(hypesStatistic);

            var nominationsStatistic = NominationsStatistic.CreateFor(BeatmapSet);
            if (nominationsStatistic != null)
                statisticsContainer.Content[1][0] = withMargin(nominationsStatistic);

            statisticsContainer.Content[0][1] = withMargin(new PlayCountStatistic(BeatmapSet));

            statisticsContainer.Content[1][1] = withMargin(new FavouritesStatistic(BeatmapSet)
            {
                Current = FavouriteState,
            });

            var dateStatistic = BeatmapCardDateStatistic.CreateFor(BeatmapSet);
            if (dateStatistic != null)
                statisticsContainer.Content[0][2] = withMargin(dateStatistic);
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            bool showDetails = IsHovered || Expanded.Value;

            buttonContainer.ShowDetails.Value = showDetails;
            thumbnail.Dimmed.Value = showDetails;
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
