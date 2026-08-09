// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Dodge.UI
{
    public partial class DodgeReplayRecorder : ReplayRecorder<DodgeAction>
    {
        private readonly DodgePlayfield playfield;

        public DodgeReplayRecorder(Score score, DodgePlayfield playfield)
            : base(score)
        {
            this.playfield = playfield;
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<DodgeAction> actions, ReplayFrame previousFrame)
            => new DodgeReplayFrame(Time.Current, playfield.Player.Position, actions.ToArray());
    }
}
