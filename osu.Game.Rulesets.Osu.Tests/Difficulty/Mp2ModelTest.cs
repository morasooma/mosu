// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    /// <summary>Physical behaviour of MosuPp 2 on synthetic maps (what must hold for any fitted parameters).</summary>
    [TestFixture]
    public class Mp2ModelTest
    {
        private static readonly Mp2Parameters parameters = Mp2Parameters.Default;

        [Test]
        public void TestStackedNotesAreWorthAlmostNothing()
        {
            double stacked = pp(map(Enumerable.Range(0, 400).Select(i => (i * 150.0, new Vector2(256, 192)))));
            double jumps = pp(jumpMap(400, 300));
            Assert.That(stacked, Is.LessThan(jumps / 50));
        }

        [TestCase(1.25)]
        [TestCase(1.5)]
        [TestCase(2.0)]
        public void TestFasterIsWorthMore(double rate)
        {
            var beatmap = jumpMap(300, 300);
            Assert.That(pp(beatmap, new OsuModDoubleTime { SpeedChange = { Value = rate } }), Is.GreaterThan(pp(beatmap)));
        }

        [Test]
        public void TestBiggerCirclesAreWorthLess()
        {
            var positions = jumpNotes(300, 300).ToList();
            Assert.That(pp(map(positions, circleSize: 2)), Is.LessThan(pp(map(positions, circleSize: 4))));
            Assert.That(pp(map(positions, circleSize: 4)), Is.LessThan(pp(map(positions, circleSize: 6))));
        }

        [Test]
        public void TestStraightStreamIsEasierThanJumpsAtTheSameRhythm()
        {
            var stream = map(Enumerable.Range(0, 400).Select(i => (i * 80.0, new Vector2(64 + i % 40 * 10, 192))));
            var jumps = map(Enumerable.Range(0, 400).Select(i => (i * 80.0, new Vector2(i % 2 == 0 ? 96 : 416, 192))));
            Assert.That(pp(stream), Is.LessThan(pp(jumps)));
        }

        [Test]
        public void TestChordDoesNotExplode()
        {
            var notes = jumpNotes(300, 300).ToList();
            // A 2B-style chord: a second note at the same time on the other side of the screen.
            var withChord = notes.Append((notes[150].Item1, new Vector2(500, 380))).OrderBy(n => n.Item1).ToList();
            // One far chord in 300 jumps: the move back from the corner is real; at most ≈ +10%.
            Assert.That(pp(map(withChord)), Is.LessThan(pp(map(notes)) * 1.12));
        }

        [Test]
        public void TestMoreMapNeverLowersDifficulty()
        {
            var notes = jumpNotes(200, 300).ToList();
            double end = notes.Last().Item1;
            var longer = notes.Concat(Enumerable.Range(1, 200).Select(i => (end + i * 400.0, new Vector2(200 + i % 2 * 60, 200)))).ToList();
            Assert.That(evaluate(map(longer)).Difficulty, Is.GreaterThanOrEqualTo(evaluate(map(notes)).Difficulty));
        }

        [Test]
        public void TestLengthHelpsButIsBounded()
        {
            double minute = pp(jumpMap(200, 300));
            double tenMinutes = pp(jumpMap(2000, 300));
            Assert.That(tenMinutes, Is.GreaterThan(minute));
            Assert.That(tenMinutes, Is.LessThan(minute * 4));
        }

        [Test]
        public void TestAccuracyAndMissesLowerPp()
        {
            var d = evaluate(jumpMap(300, 300));
            double ss = Mp2Model.Performance(d, parameters, 1, 0);
            Assert.That(Mp2Model.Performance(d, parameters, 0.97, 0), Is.LessThan(ss));
            Assert.That(Mp2Model.Performance(d, parameters, 1, 5), Is.LessThan(ss));
            Assert.That(ss, Is.EqualTo(d.PpSs).Within(1e-9));
        }

        /// <summary>A hand-made difficulty: level 1, given per-note moments with HardNotes consistent with them.</summary>
        private static Mp2Difficulty difficulty(double[] moments)
        {
            double level = 1;
            return new Mp2Difficulty
            {
                Difficulty = 1,
                Level = level,
                PpSs = 100,
                Moments = moments,
                HardNotes = moments.Sum(m => Math.Min(1, Math.Pow(m / level, 2))),
                Notes = moments.Length,
                SkillShares = new Dictionary<string, double>(),
            };
        }

        [Test]
        public void TestMissLoadSumsTheLoadOfMissedNotes()
        {
            // Five notes at the level: two hard (load 1), three in the middle (mix ~0.1 / 0.5 / 1).
            var d = difficulty(new[] { 1.5, 0.32, 0.71, 0.9, 1.2 });
            var loads = Mp2Model.NoteLoads(d);
            Assert.That(loads[0], Is.EqualTo(1).Within(1e-9));
            Assert.That(loads[1], Is.EqualTo(Math.Pow(0.32, 2)).Within(1e-9));
            var missed = new[] { false, true, true, false, true };
            Assert.That(Mp2Model.MissLoad(d, missed), Is.EqualTo(Math.Pow(0.32, 2) + Math.Pow(0.71, 2) + 1).Within(1e-9));
        }

        [Test]
        public void TestEasySpotMissesAreShielded()
        {
            // Four hard moments and many easy ones: two misses on the easy spots carry much less load than a flat count
            // of 2, so the score keeps more PP than the count-based rule would give.
            var moments = Enumerable.Range(0, 400).Select(i => i % 20 == 0 ? 1.4 : 0.3).ToArray();
            var d = difficulty(moments);
            var missed = new bool[moments.Length];
            var easySpots = Enumerable.Range(0, missed.Length).Where(i => moments[i] < 1).Take(2).ToList();
            foreach (int i in easySpots)
                missed[i] = true;

            double shielded = Mp2Model.Performance(d, parameters, 1, 2, Mp2Model.MissLoad(d, missed));
            double flat = Mp2Model.Performance(d, parameters, 1, 2);
            Assert.That(shielded, Is.GreaterThan(flat));

            // The same two misses when they are the hardest: full load, the shield is off.
            double hard = Mp2Model.Performance(d, parameters, 1, 2, 2);
            Assert.That(hard, Is.LessThan(shielded).Within(1e-9));
        }

        [Test]
        public void TestFullLoadMissesMatchTheFlatRule()
        {
            var d = difficulty(new[] { 2.0, 1.1, 0.9, 1.0 });
            double flat5 = Mp2Model.Performance(d, parameters, 0.98, 5);
            double loaded5 = Mp2Model.Performance(d, parameters, 0.98, 5, 5);
            Assert.That(loaded5, Is.EqualTo(flat5).Within(1e-9));
        }

        [Test]
        public void TestParametersRoundTrip()
        {
            var p = Mp2Parameters.Default.Clone();
            p.AgilityWeight = 0.123;
            Assert.That(Mp2Parameters.FromJson(p.ToJson()).AgilityWeight, Is.EqualTo(0.123));
            Assert.That(Mp2Parameters.FromVector(p.ToVector()).ToVector(), Is.EqualTo(p.ToVector()));
            Assert.That(Mp2Parameters.Names, Does.Contain("LazyRadius"));
            Assert.Throws<ArgumentException>(() => Mp2Parameters.FromJson("{\"NoSuchParameter\": 1}"));
        }

        [Test]
        public void TestClassifierSeesTheShape()
        {
            var stream = features(map(Enumerable.Range(0, 400).Select(i => (i * 80.0, new Vector2(64 + i % 40 * 10, 192 + (i / 40 % 2) * 40)))));
            var backAndForth = features(map(Enumerable.Range(0, 400).Select(i => (i * 300.0, new Vector2(i % 2 == 0 ? 96 : 416, 192)))));
            Assert.That(share(stream, Mp2NoteType.Stream), Is.GreaterThan(0.6));
            Assert.That(share(backAndForth, Mp2NoteType.JumpBackForth), Is.GreaterThan(0.8));
        }

        [Test]
        public void TestClassifierSeesControl()
        {
            // Fast zigzag = aim control; square jumps (90° turns) = jump control; back-and-forth jumps = neither.
            var zigzag = features(map(Enumerable.Range(0, 400).Select(i => (i * 80.0, new Vector2(64 + i % 12 * 30, 192 + (i % 2) * 80)))));
            var square = features(map(Enumerable.Range(0, 400).Select(i => (i * 300.0, new Vector2(i % 4 is 0 or 1 ? 96 : 416, i % 4 is 1 or 2 ? 64 : 320)))));
            var backAndForth = features(map(Enumerable.Range(0, 400).Select(i => (i * 300.0, new Vector2(i % 2 == 0 ? 96 : 416, 192)))));
            Assert.That(zigzag.Types.AimControl.Average(), Is.GreaterThan(0.5));
            Assert.That(square.Types.JumpControl.Average(), Is.GreaterThan(0.2)); // mid angle, half off for the exact repeat
            Assert.That(backAndForth.Types.JumpControl.Average(), Is.LessThan(0.05));
            Assert.That(backAndForth.Types.AimControl.Average(), Is.LessThan(0.05));
            foreach (var f in new[] { zigzag, square, backAndForth })
                Assert.That(f.Types.Control.Max(), Is.LessThanOrEqualTo(1 + 1e-9));
        }

        [Test]
        public void TestRateDoesNotChangeTheTypes()
        {
            var beatmap = jumpMap(200, 150);
            var nm = features(beatmap).Types;
            var dt = features(jumpMap(200, 150), new OsuModDoubleTime()).Types;
            for (int i = 0; i < nm.Membership.Length; i++)
            {
                Assert.That(dt.Membership[i], Is.EqualTo(nm.Membership[i]).Within(1e-9));
                Assert.That(dt.Control[i], Is.EqualTo(nm.Control[i]).Within(1e-9));
            }
        }

        [Test]
        public void TestAttributionSumsToDifficulty()
        {
            var d = Mp2Model.Evaluate(features(jumpMap(300, 250)), parameters, trace: true);
            Assert.That(d.Trace!.Contribution.Sum(), Is.EqualTo(d.Difficulty).Within(1e-9 * d.Difficulty + 1e-12));
            Assert.That(d.SkillShares.Values.Sum(), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void TestExplanationIsComplete()
        {
            var e = Mp2Explain.Explain(features(jumpMap(300, 250)), parameters);
            Assert.That(e.TypeShares.Values.Sum(), Is.EqualTo(1).Within(1e-9));
            Assert.That(e.MapType.Values.Sum(), Is.EqualTo(1).Within(1e-9));
            Assert.That(e.Sections.Sum(s => s.Share), Is.EqualTo(1).Within(0.02));
            Assert.That(e.Lines.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(e.Effects.Select(x => x.Name), Does.Contain("headroom"));
        }

        [Test]
        public void TestModGainCapOnlyOnEasyResults()
        {
            var p = parameters;
            // Easy result: a ×3 gain is capped below ModCapEasy; hard result: untouched; monotone; losses (big circles) untouched.
            Assert.That(Mp2Model.CapModGain(300, 100, p) / 100, Is.LessThanOrEqualTo(p.ModCapEasy + 1e-9));
            Assert.That(Mp2Model.CapModGain(6000, 2000, p), Is.EqualTo(6000).Within(1e-6));
            Assert.That(Mp2Model.CapModGain(80, 100, p), Is.EqualTo(80));
            double previous = 0;
            for (double pp = 100; pp <= 3000; pp += 10)
            {
                double capped = Mp2Model.CapModGain(pp, 100, p);
                Assert.That(capped, Is.GreaterThanOrEqualTo(previous));
                previous = capped;
            }
        }

        [Test]
        public void TestModGainCapSparesMapsThatAreNotEasy()
        {
            // 400 PP in Relax only is not an easy map: its DT keeps the whole gain.
            Assert.That(Mp2Model.CapModGain(1200, 400, parameters), Is.EqualTo(1200).Within(1e-6));
        }

        [Test]
        public void TestSustainedControlTiresTheHand()
        {
            // The structure, without the easy-map gate.
            var ungated = parameters.Clone();
            ungated.ControlStaminaEasyPp = 1;
            ungated.ControlStaminaHardPp = 2;
            var noStamina = parameters.Clone();
            noStamina.ControlStaminaBonus = 0;

            // Three minutes of zigzags without rest vs the same 8-second zigzag bursts with 40 s of rest between them.
            var sustained = features(map(zigzagNotes(2000, 0, 90)));
            var bursts = features(map(zigzagNotes(2000, 90, 90)));
            double gain(Mp2Features f, Mp2Parameters with) => Mp2Model.Evaluate(f, with).Difficulty / Mp2Model.Evaluate(f, noStamina).Difficulty;

            Assert.That(gain(sustained, ungated), Is.GreaterThan(1.05));
            Assert.That(gain(sustained, ungated), Is.LessThanOrEqualTo(1 + parameters.ControlStaminaBonus + 1e-9));
            Assert.That(gain(bursts, ungated), Is.LessThan(1.02));
            // Jumps are not control: no stamina.
            Assert.That(gain(features(jumpMap(2000, 150)), ungated), Is.EqualTo(1).Within(1e-6));

            // An easy zigzag marathon (no farm): below ControlStaminaEasyPp nothing.
            var easy = features(map(zigzagNotes(4000, 0, 200)));
            Assume.That(Mp2Model.Evaluate(easy, noStamina).PpSs, Is.LessThan(parameters.ControlStaminaEasyPp));
            Assert.That(gain(easy, parameters), Is.EqualTo(1).Within(1e-9));
        }

        [Test]
        public void TestPpCurveIsMonotone()
        {
            double previous = 0;
            for (double d = 0.5; d < 200; d *= 1.05)
            {
                double value = Mp2Model.PpOfDifficulty(d, parameters);
                Assert.That(value, Is.GreaterThan(previous));
                previous = value;
            }
        }

        private static Mp2Features features(IBeatmap beatmap, params Mod[] mods)
        {
            IReadOnlyList<Mod> all = mods.Append(new OsuModRelax()).ToArray();
            return Mp2Features.Extract(beatmap, all);
        }

        private static double share(Mp2Features f, Mp2NoteType type) => f.Types.Membership.Average(m => m[(int)type]);

        /// <summary>Jumps to deterministic pseudo-random spots (no repeating pattern), one every <paramref name="gap"/> ms.</summary>
        private static IEnumerable<(double, Vector2)> jumpNotes(int count, double gap)
        {
            var random = new Random(7);
            return Enumerable.Range(0, count).Select(i => (i * gap, new Vector2(40 + (float)random.NextDouble() * 432, 40 + (float)random.NextDouble() * 304))).ToList();
        }

        private static IBeatmap jumpMap(int count, double gap) => map(jumpNotes(count, gap));

        /// <summary>A zigzag run every <paramref name="gap"/> ms (≈ 0.9 diameters, alternating sides); with <paramref name="burst"/>
        /// notes, 40 s of rest follow every burst.</summary>
        private static IEnumerable<(double, Vector2)> zigzagNotes(int count, int burst, double gap)
        {
            double time = 0;
            for (int i = 0; i < count; i++)
            {
                if (burst > 0 && i > 0 && i % burst == 0)
                    time += 40000;
                yield return (time, new Vector2(100 + i % 16 * 20, 192 + (i % 2 == 0 ? -30 : 30) + i / 16 % 3 * 20));
                time += gap;
            }
        }

        private static IBeatmap map(IEnumerable<(double Time, Vector2 Position)> notes, float circleSize = 4)
        {
            var difficulty = new BeatmapDifficulty { CircleSize = circleSize, ApproachRate = 9, OverallDifficulty = 8 };
            var beatmap = new Beatmap<OsuHitObject> { Difficulty = difficulty };

            foreach (var (time, position) in notes)
            {
                var circle = new HitCircle { StartTime = time, Position = position };
                circle.ApplyDefaults(new ControlPointInfo(), difficulty);
                beatmap.HitObjects.Add(circle);
            }

            return beatmap;
        }

        private static Mp2Difficulty evaluate(IBeatmap beatmap, params Mod[] mods)
        {
            IReadOnlyList<Mod> all = mods.Append(new OsuModRelax()).ToArray();
            return Mp2Model.Evaluate(Mp2Features.Extract(beatmap, all), parameters);
        }

        private static double pp(IBeatmap beatmap, params Mod[] mods) => evaluate(beatmap, mods).PpSs;
    }
}
