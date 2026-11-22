using System.Collections.Generic;
using osu.Framework.Input;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Catch.UI
{
    public partial class DrawableFruitNinjaRuleset : DrawableRuleset<CatchHitObject>
    {
        public DrawableFruitNinjaRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // Cursor/trail live in the same unscaled overlay space as normal gameplay overlays,
            // not in the 512-unit fruit arena. Keep their native screen size on every aspect ratio.
            var cursor = ((FruitNinjaPlayfield)Playfield).DetachCursor();
            cursor.RelativeSizeAxes = Axes.Both;
            // Legacy cursor sprites cancel standard's 1.6 playfield scale internally.
            // Restore that factor here, without inheriting this arena's unrelated scaling.
            Overlays.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Scale = new Vector2(LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                Size = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                Child = cursor,
            });
        }

        protected override Playfield CreatePlayfield() => new FruitNinjaPlayfield();

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new FruitNinjaPlayfieldAdjustmentContainer();

        protected override PassThroughInputManager CreateInputManager() => new CatchInputManager(Ruleset.RulesetInfo);

        public override DrawableHitObject<CatchHitObject>? CreateDrawableRepresentation(CatchHitObject h) => null;

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new CatchFramedReplayInputHandler(replay, fruitNinja: true);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new FruitNinjaReplayRecorder(score, (FruitNinjaPlayfield)Playfield);
    }
}
