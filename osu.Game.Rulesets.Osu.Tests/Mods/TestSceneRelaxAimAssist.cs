using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [HeadlessTest]
    public partial class TestSceneRelaxAimAssist : OsuModTestScene
    {
        [TestCase(MosuAimAssistAlgorithm.Mosu, false)]
        [TestCase(MosuAimAssistAlgorithm.MapleV2, false)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3, false)]
        [TestCase(MosuAimAssistAlgorithm.Mosu, true)]
        [TestCase(MosuAimAssistAlgorithm.MapleV2, true)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3, true)]
        public void TestRelaxAutomaticallyHitsWithDeclaredAimAssist(MosuAimAssistAlgorithm algorithm, bool mosuRelax)
        {
            var assist = new OsuModMosuAimAssist { Algorithm = { Value = algorithm } };
            Mod relax = mosuRelax ? new OsuModMosuRelax() : new OsuModRelax();
            Mod[] mods = { assist, relax };
            CreateModTest(new ModTestData
            {
                Autoplay = false,
                Mods = mods,
                CreateBeatmap = () => new OsuBeatmap
                {
                    HitObjects = { new HitCircle { StartTime = 2000, Position = new Vector2(256, 192) } },
                },
                PassCondition = () => Player.ScoreProcessor.Combo.Value == 1,
            });
            AddStep("disable config-only assist and relax", () =>
            {
                LocalConfig.SetValue(OsuSetting.ForkAimAssistEnabled, false);
                LocalConfig.SetValue(OsuSetting.ForkRelaxEnabled, false);
                LocalConfig.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
            });
            AddUntilStep("aim assist declared and enabled", () =>
                ((DrawableOsuRuleset)Player.DrawableRuleset).Playfield.AimAssistController.IsAimAssistDeclaredMod);
            AddStep("aim at note without pressing keys", () => InputManager.MoveMouseTo(
                ((DrawableOsuRuleset)Player.DrawableRuleset).Playfield.GamefieldToScreenSpace(new Vector2(276, 192))));
            AddUntilStep("Relax generated a successful hit", () => Player.ScoreProcessor.Combo.Value == 1);
            AddAssert("combination never awards PP", () => !ModPerformancePointHelper.ModsAwardPerformancePoints(
                new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty()), mods));
        }
    }
}
