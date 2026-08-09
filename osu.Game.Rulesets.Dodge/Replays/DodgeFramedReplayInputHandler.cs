// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Replays
{
    public class DodgeFramedReplayInputHandler : FramedReplayInputHandler<DodgeReplayFrame>
    {
        public DodgeFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(DodgeReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            Vector2 position = Interpolation.ValueAt(CurrentTime, StartFrame.Position, EndFrame.Position, StartFrame.Time, EndFrame.Time);

            inputs.Add(new DodgeReplayState
            {
                PressedActions = CurrentFrame?.Actions ?? new List<DodgeAction>(),
                PlayerPosition = position,
            });
        }

        public class DodgeReplayState : ReplayInputHandler.ReplayState<DodgeAction>
        {
            public Vector2? PlayerPosition { get; set; }
        }
    }
}
