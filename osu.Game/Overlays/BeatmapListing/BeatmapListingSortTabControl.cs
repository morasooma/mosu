// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Graphics;
using osuTK.Graphics;
using osuTK;
using osu.Framework.Input.Events;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapListingSortTabControl : OverlaySortTabControl<SortCriteria>
    {
        public readonly Bindable<SortDirection> SortDirection = new Bindable<SortDirection>(Overlays.SortDirection.Descending);

        private (SearchCategory category, bool hasQuery)? currentParameters;
        private BeatmapCatalogueSource catalogue = BeatmapCatalogueSource.Official;

        public void SetCatalogue(BeatmapCatalogueSource source, SearchCategory category, bool hasQuery)
        {
            if (catalogue != source)
                currentParameters = null;
            catalogue = source;
            Reset(category, hasQuery);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (currentParameters == null)
                Reset(SearchCategory.Leaderboard, false);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Current.BindValueChanged(_ => SortDirection.Value = Overlays.SortDirection.Descending);
        }

        public void Reset(SearchCategory category, bool hasQuery)
        {
            var newParameters = (category, hasQuery);

            bool tabsChanged = false;
            if (currentParameters != newParameters)
            {
                // Filter before construction: adding to an already loaded tab
                // control synchronously loads every label/button/sample subtree.
                // A query/category change can leave the supported mirror tabs
                // identical, so keep their existing geometry and bindings too.
                var criteria = availableCriteria(category, hasQuery)
                    .Where(c => catalogue == BeatmapCatalogueSource.Official || BeatmapCatalogueSearch.SupportsSort(catalogue, c))
                    .ToArray();
                if (!TabControl.Items.SequenceEqual(criteria))
                {
                    TabControl.Clear();
                    foreach (var criterion in criteria)
                        TabControl.AddItem(criterion);
                    tabsChanged = true;
                }
            }

            var nonQueryCriteria = category >= SearchCategory.Pending ? SortCriteria.Updated : SortCriteria.Ranked;

            Current.Value = catalogue != BeatmapCatalogueSource.Official ? SortCriteria.Updated : hasQuery ? SortCriteria.Relevance : nonQueryCriteria;
            SortDirection.Value = Overlays.SortDirection.Descending;

            // if the new criteria isn't different from the previous one,
            // then re-adding tab items will not mark the current tab as selected.
            // see: https://github.com/ppy/osu-framework/issues/5412
            if (tabsChanged)
                TabControl.Current.TriggerChange();

            currentParameters = newParameters;
        }

        private IEnumerable<SortCriteria> availableCriteria(SearchCategory category, bool hasQuery)
        {
            yield return SortCriteria.Title;
            yield return SortCriteria.Artist;
            yield return SortCriteria.Difficulty;
            if (catalogue != BeatmapCatalogueSource.Official || category == SearchCategory.Any || category > SearchCategory.Loved)
                yield return SortCriteria.Updated;
            if (category < SearchCategory.Pending || category == SearchCategory.Mine)
                yield return SortCriteria.Ranked;
            yield return SortCriteria.Rating;
            yield return SortCriteria.Plays;
            yield return SortCriteria.Favourites;
            if (hasQuery)
                yield return SortCriteria.Relevance;
            if (category == SearchCategory.Pending)
                yield return SortCriteria.Nominations;
        }

        protected override SortTabControl CreateControl() => new BeatmapSortTabControl
        {
            SortDirection = { BindTarget = SortDirection },
        };

        private partial class BeatmapSortTabControl : SortTabControl
        {
            protected override bool AddEnumEntriesAutomatically => false;

            public readonly Bindable<SortDirection> SortDirection = new Bindable<SortDirection>();

            protected override TabItem<SortCriteria> CreateTabItem(SortCriteria value) => new BeatmapSortTabItem(value)
            {
                SortDirection = { BindTarget = SortDirection }
            };
        }

        private partial class BeatmapSortTabItem : SortTabItem
        {
            public readonly Bindable<SortDirection> SortDirection = new Bindable<SortDirection>();

            public BeatmapSortTabItem(SortCriteria value)
                : base(value)
            {
            }

            protected override TabButton CreateTabButton(SortCriteria value) => new BeatmapTabButton(value)
            {
                Active = { BindTarget = Active },
                SortDirection = { BindTarget = SortDirection }
            };
        }

        public partial class BeatmapTabButton : TabButton
        {
            public readonly Bindable<SortDirection> SortDirection = new Bindable<SortDirection>();

            protected override Color4 ContentColour
            {
                set
                {
                    base.ContentColour = value;
                    icon.Colour = value;
                }
            }

            private readonly SpriteIcon icon;

            public BeatmapTabButton(SortCriteria value)
                : base(value)
            {
                Add(icon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AlwaysPresent = true,
                    Alpha = 0,
                    Size = new Vector2(6),
                    Icon = FontAwesome.Solid.CaretDown,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                SortDirection.BindValueChanged(direction =>
                {
                    icon.ScaleTo(direction.NewValue == Overlays.SortDirection.Ascending && Active.Value ? new Vector2(1f, -1f) : Vector2.One, 300, Easing.OutQuint);
                }, true);
            }

            protected override void UpdateState()
            {
                base.UpdateState();
                icon.FadeTo(Active.Value || IsHovered ? 1 : 0, 200, Easing.OutQuint);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (Active.Value)
                    SortDirection.Value = SortDirection.Value == Overlays.SortDirection.Ascending ? Overlays.SortDirection.Descending : Overlays.SortDirection.Ascending;

                return base.OnClick(e);
            }
        }
    }
}
