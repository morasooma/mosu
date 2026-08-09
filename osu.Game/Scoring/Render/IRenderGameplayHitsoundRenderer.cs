// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio.Mixing;
using osu.Game.Audio;

namespace osu.Game.Scoring.Render
{
    internal interface IRenderGameplayHitsoundRenderer
    {
        AudioMixer? CaptureMixer { get; }

        double PlaybackFrequency { get; set; }

        double PlaybackTempo { get; set; }

        void PlaySamples(ISampleInfo[] samples, double balance = 0, int minimumSampleVolume = 0);
    }
}
