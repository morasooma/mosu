using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.CustomTheme;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>
    /// A single settings row representing one semantic <see cref="GeometryRole"/>
    /// in the custom theme editor, with a live slider, preview chip, and independent reset action.
    /// </summary>
    public partial class CustomThemeRadiusRow : CompositeDrawable, IFilterable
    {
        public readonly GeometryRole Role;
        private readonly LocalisableString caption;
        private readonly LocalisableString hint;
        private readonly OsuConfigManager config;
        private readonly float minRadius;
        private readonly float maxRadius;
        private readonly float stepRadius;

        private FormSliderBar<float> slider = null!;
        private ThemeResetButton resetButton = null!;
        private Container previewBadge = null!;
        private Box previewFill = null!;
        private Container previewBorder = null!;
        private OsuSpriteText captionText = null!;
        private OsuSpriteText statusText = null!;
        private OsuTextFlowContainer hintText = null!;
        private IBindable<Colour4> content1Bindable = null!;
        private IBindable<Colour4> content2Bindable = null!;
        private IBindable<CustomThemeDefinition> customThemeBindable = null!;
        private bool isUpdatingInternally;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public CustomThemeRadiusRow(
            GeometryRole role,
            LocalisableString caption,
            LocalisableString hint,
            OsuConfigManager config,
            float minRadius = 0f,
            float maxRadius = 30f,
            float stepRadius = 1f)
        {
            Role = role;
            this.caption = caption;
            this.hint = hint;
            this.config = config;
            this.minRadius = minRadius;
            this.maxRadius = maxRadius;
            this.stepRadius = stepRadius;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = SettingsPanel.CONTENT_PADDING;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            float defaultRadius = OverlayColourProvider.GetDefaultRadius(Role);

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
                                Padding = new MarginPadding { Right = 78 },
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
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    previewBadge = new Container
                                    {
                                        Size = new Vector2(32, 24),
                                        Masking = true,
                                        CornerRadius = defaultRadius,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Children = new Drawable[]
                                        {
                                            previewFill = new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                            },
                                            previewBorder = new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = true,
                                                CornerRadius = defaultRadius,
                                                BorderThickness = 1.5f,
                                                Child = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Alpha = 0,
                                                    AlwaysPresent = true,
                                                }
                                            }
                                        }
                                    },
                                    resetButton = new ThemeResetButton
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        TooltipText = ForkSettingsStrings.CustomThemeGeometryRevert,
                                        Action = revertToDefault,
                                    }
                                }
                            }
                        }
                    },
                    slider = new FormSliderBar<float>
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = new BindableFloat(defaultRadius)
                        {
                            MinValue = minRadius,
                            MaxValue = maxRadius,
                            Precision = stepRadius,
                            Default = defaultRadius,
                        },
                        LabelFormat = v => global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4036(v),
                        TransferValueOnCommit = false,
                    }
                }
            };

            hintText.AddText(hint);

            content1Bindable = colourProvider.GetColourBindable(OverlayColour.Content1).GetBoundCopy();
            content1Bindable.BindValueChanged(c =>
            {
                captionText.Colour = c.NewValue;
                previewBorder.BorderColour = c.NewValue.Opacity(0.6f);
            }, true);

            content2Bindable = colourProvider.GetColourBindable(OverlayColour.Content2).GetBoundCopy();
            content2Bindable.BindValueChanged(c =>
            {
                statusText.Colour = c.NewValue;
                hintText.Colour = c.NewValue;
                previewFill.Colour = colourProvider.Background3;
            }, true);

            slider.Current.BindValueChanged(onSliderChanged);

            customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
            customThemeBindable.BindValueChanged(_ => UpdateState(), true);
        }

        private void onSliderChanged(ValueChangedEvent<float> e)
        {
            if (isUpdatingInternally)
                return;

            var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
            theme.SetRadius(Role, e.NewValue);
            saveAndApplyTheme(theme);
        }

        private void revertToDefault()
        {
            var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();

            if (theme.ClearRadius(Role))
                saveAndApplyTheme(theme);
        }

        private void saveAndApplyTheme(CustomThemeDefinition theme)
        {
            config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
            OverlayColourProvider.CustomTheme.Value = theme;
            UpdateState();
        }

        public void UpdateState()
        {
            isUpdatingInternally = true;

            try
            {
                var theme = OverlayColourProvider.CustomTheme.Value ?? new CustomThemeDefinition();
                bool hasOverride = theme.TryGetRadius(Role, out float radius);

                float effective = hasOverride ? radius : OverlayColourProvider.GetDefaultRadius(Role);

                slider.Current.Value = effective;

                if (previewBadge != null)
                {
                    previewBadge.CornerRadius = effective;
                    previewBorder.CornerRadius = effective;
                }

                if (resetButton != null)
                {
                    resetButton.FadeTo(hasOverride ? 1f : 0f, 200, Easing.OutQuint);
                    resetButton.Enabled.Value = hasOverride;
                }

                if (statusText != null)
                {
                    statusText.Text = hasOverride
                        ? ForkSettingsStrings.CustomThemeCustomizedLabel
                        : ForkSettingsStrings.CustomThemeInheritedLabel;
                }
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

        public bool FilteringActive { get; set; }

        public System.Collections.Generic.IEnumerable<LocalisableString> FilterTerms =>
            new[] { caption, hint, Role.ToString() };

        #endregion
    }
}
