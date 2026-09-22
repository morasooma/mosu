// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Torii's HS mod. Plays hitsounds from key presses rather than judgements.
    /// </summary>
    public partial class OsuModHitSounder : Mod, IApplicableToDrawableRuleset<OsuHitObject>, IApplicableToBeatmap, IToriiServerMod
    {
        public override string Name => "Hit Sounder";
        public override string Acronym => "HS";
        public override LocalisableString Description => "Hitsounds follow your fingers, not the notes. Mash freely.";
        public override ModType Type => ModType.Fun;
        public override IconUsage? Icon => OsuIcon.Metronome;
        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay),
            typeof(OsuModCinema),
        };

        [SettingSource("Sample set", "Which bank to use when there is no note nearby.")]
        public Bindable<HitSounderBank> Bank { get; } = new Bindable<HitSounderBank>(HitSounderBank.Auto);

        [SettingSource("Keep note hitsounds", "When disabled, every press uses the current sample bank and ignores mapped hitsounds.")]
        public BindableBool UseNoteSamples { get; } = new BindableBool(true);

        private const double borrow_window_ms = 200;

        private IBeatmap beatmap = null!;
        private double[] objectTimes = Array.Empty<double>();
        private IList<HitSampleInfo>[] objectSamples = Array.Empty<IList<HitSampleInfo>>();

        public void ApplyToBeatmap(IBeatmap targetBeatmap)
        {
            beatmap = targetBeatmap;

            var ordered = targetBeatmap.HitObjects.OrderBy(hitObject => hitObject.StartTime).ToArray();
            objectTimes = ordered.Select(hitObject => hitObject.StartTime).ToArray();
            objectSamples = ordered.Select(hitObject => hitObject.Samples.ToArray()).Cast<IList<HitSampleInfo>>().ToArray();

            // The key-triggered player owns top-level samples while this mod is active.
            // Nested slider/spinner samples remain unchanged.
            foreach (var hitObject in ordered)
                hitObject.Samples = Array.Empty<HitSampleInfo>();
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            var osuRuleset = (DrawableOsuRuleset)drawableRuleset;
            osuRuleset.KeyBindingInputManager.Add(new HitSoundPlayer(this, drawableRuleset.FrameStableClock));
        }

        internal ISampleInfo[] SamplesForTesting(double time) => samplesFor(time) ?? Array.Empty<ISampleInfo>();

        private ISampleInfo[]? samplesFor(double time)
        {
            if (UseNoteSamples.Value)
            {
                IList<HitSampleInfo>? borrowed = nearestNoteSamples(time);

                if (borrowed != null)
                    return borrowed.Cast<ISampleInfo>().ToArray();
            }

            return new ISampleInfo[] { fallbackSample(time) };
        }

        private IList<HitSampleInfo>? nearestNoteSamples(double time)
        {
            if (objectTimes.Length == 0)
                return null;

            int index = Array.BinarySearch(objectTimes, time);

            if (index < 0)
                index = ~index;

            int best = -1;
            double bestDistance = double.MaxValue;

            foreach (int candidate in new[] { index - 1, index })
            {
                if (candidate < 0 || candidate >= objectTimes.Length)
                    continue;

                double distance = Math.Abs(objectTimes[candidate] - time);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best < 0 || bestDistance > borrow_window_ms)
                return null;

            IList<HitSampleInfo> samples = objectSamples[best];
            return samples.Count > 0 ? samples : null;
        }

        private HitSampleInfo fallbackSample(double time)
        {
            string bank = Bank.Value switch
            {
                HitSounderBank.Normal => HitSampleInfo.BANK_NORMAL,
                HitSounderBank.Soft => HitSampleInfo.BANK_SOFT,
                HitSounderBank.Drum => HitSampleInfo.BANK_DRUM,
                _ => ((beatmap.ControlPointInfo as LegacyControlPointInfo)?.SamplePointAt(time)
                      ?? SampleControlPoint.DEFAULT).SampleBank,
            };

            return new HitSampleInfo(HitSampleInfo.HIT_NORMAL, bank);
        }

        private partial class HitSoundPlayer : CompositeDrawable, IKeyBindingHandler<OsuAction>
        {
            private const int voices = 8;

            private readonly OsuModHitSounder mod;
            private readonly IFrameStableClock clock;
            private readonly PausableSkinnableSound[] pool = new PausableSkinnableSound[voices];

            private int nextVoice;

            public HitSoundPlayer(OsuModHitSounder mod, IFrameStableClock clock)
            {
                this.mod = mod;
                this.clock = clock;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                for (int i = 0; i < voices; i++)
                    AddInternal(pool[i] = new PausableSkinnableSound());
            }

            public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
            {
                switch (e.Action)
                {
                    case OsuAction.LeftButton:
                    case OsuAction.RightButton:
                        play();
                        break;
                }

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
            {
            }

            private void play()
            {
                if (clock.IsRewinding)
                    return;

                ISampleInfo[]? samples = mod.samplesFor(clock.CurrentTime);

                if (samples == null || samples.Length == 0)
                    return;

                PausableSkinnableSound voice = pool[nextVoice];
                nextVoice = (nextVoice + 1) % voices;
                voice.Samples = samples;
                voice.Play();
            }
        }
    }

    public enum HitSounderBank
    {
        [System.ComponentModel.Description("Follow the map")]
        Auto,

        Normal,
        Soft,
        Drum,
    }
}
