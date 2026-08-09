using System;
using osu.Framework.Screens;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play
{
    public partial class ReplayPeriodSelectorLoader : PlayerLoader
    {
        public readonly ScoreInfo Score;

        protected override bool UseHighPerformanceSession => false;

        public ReplayPeriodSelectorLoader(Score score, Action<double, double> onConfirm)
            : base(() => new osu.Game.Scoring.Render.ReplayPeriodSelectorScreen(score, onConfirm))
        {
            if (score.Replay == null)
                throw new ArgumentException($"{nameof(score)} must have a non-null {nameof(score.Replay)}.", nameof(score));

            Score = score.ScoreInfo;
            WindowShouldBeActiveForGameplayStart = false;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            Mods.Value = Score.Mods;
            Ruleset.Value = Score.Ruleset;

            base.OnEntering(e);
        }
    }
}
