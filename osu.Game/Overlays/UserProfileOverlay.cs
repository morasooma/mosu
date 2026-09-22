// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        [Resolved]
        private IBindable<RulesetInfo> gameRuleset { get; set; } = null!;

        [Resolved]
        private IBindable<System.Collections.Generic.IReadOnlyList<osu.Game.Rulesets.Mods.Mod>> selectedMods { get; set; } = null!;

        public UserProfileOverlay()
            : base(OverlayColourScheme.Pink)
        {
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
            Debug.Assert(user != null);

            bool sameUser = user.OnlineID == Header.User.Value?.User.Id;
            // Keep Morasooma's existing profile reuse behaviour intact. On third-party
            // servers always refetch when a profile is opened: their user/statistics
            // caches are commonly refreshed after score submission, and reusing the
            // already rendered profile otherwise leaves the previous values on screen.
            if (ShouldReuseDisplayedProfile(Online.MosuServerEnvironment.IsThirdPartyServer,
                    sameUser,
                    ruleset?.MatchesOnlineID(Header.User.Value?.Ruleset) == true,
                    string.Equals(variant, Header.User.Value?.Variant, StringComparison.OrdinalIgnoreCase)))
                return;

            sectionsContainer?.ExpandableHeader = null;

            userReq?.Cancel();
            lastSection = null;

            var sectionList = new List<ProfileSection>
            {
                //new AboutSection(),
                new RecentSection(),
                new RanksSection(),
            };

            if (!Online.MosuServerEnvironment.IsThirdPartyServer)
                sectionList.Add(new MedalsSection());

            sectionList.Add(new HistoricalSection());
            sectionList.Add(new BeatmapsSection());
            sectionList.Add(new KudosuSection());

            sections = !user.IsBot
                ? sectionList.ToArray()
                : Array.Empty<ProfileSection>();

            if (!sameUser)
                changeOverlayColours(OverlayColourScheme.Pink.GetHue());

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

        internal static bool ShouldReuseDisplayedProfile(bool isThirdPartyServer, bool sameUser, bool sameRuleset, bool sameVariant)
            => !isThirdPartyServer && sameUser && sameRuleset && sameVariant;

        private void userLoadComplete(APIUser loadedUser, IRulesetInfo? userRuleset, string? userVariant)
        {
            Debug.Assert(sections != null && sectionsContainer != null && tabs != null);

            // reuse header and content if same colour scheme, otherwise recreate both.
            int profileHue = loadedUser.ProfileHue ?? OverlayColourScheme.Pink.GetHue();

            if (changeOverlayColours(profileHue))
                recreateBaseContent();

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
            Header.User.Value = userProfile;

            if (loadedUser.ProfileOrder != null)
            {
                foreach (string id in loadedUser.ProfileOrder)
                {
                    var sec = sections.FirstOrDefault(s => s.Identifier == id);

                    if (sec != null)
                    {
                        sec.User.Value = userProfile;

                        sectionsContainer.Add(sec);
                        tabs.AddItem(sec);
                    }
                }
            }

            loadingLayer.Hide();
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
