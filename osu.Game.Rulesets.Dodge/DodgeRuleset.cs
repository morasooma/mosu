// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Configuration;
using osu.Game.Rulesets.Dodge.Difficulty;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Edit.Design;
using osu.Game.Rulesets.Dodge.Edit.Setup;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Dodge.Scoring;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Setup;
using osu.Game.Screens.Edit.Design;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Dodge
{
    public partial class DodgeRuleset : Ruleset, ILegacyRuleset, ICustomEditorBeatmapStateHandler, ICustomBeatmapFormat, IEditorDesignScreenProvider, IBeatmapVisualOverrideProvider, IDodgeMultiplayerRuleset
    {
        public const int ONLINE_ID = 10;

        public int LegacyID => ONLINE_ID;

        public override string Description => "Dodge";

        public override string ShortName => RulesetInfo.DODGE_MODE_SHORTNAME;

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new DrawableDodgeRuleset(this, beatmap, mods);

        public DodgeMultiplayerGameplayController CreateDodgeMultiplayerGameplayController(
            DrawableRuleset drawableRuleset,
            DodgeMultiplayerGameplayConfiguration configuration)
            => new DodgeMultiplayerController((DrawableDodgeRuleset)drawableRuleset, configuration);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
            => new DodgeBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
            => new DodgeDifficultyCalculator(RulesetInfo, beatmap);

        public override PerformanceCalculator CreatePerformanceCalculator()
            => new DodgePerformanceCalculator();

        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap)
            => new DodgeBeatmapProcessor(beatmap);

        public override ScoreProcessor CreateScoreProcessor() => new DodgeScoreProcessor(this);

        public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context)
            => new DodgeScoreMultiplierCalculator(context);

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings)
            => new DodgeRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings() => new DodgeSettingsSubsection(this);

        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            switch (type)
            {
                case ModType.Mosu:
                    return new Mod[]
                    {
                        new DodgeModMosuStaticBpm(),
                        new DodgeModMosuTargetDifficulty(),
                        new DodgeModAudioEffects(),
                        new DodgeModFullPaths(),
                    };

                case ModType.DifficultyReduction:
                    return new Mod[]
                    {
                        new DodgeModNoFail(),
                        new MultiMod(new DodgeModHalfTime(), new DodgeModDaycore()),
                    };

                case ModType.DifficultyIncrease:
                    return new Mod[]
                    {
                        new MultiMod(new DodgeModSuddenDeath(), new DodgeModPerfect()),
                        new MultiMod(new DodgeModDoubleTime(), new DodgeModNightcore()),
                    };

                case ModType.Automation:
                    return new Mod[]
                    {
                        new MultiMod(new DodgeModAutoplay(), new DodgeModCinema()),
                    };

                case ModType.Fun:
                    return new Mod[]
                    {
                        new MultiMod(new ModWindUp(), new ModWindDown()),
                        new ModAdaptiveSpeed(),
                    };

                case ModType.System:
                    return new Mod[]
                    {
                        new ModScoreV2(),
                    };

                default:
                    return Array.Empty<Mod>();
            }
        }

        public override IEnumerable<HitResult> GetValidHitResults() => new[] { HitResult.Perfect, HitResult.SmallBonus };

        public override LocalisableString GetDisplayNameForHitResult(HitResult result) => result switch
        {
            HitResult.Perfect => "DODGED",
            HitResult.SmallBonus => "GRAZE",
            HitResult.Miss => "HIT",
            _ => base.GetDisplayNameForHitResult(result),
        };

        public override HitObjectComposer CreateHitObjectComposer() => new DodgeHitObjectComposer(this);

        public override IEnumerable<Drawable> CreateEditorSetupSections()
            => base.CreateEditorSetupSections().Select(section => section is DifficultySection ? new DodgeDifficultySection() : section);

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0)
        {
            if (variant == EDITOR_VARIANT)
            {
                return new[]
                {
                    new KeyBinding(InputKey.Number2, DodgeAction.EditorBulletTool),
                    new KeyBinding(InputKey.Number3, DodgeAction.EditorArenaChangeTool),
                    new KeyBinding(InputKey.Number4, DodgeAction.EditorEmitterTool),
                    new KeyBinding(InputKey.Number5, DodgeAction.EditorBeamTool),
                    new KeyBinding(InputKey.Number6, DodgeAction.EditorCameraChangeTool),
                    new KeyBinding(InputKey.Number7, DodgeAction.EditorTriggerTool),
                };
            }

            return new[]
            {
                new KeyBinding(InputKey.A, DodgeAction.MoveLeft),
                new KeyBinding(InputKey.Left, DodgeAction.MoveLeft),
                new KeyBinding(InputKey.D, DodgeAction.MoveRight),
                new KeyBinding(InputKey.Right, DodgeAction.MoveRight),
                new KeyBinding(InputKey.W, DodgeAction.MoveUp),
                new KeyBinding(InputKey.Up, DodgeAction.MoveUp),
                new KeyBinding(InputKey.S, DodgeAction.MoveDown),
                new KeyBinding(InputKey.Down, DodgeAction.MoveDown),
                new KeyBinding(InputKey.Shift, DodgeAction.Slow),
                new KeyBinding(InputKey.H, DodgeAction.ToggleHud),
            };
        }

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.RulesetDodge,
        };

        public override ISkin CreateSkinTransformer(ISkin skin, IBeatmap beatmap)
            => new DodgeSkinTransformer(skin);

        public ILegacyScoreSimulator CreateLegacyScoreSimulator() => new DodgeLegacyScoreSimulator();

        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => new DodgeReplayFrame();

        public void WriteEditorState(EditorBeatmap beatmap, System.IO.Stream stream)
            => DodgeBeatmapSerializer.Serialize(beatmap, beatmap.Storyboard, stream);

        public void ApplyEditorState(EditorBeatmap beatmap, byte[] previousState, byte[] newState)
            => DodgeBeatmapSerializer.ApplyToEditor(beatmap, newState);

        public void EncodeCompatibilityBeatmap(IBeatmap beatmap, osu.Game.Skinning.ISkin? skin, osu.Game.Storyboards.Storyboard? storyboard, System.IO.Stream stream)
            => DodgeBeatmapSerializer.EncodeCompatibilityBeatmap(beatmap, skin, storyboard, stream);

        public void EncodeSidecar(IBeatmap beatmap, ISkin? skin, osu.Game.Storyboards.Storyboard? storyboard, System.IO.Stream stream)
            => DodgeBeatmapSerializer.Serialize(beatmap, storyboard, stream);

        public IBeatmap DecodeSidecar(IBeatmap compatibilityBeatmap, System.IO.Stream stream)
            => DodgeBeatmapSerializer.DecodeCompatibilityBeatmap(compatibilityBeatmap, stream);

        public void CopyCustomDifficultySettings(BeatmapDifficulty source, BeatmapDifficulty target)
        {
            DodgeBeatmapSettings.SetPlayerSize(target, DodgeBeatmapSettings.GetPlayerSize(source));
            DodgeBeatmapSettings.SetForceStoryboard(target, DodgeBeatmapSettings.GetForceStoryboard(source));
            DodgeBeatmapSettings.SetForceBeatmapSkin(target, DodgeBeatmapSettings.GetForceBeatmapSkin(source));
        }

        public EditorScreen CreateDesignScreen() => new DodgeDesignScreen();

        public bool ForceStoryboard(IBeatmap beatmap) => DodgeBeatmapSettings.GetForceStoryboard(beatmap.Difficulty);

        public bool ForceBeatmapSkin(IBeatmap beatmap) => DodgeBeatmapSettings.GetForceBeatmapSkin(beatmap.Difficulty);

        public LocalisableString VisualOverrideNotice(IBeatmap beatmap)
            => DodgeEditorStrings.ForcedVisualsNotice;
    }
}
