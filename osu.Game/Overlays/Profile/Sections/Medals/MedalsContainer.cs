// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Sections.Medals
{
    public partial class MedalsContainer : ProfileSubsection
    {
        private static readonly string[] grouping_order =
        {
            "Skill & Dedication",
            "Mod Introduction",
            "Hush-Hush",
        };

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private APIRequest<List<APIMedal>>? retrievalRequest;
        private CancellationTokenSource? loadCancellation;

        private FillFlowContainer contentFlow = null!;
        private OverlayColourProvider colourProvider = null!;
        private IBindable<Colour4> themeColour = null!;
        private readonly List<OsuSpriteText> groupHeaders = new List<OsuSpriteText>();

        public MedalsContainer(Bindable<UserProfileData?> user)
            : base(user, counterVisibilityState: CounterVisibilityState.AlwaysVisible)
        {
        }

        protected override Drawable CreateContent() => contentFlow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 12),
        };

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(colour =>
            {
                foreach (var header in groupHeaders)
                    header.Colour = colour.NewValue;
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(onUserChanged, true);
        }

        private void onUserChanged(ValueChangedEvent<UserProfileData?> e)
        {
            retrievalRequest?.Cancel();
            loadCancellation?.Cancel();
            contentFlow.Clear();
            groupHeaders.Clear();

            if (e.NewValue?.User != null)
                fetchMedals(e.NewValue);
        }

        private void fetchMedals(UserProfileData profile)
        {
            loadCancellation = new CancellationTokenSource();
            long userId = profile.User.Id;

            SetCount(profile.User.Achievements?.Length ?? 0);

            retrievalRequest = new GetUserMedalsRequest(userId);
            retrievalRequest.Success += medals => Schedule(() =>
            {
                if (userId != api.LocalUser.Value.Id)
                    medals = medals.Where(m => m.Achieved).ToList();

                string? modeFilter = getMedalModeFilter(profile.Ruleset);

                medals = medals
                    .Where(m => string.IsNullOrEmpty(m.Mode) || m.Mode == modeFilter)
                    .OrderBy(m => grouping_order.Contains(m.Grouping) ? Array.IndexOf(grouping_order, m.Grouping) : int.MaxValue)
                    .ThenBy(m => m.Grouping, StringComparer.Ordinal)
                    .ThenBy(m => m.Ordering)
                    .ThenBy(m => m.ID)
                    .ToList();

                foreach (var group in medals.GroupBy(m => m.Grouping).OrderBy(g => grouping_order.Contains(g.Key) ? Array.IndexOf(grouping_order, g.Key) : int.MaxValue))
                {
                    var section = createGroupSection(group.Key, group.ToList());
                    contentFlow.Add(section);
                }
            });

            api.Queue(retrievalRequest);
        }

        private static string? getMedalModeFilter(RulesetInfo ruleset) => ruleset.ShortName switch
        {
            RulesetInfo.OSU_RELAX_MODE_SHORTNAME => "osu",
            RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME => "taiko",
            RulesetInfo.CATCH_RELAX_MODE_SHORTNAME => "fruits",
            _ => ruleset.ShortName,
        };

        private Container createGroupSection(string grouping, List<APIMedal> medals)
        {
            var section = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            var header = new OsuSpriteText
            {
                Text = grouping,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                Margin = new MarginPadding { Bottom = 6 },
                Colour = colourProvider.Content1,
            };
            groupHeaders.Add(header);

            var rowsFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            section.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    header,
                    rowsFlow,
                },
            });

            foreach (var orderingGroup in medals.GroupBy(m => m.Ordering).OrderBy(g => g.Key))
            {
                var rowMedals = orderingGroup.OrderBy(m => m.ID).ToList();
                if (rowMedals.Count == 0)
                    continue;

                var row = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(8),
                };

                rowsFlow.Add(row);

                foreach (var medal in rowMedals)
                {
                    var drawable = new DrawableProfileMedal(medal);
                    LoadComponent(drawable);
                    row.Add(drawable);
                }
            }

            return section;
        }

        protected override void Dispose(bool isDisposing)
        {
            retrievalRequest?.Cancel();
            loadCancellation?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
