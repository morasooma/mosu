// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.TagCoop
{
    /// <summary>
    /// Configuration shared between multiplayer and a ruleset-specific Tag Co-op implementation.
    /// </summary>
    public class TagCoopGameplayConfiguration
    {
        public required IReadOnlyList<int> PlayerOrder { get; init; }

        public required int LocalUserId { get; init; }

        public bool AutoplayLocalTurns { get; init; }

        public required IReadOnlyDictionary<int, Color4> PlayerColours { get; init; }

        public required Func<int, bool> IsPlayerPresent { get; init; }

        public required Action<int, HitResult> SendJudgement { get; init; }
    }

    /// <summary>
    /// Implemented by rulesets which can split gameplay by combo for Tag Co-op.
    /// </summary>
    public interface ITagCoopRuleset
    {
        TagCoopGameplayController CreateTagCoopGameplayController(DrawableRuleset drawableRuleset, TagCoopGameplayConfiguration configuration);

        IReadOnlyList<ReplayFrame> CreateTagCoopCompatibilityReplayFrames(DrawableRuleset drawableRuleset, TagCoopReplayMetadata metadata);
    }

    public abstract partial class TagCoopGameplayController : Drawable
    {
        public event Action<int, Vector2, HitResult>? RemoteJudgementFeedback;

        public abstract int? ActiveUserId { get; }

        public virtual int? NextUserId => null;

        public virtual double MillisecondsUntilNextTurn => double.PositiveInfinity;

        public virtual bool AllPlayersActive => false;

        public virtual bool AllPlayersNext => false;

        /// <summary>
        /// A gamefield-space cursor position supplied by a local debug bot.
        /// </summary>
        public virtual Vector2? AutomatedCursorPosition => null;

        /// <summary>
        /// Bit 0 is the primary button and bit 1 is the secondary button.
        /// </summary>
        public virtual byte LocalButtonState => 0;

        /// <summary>
        /// Converts a per-player Tag Co-op cursor sample into the single compatibility replay
        /// stream when that player owns gameplay at the sample time.
        /// </summary>
        public virtual ReplayFrame? CreateCompatibilityReplayFrame(TagCoopReplayFrame frame) => null;

        /// <summary>
        /// Builds the ordinary single-cursor replay track used by unmodified clients.
        /// </summary>
        public virtual IReadOnlyList<ReplayFrame> CreateCompatibilityReplayFrames(TagCoopReplayMetadata metadata)
            => metadata.Frames.DistinctBy(frame => (frame.UserID, frame.Sequence))
                       .OrderBy(frame => frame.GameplayTime)
                       .ThenBy(frame => frame.Sequence)
                       .Select(CreateCompatibilityReplayFrame)
                       .OfType<ReplayFrame>()
                       .ToArray();

        public abstract void ApplyRemoteJudgement(int userId, int objectIndex, HitResult result);

        protected void NotifyRemoteJudgementFeedback(int userId, Vector2 position, HitResult result)
            => RemoteJudgementFeedback?.Invoke(userId, position, result);
    }
}
