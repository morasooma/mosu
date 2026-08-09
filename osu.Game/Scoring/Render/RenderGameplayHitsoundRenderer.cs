// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio;
using osu.Framework.Audio.Mixing;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Scoring.Render
{
    internal sealed class RenderGameplayHitsoundRenderer : IRenderGameplayHitsoundRenderer, IDisposable
    {
        public AudioMixer CaptureMixer { get; }

        public double PlaybackFrequency { get; set; } = 1;

        public double PlaybackTempo { get; set; } = 1;

        public Func<ISkinSource?>? SkinSourceProvider { get; set; }

        private readonly AudioManager audio;

        public RenderGameplayHitsoundRenderer(AudioManager audio, AudioMixer? preferredMixer = null)
        {
            this.audio = audio;
            // Always capture/play hitsounds on the main SampleMixer.
            // We force Audio.VolumeSample = 1 early in ReplayRenderGame, so this path will always
            // have full volume for hitsounds, even if the client had the volume slider at 0.
            // The remove adjustments below strips any 0-gain that leaked from pre-render skin stores.
            CaptureMixer = audio.SampleMixer;
        }

        public void PlaySamples(ISampleInfo[] samples, double balance = 0, int minimumSampleVolume = 0)
        {
            if (samples.Length == 0)
                return;

            // Force full volume right before playing. This is the nuclear option to ensure
            // hitsounds are audible in the render even if the client's volume was 0 when
            // the render process started or some adjustment state was initialized at 0.
            if (audio != null)
            {
                audio.VolumeSample.Value = 1;
                audio.Volume.Value = 1;
            }

            ISkinSource? skinSource = SkinSourceProvider?.Invoke();

            if (skinSource == null)
                return;

            foreach (ISampleInfo sampleInfo in samples)
            {
                var sample = skinSource.GetSample(sampleInfo);

                if (sample == null)
                    continue;

                var channel = sample.GetChannel();

                // Remove any volume adjustments that may come from the user's global VolumeSample (which
                // can be 0). This ensures hitsounds always play at full intended level in renders,
                // regardless of the client's volume setting. We set our own volume right after.
                channel.RemoveAllAdjustments(AdjustableProperty.Volume);

                CaptureMixer.Add(channel);
                channel.Volume.Value = Math.Max(sampleInfo.Volume, minimumSampleVolume) / 100.0;
                channel.Balance.Value = balance;
                channel.Frequency.Value = PlaybackFrequency;
                channel.Tempo.Value = PlaybackTempo;
                channel.Play();
            }
        }

        public void Dispose()
        {
        }
    }
}
