using System;
using osu.Framework.Audio.Sample;

namespace osu.Game.Audio
{
    /// <summary>
    /// Keeps references held by loaded controls valid across sound-pack changes.
    /// Resolution is an in-memory lookup; channels already playing retain their source.
    /// </summary>
    public sealed class InterfaceSoundSample : Sample
    {
        private readonly Sample original;
        private readonly Func<Sample> resolve;

        public InterfaceSoundSample(Sample original, Func<Sample> resolve)
            : base(original.Name)
        {
            this.original = original;
            this.resolve = resolve;
            PlaybackConcurrency.BindTo(original.PlaybackConcurrency);
        }

        public override double Length => resolve().Length;
        public override bool IsLoaded => resolve().IsLoaded;

        protected override SampleChannel CreateChannel()
        {
            // The global store registers this wrapper on playback, preserving the usual
            // channel lifetime and adjustment propagation without polling idle controls.
            return resolve().GetChannel();
        }

        protected override void Dispose(bool disposing)
        {
            original.Dispose();
            base.Dispose(disposing);
        }
    }
}
