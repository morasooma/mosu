// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;
using osu.Framework.Graphics;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osuTK.Graphics;
using osu.Game.Overlays;

namespace osu.Game.Overlays.Settings
{
    public abstract partial class SettingsSubsection : FillFlowContainer, IFilterable
    {
        public const float VERTICAL_PADDING = (header_height - header_font_size) * 0.5f;

        protected override Container<Drawable> Content => FlowContent;

        protected readonly FillFlowContainer FlowContent;
        private OsuSpriteText headerText = null!;
        private IBindable<Colour4> themeColour = null!;

        protected abstract LocalisableString Header { get; }

        public virtual IEnumerable<LocalisableString> FilterTerms => new[] { Header };

        public bool MatchingFilter
        {
            set => this.FadeTo(value ? 1 : 0);
        }

        public bool FilteringActive { get; set; }

        protected SettingsSubsection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;

            FlowContent = new FillFlowContainer
            {
                Margin = new MarginPadding { Top = SettingsSection.ITEM_SPACING_V2 },
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };
        }

        private const int header_height = 43;
        private const int header_font_size = 20;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            SubsectionColourProvider = colourProvider;
            AddRangeInternal(new[]
            {
                CreateHeader(),
                FlowContent
            });

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                // Subsections may provide a custom header (for example the
                // toggleable input-device header), so the default text field
                // is not guaranteed to exist here.
                if (headerText != null)
                    headerText.Colour = colourProvider.Content1;
            }, true);
        }

        protected OverlayColourProvider? SubsectionColourProvider { get; private set; }

        protected virtual Drawable CreateHeader()
        {
            return headerText = new OsuSpriteText
            {
                Text = Header,
                Font = OsuFont.GetFont(size: header_font_size),
                Colour = SubsectionColourProvider?.Content1 ?? Color4.White,
                Margin = new MarginPadding { Vertical = VERTICAL_PADDING },
                Padding = SettingsPanel.CONTENT_PADDING,
            };
        }
    }
}
