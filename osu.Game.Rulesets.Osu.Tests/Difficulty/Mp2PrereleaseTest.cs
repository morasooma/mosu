// Adapted from taziksfear’s MosuPP prerelease tests (PR #8, a008636b).

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPp2;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;

[TestFixture]
[NonParallelizable]
public class Mp2PrereleaseTest : DifficultyCalculatorTest
{
    protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";
    private ForkRelaxPpSystem previous;

    [SetUp]
    public void SelectPrerelease()
    {
        previous = RelaxPpSystemSelection.Current;
        RelaxPpSystemSelection.Current = ForkRelaxPpSystem.MosuPpPrerelease;
    }

    [TearDown]
    public void RestoreSystem() => RelaxPpSystemSelection.Current = previous;

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void MatchesLaboratoryDraft(int modCase)
    {
        IWorkingBeatmap working = GetBeatmap("801165");
        Mod[] mods = modCase switch
        {
            1 => new Mod[] { new OsuModRelax(), new OsuModDoubleTime() },
            2 => new Mod[] { new OsuModMosuRelax(), new OsuModDoubleTime { LockDifficultyAdjust = { Value = true } },
                            new OsuModDifficultyAdjust { CircleSize = { Value = 2 }, ApproachRate = { Value = 8 } } },
            3 => new Mod[] { new OsuModRelax(), new OsuModHardRock(), new OsuModHidden() },
            _ => new Mod[] { new OsuModRelax() },
        };
        var attributes = (OsuDifficultyAttributes)new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).Calculate(mods);
        IBeatmap playable = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
        var features = Mp2Features.Extract(playable, mods);
        Mod[] rxOnly = mods.Where(m => m is OsuModRelax or OsuModMosuRelax).ToArray();
        var reference = Mp2Features.Extract(working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, rxOnly), rxOnly);
        var model = Mp2Model.Evaluate(features, Mp2Parameters.Default, noMod: reference);
        var score = perfect(working, attributes, mods);
        var calculator = new OsuPerformanceCalculator();
        Assert.That(calculator.RequiresBackgroundLivePerformanceCalculation(score), Is.True);
        Assert.That(calculator.Calculate(score, attributes).Total, Is.EqualTo(model.PpSs));
        score.Statistics[HitResult.Great] -= 3;
        score.Statistics[HitResult.Ok] = 2;
        score.Statistics[HitResult.Miss] = 1;
        score.Accuracy = .98;
        score.MaxCombo /= 2;
        Assert.That(calculator.Calculate(score, attributes).Total,
            Is.EqualTo(Mp2Model.Performance(model, Mp2Parameters.Default, .98, 1)).Within(1e-8));

        // Complete head-miss evidence uses the original draft's note-load rule.
        var notes = playable.HitObjects.OfType<OsuHitObject>().Where(n => n is not Spinner).ToArray();
        var missed = new bool[notes.Length];
        missed[5] = true;
        score.HitEvents.Add(new HitEvent(0, 1, HitResult.Miss, notes[5], null, null));
        Assert.That(calculator.Calculate(score, attributes).Total,
            Is.EqualTo(Mp2Model.Performance(model, Mp2Parameters.Default, .98, 1, Mp2Model.MissLoad(model, missed))).Within(1e-8));
        score.HitEvents.Add(score.HitEvents[0]);
        Assert.That(calculator.Calculate(score, attributes).Total,
            Is.EqualTo(Mp2Model.Performance(model, Mp2Parameters.Default, .98, 1)).Within(1e-8));
    }

    [Test]
    public void TimedPerformanceDoesNotIncludeFutureNotes()
    {
        IWorkingBeatmap working = GetBeatmap("801165");
        Mod[] mods = { new OsuModRelax() };
        var timed = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).CalculateTimed(mods);
        var attributes = (OsuDifficultyAttributes)timed[20].Attributes;
        int count = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;
        IBeatmap playable = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);
        var prefix = new Beatmap<HitObject>
        {
            BeatmapInfo = playable.BeatmapInfo.Clone(), Difficulty = playable.Difficulty.Clone(),
            ControlPointInfo = playable.ControlPointInfo, Breaks = playable.Breaks,
            HitObjects = playable.HitObjects.Take(count).ToList(),
        };
        var features = Mp2Features.Extract(prefix, mods);
        var expected = Mp2Model.Evaluate(features, Mp2Parameters.Default, noMod: features);
        Assert.That(new OsuPerformanceCalculator().Calculate(perfect(working, attributes, mods), attributes).Total,
            Is.EqualTo(expected.PpSs).Within(1e-8));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreservesDisplayedStarsAndNonRelaxPerformance(bool relax)
    {
        var working = GetBeatmap("801165");
        Mod[] mods = relax ? new Mod[] { new OsuModRelax(), new OsuModDoubleTime() } : new Mod[] { new OsuModDoubleTime() };
        var difficulty = CreateDifficultyCalculator(working);
        var calculator = new OsuPerformanceCalculator();
        var attributes = (OsuDifficultyAttributes)difficulty.Calculate(mods);
        var score = perfect(working, attributes, mods);
        double prerelease = calculator.Calculate(score, attributes).Total;

        RelaxPpSystemSelection.Current = ForkRelaxPpSystem.TazikPp;
        var stable = (OsuDifficultyAttributes)difficulty.Calculate(mods);
        Assert.That(attributes.StarRating, Is.EqualTo(stable.StarRating));
        double stablePp = calculator.Calculate(perfect(working, stable, mods), stable).Total;
        if (relax)
            Assert.That(prerelease, Is.Not.EqualTo(stablePp));
        else
            Assert.That(prerelease, Is.EqualTo(stablePp));
    }

    private static ScoreInfo perfect(IWorkingBeatmap working, OsuDifficultyAttributes attributes, Mod[] mods)
        => new ScoreInfo((BeatmapInfo)working.BeatmapInfo, new OsuRuleset().RulesetInfo)
        {
            Mods = mods, Accuracy = 1, MaxCombo = attributes.MaxCombo,
            Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount,
                [HitResult.SliderTailHit] = attributes.SliderCount,
            },
        };

    protected override Ruleset CreateRuleset() => new OsuRuleset();

    protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);
}
