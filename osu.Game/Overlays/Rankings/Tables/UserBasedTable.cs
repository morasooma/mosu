// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Users;
using osu.Game.Scoring;
using osu.Framework.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Rankings.Tables
{
    public abstract partial class UserBasedTable : RankingsTable<UserStatistics>
    {
        protected override bool SupportsEnhancedRows => true;

        protected UserBasedTable(int page, IReadOnlyList<UserStatistics> rankings)
            : base(page, rankings)
        {
        }

        protected virtual IEnumerable<LocalisableString> GradeColumns => new List<LocalisableString> { RankingsStrings.Statss, RankingsStrings.Stats, RankingsStrings.Stata };

        protected override Drawable CreateRowBackground(UserStatistics item)
        {
            var background = base.CreateRowBackground(item);

            // see: https://github.com/ppy/osu-web/blob/9de00a0b874c56893d98261d558d78d76259d81b/resources/views/multiplayer/rooms/_rankings_table.blade.php#L23
            if (!item.User.Active)
                background.Alpha = 0.5f;

            return background;
        }

        protected override Drawable[] CreateRowContent(int index, UserStatistics item)
        {
            var content = base.CreateRowContent(index, item);

            // see: https://github.com/ppy/osu-web/blob/9de00a0b874c56893d98261d558d78d76259d81b/resources/views/multiplayer/rooms/_rankings_table.blade.php#L23
            if (!item.User.Active)
            {
                foreach (var d in content)
                    d.Alpha = 0.5f;
            }

            return content;
        }

        protected override Drawable CreateIndexDrawable(int index, UserStatistics item)
        {
            if (item is WeeklyRankingEntry weeklyEntry)
                return new WeeklyRankIndex(weeklyEntry, TextSize);

            return base.CreateIndexDrawable(index, item);
        }

        protected override RankingsTableColumn[] CreateAdditionalHeaders() => new[]
            {
                new RankingsTableColumn(RankingsStrings.StatAccuracy, Anchor.Centre, new Dimension(GridSizeMode.AutoSize)),
                new RankingsTableColumn(RankingsStrings.StatPlayCount, Anchor.Centre, new Dimension(GridSizeMode.AutoSize)),
            }.Concat(CreateUniqueHeaders())
             .Concat(GradeColumns.Select(grade => new GradeTableColumn(grade, Anchor.Centre, new Dimension(GridSizeMode.AutoSize))))
             .ToArray();

        protected sealed override CountryCode GetCountryCode(UserStatistics item) => item.User.CountryCode;

        protected sealed override APIUser GetAvatarUser(UserStatistics item) => item.User;

        protected sealed override Drawable[] CreateFlagContent(UserStatistics item)
        {
            var username = new LinkFlowContainer(t => t.Font = OsuFont.GetFont(size: TextSize, italics: true))
            {
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                TextAnchor = Anchor.CentreLeft
            };
            username.AddUserLink(item.User);
            return [new UpdateableTeamFlag(item.User.Team) { Size = new Vector2(40, 20) }, username];
        }

        protected sealed override Drawable[] CreateAdditionalContent(UserStatistics item) => new[]
        {
            new ColouredRowText(TextSize) { Text = item.DisplayAccuracy, },
            new ColouredRowText(TextSize) { Text = item.PlayCount.ToLocalisableString(@"N0") },
        }.Concat(CreateUniqueContent(item)).Concat(new[]
        {
            new ColouredRowText(TextSize) { Text = (item.GradesCount[ScoreRank.XH] + item.GradesCount[ScoreRank.X]).ToLocalisableString(@"N0"), },
            new ColouredRowText(TextSize) { Text = (item.GradesCount[ScoreRank.SH] + item.GradesCount[ScoreRank.S]).ToLocalisableString(@"N0"), },
            new ColouredRowText(TextSize) { Text = item.GradesCount[ScoreRank.A].ToLocalisableString(@"N0"), }
        }).ToArray();

        protected abstract RankingsTableColumn[] CreateUniqueHeaders();

        protected abstract Drawable[] CreateUniqueContent(UserStatistics item);

        private partial class WeeklyRankIndex : FillFlowContainer
        {
            private readonly WeeklyRankingEntry entry;
            private readonly float textSize;

            public WeeklyRankIndex(WeeklyRankingEntry entry, float textSize)
            {
                this.entry = entry;
                this.textSize = textSize;

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(1, 0);
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                var (icon, colour, changeText) = getChangeDisplay(colours);

                AddRange(new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Icon = icon,
                        Size = new Vector2(8),
                        Colour = colour,
                    },
                    new OsuSpriteText
                    {
                        Text = changeText,
                        Font = OsuFont.GetFont(size: 9, weight: FontWeight.SemiBold),
                        Colour = colour,
                    },
                    new RowText(textSize)
                    {
                        Text = entry.GlobalRank?.ToLocalisableString(@"N0") ?? default,
                        Font = OsuFont.GetFont(size: textSize, weight: FontWeight.SemiBold),
                        Margin = new MarginPadding { Left = 2, Bottom = 3 },
                    }
                });
            }

            private (IconUsage icon, Colour4 colour, string changeText) getChangeDisplay(OsuColour colours)
            {
                int delta = entry.RankChange?.Delta ?? 0;

                return entry.RankChange?.Status switch
                {
                    WeeklyRankChangeStatus.Up => (FontAwesome.Solid.ArrowUp, colours.Lime1, delta.ToString()),
                    WeeklyRankChangeStatus.Down => (FontAwesome.Solid.ArrowDown, colours.Red1, System.Math.Abs(delta).ToString()),
                    WeeklyRankChangeStatus.Same => (FontAwesome.Solid.Minus, colours.Gray4, string.Empty),
                    WeeklyRankChangeStatus.New => (FontAwesome.Solid.Asterisk, colours.Gray4, string.Empty),
                    _ => (FontAwesome.Solid.Question, colours.Gray4, string.Empty),
                };
            }
        }

        private class GradeTableColumn : RankingsTableColumn
        {
            public GradeTableColumn(LocalisableString? header = null, Anchor anchor = Anchor.TopLeft, Dimension dimension = null, bool highlighted = false)
                : base(header, anchor, dimension, highlighted)
            {
            }

            public override HeaderText CreateHeaderText() => new GradeHeaderText(Header, Highlighted);
        }

        private partial class GradeHeaderText : HeaderText
        {
            public GradeHeaderText(LocalisableString text, bool isHighlighted)
                : base(text, isHighlighted)
            {
                Margin = new MarginPadding
                {
                    // Grade columns have extra horizontal padding for readibility
                    Horizontal = 20,
                    Vertical = 5
                };
            }
        }
    }
}
