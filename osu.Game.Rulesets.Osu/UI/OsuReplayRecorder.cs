// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class OsuReplayRecorder : ReplayRecorder<OsuAction>
    {
        public OsuReplayRecorder(Score score)
            : base(score)
        {
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (GetContainingInputManager() is OsuInputManager inputManager && inputManager.ShouldSuppressPhysicalCursorMove)
                return false;

            return base.OnMouseMove(e);
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<OsuAction> actions, ReplayFrame previousFrame)
            => new OsuReplayFrame(Time.Current, mousePosition, actions.ToArray());
    }
}
