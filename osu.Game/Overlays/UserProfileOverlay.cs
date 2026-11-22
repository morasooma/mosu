// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Rulesets;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class UserProfileOverlay : FullscreenOverlay<ProfileHeader>
    {
        protected override Container<Drawable> Content => onlineViewContainer;

        private readonly OnlineViewContainer onlineViewContainer;
        private readonly LoadingLayer loadingLayer;

        private ProfileSection? lastSection;
        private ProfileSection[]? sections;
        private GetUserRequest? userReq;
        private CancellationTokenSource? sectionLoadCancellation;
        private bool headerLoading;
        private bool headerReady;
        private ProfileSectionsContainer? sectionsContainer;
        private ProfileSectionTabControl? tabs;
        private Box? headerBackground;
        private IBindable<Colour4> themeColour = null!;

        private IUser? user;
        private IRulesetInfo? ruleset;
        private string? variant;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> gameRuleset { get; set; } = null!;

        [Resolved]
        private IBindable<System.Collections.Generic.IReadOnlyList<osu.Game.Rulesets.Mods.Mod>> selectedMods { get; set; } = null!;

        public UserProfileOverlay()
            : base(OverlayColourScheme.Pink)
        {
            ColourProvider.ExemptAccentFromCustomTheme = true;

            base.Content.Add(new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    onlineViewContainer = new OnlineViewContainer($"Sign in to view the {Header.Title.Title}")
                    {
                        RelativeSizeAxes = Axes.Both
                    },
                    loadingLayer = new LoadingLayer(true)
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            apiState.BindTo(API.State);
            apiState.BindValueChanged(state => Schedule(() =>
            {
                if (state.NewValue == APIState.Online && user != null)
                    Scheduler.AddOnce(fetchAndSetContent);
            }));

            themeColour = ColourProvider.GetColourBindable(OverlayColour.Background5);
            themeColour.BindValueChanged(_ =>
            {
                if (headerBackground != null)
                    headerBackground.Colour = ColourProvider.Background5;
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;
        }

        private void onStatisticsUpdated(UserStatisticsUpdate update)
        {
            if (State.Value == Visibility.Visible && user?.OnlineID == API.LocalUser.Value.Id && ruleset?.Equals(update.Ruleset) == true)
                Scheduler.AddOnce(fetchAndSetContent);
        }

        protected override ProfileHeader CreateHeader() => new ProfileHeader();

        protected override Color4 BackgroundColour => ColourProvider.Background5;

        public void ShowUser(IUser userToShow, IRulesetInfo? userRuleset = null, string? userVariant = null)
        {
            if (userToShow.OnlineID == APIUser.SYSTEM_USER_ID)
                return;

            user = userToShow;
            ruleset = userRuleset ?? getCurrentGameRuleset();
            variant = userVariant;

            Show();
            Scheduler.AddOnce(fetchAndSetContent);
        }

        /// <summary>
        /// Returns the ruleset currently selected in the game, converting to a special ruleset
        /// (e.g. osu!relax) when the relax mod is active on the Mosu server.
        /// </summary>
        private IRulesetInfo? getCurrentGameRuleset()
        {
            var currentRuleset = gameRuleset.Value;

            if (Online.MosuServerEnvironment.SupportsSpecialRulesets && selectedMods?.Value != null)
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
                        return currentRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);
                    else if (!Online.MosuServerEnvironment.OnlyOsuRelax)
                    {
                        if (currentRuleset.ShortName == RulesetInfo.TAIKO_MODE_SHORTNAME)
                            return currentRuleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID);
                        else if (currentRuleset.ShortName == RulesetInfo.CATCH_MODE_SHORTNAME)
                            return currentRuleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID);
                    }
                }
            }

            return currentRuleset;
        }

        private void fetchAndSetContent()
        {
            using var operation = osu.Framework.Statistics.StutterTraceEventSource.Log.Measure("Profile.FetchAndSetContent");
            Debug.Assert(user != null);

            bool sameUser = user.OnlineID == Header.User.Value?.User.Id;
            // Reopening the same profile must refresh its statistics as well.
            // Header/content reuse must not suppress the API request.

            // The header is constructed with the overlay, but was first loaded
            // synchronously when recreateBaseContent attached it on Update. Keep
            // it unattached until dependency loading finishes in the loader.
            if (!Header.IsLoaded && !headerReady)
            {
                if (!headerLoading)
                {
                    headerLoading = true;
                    loadingLayer.Show();
                    LoadComponentAsync(Header, _ =>
                    {
                        headerLoading = false;
                        headerReady = true;
                        Scheduler.AddOnce(fetchAndSetContent);
                    });
                }
                return;
            }

            userReq?.Cancel();
            sectionLoadCancellation?.Cancel();
            lastSection = null;

            // The API's ProfileOrder decides which sections are actually displayed.
            // Do not construct every section (and its subtrees) before that response.
            sections = Array.Empty<ProfileSection>();

            if (!sameUser && changeOverlayColours(OverlayColourScheme.Pink.GetHue()))
            {
                // A palette change creates a fresh header; pass it through the
                // same background-load gate rather than attach it synchronously.
                Scheduler.AddOnce(fetchAndSetContent);
                return;
            }

            // Detach only a header that will actually be reused. A replaced
            // palette header must stay owned by the old content for disposal.
            if (ReferenceEquals(sectionsContainer?.ExpandableHeader, Header) && sectionsContainer != null)
                sectionsContainer.ExpandableHeader = null;
            recreateBaseContent();

            if (API.State.Value != APIState.Offline)
            {
                var requestedRuleset = ruleset;
                var requestedVariant = variant;
                userReq = user.OnlineID > 1
                    ? new GetUserRequest(user.OnlineID, requestedRuleset, requestedVariant)
                    : new GetUserRequest(user.Username, requestedRuleset, requestedVariant);
                userReq.Success += u => userLoadComplete(u, requestedRuleset, requestedVariant);

                API.Queue(userReq);
                loadingLayer.Show();
            }
        }

        private void userLoadComplete(APIUser loadedUser, IRulesetInfo? userRuleset, string? userVariant)
        {
            using var operation = osu.Framework.Statistics.StutterTraceEventSource.Log.Measure("Profile.UserLoadComplete");
            Debug.Assert(sections != null && sectionsContainer != null && tabs != null);

            // reuse header and content if same colour scheme, otherwise recreate both.
            int profileHue = loadedUser.ProfileHue ?? OverlayColourScheme.Pink.GetHue();

            if (changeOverlayColours(profileHue))
            {
                var request = userReq;
                var header = Header;
                LoadComponentAsync(header, _ =>
                {
                    // A new selection may have superseded this response while
                    // its replacement header was loading.
                    if (!ReferenceEquals(request, userReq) || !ReferenceEquals(header, Header))
                        return;
                    headerReady = true;
                    recreateBaseContent();
                    userLoadComplete(loadedUser, userRuleset, userVariant);
                });
                return;
            }

            RulesetInfo? actualRuleset = rulesets.GetRuleset(userRuleset?.ShortName ?? loadedUser.PlayMode);

            switch (actualRuleset)
            {
                case null when userRuleset != null && userRuleset.IsSpecialRuleset():
                    RulesetInfo normalRuleset = (userRuleset as RulesetInfo)?.CreateNormalRuleset()
                                               ?? rulesets.GetRuleset(userRuleset.ShortName).AsNonNull().CreateNormalRuleset();
                    actualRuleset = rulesets.GetRuleset(normalRuleset.ShortName).AsNonNull().CreateSpecialRuleset(userRuleset.ShortName, userRuleset.OnlineID);
                    break;

                case null:
                    actualRuleset = rulesets.GetRuleset(loadedUser.PlayMode).AsNonNull();
                    break;
            }

            var userProfile = new UserProfileData(loadedUser, actualRuleset, userVariant);
            using (osu.Framework.Statistics.StutterTraceEventSource.Log.Measure("Profile.ApplyHeaderUser"))
                Header.User.Value = userProfile;

            bool showSections = user?.IsBot != true;
            var target = sectionsContainer;
            var targetTabs = tabs;
            sectionLoadCancellation?.Cancel();
            var cancellation = sectionLoadCancellation = new CancellationTokenSource();
            LoadComponentAsync(new SectionBatch(loadedUser, userProfile, Online.MosuServerEnvironment.IsThirdPartyServer, showSections), batch =>
            {
                sections = batch.ToArray();
                batch.Clear(false);
                foreach (var section in sections)
                {
                    target.Add(section);
                    targetTabs.AddItem(section);
                }
                batch.Dispose();
                loadingLayer.Hide();
            }, cancellation.Token);
        }

        private partial class SectionBatch : Container<ProfileSection>
        {
            private readonly APIUser loadedUser;
            private readonly UserProfileData profile;
            private readonly bool thirdParty;
            private readonly bool showSections;

            public SectionBatch(APIUser loadedUser, UserProfileData profile, bool thirdParty, bool showSections)
            {
                this.loadedUser = loadedUser;
                this.profile = profile;
                this.thirdParty = thirdParty;
                this.showSections = showSections;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (!showSections)
                    return;

                Children = CreateSections(loadedUser, thirdParty);
                foreach (var section in this)
                    section.User.Value = profile;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;

            userReq?.Cancel();
            sectionLoadCancellation?.Cancel();
            base.Dispose(isDisposing);
        }

        internal static ProfileSection[] CreateSections(APIUser loadedUser, bool isThirdPartyServer)
        {
            if (loadedUser.IsBot || loadedUser.ProfileOrder == null)
                return Array.Empty<ProfileSection>();

            var result = new List<ProfileSection>();
            foreach (string id in loadedUser.ProfileOrder.Distinct())
            {
                ProfileSection? section = id switch
                {
                    "recent_activity" => new RecentSection(),
                    "top_ranks" => new RanksSection(),
                    "medals" when !isThirdPartyServer => new MedalsSection(),
                    "historical" => new HistoricalSection(isThirdPartyServer && !loadedUser.ProfileOrder.Contains("replay_views")),
                    "replay_views" when isThirdPartyServer => new ReplayViewsSection(),
                    "beatmaps" => new BeatmapsSection(),
                    "kudosu" => new KudosuSection(),
                    _ => null
                };
                if (section != null)
                    result.Add(section);
            }
            return result.ToArray();
        }

        private void recreateBaseContent()
        {
            Child = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = sectionsContainer = new ProfileSectionsContainer
                {
                    ExpandableHeader = Header,
                    FixedHeader = tabs = new ProfileSectionTabControl
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    HeaderBackground = headerBackground = new Box
                    {
                        // this is only visible as the ProfileTabControl background
                        Colour = ColourProvider.Background5,
                        RelativeSizeAxes = Axes.Both
                    },
                }
            };

            sectionsContainer.SelectedSection.ValueChanged += section =>
            {
                if (lastSection != section.NewValue)
                {
                    lastSection = section.NewValue;
                    tabs.Current.Value = lastSection!;
                }
            };

            tabs.Current.ValueChanged += section =>
            {
                if (lastSection == null)
                {
                    lastSection = sectionsContainer.Children.FirstOrDefault();
                    if (lastSection != null)
                        tabs.Current.Value = lastSection;
                    return;
                }

                if (lastSection != section.NewValue)
                {
                    lastSection = section.NewValue;
                    sectionsContainer.ScrollTo(lastSection);
                }
            };
        }

        private bool changeOverlayColours(int hue)
        {
            if (hue == ColourProvider.Hue)
                return false;

            ColourProvider.ChangeColourScheme(hue);

            RecreateHeader();
            headerReady = false;
            UpdateColours();
            return true;
        }

        private partial class ProfileSectionTabControl : OsuTabControl<ProfileSection>
        {
            public ProfileSectionTabControl()
            {
                Height = 40;
                Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING };
                TabContainer.Spacing = new Vector2(20);
            }

            protected override TabItem<ProfileSection> CreateTabItem(ProfileSection value) => new ProfileSectionTabItem(value);

            protected override bool OnClick(ClickEvent e) => true;

            protected override bool OnHover(HoverEvent e) => true;

            private partial class ProfileSectionTabItem : TabItem<ProfileSection>
            {
                private OsuSpriteText text = null!;

                [Resolved]
                private OverlayColourProvider colourProvider { get; set; } = null!;

                public ProfileSectionTabItem(ProfileSection value)
                    : base(value)
                {
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AutoSizeAxes = Axes.Both;
                    Anchor = Anchor.CentreLeft;
                    Origin = Anchor.CentreLeft;

                    InternalChild = text = new OsuSpriteText
                    {
                        Text = Value.Title
                    };

                    updateState();
                }

                protected override void OnActivated() => updateState();

                protected override void OnDeactivated() => updateState();

                protected override bool OnHover(HoverEvent e)
                {
                    updateState();
                    return true;
                }

                protected override void OnHoverLost(HoverLostEvent e) => updateState();

                private void updateState()
                {
                    text.Font = OsuFont.Default.With(size: 14, weight: Active.Value ? FontWeight.SemiBold : FontWeight.Regular);

                    Colour4 textColour;

                    if (IsHovered)
                        textColour = colourProvider.Light1;
                    else
                        textColour = Active.Value ? colourProvider.Content1 : colourProvider.Light2;

                    text.FadeColour(textColour, 300, Easing.OutQuint);
                }
            }
        }

        private partial class ProfileSectionsContainer : SectionsContainer<ProfileSection>
        {
            private OverlayScrollContainer scroll = null!;

            public ProfileSectionsContainer()
            {
                RelativeSizeAxes = Axes.Both;
            }

            protected override UserTrackingScrollContainer CreateScrollContainer() => scroll = new OverlayScrollContainer();

            // Reverse child ID is required so expanding beatmap panels can appear above sections below them.
            // This can also be done by setting Depth when adding new sections above if using ReverseChildID turns out to have any issues.
            protected override FlowContainer<ProfileSection> CreateScrollContentContainer() => new ReverseChildIDFillFlowContainer<ProfileSection>
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding { Horizontal = 10 },
                Margin = new MarginPadding { Bottom = 10 },
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // Ensure the scroll-to-top button is displayed above the fixed header.
                AddInternal(scroll.Button.CreateProxy());
            }
        }
    }
}
