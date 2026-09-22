// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmapSet : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;
        public const float CLASSIC_HEIGHT = CarouselItem.DEFAULT_HEIGHT * 2f;
        private const float legacy_expanded_background_alpha = 0.92f;

        private const float metadata_padding_left = 15;
        private const float legacy_preview_metadata_gap = 14;

        public Bindable<HashSet<BeatmapInfo>?> VisibleBeatmaps { get; } = new Bindable<HashSet<BeatmapInfo>?>();

        private readonly BindableBool useLegacyPreviewLayout = new BindableBool();
        private readonly BindableBool useSkinnedLegacyCarousel = new BindableBool();

        private Container chevronBackgroundContainer = null!;
        private Box chevronBackground = null!;
        private PanelSetBackground setBackground = null!;
        private Box classicSelectionBackground = null!;
        private Box classicInactiveBackground = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private FillFlowContainer metadataFlow = null!;
        private FillFlowContainer modernDetailsFlow = null!;
        private FillFlowContainer classicDifficultyFlow = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private OsuSpriteText classicDifficultyText = null!;
        private Drawable chevronIcon = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;
        private BeatmapSetOnlineStatusPill importedPill = null!;
        private SpreadDisplay spreadDisplay = null!;
        private IBindable<Colour4> themeColour = null!;

        private float lastMetadataPaddingLeft = float.NaN;
        private float lastChevronBackgroundWidth = float.NaN;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> selectedBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapCarousel? beatmapCarousel { get; set; }

        private GroupedBeatmapSet groupedBeatmapSet
        {
            get
            {
                Debug.Assert(Item != null);
                return (GroupedBeatmapSet)Item!.Model;
            }
        }

        internal bool MetadataClearsLegacyPreview
            => metadataFlow.Padding.Left >= PanelSetBackground.GetLegacyPreviewWidth(Content.DrawWidth, Content.DrawHeight);

        internal bool ShowsSelectedDifficulty => classicDifficultyFlow.Alpha > 0;

        internal string DisplayedSelectedDifficulty => classicDifficultyText.Text.ToString();

        internal bool SelectedDifficultyUsesActiveTextColour
            => classicDifficultyFlow.Colour == setBackground.LegacyActiveTextColour;

        internal bool ShowsClassicDifficultyStars
            => classicDifficultyFlow.Children.Any(child => child is StarCounter && child.Alpha > 0);

        internal Color4 TitleTextColour => titleText.Colour;
        internal Color4 ArtistTextColour => artistText.Colour;
        internal Color4 ChevronIconColour => chevronIcon.Colour;

        internal bool LegacyInactiveFullCardTintIsStablePink
            => !useSkinnedLegacyCarousel.Value || Expanded.Value || classicInactiveBackground.Colour == new Color4(235, 73, 153, 255);

        internal bool LegacySkinnedButtonBackgroundVisible
            => !useSkinnedLegacyCarousel.Value || setBackground.SkinnedLegacyModeEnabled;

        internal bool LegacyExpandedBackgroundMatchesPreview
        {
            get
            {
                if (!(useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value) || !Expanded.Value || TopLevelContent.DrawWidth <= 0)
                    return true;

                float previewWidth = PanelSetBackground.GetLegacyPreviewWidth(Content.DrawWidth, Content.DrawHeight, useSkinnedLegacyCarousel.Value);
                float contentOffset = (Content.Parent?.DrawPosition.X ?? 0) + Content.DrawPosition.X;
                float expectedWidth = Math.Min(TopLevelContent.DrawWidth, contentOffset + previewWidth);

                return Math.Abs(chevronBackgroundContainer.DrawWidth - expectedWidth) <= 1f;
            }
        }

        public PanelBeatmapSet()
        {
            PanelXOffset = 20f;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkSongSelectOldCarouselPreviews, useLegacyPreviewLayout);
            ForkSongSelectStyleBinding.BindSkinnedLegacyCarousel(config, useSkinnedLegacyCarousel, () => songSelect is SoloSongSelect);
            useLegacyPreviewLayout.BindValueChanged(_ => ScheduleAfterChildren(updatePreviewLayoutState));
            useSkinnedLegacyCarousel.BindValueChanged(_ => ScheduleAfterChildren(updatePreviewLayoutState));

            Height = HEIGHT;

            Icon = chevronIcon = new Container
            {
                Size = new Vector2(0, 22),
                Child = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.ChevronRight,
                    Size = new Vector2(8),
                    X = 1f,
                    Colour = colourProvider.Background5,
                },
            };

            Background = chevronBackgroundContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Width = 1,
                Alpha = 0f,
                Child = chevronBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            };

            Content.Children = new Drawable[]
            {
                setBackground = new PanelSetBackground(),
                classicInactiveBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0, 150, 236, 255),
                    Alpha = 0,
                },
                classicSelectionBackground = new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0,
                },
                metadataFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Top = 7.5f, Left = 15, Bottom = 13 },
                    Children = new Drawable[]
                    {
                        titleText = new OsuSpriteText
                        {
                            Font = OsuFont.Style.Heading1.With(typeface: Typeface.TorusAlternate),
                        },
                        artistText = new OsuSpriteText
                        {
                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                        },
                        classicDifficultyFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Alpha = 0,
                            Child = classicDifficultyText = new OsuSpriteText
                            {
                                Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                            },
                        },
                        modernDetailsFlow = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding { Top = 4f },
                            Children = new Drawable[]
                            {
                                statusPill = new BeatmapSetOnlineStatusPill
                                {
                                    Origin = Anchor.CentreLeft,
                                    Anchor = Anchor.CentreLeft,
                                    TextSize = OsuFont.Style.Caption2.Size,
                                    Margin = new MarginPadding { Right = 5f },
                                    Animated = false,
                                },
                                importedPill = new BeatmapSetOnlineStatusPill
                                {
                                    Origin = Anchor.CentreLeft,
                                    Anchor = Anchor.CentreLeft,
                                    TextSize = OsuFont.Style.Caption2.Size,
                                    Margin = new MarginPadding { Right = 5f },
                                    Animated = false,
                                    TextOverride = @"imported",
                                    TooltipOverride = @"Directly read from osu!stable",
                                    BackgroundColourOverride = Color4.ForestGreen,
                                    TextColourOverride = Color4.White,
                                },
                                updateButton = new PanelUpdateBeatmapButton
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Margin = new MarginPadding { Right = 5f, Top = -2f },
                                },
                                spreadDisplay = new SpreadDisplay
                                {
                                    Origin = Anchor.CentreLeft,
                                    Anchor = Anchor.CentreLeft,
                                    VisibleBeatmaps = { BindTarget = VisibleBeatmaps },
                                },
                            },
                        }
                    }
                }
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                chevronIcon.Colour = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : colourProvider.Background5;
                if (!useSkinnedLegacyCarousel.Value)
                {
                    titleText.Colour = Color4.White;
                    artistText.Colour = Color4.White.Opacity(0.85f);
                }
                updateClassicSelectionState();
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Expanded.BindValueChanged(_ => onExpanded(), true);
            KeyboardSelected.BindValueChanged(k => KeyboardSelected.Value = k.NewValue, true);
            selectedBeatmap.BindValueChanged(_ => updateClassicSelectionState(), true);
        }

        private void updatePreviewLayoutState()
        {
            updateExpandedBackground();
            updateClassicSelectionState();
        }

        private void onExpanded()
        {
            if (Expanded.Value)
            {
                updateExpandedBackground();

                if (useSkinnedLegacyCarousel.Value)
                {
                    chevronIcon.ResizeWidthTo(0, DURATION, Easing.OutQuint);
                    chevronIcon.FadeOut(DURATION, Easing.OutQuint);
                }
                else
                {
                    chevronIcon.ResizeWidthTo(18, DURATION * 1.5f, Easing.OutElasticQuarter);
                    chevronIcon.FadeTo(1f, DURATION, Easing.OutQuint);
                }
            }
            else
            {
                chevronBackgroundContainer.FadeOut(DURATION, Easing.OutQuint);
                chevronIcon.ResizeWidthTo(0f, DURATION, Easing.OutQuint);
                chevronIcon.FadeTo(0f, DURATION, Easing.OutQuint);
            }

            spreadDisplay.Expanded.Value = Expanded.Value;
            updateClassicSelectionState();
        }

        private void updateExpandedBackground()
        {
            chevronBackgroundContainer.FadeTo(useSkinnedLegacyCarousel.Value ? 0 : legacy_expanded_background_alpha, DURATION / 2, Easing.OutQuint);
        }

        private void updateClassicSelectionState()
        {
            bool classicSelected = useSkinnedLegacyCarousel.Value && Expanded.Value;

            BeatmapInfo? selectedInfo = beatmapCarousel?.CurrentBeatmap ?? selectedBeatmap.Value?.BeatmapInfo;
            bool selectedBelongsToSet = Item?.Model is GroupedBeatmapSet set
                                        && selectedInfo?.BeatmapSet?.ID == set.BeatmapSet.ID;

            bool showClassicDifficulty = classicSelected && selectedBelongsToSet;
            classicDifficultyFlow.Alpha = showClassicDifficulty ? 1 : 0;

            if (showClassicDifficulty && selectedInfo != null)
            {
                classicDifficultyText.Text = selectedInfo.DifficultyName;
                classicDifficultyFlow.Colour = setBackground.LegacyActiveTextColour;
            }

            modernDetailsFlow.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;
            classicInactiveBackground.Colour = new Color4(235, 73, 153, 255);
            classicInactiveBackground.Alpha = useSkinnedLegacyCarousel.Value && !Expanded.Value ? 0.72f : 0;
            classicSelectionBackground.Colour = setBackground.LegacyMenuGlowColour;
            classicSelectionBackground.FadeTo(classicSelected ? setBackground.LegacySelectedOverlayAlpha : 0, OverlayColourProvider.ThemeTransitionDuration(DURATION / 2), Easing.OutQuint);
            Color4 textColour = classicSelected ? setBackground.LegacyActiveTextColour : setBackground.LegacyInactiveTextColour;
            double duration = OverlayColourProvider.ThemeTransitionDuration(DURATION / 2);
            titleText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White, duration, Easing.OutQuint);
            artistText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White.Opacity(0.85f), duration, Easing.OutQuint);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = groupedBeatmapSet.BeatmapSet;

            // Choice of background image matches BSS implementation (always uses the lowest `beatmap_id` from the set).
            if (CarouselPreviews.Value)
                scheduledBackgroundRetrieval = Scheduler.AddDelayed(s => setBackground.Beatmap = beatmaps.GetWorkingBeatmap(s.Beatmaps.MinBy(b => b.OnlineID)), beatmapSet, 50);

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            updateButton.Beatmap = null;
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmapSet.Status;
            importedPill.Status = beatmapSet.Beatmaps.Count > 0 && StablePathManager.IsStableBeatmap(beatmapSet.Beatmaps[0].ID) ? BeatmapOnlineStatus.Ranked : BeatmapOnlineStatus.None;
            spreadDisplay.BeatmapSet.Value = beatmapSet;
            updateClassicSelectionState();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            setBackground.Beatmap = null;
            updateButton.Beatmap = null;
            updateButton.BeatmapSet = null;
            importedPill.Status = BeatmapOnlineStatus.None;
            spreadDisplay.BeatmapSet.Value = null;
        }

        protected override void Update()
        {
            base.Update();

            float previewWidth = PanelSetBackground.GetLegacyPreviewWidth(Content.DrawWidth, Content.DrawHeight, useSkinnedLegacyCarousel.Value);

            Height = Item?.DrawHeight ?? HEIGHT;

            if (useSkinnedLegacyCarousel.Value)
            {
                modernDetailsFlow.Hide();
                classicInactiveBackground.Colour = new Color4(235, 73, 153, 255);
                classicInactiveBackground.Alpha = Expanded.Value ? 0 : 0.72f;
                Color4 textColour = Expanded.Value ? setBackground.LegacyActiveTextColour : setBackground.LegacyInactiveTextColour;
                classicSelectionBackground.Colour = setBackground.LegacyMenuGlowColour;
                titleText.Colour = textColour;
                artistText.Colour = textColour;
            }

            classicSelectionBackground.Width = Content.DrawWidth <= 0 ? 1 : Math.Max(0, 1 - previewWidth / Content.DrawWidth);

            float metadataPaddingLeft = useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value
                ? Math.Max(metadata_padding_left, previewWidth + legacy_preview_metadata_gap)
                : metadata_padding_left;

            if (metadataPaddingLeft != lastMetadataPaddingLeft)
            {
                lastMetadataPaddingLeft = metadataPaddingLeft;
                metadataFlow.Padding = metadataFlow.Padding with { Left = metadataPaddingLeft };
            }

            float contentOffset = (Content.Parent?.DrawPosition.X ?? 0) + Content.DrawPosition.X;
            float chevronWidth = TopLevelContent.DrawWidth <= 0 ? 1 : Math.Min(1f, (contentOffset + previewWidth) / TopLevelContent.DrawWidth);

            if (chevronWidth != lastChevronBackgroundWidth)
            {
                lastChevronBackgroundWidth = chevronWidth;
                chevronBackgroundContainer.Width = chevronWidth;
            }
        }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                var beatmapSet = groupedBeatmapSet.BeatmapSet;
                // Check if this is a stable beatmap (not in Realm DB) — we don't allow modification
                bool isStableDirect = beatmapSet.Beatmaps.Count > 0 && StablePathManager.IsStableBeatmap(beatmapSet.Beatmaps[0].ID);

                List<MenuItem> items = new List<MenuItem>();

                if (!isStableDirect && Expanded.Value)
                {
                    if (songSelect is SoloSongSelect soloSongSelect)
                    {
                        // Assume the current set has one of its beatmaps selected since it is expanded.
                        items.Add(new OsuMenuItem(ButtonSystemStrings.Edit.ToSentence(), MenuItemType.Standard, () => soloSongSelect.Edit(soloSongSelect.Beatmap.Value.BeatmapInfo))
                        {
                            Icon = FontAwesome.Solid.PencilAlt
                        });
                        items.Add(new OsuMenuItemSpacer());
                    }
                }
                else if (!isStableDirect)
                {
                    items.Add(new OsuMenuItem(WebCommonStrings.ButtonsExpand.ToSentence(), MenuItemType.Highlighted, () => TriggerClick()));
                    items.Add(new OsuMenuItemSpacer());
                }
                else
                {
                    // Stable direct: only allow expand/collapse
                    items.Add(new OsuMenuItem(Expanded.Value
                        ? WebCommonStrings.ButtonsCollapse.ToSentence()
                        : WebCommonStrings.ButtonsExpand.ToSentence(), MenuItemType.Highlighted, () => TriggerClick()));
                    items.Add(new OsuMenuItemSpacer());
                }

                if (beatmapSet.OnlineID > 0)
                {
                    items.Add(new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmapSet(beatmapSet.OnlineID)));

                    if (beatmapSet.GetOnlineURL(api, ruleset.Value) is string url)
                        items.Add(new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard(url)));

                    items.Add(new OsuMenuItemSpacer());
                }

                var collectionItems = realm.Realm.All<BeatmapCollection>()
                                           .OrderBy(c => c.Name)
                                           .AsEnumerable()
                                           .Select(createCollectionMenuItem)
                                           .ToList();

                if (manageCollectionsDialog != null)
                    collectionItems.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, manageCollectionsDialog.Show));

                items.Add(new OsuMenuItem(CommonStrings.Collections) { Items = collectionItems });

                if (!isStableDirect)
                {
                    if (beatmapSet.Beatmaps.Any(b => b.Hidden))
                        items.Add(new OsuMenuItem(SongSelectStrings.RestoreAllHidden, MenuItemType.Standard, () => songSelect?.RestoreAllHidden(beatmapSet)));

                    items.Add(new OsuMenuItem(CommonStrings.DeleteWithConfirmation, MenuItemType.Destructive, () => songSelect?.Delete(beatmapSet)));
                }

                return items.ToArray();
            }
        }

        private MenuItem createCollectionMenuItem(BeatmapCollection collection)
        {
            var beatmapSet = groupedBeatmapSet.BeatmapSet;

            TernaryState state;

            int countExisting = beatmapSet.Beatmaps.Count(b => collection.BeatmapMD5Hashes.Contains(b.MD5Hash));

            if (countExisting == beatmapSet.Beatmaps.Count)
                state = TernaryState.True;
            else if (countExisting > 0)
                state = TernaryState.Indeterminate;
            else
                state = TernaryState.False;

            var liveCollection = collection.ToLive(realm);

            return new TernaryStateToggleMenuItem(collection.Name, MenuItemType.Standard, s =>
            {
                Task.Run(() => liveCollection.PerformWrite(c =>
                {
                    foreach (var b in beatmapSet.Beatmaps)
                    {
                        switch (s)
                        {
                            case TernaryState.True:
                                if (c.BeatmapMD5Hashes.Contains(b.MD5Hash))
                                    continue;

                                c.BeatmapMD5Hashes.Add(b.MD5Hash);
                                break;

                            case TernaryState.False:
                                c.BeatmapMD5Hashes.Remove(b.MD5Hash);
                                break;
                        }
                    }
                }));
            })
            {
                State = { Value = state }
            };
        }
    }
}
