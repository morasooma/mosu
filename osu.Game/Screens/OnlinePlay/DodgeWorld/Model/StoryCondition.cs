// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// What has to already be true about a player's story for something to exist for them.
    /// </summary>
    /// <remarks>
    /// One flag and a range, not an expression language. A range covers every gate that is actually needed
    /// — before, after, and between — while staying something the editor can show as two fields and the
    /// server can check without interpreting anything.
    /// <para>
    /// A flag nobody has ever set reads as zero rather than as an error, which is what makes a story safe
    /// to rewrite: a condition added today is merely unmet for players from yesterday, and a flag deleted
    /// from the world stops gating anything instead of hiding the object forever.
    /// </para>
    /// </remarks>
    internal readonly record struct StoryCondition(string? Flag, long AtLeast, long? Below)
    {
        public static readonly StoryCondition NONE = new StoryCondition(null, 0, null);

        /// <summary>Whether this condition gates anything at all.</summary>
        public bool IsSet => !string.IsNullOrEmpty(Flag);

        public bool Matches(IReadOnlyDictionary<string, long> flags)
        {
            if (!IsSet)
                return true;

            long value = flags.TryGetValue(Flag!, out long stored) ? stored : 0;

            return value >= AtLeast && (Below == null || value < Below.Value);
        }

        /// <summary>
        /// Reads a condition from a stored record, defaulting the floor to one so that naming a flag alone
        /// means the obvious thing: "once this has happened".
        /// </summary>
        public static StoryCondition Read(string? flag, long? atLeast, long? below) =>
            string.IsNullOrWhiteSpace(flag)
                ? NONE
                : new StoryCondition(flag.Trim(), atLeast ?? 1, below);

        public override string ToString() => !IsSet
            ? "—"
            : Below == null
                ? $"{Flag} ≥ {AtLeast}"
                : $"{AtLeast} ≤ {Flag} < {Below}";
    }
}
