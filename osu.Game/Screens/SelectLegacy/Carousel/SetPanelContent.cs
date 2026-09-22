// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

using osu.Game.Screens.Select;

namespace osu.Game.Screens.SelectLegacy.Carousel
{
    public partial class SetPanelContent : CompositeDrawable
    {
        // Disallow interacting with difficulty icons on a panel until the panel has been selected.
        public override bool PropagatePositionalInputSubTree => carouselSet.State.Value == CarouselItemState.Selected;

        private const float metadata_padding_left = 18;
        private const float legacy_preview_metadata_gap = 14;
        private const float legacy_preview_aspect_ratio = 16f / 9f;

        private readonly CarouselBeatmapSet carouselSet;
        private readonly BindableBool useLegacyPreviewLayout = new BindableBool();

        private FillFlowContainer<DifficultyIcon> iconFlow = null!;
        private FillFlowContainer contentFlow = null!;

        public SetPanelContent(CarouselBeatmapSet carouselSet)
        {
            this.carouselSet = carouselSet;

            // required to ensure we load as soon as any part of the panel comes on screen
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var beatmapSet = carouselSet.BeatmapSet;

            InternalChild = contentFlow = new FillFlowContainer
            {
                // required to ensure we load as soon as any part of the panel comes on screen
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Top = 5, Left = metadata_padding_left, Right = 10, Bottom = 10 },
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title),
                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 22, italics: true),
                        Shadow = true,
                    },
                    new OsuSpriteText
                    {
                        Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist),
                        Font = OsuFont.GetFont(weight: FontWeight.SemiBold, size: 17, italics: true),
                        Shadow = true,
                    },
                    new FillFlowContainer
                    {
                        Direction = FillDirection.Horizontal,
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding { Top = 5 },
                        Spacing = new Vector2(5),
                        Children = new[]
                        {
                            beatmapSet.AllBeatmapsUpToDate
                                ? Empty()
                                : new Container
                                {
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new UpdateBeatmapSetButton(beatmapSet),
                                    }
                                },
                            new BeatmapSetOnlineStatusPill
                            {
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                TextSize = 11,
                                TextPadding = new MarginPadding { Horizontal = 8, Vertical = 2 },
                                Status = beatmapSet.Status
                            },
                            iconFlow = new FillFlowContainer<DifficultyIcon>
                            {
                                AutoSizeAxes = Axes.Both,
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                Spacing = new Vector2(3),
                            },
                        }
                    }
                }
            };

            config.BindWith(OsuSetting.ForkSongSelectOldCarouselPreviews, useLegacyPreviewLayout);
            useLegacyPreviewLayout.BindValueChanged(_ => updateMetadataPadding(), true);
        }

        protected override void Update()
        {
            base.Update();

            updateMetadataPadding();
        }

        /// <summary>
        /// In the legacy preview layout the text is shifted right of the 16:9 preview block, matching the current carousel.
        /// </summary>
        private void updateMetadataPadding()
        {
            float left = metadata_padding_left;

            if (useLegacyPreviewLayout.Value && DrawSize.X > 0)
            {
                float previewWidth = Math.Min(DrawWidth, DrawHeight * legacy_preview_aspect_ratio);
                left = Math.Max(metadata_padding_left, previewWidth + legacy_preview_metadata_gap);
            }

            contentFlow.Padding = new MarginPadding { Top = 5, Left = left, Right = 10, Bottom = 10 };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            iconFlow.ChildrenEnumerable = getDifficultyIcons();
        }

        private const int maximum_difficulty_icons = 18;

        private IEnumerable<DifficultyIcon> getDifficultyIcons()
        {
            var beatmaps = carouselSet.Beatmaps.ToList();

            return beatmaps.Count > maximum_difficulty_icons
                ? beatmaps.GroupBy(b => b.BeatmapInfo.Ruleset)
                          .Select(group => new GroupedDifficultyIcon(group.ToList(), group.Last().BeatmapInfo.Ruleset))
                : beatmaps.Select(b => new FilterableDifficultyIcon(b));
        }
    }
}
