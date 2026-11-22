// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneMosuRelaxGameplay : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private DrawableOsuRuleset ruleset = null!;
        private ManualClock clock = null!;
        private readonly List<JudgementResult> judgements = new List<JudgementResult>();
        private RelaxController relax => ruleset.Playfield.RelaxController;

        [TestCase(MosuRelaxPreset.Natural)]
        [TestCase(MosuRelaxPreset.Balanced)]
        [TestCase(MosuRelaxPreset.Reliable)]
        public void TestPresetPlaysJumpSequenceWithSmallTimingVariation(MosuRelaxPreset preset)
        {
            var mod = new OsuModMosuRelax { Preset = { Value = preset } };
            HitCircle[] notes = Enumerable.Range(0, 8).Select(i => new HitCircle
            {
                Position = new Vector2(i % 2 == 0 ? 128 : 384, 192),
                StartTime = 1000 + i * 240,
            }).ToArray();

            createScene(mod, 1, notes);
            playSequence(notes, 1);
        }

        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestReliablePlaysFastStreamWithoutLosingRhythm(double rate)
        {
            HitCircle[] notes = Enumerable.Range(0, 12).Select(i => new HitCircle
            {
                Position = new Vector2(100 + i % 6 * 55, i < 6 ? 150 : 230),
                StartTime = 1000 + i * 60 * rate,
            }).ToArray();

            createScene(new OsuModMosuRelax(), rate, notes);
            playSequence(notes, rate);
            AddAssert("стрим использует оба пальца", () => relax.InputEvents.Where(e => e.IsPress).Select(e => e.Action).Distinct().Count() == 2);
        }

        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestConfiguredTimingUsesRealMillisecondsAtDifferentRates(double rate)
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            var mod = new OsuModMosuRelax
            {
                BaseOffset = { Value = 12 },
                TimingVariance = { Value = 0 },
                DynamicDrift = { Value = 0 },
                SyncRadius = { Value = 0 },
            };

            createScene(mod, rate, note);
            AddStep("навести курсор", () => InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(note.StackedPosition)));
            AddUntilStep("тайминг учитывает скорость игры", () =>
                relax.TryGetTargetScheduledPressTime(note, out double time) && Math.Abs((time - 1000) / rate - 12) < 0.01);
            AddStep("время нажатия", () => clock.CurrentTime = 1000 + 12 * rate);
            AddAssert("попадание с заданным offset", () => judgements.Count == 1 && judgements[0].IsHit);
        }

        private void createScene(OsuModMosuRelax mod, double rate, params OsuHitObject[] notes)
        {
            AddStep("создать игровое поле с модом MRX", () =>
            {
                config.SetValue(OsuSetting.ForkRelaxEnabled, false);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
                config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
                judgements.Clear();
                clock = new ManualClock { CurrentTime = -2000, Rate = rate };
                var beatmap = new OsuBeatmap { Difficulty = new BeatmapDifficulty { OverallDifficulty = 8 } };
                foreach (OsuHitObject note in notes)
                {
                    note.ApplyDefaults(new ControlPointInfo(), beatmap.Difficulty);
                    beatmap.HitObjects.Add(note);
                }

                Child = new SkinProvidingContainer(new TrianglesSkin(null!))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = ruleset = new DrawableOsuRuleset(new OsuRuleset(), beatmap, new Mod[] { mod })
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = new FramedClock(clock),
                    },
                };
                ruleset.NewResult += judgements.Add;
            });
            AddUntilStep("MRX включён через мод", () => ruleset.IsLoaded && relax.IsLoaded && relax.IsDeclaredMod && relax.IsEnabled);
            AddStep("применить скорость мода до появления нот", () =>
            {
                ruleset.FrameStableClock.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Frequency, new BindableDouble(rate));
                clock.CurrentTime = 800;
            });
        }

        private void playSequence(HitCircle[] notes, double rate)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                HitCircle note = notes[i];
                int expectedHits = i + 1;
                double plannedTime = double.NaN;
                AddStep("раннее доведение к следующей ноте", () => clock.CurrentTime = note.StartTime - 35 * rate);
                AddStep("навести курсор игрока", () => InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(note.StackedPosition)));
                AddUntilStep("нота запланирована", () => relax.TryGetTargetLinkedPressTime(note, out plannedTime));
                AddAssert("нажатие остаётся рядом с ритмом", () => (plannedTime - note.StartTime) / rate, () => Is.InRange(-30, 40));
                AddStep("дойти до запланированного нажатия", () => clock.CurrentTime = Math.Ceiling(plannedTime) + 1);
                AddAssert("нота получила настоящее попадание", () => judgements.Count == expectedHits && judgements.All(r => r.IsHit));
                AddAssert("на ноту приходится один клик", () => relax.InputEvents.Count(e => e.IsPress) == expectedHits);
            }
        }
    }
}
