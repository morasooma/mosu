using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Catch.UI
{
    public partial class FruitNinjaReplayRecorder : ReplayRecorder<CatchAction>
    {
        private readonly FruitNinjaPlayfield playfield;

        public FruitNinjaReplayRecorder(Score score, FruitNinjaPlayfield playfield)
            : base(score)
        {
            this.playfield = playfield;
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<CatchAction> actions, ReplayFrame previousFrame)
            => new CatchReplayFrame(Time.Current, mousePosition.X,
                actions.Any(a => a is CatchAction.Dash or CatchAction.MoveLeft or CatchAction.MoveRight) || playfield.TouchActive)
            {
                BladeY = mousePosition.Y,
            };
    }
}
