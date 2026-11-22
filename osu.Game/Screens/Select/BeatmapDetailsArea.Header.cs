// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDetailsArea
    {
        public partial class Header : CompositeDrawable
        {
            private WedgeSelector<Selection> tabControl = null!;
            private GridContainer leaderboardControls = null!;

            private ShearedDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
            private ShearedDropdown<LeaderboardSortMode> sortDropdown = null!;
            private ShearedToggleButton selectedModsToggle = null!;
            private ILocalisedBindableString sortLabel = null!;
            private ILocalisedBindableString modsLabel = null!;

            public IBindable<Selection> Type => tabControl.Current;

            public IBindable<BeatmapLeaderboardScope> Scope => scopeDropdown.Current;

            private readonly Bindable<BeatmapDetailTab> configDetailTab = new Bindable<BeatmapDetailTab>();

            public IBindable<LeaderboardSortMode> Sorting => sortDropdown.Current;

            private readonly Bindable<LeaderboardSortMode> configLeaderboardSortMode = new Bindable<LeaderboardSortMode>();

            public IBindable<bool> FilterBySelectedMods => selectedModsToggle.Active;

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, LocalisationManager localisation)
            {
                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 5f },
                        Children = new Drawable[]
                        {
                            tabControl = new WedgeSelector<Selection>(20f)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 22,
                                Margin = new MarginPadding { Top = 2f },
                                IsSwitchable = true,
                            },
                            leaderboardControls = new GridContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Padding = new MarginPadding { Left = 205 },
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.Absolute, 5),
                                    new Dimension(),
                                    new Dimension(GridSizeMode.Absolute, 5),
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        scopeDropdown = new ScopeDropdown
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Current = { Value = BeatmapLeaderboardScope.Global },
                                        },
                                        new Container(),
                                        sortDropdown = new ShearedDropdown<LeaderboardSortMode>(BeatmapLeaderboardWedgeStrings.Sort)
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Items = Enum.GetValues<LeaderboardSortMode>(),
                                        },
                                        new Container(),
                                        selectedModsToggle = new ShearedToggleButton
                                        {
                                            AutoSizeAxes = Axes.X,
                                            Text = UserInterfaceStrings.SelectedMods,
                                            Height = 30f,
                                        },
                                    },
                                },
                            },
                        },
                    },
                };

                sortLabel = localisation.GetLocalisedBindableString(BeatmapLeaderboardWedgeStrings.Sort);
                sortLabel.BindValueChanged(v => sortDropdown.LeftSideLabel = v.NewValue == "Сортировка" ? "Сорт." : v.NewValue, true);

                modsLabel = localisation.GetLocalisedBindableString(UserInterfaceStrings.SelectedMods);
                modsLabel.BindValueChanged(v => selectedModsToggle.Text = v.NewValue == "Выбранные моды" ? global::osu.Game.Localisation.ClientTranslationStrings.J5149 : v.NewValue, true);
                selectedModsToggle.TooltipText = UserInterfaceStrings.SelectedMods;

                config.BindWith(OsuSetting.BeatmapDetailTab, configDetailTab);
                config.BindWith(OsuSetting.BeatmapLeaderboardSortMode, configLeaderboardSortMode);
                config.BindWith(OsuSetting.BeatmapDetailModsFilter, selectedModsToggle.Active);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                scopeDropdown.Current.Value = tryMapDetailTabToLeaderboardScope(configDetailTab.Value) ?? scopeDropdown.Current.Value;
                scopeDropdown.Current.BindValueChanged(_ => updateConfigDetailTab());

                tabControl.Current.Value = configDetailTab.Value == BeatmapDetailTab.Details ? Selection.Details : Selection.Ranking;
                tabControl.Current.BindValueChanged(v =>
                {
                    leaderboardControls.FadeTo(v.NewValue == Selection.Ranking ? 1 : 0, 300, Easing.OutQuint);
                    updateConfigDetailTab();
                }, true);

                sortDropdown.Current.BindTo(configLeaderboardSortMode);
            }

            #region Reading / writing state from / to configuration

            private void updateConfigDetailTab()
            {
                switch (tabControl.Current.Value)
                {
                    case Selection.Details:
                        configDetailTab.Value = BeatmapDetailTab.Details;
                        return;

                    case Selection.Ranking:
                        configDetailTab.Value = mapLeaderboardScopeToDetailTab(scopeDropdown.Current.Value);
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(tabControl.Current.Value), tabControl.Current.Value, null);
                }
            }

            private static BeatmapLeaderboardScope? tryMapDetailTabToLeaderboardScope(BeatmapDetailTab tab)
            {
                switch (tab)
                {
                    case BeatmapDetailTab.Local:
                        return BeatmapLeaderboardScope.Local;

                    case BeatmapDetailTab.Country:
                        return BeatmapLeaderboardScope.Country;

                    case BeatmapDetailTab.Global:
                        return BeatmapLeaderboardScope.Global;

                    case BeatmapDetailTab.Friends:
                        return BeatmapLeaderboardScope.Friend;

                    case BeatmapDetailTab.Team:
                        return BeatmapLeaderboardScope.Team;

                    default:
                        return null;
                }
            }

            private static BeatmapDetailTab mapLeaderboardScopeToDetailTab(BeatmapLeaderboardScope scope)
            {
                switch (scope)
                {
                    case BeatmapLeaderboardScope.Local:
                        return BeatmapDetailTab.Local;

                    case BeatmapLeaderboardScope.Country:
                        return BeatmapDetailTab.Country;

                    case BeatmapLeaderboardScope.Global:
                        return BeatmapDetailTab.Global;

                    case BeatmapLeaderboardScope.Friend:
                        return BeatmapDetailTab.Friends;

                    case BeatmapLeaderboardScope.Team:
                        return BeatmapDetailTab.Team;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
                }
            }

            #endregion

            public enum Selection
            {
                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Details))]
                Details,

                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Ranking))]
                Ranking,
            }

            private partial class ScopeDropdown : ShearedDropdown<BeatmapLeaderboardScope>
            {
                public ScopeDropdown()
                    : base(BeatmapLeaderboardWedgeStrings.Scope)
                {
                    Items = Enum.GetValues<BeatmapLeaderboardScope>();
                }

                protected override LocalisableString GenerateItemText(BeatmapLeaderboardScope item) => item.GetLocalisableDescription();
            }
        }
    }
}
