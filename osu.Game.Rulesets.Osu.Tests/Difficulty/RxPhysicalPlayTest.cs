// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty;

[TestFixture]
public class RxPhysicalPlayTest
{
    private static Beatmap<OsuHitObject> map(double rate)
    {
        var result = new Beatmap<OsuHitObject> { StackLeniency = 0, BeatmapVersion = 14,
            Difficulty = new BeatmapDifficulty { CircleSize = 4, ApproachRate = 9, OverallDifficulty = 8 } };
        result.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 300 / rate });
        result.HitObjects.Add(new HitCircle { StartTime = 1200 / rate, Position = new Vector2(100, 150) });
        result.HitObjects.Add(new Slider { StartTime = 1500 / rate, Position = new Vector2(200, 250),
            Path = new osu.Game.Rulesets.Objects.SliderPath(new[] {
                new osu.Game.Rulesets.Objects.PathControlPoint(Vector2.Zero),
                new osu.Game.Rulesets.Objects.PathControlPoint(new Vector2(150, 20)) }, 150), RepeatCount = 1 });
        result.HitObjects.Add(new Spinner { StartTime = 2400 / rate, Duration = 600 / rate });
        foreach (var obj in result.HitObjects) obj.ApplyDefaults(result.ControlPointInfo, result.Difficulty);
        return result;
    }

    [TestCase(0.75)]
    [TestCase(1.3)]
    [TestCase(1.5)]
    [TestCase(2)]
    public void LockedRateSnapshotEqualsEditedMapWithoutMutatingSource(double rate)
    {
        var source = map(1);
        ModRateAdjust mod = rate < 1 ? new OsuModHalfTime() : new OsuModDoubleTime();
        mod.SpeedChange.Value = rate; mod.LockDifficultyAdjust.Value = true;
        var actual = RxPhysicalPlay.Create(source, new Mod[] { mod });
        var expected = map(rate);
        Assert.Multiple(() =>
        {
            Assert.That(actual.Difficulty.ApproachRate, Is.EqualTo(9));
            Assert.That(actual.Difficulty.OverallDifficulty, Is.EqualTo(8));
            Assert.That(actual.HitObjects.Select(x => x.StartTime), Is.EqualTo(expected.HitObjects.Select(x => x.StartTime)));
            Assert.That(((Slider)actual.HitObjects[1]).Duration, Is.EqualTo(((Slider)expected.HitObjects[1]).Duration).Within(1e-8));
            Assert.That(((Spinner)actual.HitObjects[2]).Duration, Is.EqualTo(600 / rate).Within(1e-8));
            Assert.That(source.HitObjects[0].StartTime, Is.EqualTo(1200));
            Assert.That(source.ControlPointInfo.TimingPoints[0].BeatLength, Is.EqualTo(300));
            Assert.That(actual.BeatmapInfo.Difficulty, Is.Not.SameAs(source.BeatmapInfo.Difficulty));
        });
    }

    [Test]
    public void UnlockedSnapshotUsesActualApproachTimeAndHitWindow()
    {
        var actual = RxPhysicalPlay.Create(map(1), new Mod[] { new OsuModDoubleTime() });
        Assert.That(actual.Difficulty.ApproachRate, Is.EqualTo(RxLockedDifficulty.OnScreenAr(9, 1.5)).Within(1e-6));
        Assert.That(actual.Difficulty.OverallDifficulty, Is.EqualTo((80 - (80 - 6 * 8) / 1.5) / 6).Within(1e-6));
    }

    [Test]
    public void DifficultyTagsAreBakedInButReadingAndScoreModsRemain()
    {
        var mods = RxPhysicalPlay.Mods(new Mod[] { new OsuModDoubleTime(), new OsuModHardRock(),
            new OsuModDifficultyAdjust(), new ModWindDown(), new OsuModHidden(), new OsuModFlashlight(), new OsuModRelax(), new OsuModNoFail() });
        Assert.That(mods.Select(m => m.Acronym), Is.EqualTo(new[] { "HD", "FL", "RX", "NF" }));
    }
}
