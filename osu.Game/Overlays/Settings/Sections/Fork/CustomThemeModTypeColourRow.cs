// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class CustomThemeModTypeColourRow : Container
    {
        public readonly ModType ModType;

        private readonly OsuConfigManager config;
        private readonly CustomThemeColourRow.ColourButton colourButton;
        private readonly ThemeResetButton resetButton;
        private readonly OsuSpriteText captionText;
        private readonly OsuSpriteText statusText;

        private IBindable<Colour4>? content1Bindable;
        private IBindable<Colour4>? content2Bindable;
        private IBindable<CustomThemeDefinition>? customThemeBindable;
        private bool isUpdatingInternally;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public CustomThemeModTypeColourRow(ModType modType, LocalisableString caption, OsuConfigManager config)
        {
            ModType = modType;
            this.config = config;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS, Vertical = 4 };

            InternalChildren = new Drawable[]
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
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            content1Bindable = colourProvider.GetColourBindable(OverlayColour.Content1);
            content1Bindable.BindValueChanged(c => captionText.Colour = c.NewValue, true);

            content2Bindable = colourProvider.GetColourBindable(OverlayColour.Content2);
            content2Bindable.BindValueChanged(c => statusText.Colour = c.NewValue, true);

            colourButton.Current.BindValueChanged(onColourPicked);

            customThemeBindable = OverlayColourProvider.CustomTheme.GetBoundCopy();
            customThemeBindable.BindValueChanged(_ => UpdateState(), true);
        }

        private void onColourPicked(ValueChangedEvent<Colour4> e)
        {
            if (isUpdatingInternally)
                return;

            var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
            theme.SetModTypeOverride(ModType, e.NewValue);
            saveAndApplyTheme(theme);
        }

        private void revertToInherited()
        {
            var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();

            if (theme.ClearModTypeOverride(ModType))
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
                bool hasOverride = theme.TryGetModTypeOverride(ModType, out var overrideColour);

                Colour4 effective = hasOverride ? overrideColour : colours.ForModType(ModType);

                colourButton.Current.Value = effective;
                colourButton.UpdateColour(effective);

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
    }
}
