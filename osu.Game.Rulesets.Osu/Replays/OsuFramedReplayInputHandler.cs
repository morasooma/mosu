// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Osu.Replays
{
    public class OsuFramedReplayInputHandler : FramedReplayInputHandler<OsuReplayFrame>
    {
        private readonly IBindable<bool>? relaxEnabled;
        private readonly bool ignoreReplayActionsWhenRelaxEnabled;
        private readonly bool legacyAutomaticJudgementsBetweenFrames;
        private OsuReplayFrame? lastButtonFrame;
        public int StableButtons { get; private set; }
        public int StablePreviousButtons { get; private set; }
        public int StablePreviousButtons2 { get; private set; }
        public int StableNewButtons { get; private set; }

        public bool IsRecordedGameplayFrame => !UsesRecordedFrameTimes || CurrentFrame == null
            || NextFrame == null || CurrentTime == CurrentFrame.Time;

        public OsuFramedReplayInputHandler(Replay replay, IBindable<bool>? relaxEnabled = null, bool ignoreReplayActionsWhenRelaxEnabled = false)
            : base(replay)
        {
            this.relaxEnabled = relaxEnabled;
            this.ignoreReplayActionsWhenRelaxEnabled = ignoreReplayActionsWhenRelaxEnabled;
            // danser-go's pre-20190506 handling advances automatic judgements between
            // recorded frames, without advancing slider tracking or input there.
            legacyAutomaticJudgementsBetweenFrames = replay.LegacyClientVersion is > 0 and < 20190506;
        }

        public override bool UsesRecordedFrameTimes => osu.Game.Online.MosuServerEnvironment.UsesStableProtocol;

        protected override bool IsImportant(OsuReplayFrame frame) => frame.Actions.Any();

        public override double? SetFrameFromTime(double time)
        {
            // Null would pause the parent clock as if waiting for network frames.
            if (UsesRecordedFrameTimes && CurrentFrame != null && NextFrame != null && time >= CurrentTime)
            {
                if (legacyAutomaticJudgementsBetweenFrames)
                    time = System.Math.Min(time, CurrentTime + 1);
                else if (time < NextFrame.Time)
                    time = CurrentFrame.Time;
            }

            return base.SetFrameFromTime(time);
        }

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            if (CurrentFrame != lastButtonFrame)
            {
                int buttons = (CurrentFrame?.Actions.Contains(OsuAction.LeftButton) == true ? 1 : 0)
                              | (CurrentFrame?.Actions.Contains(OsuAction.RightButton) == true ? 2 : 0);
                StableNewButtons = buttons & ~StableButtons;
                if (buttons != StableButtons)
                {
                    StablePreviousButtons2 = StablePreviousButtons;
                    StablePreviousButtons = StableButtons;
                    StableButtons = buttons;
                }
                lastButtonFrame = CurrentFrame;
            }

            var position = UsesRecordedFrameTimes && !IsRecordedGameplayFrame
                ? StartFrame.Position
                : Interpolation.ValueAt(CurrentTime, StartFrame.Position, EndFrame.Position, StartFrame.Time, EndFrame.Time);

            inputs.Add(new OsuInputManager.ReplayCursorPositionInput { Position = GamefieldToScreenSpace(position) });
            inputs.Add(new ReplayState<OsuAction>
            {
                PressedActions = shouldIgnoreReplayActions()
                    ? new List<OsuAction>()
                    : CurrentFrame?.Actions ?? new List<OsuAction>()
            });
        }

        private bool shouldIgnoreReplayActions()
            => ignoreReplayActionsWhenRelaxEnabled && relaxEnabled?.Value == true;
    }
}
