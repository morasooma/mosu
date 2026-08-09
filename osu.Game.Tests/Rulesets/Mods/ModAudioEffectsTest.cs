// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Rulesets.Mods
{
    [TestFixture]
    public class ModAudioEffectsTest
    {
        [TestCase(-12)]
        [TestCase(-5)]
        [TestCase(0)]
        [TestCase(7)]
        [TestCase(12)]
        public void TestPitchDoesNotChangePlaybackRate(int semitones)
        {
            var track = new TrackVirtual(10_000);
            var mod = new OsuModAudioEffects
            {
                Pitch = { Value = semitones }
            };

            mod.ApplyToTrack(track);

            double expectedPitch = Math.Pow(2, semitones / 12.0);

            Assert.That(track.AggregateFrequency.Value, Is.EqualTo(expectedPitch).Within(1e-10));
            Assert.That(track.AggregateTempo.Value, Is.EqualTo(1 / expectedPitch).Within(1e-10));
            Assert.That(track.AggregateFrequency.Value * track.AggregateTempo.Value, Is.EqualTo(1).Within(1e-10));
        }
    }
}
