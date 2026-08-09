// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Fx;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Mixing;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Applies music effects without changing the effective playback rate of the beatmap.
    /// </summary>
    public abstract class ModAudioEffects : Mod, IApplicableToTrack
    {
        public override string Name => "Audio FX";
        public override string Acronym => "FX";
        public override IconUsage? Icon => FontAwesome.Solid.VolumeUp;
        public override float IconScale => 0.78f;
        public override ModType Type => ModType.Mosu;
        public override LocalisableString Description => MosuModsStrings.ModAudioEffectsDescription;
        public override bool ValidForFreestyleAsRequiredMod => true;

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAudioEffectsPreset), nameof(MosuModsStrings.ModAudioEffectsPresetDescription), 0)]
        public Bindable<AudioEffectPreset> Preset { get; } = new Bindable<AudioEffectPreset>(AudioEffectPreset.Warm);

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAudioEffectsIntensity), nameof(MosuModsStrings.ModAudioEffectsIntensityDescription), 1)]
        public BindableDouble Intensity { get; } = new BindableDouble(0.65)
        {
            MinValue = 0.1,
            MaxValue = 1,
            Precision = 0.05,
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAudioEffectsPitch), nameof(MosuModsStrings.ModAudioEffectsPitchDescription), 2)]
        public BindableInt Pitch { get; } = new BindableInt
        {
            MinValue = -12,
            MaxValue = 12,
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAudioEffectsAffectHitSounds),
            nameof(MosuModsStrings.ModAudioEffectsAffectHitSoundsDescription), 3)]
        public BindableBool AffectHitSounds { get; } = new BindableBool();

        private readonly BindableDouble pitchFrequency = new BindableDouble(1);
        private readonly BindableDouble pitchTempo = new BindableDouble(1);

        protected ModAudioEffects()
        {
            Pitch.BindValueChanged(value =>
            {
                // Frequency changes both pitch and speed. The inverse tempo adjustment
                // cancels the speed change, leaving only the requested pitch shift.
                double factor = Math.Pow(2, value.NewValue / 12.0);
                pitchFrequency.Value = factor;
                pitchTempo.Value = 1 / factor;
            }, true);
        }

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
            track.AddAdjustment(AdjustableProperty.Frequency, pitchFrequency);
            track.AddAdjustment(AdjustableProperty.Tempo, pitchTempo);
        }

        protected override LocalisableString GetSettingTooltipText(IBindable bindable)
        {
            if (ReferenceEquals(bindable, Pitch))
                return Pitch.Value == 0
                    ? MosuModsStrings.ModAudioEffectsOriginalPitch
                    : LocalisableString.Interpolate($"{Pitch.Value:+#;-#} {MosuModsStrings.ModAudioEffectsSemitones}");

            if (ReferenceEquals(bindable, Intensity))
                return $"{Intensity.Value:P0}";

            return base.GetSettingTooltipText(bindable);
        }

        public AudioEffectsComponent CreateEffectsComponent(bool? affectHitSounds = null) =>
            new AudioEffectsComponent(Preset.Value, (float)Intensity.Value, affectHitSounds ?? AffectHitSounds.Value);
    }

    public abstract class ModAudioEffects<TObject> : ModAudioEffects, IApplicableToDrawableRuleset<TObject>
        where TObject : HitObject
    {
        public void ApplyToDrawableRuleset(DrawableRuleset<TObject> drawableRuleset)
        {
            drawableRuleset.Overlays.Add(CreateEffectsComponent());

            if (AffectHitSounds.Value && Pitch.Value != 0)
            {
                double factor = Math.Pow(2, Pitch.Value / 12.0);
                drawableRuleset.Audio.AddAdjustment(AdjustableProperty.Frequency, new BindableDouble(factor));
            }
        }
    }

    public enum AudioEffectPreset
    {
        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetNone))]
        None = 0,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetWarm))]
        Warm = 1,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetBassBoost))]
        BassBoost = 2,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetBright))]
        Bright = 3,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetRadio))]
        Radio = 4,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetUnderwater))]
        Underwater = 5,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetSpacious))]
        Spacious = 6,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetChorus))]
        Chorus = 7,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetPhaser))]
        Phaser = 8,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetLoFi))]
        LoFi = 9,

        [LocalisableDescription(typeof(MosuModsStrings), nameof(MosuModsStrings.AudioPresetRotation))]
        Rotation = 10,
    }

    /// <summary>
    /// Owns all mixer effects applied by <see cref="ModAudioEffects"/> and removes them
    /// when gameplay is exited or restarted.
    /// </summary>
    public partial class AudioEffectsComponent : Component
    {
        private readonly AudioEffectPreset preset;
        private readonly float intensity;
        private readonly bool affectHitSounds;

        private readonly List<(AudioMixer mixer, IEffectParameter effect)> attachedEffects = new List<(AudioMixer, IEffectParameter)>();

        public AudioEffectsComponent(AudioEffectPreset preset, float intensity, bool affectHitSounds)
        {
            this.preset = preset;
            this.intensity = Math.Clamp(intensity, 0.1f, 1);
            this.affectHitSounds = affectHitSounds;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            addPreset(audio.TrackMixer);

            if (affectHitSounds)
                addPreset(audio.SampleMixer);
        }

        private void addPreset(AudioMixer mixer)
        {
            switch (preset)
            {
                case AudioEffectPreset.Warm:
                    add(mixer, shelf(BQFType.LowShelf, 180, 5.5f * intensity));
                    add(mixer, shelf(BQFType.HighShelf, 7000, -2 * intensity));
                    break;

                case AudioEffectPreset.BassBoost:
                    add(mixer, shelf(BQFType.LowShelf, 160, 10 * intensity));
                    add(mixer, peak(80, 3.5f * intensity, 0.8f));
                    break;

                case AudioEffectPreset.Bright:
                    add(mixer, shelf(BQFType.HighShelf, 5000, 8 * intensity));
                    add(mixer, peak(3000, 3 * intensity, 0.9f));
                    break;

                case AudioEffectPreset.Radio:
                    add(mixer, filter(BQFType.HighPass, 260 + 1800 * intensity));
                    add(mixer, filter(BQFType.LowPass, 6200 - 2200 * intensity));
                    add(mixer, peak(1700, 4 * intensity, 1.1f));
                    break;

                case AudioEffectPreset.Underwater:
                    add(mixer, filter(BQFType.LowPass, 6000 - 5000 * intensity));
                    add(mixer, new ChorusParameters
                    {
                        fDryMix = 1,
                        fWetMix = 0.08f + 0.22f * intensity,
                        fFeedback = 0.15f,
                        fMinSweep = 4,
                        fMaxSweep = 18,
                        fRate = 45,
                    }, 10);
                    break;

                case AudioEffectPreset.Spacious:
                    add(mixer, new ReverbParameters
                    {
                        fDryMix = 1,
                        fWetMix = 0.12f + 0.5f * intensity,
                        fRoomSize = 0.35f + 0.5f * intensity,
                        fDamp = 0.55f,
                        fWidth = 1,
                    }, 10);
                    break;

                case AudioEffectPreset.Chorus:
                    add(mixer, new ChorusParameters
                    {
                        fDryMix = 1,
                        fWetMix = 0.1f + 0.4f * intensity,
                        fFeedback = 0.2f + 0.25f * intensity,
                        fMinSweep = 1,
                        fMaxSweep = 12 + 28 * intensity,
                        fRate = 80 + 160 * intensity,
                    }, 10);
                    break;

                case AudioEffectPreset.Phaser:
                    add(mixer, new PhaserParameters
                    {
                        fDryMix = 1,
                        fWetMix = 0.15f + 0.45f * intensity,
                        fFeedback = 0.1f + 0.35f * intensity,
                        fRate = 0.25f + 1.5f * intensity,
                        fRange = 2 + 3 * intensity,
                        fFreq = 180 + 220 * intensity,
                    }, 10);
                    break;

                case AudioEffectPreset.LoFi:
                    add(mixer, filter(BQFType.HighPass, 40 + 140 * intensity));
                    add(mixer, filter(BQFType.LowPass, 9000 - 5000 * intensity));
                    add(mixer, new ChorusParameters
                    {
                        fDryMix = 0.95f,
                        fWetMix = 0.03f + 0.07f * intensity,
                        fFeedback = 0.08f,
                        fMinSweep = 2,
                        fMaxSweep = 8,
                        fRate = 35,
                    }, 10);
                    break;

                case AudioEffectPreset.Rotation:
                    add(mixer, new RotateParameters
                    {
                        fRate = 0.04f + 0.18f * intensity,
                    }, 10);
                    break;

                case AudioEffectPreset.None:
                    break;
            }

            if (preset is not AudioEffectPreset.None and not AudioEffectPreset.Rotation)
            {
                // FX chains, especially distortion and positive EQ, can otherwise exceed
                // the original track level. Keep peaks controlled and leave headroom.
                add(mixer, new CompressorParameters
                {
                    fGain = -1 - 2 * intensity,
                    fThreshold = -4,
                    fRatio = 10,
                    fAttack = 1,
                    fRelease = 120,
                }, 100);
            }
        }

        private void add(AudioMixer mixer, IEffectParameter effect, int priority = 0)
        {
            mixer.AddEffect(effect, priority);
            attachedEffects.Add((mixer, effect));
        }

        private static BQFParameters filter(BQFType type, float cutoff) => new BQFParameters
        {
            lFilter = type,
            fCenter = cutoff,
            fBandwidth = 0,
            fQ = 0.7f,
        };

        private static BQFParameters shelf(BQFType type, float centre, float gain) => new BQFParameters
        {
            lFilter = type,
            fCenter = centre,
            fGain = gain,
            fBandwidth = 0,
            fQ = 0,
            fS = 1,
        };

        private static BQFParameters peak(float centre, float gain, float q) => new BQFParameters
        {
            lFilter = BQFType.PeakingEQ,
            fCenter = centre,
            fGain = gain,
            fBandwidth = 0,
            fQ = q,
        };

        protected override void Dispose(bool isDisposing)
        {
            foreach ((AudioMixer mixer, IEffectParameter effect) in attachedEffects)
                mixer.RemoveEffect(effect);

            attachedEffects.Clear();
            base.Dispose(isDisposing);
        }
    }
}
