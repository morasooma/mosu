// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
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
        private void load(OsuConfigManager config, AudioManager audio)
        {
            AddSection(new CarouselPerformanceSettingsSection(config));
            AddSection(new RendererPerformanceSettingsSection(config));
            AddSection(new AudioLatencySettingsSection(config, audio));
            AddSection(new SkinPerformanceSettingsSection(config));
        }

        private partial class CarouselPerformanceSettingsSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.CarouselOptimisationSettingsHeader;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.Image
            };

            public CarouselPerformanceSettingsSection(OsuConfigManager config)
            {
                Children = new Drawable[]
                {
                    createCheckBox(ForkSettingsStrings.CarouselPerformanceModeCaption, ForkSettingsStrings.CarouselPerformanceModeHint,
                        config.GetBindable<bool>(OsuSetting.ForkSongSelectCarouselPerformanceMode), tradeOff(ForkSettingsStrings.PerformanceEvidenceCarousel)),
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = ForkSettingsStrings.CarouselPreviewsCaption,
                        HintText = ForkSettingsStrings.CarouselPreviewsHint,
                        Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectCarouselPreviews),
                    }),
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = ForkSettingsStrings.CarouselLazyLoadingCaption,
                        HintText = ForkSettingsStrings.CarouselLazyLoadingHint,
                        Current = config.GetBindable<bool>(OsuSetting.ForkSongSelectCarouselLazyLoading),
                    }),
                    new SettingsItemV2(new FormSliderBar<int>
                    {
                        Caption = ForkSettingsStrings.CarouselPreviewResolutionCaption,
                        HintText = ForkSettingsStrings.CarouselPreviewResolutionHint,
                        Current = config.GetBindable<int>(OsuSetting.ForkSongSelectCarouselPreviewResolution),
                        KeyboardStep = 5,
                        LabelFormat = value => $"{value}%",
                    }),
                };
            }
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
                    createCheckBox(ForkSettingsStrings.Use8kPollingRateCaption, ForkSettingsStrings.Use8kPollingRateHint, config.GetBindable<bool>(OsuSetting.ForkUse8kPollingRate),
                        situational(ForkSettingsStrings.PerformanceEvidenceInput8k)),
                    createCheckBox(ForkSettingsStrings.LargeTextureAtlasCaption, ForkSettingsStrings.LargeTextureAtlasHint, config.GetBindable<bool>(OsuSetting.ForkLargeTextureAtlas),
                        negative(ForkSettingsStrings.PerformanceEvidenceAtlas4096)),
                    createCheckBox(ForkSettingsStrings.DeferredVertexBatchingCaption, ForkSettingsStrings.DeferredVertexBatchingHint, config.GetBindable<bool>(OsuSetting.ForkDeferredVertexUploadBatching),
                        negative(ForkSettingsStrings.PerformanceEvidenceDeferredVertexBatching)),
                    createCheckBox(ForkSettingsStrings.DeferredDirectVertexUploadCaption, ForkSettingsStrings.DeferredDirectVertexUploadHint, config.GetBindable<bool>(OsuSetting.ForkDeferredDirectVertexUpload),
                        tradeOff(ForkSettingsStrings.PerformanceEvidenceDeferredDirectVertex)),
                    createCheckBox(ForkSettingsStrings.DeferredDirectUniformUploadCaption, ForkSettingsStrings.DeferredDirectUniformUploadHint, config.GetBindable<bool>(OsuSetting.ForkDeferredDirectUniformUpload),
                        tradeOff(ForkSettingsStrings.PerformanceEvidenceDeferredDirectUniform)),
                    createCheckBox(ForkSettingsStrings.VeldridPipelineLookupCacheCaption, ForkSettingsStrings.VeldridPipelineLookupCacheHint, config.GetBindable<bool>(OsuSetting.ForkVeldridPipelineLookupCache),
                        tradeOff(ForkSettingsStrings.PerformanceEvidencePipelineCache)),
                    createCheckBox(ForkSettingsStrings.StaticChildLifetimeCacheCaption, ForkSettingsStrings.StaticChildLifetimeCacheHint, config.GetBindable<bool>(OsuSetting.ForkStaticChildLifetimeCache),
                        tradeOff(ForkSettingsStrings.PerformanceEvidenceStaticLifetime)),
                    createCheckBox(ForkSettingsStrings.AtlasRegionAllocatorCaption, ForkSettingsStrings.AtlasRegionAllocatorHint, config.GetBindable<bool>(OsuSetting.ForkAtlasRegionAllocator),
                        tradeOff(ForkSettingsStrings.PerformanceEvidenceAtlasRegionReuse)),
                    new SettingsItemV2(new FormSliderBar<float>
                    {
                        Caption = ForkSettingsStrings.GameplayRenderScaleCaption,
                        HintText = ForkSettingsStrings.GameplayRenderScaleHint,
                        Current = config.GetBindable<float>(OsuSetting.ForkGameplayRenderScale),
                        KeyboardStep = 0.05f,
                        DisplayAsPercentage = true,
                    }),
                    createCheckBox(ForkSettingsStrings.AllowTearingCaption, ForkSettingsStrings.AllowTearingHint, config.GetBindable<bool>(OsuSetting.ForkAllowTearing),
                        situational(ForkSettingsStrings.PerformanceEvidenceAllowTearing)),
                    createCheckBox(ForkSettingsStrings.UpdateSpinWaitCaption, ForkSettingsStrings.UpdateSpinWaitHint, config.GetBindable<bool>(OsuSetting.ForkUpdateThreadSpinWait),
                        latency(ForkSettingsStrings.PerformanceEvidenceSpinWait)),
                };
            }
        }

        private partial class AudioLatencySettingsSection : SettingsSection
        {
            public override LocalisableString Header => ForkSettingsStrings.AudioLatencySettingsHeader;

            public override Drawable CreateIcon() => new SpriteIcon
            {
                Icon = FontAwesome.Solid.VolumeUp
            };

            public AudioLatencySettingsSection(OsuConfigManager config, AudioManager audio)
            {
                var control = new ExclusiveAudioPerformanceCheckBox(config.GetBindable<bool>(OsuSetting.ForkExclusiveAudio), audio);
                var item = new SettingsItemV2(control)
                {
                    Keywords = new[] { "audio", "latency", "wasapi", "exclusive", "extreme" },
                };

                control.Current.BindValueChanged(enabled =>
                {
                    item.Note.Value = enabled.NewValue
                        ? new SettingsNote.Data(
                            ForkSettingsStrings.ExclusiveAudioWarning,
                            SettingsNote.Type.Critical,
                            SettingsNote.Accent.Negative)
                        : null;
                }, true);

                Children = new Drawable[]
                {
                    item,
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = ForkSettingsStrings.ExclusiveAudioGameplayOnlyCaption,
                        HintText = ForkSettingsStrings.ExclusiveAudioGameplayOnlyHint,
                        Current = config.GetBindable<bool>(OsuSetting.ForkExclusiveAudioGameplayOnly),
                    })
                    {
                        Keywords = new[] { "audio", "wasapi", "exclusive", "gameplay" },
                    },
                };
            }
        }

        private partial class ExclusiveAudioPerformanceCheckBox : CompositeDrawable, IHasCurrentValue<bool>, IFormControl
        {
            private readonly AudioManager audio;
            private readonly Bindable<bool> configExperimentalAudio;
            private readonly IBindable<double> outputLatency = new Bindable<double>();
            private readonly BindableWithCurrent<bool> current = new BindableWithCurrent<bool>();

            private FormCheckBox checkBox = null!;
            private OsuSpriteText latencyText = null!;

            public Bindable<bool> Current
            {
                get => current.Current;
                set => current.Current = value;
            }

            public ExclusiveAudioPerformanceCheckBox(Bindable<bool> configExclusiveAudio, AudioManager audio)
            {
                this.audio = audio;
                configExperimentalAudio = audio.UseExperimentalWasapi.GetBoundCopy();
                current.Current = configExclusiveAudio;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                InternalChildren = new Drawable[]
                {
                    checkBox = new FormCheckBox
                    {
                        Caption = ForkSettingsStrings.ExclusiveAudioCaption,
                        HintText = ForkSettingsStrings.ExclusiveAudioHint,
                        Current = Current,
                    },
                    latencyText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -(SwitchButton.WIDTH + 14),
                        Colour = colours.Orange0,
                        Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                        Text = ForkSettingsStrings.AudioLatencyMeasuring,
                    },
                };

                Current.Disabled = !OperatingSystem.IsWindows();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(change =>
                {
                    ValueChanged?.Invoke();

                    if (change.NewValue)
                        configExperimentalAudio.Value = true;
                }, true);

                // Turning the regular WASAPI backend off elsewhere also turns this mode off;
                // exclusive access has no meaning on the legacy audio path.
                configExperimentalAudio.BindValueChanged(change =>
                {
                    if (!change.NewValue && Current.Value)
                        Current.Value = false;
                });

                // Standalone public build note: AudioManager.OutputLatency is an optional Mosu framework extension.
                outputLatency.BindValueChanged(change => Schedule(() =>
                {
                    latencyText.Text = change.NewValue > 0
                        ? ForkSettingsStrings.CurrentAudioLatency($"~{change.NewValue:0.0} ms")
                        : ForkSettingsStrings.AudioLatencyMeasuring;
                }), true);
            }

            public IEnumerable<LocalisableString> FilterTerms => new[] { ForkSettingsStrings.ExclusiveAudioCaption };

            public event Action? ValueChanged;

            public bool IsDefault => Current.IsDefault;

            public void SetDefault() => Current.SetDefault();

            public bool IsDisabled => Current.Disabled;

            public float MainDrawHeight => checkBox.DrawHeight;
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
                    createCheckBox(ForkSettingsStrings.SkinPerfArgonFollowRingCaption, ForkSettingsStrings.SkinPerfArgonFollowRingHint, config.GetBindable<bool>(OsuSetting.ForkArgonFollowRing),
                        mediumGain(ForkSettingsStrings.PerformanceEvidenceArgonFollowRing)),
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
