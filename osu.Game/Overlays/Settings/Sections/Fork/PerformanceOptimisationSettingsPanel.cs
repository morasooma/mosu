// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>
    /// Detailed renderer, latency and skin performance settings.
    /// </summary>
    public partial class PerformanceOptimisationSettingsPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader() =>
            new SettingsHeader(ForkSettingsStrings.PerformanceOptimisationSettingsHeader, ForkSettingsStrings.PerformanceOptimisationSettingsDescription);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            AddSection(new RendererPerformanceSettingsSection(config));
            AddSection(new SkinPerformanceSettingsSection(config));
        }

        private partial class RendererPerformanceSettingsSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.RendererOptimisationSettingsHeader;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = OsuIcon.Settings
            };

            public RendererPerformanceSettingsSection(OsuConfigManager config)
            {
                Children = new Drawable[]
                {
                    createCheckBox(ForkSettingsStrings.WindowsUltraPerfCaption, ForkSettingsStrings.WindowsUltraPerfHint, config.GetBindable<bool>(OsuSetting.ForkWindowsUltraPerformanceMode),
                        latency(ForkSettingsStrings.PerformanceEvidenceWindowsUltra)),
                };
            }
        }

        private partial class SkinPerformanceSettingsSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.SkinPerfSettingsHeader;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = OsuIcon.Settings
            };

            public SkinPerformanceSettingsSection(OsuConfigManager config)
            {
                Children = new Drawable[]
                {
                    createCheckBox(ForkSettingsStrings.SkinPerfCaption, ForkSettingsStrings.SkinPerfHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceMode),
                        highGain(ForkSettingsStrings.PerformanceEvidenceSkinMaster)),
                    createCheckBox(ForkSettingsStrings.SkinPerfFreezeAnimationsCaption, ForkSettingsStrings.SkinPerfFreezeAnimationsHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceFreezeAnimations),
                        smallGain(ForkSettingsStrings.PerformanceEvidenceSkinFreezeAnimations)),
                    createCheckBox(ForkSettingsStrings.SkinPerfSimplifyEffectsCaption, ForkSettingsStrings.SkinPerfSimplifyEffectsHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceSimplifyEffects),
                        smallGain(ForkSettingsStrings.PerformanceEvidenceSkinSimplifyEffects)),
                    createCheckBox(ForkSettingsStrings.SkinPerfOptimiseTexturesCaption, ForkSettingsStrings.SkinPerfOptimiseTexturesHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceOptimiseTextures),
                        mediumGain(ForkSettingsStrings.PerformanceEvidenceSkinOptimiseTextures)),
                    createCheckBox(ForkSettingsStrings.SkinPerfSimplifyHudCaption, ForkSettingsStrings.SkinPerfSimplifyHudHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceSimplifyHud),
                        mediumGain(ForkSettingsStrings.PerformanceEvidenceSkinSimplifyHud)),
                    createCheckBox(ForkSettingsStrings.SkinPerfSimplifyCountersCaption, ForkSettingsStrings.SkinPerfSimplifyCountersHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceSimplifyCounters),
                        smallGain(ForkSettingsStrings.PerformanceEvidenceSkinSimplifyCounters)),
                    createCheckBox(ForkSettingsStrings.SkinPerfDisableKiaiFlashingCaption, ForkSettingsStrings.SkinPerfDisableKiaiFlashingHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing),
                        mediumGain(ForkSettingsStrings.PerformanceEvidenceSkinDisableKiai)),
                    createCheckBox(ForkSettingsStrings.SkinPerfBlackBackgroundCaption, ForkSettingsStrings.SkinPerfBlackBackgroundHint, config.GetBindable<bool>(OsuSetting.ForkSkinPerformanceBlackBackground),
                        highGain(ForkSettingsStrings.PerformanceEvidenceSkinBlackBackground)),
                };
            }
        }

        private static SettingsItemV2 createCheckBox(LocalisableString caption, LocalisableString hint, Bindable<bool> current, PerformanceImpact impact)
        {
            var item = new SettingsItemV2(new FormCheckBox
            {
                Caption = caption,
                HintText = hint,
                Current = current,
            });

            item.Note.Value = new SettingsNote.Data(
                ForkSettingsStrings.PerformanceImpactDescription(impact.Level, impact.Guidance, impact.Evidence),
                SettingsNote.Type.Informational,
                impact.Accent);
            return item;
        }

        private static PerformanceImpact highGain(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactHigh, ForkSettingsStrings.PerformanceImpactGuidanceHigh, evidence, SettingsNote.Accent.HighGain);

        private static PerformanceImpact mediumGain(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactMedium, ForkSettingsStrings.PerformanceImpactGuidanceMedium, evidence, SettingsNote.Accent.MediumGain);

        private static PerformanceImpact smallGain(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactLow, ForkSettingsStrings.PerformanceImpactGuidanceLow, evidence, SettingsNote.Accent.SmallGain);

        private static PerformanceImpact latency(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactLatency, ForkSettingsStrings.PerformanceImpactGuidanceLatency, evidence, SettingsNote.Accent.Latency);

        private static PerformanceImpact tradeOff(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactMixed, ForkSettingsStrings.PerformanceImpactGuidanceMixed, evidence, SettingsNote.Accent.TradeOff);

        private static PerformanceImpact situational(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactContextual, ForkSettingsStrings.PerformanceImpactGuidanceContextual, evidence, SettingsNote.Accent.Situational);

        private static PerformanceImpact negative(LocalisableString evidence) =>
            new PerformanceImpact(ForkSettingsStrings.PerformanceImpactNegative, ForkSettingsStrings.PerformanceImpactGuidanceNegative, evidence, SettingsNote.Accent.Negative);

        private sealed record PerformanceImpact(LocalisableString Level, LocalisableString Guidance, LocalisableString Evidence, SettingsNote.Accent Accent);
    }
}
