// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Configuration;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Replays;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Dodge.UI
{
    [Cached]
    public partial class DrawableDodgeRuleset : DrawableRuleset<DodgeHitObject>
    {
        protected new DodgeRulesetConfigManager Config => (DodgeRulesetConfigManager)base.Config;

        public DrawableDodgeRuleset(DodgeRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        public new DodgePlayfield Playfield => (DodgePlayfield)base.Playfield;

        public override double GameplayStartTime => DodgeGameplayTiming.GetGameplayStartTime(Beatmap.HitObjects);

        protected override Playfield CreatePlayfield() => new DodgePlayfield(
            Beatmap.HitObjects,
            playerSpeed: (float)(DodgeBeatmapSettings.GetPlayerSpeed(Beatmap.Difficulty) / 1000),
            playerSize: DodgeBeatmapSettings.GetPlayerSize(Beatmap.Difficulty),
            grazeDistance: (float)DodgeBeatmapSettings.GetGrazeDistance(Beatmap.Difficulty),
            grazeScore: DodgeBeatmapSettings.GetGrazeScore(Beatmap.Difficulty),
            showFullProjectilePaths: Mods.Any(mod => mod is DodgeModFullPaths),
            playfieldDim: Config.Get<double>(DodgeRulesetSetting.PlayfieldDim),
            grazeIndicatorBrightness: Config.Get<double>(DodgeRulesetSetting.GrazeIndicatorBrightness),
            missSoundEnabled: Config.Get<bool>(DodgeRulesetSetting.MissSoundEnabled),
            missSoundVolume: Config.Get<double>(DodgeRulesetSetting.MissSoundVolume));

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new DodgePlayfieldAdjustmentContainer();

        public override DrawableHitObject<DodgeHitObject> CreateDrawableRepresentation(DodgeHitObject h)
            => h switch
            {
                DodgeBullet => new DrawableDodgeHitObject(h),
                DodgeEmitter emitter => new DrawableDodgeEmitter(emitter),
                DodgeArenaChange arenaChange => new DrawableDodgeArenaChange(arenaChange),
                DodgeCameraChange cameraChange => new DrawableDodgeCameraChange(cameraChange),
                DodgeBeam beam => new DrawableDodgeBeam(beam),
                _ => throw new System.ArgumentException($"Unsupported Dodge object type: {h.GetType().Name}", nameof(h)),
            };

        protected override PassThroughInputManager CreateInputManager() => new DodgeInputManager(Ruleset?.RulesetInfo!);

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new DodgeFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new DodgeReplayRecorder(score, Playfield);
    }
}
