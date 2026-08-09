// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Replays
{
    public class DodgeReplayFrame : ReplayFrame, IConvertibleReplayFrame
    {
        public Vector2 Position;
        public List<DodgeAction> Actions = new List<DodgeAction>();

        public DodgeReplayFrame()
        {
        }

        public DodgeReplayFrame(double time, Vector2 position, params DodgeAction[] actions)
            : base(time)
        {
            Position = position;
            Actions.AddRange(actions);
        }

        public void FromLegacy(LegacyReplayFrame currentFrame, IBeatmap beatmap, ReplayFrame? lastFrame = null)
        {
            Position = currentFrame.Position;

            if (currentFrame.MouseLeft1) Actions.Add(DodgeAction.MoveLeft);
            if (currentFrame.MouseRight1) Actions.Add(DodgeAction.MoveRight);
            if (currentFrame.MouseLeft2) Actions.Add(DodgeAction.MoveUp);
            if (currentFrame.MouseRight2) Actions.Add(DodgeAction.MoveDown);
            if (currentFrame.Smoke) Actions.Add(DodgeAction.Slow);
        }

        public LegacyReplayFrame ToLegacy(IBeatmap beatmap)
        {
            ReplayButtonState state = ReplayButtonState.None;

            if (Actions.Contains(DodgeAction.MoveLeft)) state |= ReplayButtonState.Left1;
            if (Actions.Contains(DodgeAction.MoveRight)) state |= ReplayButtonState.Right1;
            if (Actions.Contains(DodgeAction.MoveUp)) state |= ReplayButtonState.Left2;
            if (Actions.Contains(DodgeAction.MoveDown)) state |= ReplayButtonState.Right2;
            if (Actions.Contains(DodgeAction.Slow)) state |= ReplayButtonState.Smoke;

            return new LegacyReplayFrame(Time, Position.X, Position.Y, state);
        }

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is DodgeReplayFrame dodgeFrame
               && Time == dodgeFrame.Time
               && Position == dodgeFrame.Position
               && Actions.SequenceEqual(dodgeFrame.Actions);
    }
}
