// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.Catch.Replays
{
    public class CatchFramedReplayInputHandler : FramedReplayInputHandler<CatchReplayFrame>
    {
        private readonly bool fruitNinja;

        public CatchFramedReplayInputHandler(Replay replay, bool fruitNinja = false)
            : base(replay)
        {
            this.fruitNinja = fruitNinja;
        }

        public override bool UsesRecordedFrameTimes => fruitNinja;

        protected override bool IsImportant(CatchReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            float position = Interpolation.ValueAt(CurrentTime, StartFrame.Position, EndFrame.Position, StartFrame.Time, EndFrame.Time);

            if (fruitNinja && StartFrame.BladeY is float startY && EndFrame.BladeY is float endY)
            {
                float y = Interpolation.ValueAt(CurrentTime, startY, endY, StartFrame.Time, EndFrame.Time);
                inputs.Add(new MousePositionAbsoluteInput { Position = GamefieldToScreenSpace(new Vector2(position, y)) });
            }

            inputs.Add(new CatchReplayState
            {
                PressedActions = CurrentFrame?.Actions ?? new List<CatchAction>(),
                CatcherX = position
            });
        }

        public class CatchReplayState : ReplayState<CatchAction>
        {
            public float? CatcherX { get; set; }
        }
    }
}
