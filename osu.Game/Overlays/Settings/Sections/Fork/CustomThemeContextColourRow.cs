using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.CustomTheme;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>
    /// A single settings row representing one contextual <see cref="SurfaceColourRole"/>
    /// for a specific <see cref="ThemeSurfaceContext"/> in the custom theme editor.
    /// </summary>
    public partial class CustomThemeContextColourRow : CompositeDrawable, IFilterable
    {
        public readonly ThemeSurfaceContext Context;
        public readonly SurfaceColourRole Role;
        private readonly LocalisableString caption;
        private readonly LocalisableString hint;
        private readonly OsuConfigManager config;

        private CustomThemeColourRow.ColourButton colourButton = null!;
        private ThemeResetButton resetButton = null!;
        private OsuSpriteText captionText = null!;
        private OsuSpriteText statusText = null!;
        private OsuTextFlowContainer hintText = null!;
        private IBindable<Colour4> content1Bindable = null!;
        private IBindable<Colour4> content2Bindable = null!;
        private IBindable<CustomThemeDefinition> customThemeBindable = null!;
        private bool isUpdatingInternally;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public CustomThemeContextColourRow(ThemeSurfaceContext context, SurfaceColourRole role,
                                           LocalisableString caption, LocalisableString hint, OsuConfigManager config)
        {
            Context = context;
            Role = role;
            this.caption = caption;
            this.hint = hint;
            this.config = config;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = SettingsPanel.CONTENT_PADDING;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Right = 144 },
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Full,
                                        Spacing = new Vector2(8, 2),
                                        Children = new Drawable[]
                                        {
                                            captionText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopLeft,
                                                Origin = Anchor.TopLeft,
                                                Text = caption,
                                                Font = OsuFont.GetFont(weight: FontWeight.SemiBold, size: 16),
                                            },
                                            statusText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopLeft,
                                                Origin = Anchor.TopLeft,
                                                Margin = new MarginPadding { Top = 2 },
                                                Font = OsuFont.GetFont(weight: FontWeight.Regular, size: 12),
                                            }
                                        }
                                    },
                                    hintText = new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(weight: FontWeight.Regular, size: 13))
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                    }
                                }
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                                Children = new Drawable[]
                                {
                                    colourButton = new CustomThemeColourRow.ColourButton(),
                                    resetButton = new ThemeResetButton
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        TooltipText = ForkSettingsStrings.CustomThemeRevertToInherited,
                                        Action = revertToInherited,
                                    }
                                }
                            }
                        }
                    }
                }
            };

            hintText.AddText(hint);

            content1Bindable = colourProvider.GetColourBindable(OverlayColour.Content1);
            content1Bindable.BindValueChanged(c => captionText.Colour = c.NewValue, true);

            content2Bindable = colourProvider.GetColourBindable(OverlayColour.Content2);
            content2Bindable.BindValueChanged(c =>
            {
                statusText.Colour = c.NewValue;
                hintText.Colour = c.NewValue;
            }, true);

            colourButton.Current.BindValueChanged(onColourPicked);

            customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
            customThemeBindable.BindValueChanged(_ => UpdateState(), true);
        }

        private void onColourPicked(ValueChangedEvent<Colour4> e)
        {
            if (isUpdatingInternally)
                return;

            var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
            theme.SetContextOverride(Context, Role, e.NewValue);
            saveAndApplyTheme(theme);
        }

        private void revertToInherited()
        {
            var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();

            if (theme.ClearContextOverride(Context, Role))
                saveAndApplyTheme(theme);
        }

        private void saveAndApplyTheme(CustomThemeDefinition theme)
        {
            config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
            OverlayColourProvider.CustomTheme.Value = theme;
            OverlayColourProvider.CustomTheme.TriggerChange();
            UpdateState();
        }

        public void UpdateState()
        {
            isUpdatingInternally = true;

            try
            {
                var theme = OverlayColourProvider.CustomTheme.Value ?? new CustomThemeDefinition();
                bool hasOverride = theme.TryGetContextOverride(Context, Role, out var overrideColour);

                Colour4 effectiveColour = hasOverride
                    ? overrideColour
                    : (colourProvider != null ? (Colour4)colourProvider.GetInheritedContextColour(Context, Role, theme.BaseTheme) : (Colour4)Color4.White);

                colourButton.Current.Value = effectiveColour;
                colourButton.UpdateColour(effectiveColour);

                resetButton.FadeTo(hasOverride ? 1f : 0f, 200, Easing.OutQuint);
                resetButton.Enabled.Value = hasOverride;

                statusText.Text = hasOverride
                    ? ForkSettingsStrings.CustomThemeCustomizedLabel
                    : ForkSettingsStrings.CustomThemeInheritedLabel;
            }
            finally
            {
                isUpdatingInternally = false;
            }
        }

        #region IFilterable

        public bool MatchingFilter
        {
            set => this.FadeTo(value ? 1 : 0);
        }

        public System.Collections.Generic.IEnumerable<LocalisableString> FilterTerms => new[] { caption, hint };

        public bool FilteringActive { get; set; }

        #endregion
    }
}
