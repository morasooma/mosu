// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osuTK;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapSearchFilterRow<T> : CompositeDrawable, IHasCurrentValue<T>
    {
        private readonly BindableWithCurrent<T> current = new BindableWithCurrent<T>();
        private readonly OsuTextFlowContainer headerText;
        private IBindable<Colour4> themeColour = null!;

        public Bindable<T> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        public BeatmapSearchFilterRow(LocalisableString header)
        {
            Drawable filter;
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
            AddInternal(new GridContainer
            {
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, size: 100),
                    new Dimension()
                },
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize)
                },
                Content = new[]
                {
                    new[]
                    {
                        headerText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = header
                        },
                        filter = CreateFilter()
                    }
                }
            });

            if (filter is IHasCurrentValue<T> filterWithValue)
                Current = filterWithValue.Current;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(colour => headerText.Colour = colour.NewValue, true);
        }

        protected virtual Drawable CreateFilter() => new BeatmapSearchFilter();

        protected partial class BeatmapSearchFilter : TabControl<T>
        {
            public BeatmapSearchFilter()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                TabContainer.Spacing = new Vector2(10, 0);

                if (typeof(T).IsEnum)
                {
                    foreach (var val in EnumExtensions.GetValuesInOrder<T>())
                        AddItem(val);
                }
            }

            protected override Dropdown<T> CreateDropdown() => null!;

            protected override TabItem<T> CreateTabItem(T value) => new FilterTabItem<T>(value);

            protected override TabFillFlowContainer CreateTabFlow() => new TabFillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                AllowMultiline = true,
            };
        }
    }
}
