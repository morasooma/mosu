using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>Additional entry points to the same fork settings, not independent editor-only values.</summary>
    public partial class ThemeInterfaceSection : SettingsSection
    {
        public override LocalisableString Header => ForkSettingsStrings.InterfaceHeader;
        public override Drawable CreateIcon() => new SpriteIcon { Icon = OsuIcon.NavigationSliders };

        [Resolved(canBeNull: true)] private OsuGame? game { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.DisableInterfaceShearCaption,
                    HintText = ForkSettingsStrings.DisableInterfaceShearHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkDisableInterfaceShear),
                }),
                new SettingsItemV2(new FormEnumDropdown<ForkSongSelectStyle>
                {
                    Caption = ForkSettingsStrings.SongSelectStyleCaption,
                    HintText = ForkSettingsStrings.SongSelectStyleHint,
                    Current = config.GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OldCarouselPreviewCaption,
                    HintText = ForkSettingsStrings.OldCarouselPreviewHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews),
                }),
                new SettingsItemV2(new FormEnumDropdown<InterfaceAnimationMode>
                {
                    Caption = InterfaceAnimationStrings.Caption,
                    HintText = InterfaceAnimationStrings.Hint,
                    Current = config.GetBindable<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = InterfaceAnimationStrings.EnhancedCaption,
                    HintText = InterfaceAnimationStrings.EnhancedHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkEnhancedAnimations),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OverlayTransparencyCaption,
                    HintText = ForkSettingsStrings.OverlayTransparencyHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkOverlayTransparency),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SongSelectTransparencyStrings.Caption,
                    HintText = SongSelectTransparencyStrings.Hint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectTransparency),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.OverlayBlurStrengthCaption,
                    HintText = ForkSettingsStrings.OverlayBlurStrengthHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkOverlayBlurStrength),
                    KeyboardStep = 0.01f, DisplayAsPercentage = true,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.OverlayDimCaption,
                    HintText = ForkSettingsStrings.OverlayDimHint,
                    Current = config.GetBindable<double>(OsuSetting.ForkOverlayDimAmount),
                    KeyboardStep = 0.01f, DisplayAsPercentage = true,
                }),
                new FontSettingsPanel(),
                new GameResourceSettings(),
            };
        }

    }
}
