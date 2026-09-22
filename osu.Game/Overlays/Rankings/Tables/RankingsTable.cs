// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osu.Framework.Localisation;
using osu.Framework.Bindables;
using osuTK.Graphics;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Rankings.Tables
{
    public abstract partial class RankingsTable<TModel> : TableContainer
    {
        private const float compact_text_size = 12;
        private const float enhanced_text_size = 13;
        private const float compact_row_height = 32;
        private const float enhanced_row_height = 40;
        private const float row_spacing = 3;
        private const int items_per_page = 50;

        private readonly int page;
        private readonly IReadOnlyList<TModel> rankings;
        private FillFlowContainer backgroundFlow = null!;
        private IBindable<bool> enhancedRankingRows;

        protected float TextSize => EnhancedRows ? enhanced_text_size : compact_text_size;

        protected bool EnhancedRows => SupportsEnhancedRows && enhancedRankingRows?.Value == true;

        protected virtual bool SupportsEnhancedRows => false;

        protected RankingsTable(int page, IReadOnlyList<TModel> rankings)
        {
            this.page = page;
            this.rankings = rankings;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            AddInternal(backgroundFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Depth = 1f,
            });

            if (SupportsEnhancedRows)
            {
                enhancedRankingRows = config.GetBindable<bool>(OsuSetting.ForkEnhancedRankingRows);
                enhancedRankingRows.BindValueChanged(_ => rebuild(), true);
            }
            else
            {
                rebuild();
            }
        }

        private void rebuild()
        {
            float rowHeight = EnhancedRows ? enhanced_row_height : compact_row_height;

            RowSize = new Dimension(GridSizeMode.Absolute, rowHeight + row_spacing);
            backgroundFlow.Margin = new MarginPadding { Top = rowHeight + row_spacing };
            backgroundFlow.Spacing = new Vector2(0, row_spacing);
            backgroundFlow.Clear();
            rankings.ForEach(s => backgroundFlow.Add(CreateRowBackground(s)));

            Columns = mainHeaders.Concat(CreateAdditionalHeaders()).Cast<TableColumn>().ToArray();
            Content = rankings.Select((s, i) => CreateRowContent((page - 1) * items_per_page + i, s)).ToArray().ToRectangular();
        }

        protected virtual Drawable CreateRowBackground(TModel item) => new TableRowBackground { Height = EnhancedRows ? enhanced_row_height : compact_row_height };

        protected virtual Drawable[] CreateRowContent(int index, TModel item) => new Drawable[] { CreateIndexDrawable(index, item), createMainContent(item) }.Concat(CreateAdditionalContent(item)).ToArray();

        protected virtual Drawable CreateIndexDrawable(int index, TModel item) => createIndexDrawable(index);

        private static RankingsTableColumn[] mainHeaders => new[]
        {
            new RankingsTableColumn(string.Empty, Anchor.Centre, new Dimension(GridSizeMode.Absolute, 40)), // place
            new RankingsTableColumn(string.Empty, Anchor.CentreLeft, new Dimension()), // flag and username (country name)
        };

        protected abstract RankingsTableColumn[] CreateAdditionalHeaders();

        protected abstract Drawable[] CreateAdditionalContent(TModel item);

        protected virtual APIUser GetAvatarUser(TModel item) => null;

        protected sealed override Drawable CreateHeader(int index, TableColumn column)
            => (column as RankingsTableColumn)?.CreateHeaderText() ?? new HeaderText(column?.Header ?? default, false);

        protected abstract CountryCode GetCountryCode(TModel item);

        protected abstract Drawable[] CreateFlagContent(TModel item);

        private OsuSpriteText createIndexDrawable(int index) => new RowText(TextSize)
        {
            Text = (index + 1).ToLocalisableString(@"\##"),
            Font = OsuFont.GetFont(size: TextSize, weight: FontWeight.SemiBold)
        };

        private FillFlowContainer createMainContent(TModel item)
        {
            var children = new List<Drawable>();

            if (EnhancedRows && GetAvatarUser(item) is APIUser user)
            {
                children.Add(new UpdateableAvatar(user, showUserPanelOnHover: true)
                {
                    Size = new Vector2(30),
                    Masking = true,
                    CornerRadius = 5,
                });
            }

            children.Add(new UpdateableFlag(GetCountryCode(item)) { Size = new Vector2(28, 20) });
            children.AddRange(CreateFlagContent(item));

            return new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Margin = new MarginPadding { Bottom = row_spacing },
                Children = children
            };
        }

        protected class RankingsTableColumn : TableColumn
        {
            protected readonly bool Highlighted;

            public RankingsTableColumn(LocalisableString? header = null, Anchor anchor = Anchor.TopLeft, Dimension dimension = null, bool highlighted = false)
                : base(header, anchor, dimension)
            {
                Highlighted = highlighted;
            }

            public virtual HeaderText CreateHeaderText() => new HeaderText(Header, Highlighted);
        }

        protected partial class HeaderText : OsuSpriteText
        {
            private readonly bool isHighlighted;
            private IBindable<Colour4> themeColour = null!;

            public HeaderText(LocalisableString text, bool isHighlighted)
            {
                this.isHighlighted = isHighlighted;

                Text = text;
                Font = OsuFont.GetFont(size: 12);
                Margin = new MarginPadding { Vertical = 5, Horizontal = 10 };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                themeColour = colourProvider.GetColourBindable(isHighlighted ? OverlayColour.Highlight1 : OverlayColour.Foreground1);
                themeColour.BindValueChanged(colour => Colour = colour.NewValue, true);
            }
        }

        protected partial class RowText : OsuSpriteText
        {
            private IBindable<Colour4> themeColour = null!;

            public RowText(float textSize = compact_text_size)
            {
                Font = OsuFont.GetFont(size: textSize);
                Margin = new MarginPadding { Horizontal = 10, Bottom = row_spacing };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(colour => Colour = colour.NewValue, true);
            }
        }

        protected partial class ColouredRowText : RowText
        {
            private IBindable<Colour4> foregroundColour = null!;

            public ColouredRowText(float textSize = compact_text_size)
                : base(textSize)
            {
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                foregroundColour = colourProvider.GetColourBindable(OverlayColour.Foreground1);
                foregroundColour.BindValueChanged(colour => Colour = colour.NewValue, true);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            enhancedRankingRows?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
