// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The sound of the local player being hurt: the same one a Dodge map plays on a collision.
    /// </summary>
    /// <remarks>
    /// One hit can be reported twice — the local simulation resolves it, and a room the server runs says
    /// so too — so a second play within <see cref="minimum_gap"/> is dropped.
    /// <para>
    /// The volume is fixed because the ruleset's miss-sound settings live in the Dodge ruleset's own
    /// configuration, which the game cannot see into.
    /// </para>
    /// </remarks>
    internal partial class HurtSound : CompositeDrawable
    {
        /// <summary>
        /// Matching the Dodge ruleset's default miss-sound volume, so the same hit is as loud in either place.
        /// </summary>
        private const double volume = 0.6;

        private const double minimum_gap = 70;

        private readonly SkinnableSound sample;

        private double? lastPlayed;

        public HurtSound()
        {
            InternalChild = sample = new SkinnableSound(DodgeMissSampleInfo.Default)
            {
                Volume = { Value = volume },
            };
        }

        public void Play()
        {
            if (lastPlayed != null && Time.Current - lastPlayed < minimum_gap)
                return;

            lastPlayed = Time.Current;
            PlayCountForTesting++;
            sample.Play();
        }

        /// <summary>How many times a hit has been sounded, for tests that cannot listen.</summary>
        internal int PlayCountForTesting { get; private set; }
    }
}
