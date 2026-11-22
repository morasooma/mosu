// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Users
{
    /// <summary>
    /// User card that shows user's global and country ranks in the bottom.
    /// Meant to be used in the toolbar login overlay.
    /// </summary>
    public partial class UserRankPanel : UserPanel
    {
        private const int padding = 10;
        private const int main_content_height = 80;

        private GlobalRankDisplay globalRankDisplay = null!;
        private ProfileValueDisplay countryRankDisplay = null!;
        private LoadingLayer loadingLayer = null!;
        private Container mainContentContainer = null!;
        private IBindable<float>? mainContentRadius;

        public UserRankPanel(APIUser user)
            : base(user)
        {
            AutoSizeAxes = Axes.Y;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<RulesetInfo> ruleset, IBindable<IReadOnlyList<Mod>> selectedMods)
        {
            this.ruleset.BindTo(ruleset);
            this.selectedMods.BindTo(selectedMods);

            BorderColour = ColourProvider?.Light1 ?? Colours.GreyVioletLighter;
            mainContentRadius = ColourProvider?.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Card, 10f)
                ?? OverlayColourProvider.GetGeometryRadius(osu.Game.Overlays.CustomTheme.GeometryRole.Card, 10f);
            mainContentRadius.BindValueChanged(r =>
            {
                if (mainContentContainer != null)
                    mainContentContainer.CornerRadius = r.NewValue;
            }, true);
        }

        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        // Keep owned copies: disposing a panel must not unbind the game's shared selection.
        private readonly IBindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();
        private readonly IBindable<IReadOnlyList<Mod>> selectedMods = new Bindable<IReadOnlyList<Mod>>();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;

            ruleset.BindValueChanged(_ => selectionChanged(), true);
            selectedMods.BindValueChanged(_ => selectionChanged(), true);
        }

        private void onStatisticsUpdated(UserStatisticsUpdate update)
        {
            updateDisplay();
        }

        private void selectionChanged()
        {
            updateDisplay();
            Scheduler.AddOnce(refetchStatistics);
        }

        private void refetchStatistics()
        {
            if (api.IsLoggedIn)
                statisticsProvider?.RefetchStatistics(getCurrentRuleset());
        }

        private RulesetInfo getCurrentRuleset()
        {
            var currentRuleset = ruleset.Value;

            if (MosuServerEnvironment.SupportsSpecialRulesets && selectedMods?.Value != null)
            {
                bool isRelax = false;
                foreach (var m in selectedMods.Value)
                {
                    if (m is osu.Game.Rulesets.Mods.ModRelax)
                    {
                        isRelax = true;
                        break;
                    }
                }

                if (isRelax)
                {
                    if (currentRuleset.ShortName == RulesetInfo.OSU_MODE_SHORTNAME)
                        currentRuleset = currentRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);
                    else if (!MosuServerEnvironment.OnlyOsuRelax)
                    {
                        if (currentRuleset.ShortName == RulesetInfo.TAIKO_MODE_SHORTNAME)
                            currentRuleset = currentRuleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID);
                        else if (currentRuleset.ShortName == RulesetInfo.CATCH_MODE_SHORTNAME)
                            currentRuleset = currentRuleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID);
                    }
                }
            }

            return currentRuleset;
        }

        private void updateDisplay()
        {
            var statistics = statisticsProvider?.GetStatisticsFor(getCurrentRuleset());

            loadingLayer.State.Value = statistics == null ? Visibility.Visible : Visibility.Hidden;

            // TODO: implement highest rank tooltip
            // `RankHighest` resides in `APIUser`, but `api.LocalUser` doesn't update
            // maybe move to `UserStatistics` in api, so `UserStatisticsWatcher` can update the value
            globalRankDisplay.UserStatistics.Value = statistics;

            countryRankDisplay.Content.Text = statistics?.CountryRank?.ToLocalisableString("\\##,##0") ?? "-";
        }

        protected override Drawable CreateLayout()
        {
            FillFlowContainer details;

            var layout = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    mainContentContainer = new Container
                    {
                        Name = "Main content",
                        RelativeSizeAxes = Axes.X,
                        Height = main_content_height,
                        CornerRadius = mainContentRadius?.Value ?? 10,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new UserCoverBackground
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                User = User,
                                Alpha = 0.3f
                            },
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding(padding),
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension()
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        CreateAvatar().With(avatar =>
                                        {
                                            avatar.Size = new Vector2(60);
                                            avatar.Masking = true;
                                            avatar.CornerRadius = 6;
                                        }),
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding { Left = padding },
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension()
                                            },
                                            RowDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.AutoSize),
                                                new Dimension()
                                            },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    details = new FillFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(6),
                                                        Children = new[]
                                                        {
                                                            CreateFlag(),
                                                            CreateTeamLogo(),
                                                            // supporter icon is being added later
                                                        }
                                                    }
                                                },
                                                new Drawable[]
                                                {
                                                    CreateUsername().With(username =>
                                                    {
                                                        username.Anchor = Anchor.CentreLeft;
                                                        username.Origin = Anchor.CentreLeft;
                                                    })
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new GridContainer
                    {
                        Name = "Bottom content",
                        Margin = new MarginPadding { Top = main_content_height },
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(padding),
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension()
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                globalRankDisplay = new GlobalRankDisplay(),
                                countryRankDisplay = new ProfileValueDisplay(true)
                                {
                                    Title = UsersStrings.ShowRankCountrySimple,
                                }
                            }
                        }
                    },
                    loadingLayer = new LoadingLayer(true),
                }
            };

            if (User.IsSupporter)
            {
                details.Add(new SupporterIcon
                {
                    Height = 26,
                    SupportLevel = User.SupportLevel
                });
            }

            return layout;
        }

        protected override bool OnHover(HoverEvent e)
        {
            BorderThickness = 2;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            BorderThickness = 0;
            base.OnHoverLost(e);
        }

        protected override Drawable? CreateBackground() => null;

        protected override void Dispose(bool isDisposing)
        {
            if (statisticsProvider.IsNotNull())
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;

            base.Dispose(isDisposing);
        }
    }
}
