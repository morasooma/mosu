// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    /// <summary>
    /// Adversarial MosuPp 2 cases: mods, edge cases and farm exploits (review/exploits.md). Unlike <see cref="Mp2ModelTest"/>,
    /// maps go through the production path (<see cref="WorkingBeatmap.GetPlayableBeatmap(IRulesetInfo, IReadOnlyList{Mod})"/>:
    /// conversion, mods, ApplyDefaults, stacking), so DA / HR / EZ / lock / stacking act exactly as in game.
    /// <list type="bullet">
    /// <item><c>Mp2Invariant</c>: structure — must hold for every parameter set (default, fitted from MOSUPP2_PARAMS, fit-range corners).</item>
    /// <item><c>Mp2FitBound</c>: magnitudes — must hold for the shipped (fitted) parameters; candidates for fit-time hinge limits.</item>
    /// <item><c>Mp2RealMaps</c>: the same invariants on real maps from the lab cache (inconclusive when the cache is missing).</item>
    /// </list>
    /// Written against the first skeleton ("fails now" in a summary = failed then); all pass since the exploit fixes F1–F8 of
    /// 24.10.2026 (lock, clamping, chord / slider move-time floors, real-time hard time, partial window overlap, short maps,
    /// tracking saturation, Wind Up / Down integrated).
    /// </summary>
    [TestFixture]
    public class Mp2AdversarialTest
    {
        // ================================================================ parameter sets

        /// <summary>Default, fitted (MOSUPP2_PARAMS) and corners of the plausible fit range.</summary>
        public static IEnumerable<TestCaseData> AllParameters()
        {
            yield return new TestCaseData(Mp2Parameters.Default).SetArgDisplayNames("default");

            if (fitted() is Mp2Parameters f)
                yield return new TestCaseData(f).SetArgDisplayNames("fitted");

            var low = Mp2Parameters.Default.Clone();
            low.LazyRadius = 0.2;
            low.WindowSlack = 0.3;
            low.CsExponent = 0.6;
            low.AgilityWeight = 0.15;
            low.LengthExponent = 0.2;
            low.RepetitionDiscount = 0.1;
            low.PpExponent = 1.6;
            yield return new TestCaseData(low).SetArgDisplayNames("corner-low");

            var high = Mp2Parameters.Default.Clone();
            high.LazyRadius = 0.8;
            high.WindowSlack = 1.5;
            high.CsExponent = 1.5;
            high.AgilityWeight = 0.6;
            high.TrackWeight = 2;
            high.LengthExponent = 0.45;
            high.RepetitionDiscount = 0.6;
            high.ReactionBonus = 0.3;
            high.PpExponent = 2.2;
            yield return new TestCaseData(high).SetArgDisplayNames("corner-high");
        }

        /// <summary>The parameters that will ship: fitted when MOSUPP2_PARAMS is set, else the defaults.</summary>
        public static IEnumerable<TestCaseData> ShippedParameters()
        {
            yield return fitted() is Mp2Parameters f
                ? new TestCaseData(f).SetArgDisplayNames("fitted")
                : new TestCaseData(Mp2Parameters.Default).SetArgDisplayNames("default");
        }

        private static Mp2Parameters? fitted()
        {
            string? path = Environment.GetEnvironmentVariable("MOSUPP2_PARAMS");
            return string.IsNullOrEmpty(path) || !File.Exists(path) ? null : Mp2Parameters.FromJson(File.ReadAllText(path));
        }

        // ================================================================ mods: rate, lock

        /// <summary>
        /// "Lock AR/OD" must be calculated exactly like the unlocked rate with DA giving the same on-screen AR / OD
        /// (RxLockedDifficulty.Ar / Od). Fails now: OsuModDoubleTime / HalfTime already stretch TimePreempt and the hit windows of
        /// the objects, and Mp2Features divides by 1 instead of the rate, so the lock is counted twice (DT 2.0 lock = −9%).
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestLockEqualsDifficultyAdjustEquivalent(Mp2Parameters p)
        {
            var map = jumpMap(300, 300);

            foreach (double rate in new[] { 0.75, 1.3, 1.5, 2.0 })
            {
                float ar = lockedAr(9, rate), od = lockedOd(8, rate);
                double locked = pp(map, p, rateMod(rate, true));
                double equivalent = pp(map, p, rateMod(rate), da(ar: ar, od: od));
                Assert.That(locked / equivalent, Is.EqualTo(1).Within(0.02), $"rate {rate}: lock {locked:F1}, DA AR {ar:F2} OD {od:F2} {equivalent:F1}");
            }
        }

        /// <summary>
        /// Features must describe what the player sees: approach time and 300 window in real ms = object values (map time) / rate,
        /// whatever the mod does internally. Fails now for DT / HT lock (counted twice) and for NC / DC lock (the game does not apply
        /// the lock for NC / DC at all — OsuModNightcore has no ApplyToHitObject — but Mp2Features treats it as locked).
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCase("DT", 1.5, true)]
        [TestCase("DT", 2.0, true)]
        [TestCase("HT", 0.75, true)]
        [TestCase("NC", 1.5, true)]
        [TestCase("DT", 1.5, false)]
        [TestCase("NC", 1.5, false)]
        public void TestFeaturesUseRealTimeApproachAndWindow(string acronym, double rate, bool locked)
        {
            ModRateAdjust mod = acronym switch
            {
                "NC" => new OsuModNightcore(),
                "HT" => new OsuModHalfTime(),
                _ => new OsuModDoubleTime(),
            };
            mod.SpeedChange.Value = rate;
            mod.LockDifficultyAdjust.Value = locked;

            Mod[] mods = { mod, new OsuModRelax() };
            var playable = playableOf(jumpMap(20, 300)(), mods);
            var features = Mp2Features.Extract(playable, mods);
            var first = playable.HitObjects.OfType<OsuHitObject>().First();

            Assert.That(features.Preempt[0], Is.EqualTo(first.TimePreempt / rate).Within(0.5));
            Assert.That(features.Window[0] * 1000, Is.EqualTo(first.HitWindows.WindowFor(HitResult.Great) / rate).Within(0.05));
        }

        /// <summary>
        /// PP never falls when the rate rises (1.00 → 2.00, 0.01 steps, unlocked and locked) and a 1% rate step changes PP by at most
        /// 3% (expected ≈ 1.9%). Fails now: the 1 s moment window counts whole notes, so on a regular rhythm all windows gain a note
        /// at the same rate — +20.6% for 1.20 → 1.21 on 200 BPM jumps, +6.7% on a stream, DT 1.49 → 1.51 = +5.7% on ganbare.
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestRateIsMonotoneAndContinuous(Mp2Parameters p)
        {
            foreach (var (name, map) in new[] { ("jumps", jumpMap(300, 300)), ("stream", fromObjects(stream(600, 75, 30))), ("sliders", sliderMap()) })
            {
                foreach (bool locked in new[] { false, true })
                {
                    double previous = pp(map, p, rateMod(1.01, locked));
                    for (int step = 102; step <= 200; step++)
                    {
                        double rate = step / 100.0;
                        double value = pp(map, p, rateMod(rate, locked));
                        Assert.That(value, Is.GreaterThanOrEqualTo(previous * 0.9999), $"{name} lock={locked}: PP falls at {rate:F2}");
                        // A 1% step is worth about 1.01^PpExponent; spare time shrinking on discrete moves, reaction (AR rises with the rate) and
                        // the PP curvature add a little.
                        Assert.That(value / previous, Is.LessThanOrEqualTo(Math.Pow(1.01, p.PpExponent + 1.5)), $"{name} lock={locked}: jump at {rate:F2}");
                        previous = value;
                    }
                }
            }
        }

        /// <summary>A map uploaded at 1.3× (times / 1.3, AR / OD raised to the same real values) is worth the same as DT 1.3 (±3%).
        /// Passes now (−1.3%: Relax leniency is 12 map ms, so 12 real ms on the edit and 9.2 on DT).</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestRateEditEqualsRateMod(Mp2Parameters p)
        {
            var notes = stream(600, 75, 30).Concat(jumpNotes(100, 300, start: 600 * 75 + 1000)).ToList();
            double dt = pp(fromObjects(notes, ar: 9, od: 8), p, rateMod(1.3));
            float ar = (float)((1950 - (1950 - 150 * 9) / 1.3) / 150);
            float od = (float)((80 - (80 - 6 * 8) / 1.3) / 6);
            double edit = pp(fromObjects(notes.Select(n => n with { Time = n.Time / 1.3 }), ar: ar, od: od, beatLength: 300 / 1.3), p);
            // Relax leniency is 12 map ms: 12 real ms on the edit, 9.2 on DT 1.3 (−1…−3%, more with a small WindowSlack).
            Assert.That(edit / dt, Is.EqualTo(1).Within(0.04));
        }

        /// <summary>
        /// Variable-rate mods must not be scored at their initial rate. Failed before: ModUtils.CalculateRateWithMods takes the rate at
        /// time 0, so Wind Down 1.5 → 1.0 gets full DT 1.5 PP (+55% over the average-rate value). Fixed: the features integrate the rate
        /// over time.
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestVariableRateIsNotScoredAtItsStart(Mp2Parameters p)
        {
            var map = jumpMap(300, 300);
            var windDown = new ModWindDown();
            windDown.InitialRate.Value = 1.5;
            windDown.FinalRate.Value = 1.0;
            // Integrated: the hardest part is played at up to 1.5, the rest slower, so between NM and full DT 1.5.
            double value = pp(map, p, windDown);
            Assert.That(value, Is.LessThan(pp(map, p, rateMod(1.5)) * 0.97));
            Assert.That(value, Is.GreaterThan(pp(map, p)));
        }

        // ================================================================ mods: CS, OD, AR, HR, HD, FL

        /// <summary>PP never falls with a smaller circle (DA CS 0 → 11) and never rises with a bigger one; on easy maps the CS gain is
        /// smaller than on hard maps (the design goal of the speed-dependent size factor). Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestCircleSizeIsMonotoneAndPaysOnHardMaps(Mp2Parameters p)
        {
            var hard = jumpMap(300, 300);
            var easy = fromObjects(spacedJumps(240, 500, 140));
            double previous = 0;

            for (int cs10 = 0; cs10 <= 110; cs10 += 5)
            {
                double value = pp(hard, p, da(cs: cs10 / 10f));
                Assert.That(value, Is.GreaterThanOrEqualTo(previous * 0.9999), $"CS {cs10 / 10.0}");
                Assert.That(double.IsFinite(value));
                previous = value;
            }

            double hardGain = pp(hard, p, da(cs: 8)) / pp(hard, p);
            double easyGain = pp(easy, p, da(cs: 8)) / pp(easy, p);
            Assert.That(easyGain, Is.LessThan(hardGain));
        }

        /// <summary>
        /// DA CS farm. Easy map (120 BPM 1/1 jumps, 140 px): CS 8 at most +35%, CS 10 at most +70%; hard jumps: CS 8 at most ×2.2,
        /// CS 11 at most ×4 (Fitts: halving the target adds about one bit). Fails now: easy CS 8 = 1.41, CS 11 on jumps = 5.4
        /// (real maps: easy set CS 8 = 1.7–2.4, CS 10 = 3.3–5.3, ganbare CS 11 = 22×) — the power law (D₄/D)^(CsExponent·u) does
        /// not saturate.
        /// </summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestCircleSizeFarmIsBounded(Mp2Parameters p)
        {
            var easy = fromObjects(spacedJumps(240, 500, 140));
            var hard = jumpMap(300, 300);
            Assert.That(pp(easy, p, da(cs: 8)) / pp(easy, p), Is.LessThanOrEqualTo(1.35), "easy CS 8");
            Assert.That(pp(easy, p, da(cs: 10)) / pp(easy, p), Is.LessThanOrEqualTo(1.7), "easy CS 10");
            Assert.That(pp(hard, p, da(cs: 8)) / pp(hard, p), Is.LessThanOrEqualTo(2.2), "hard CS 8");
            Assert.That(pp(hard, p, da(cs: 11)) / pp(hard, p), Is.LessThanOrEqualTo(4), "hard CS 11");
        }

        /// <summary>The top-player farm "DA CS 2 / AR 10.4 / OD 11 / HP 0" on a CS 4 / AR 9 / OD 8 map is not worth more than NM.
        /// Passes now (0.63–0.93 on real maps).</summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestBigCircleFarmIsNotWorthMore(Mp2Parameters p)
        {
            foreach (var map in new[] { jumpMap(300, 300), fromObjects(stream(600, 75, 30)), fromObjects(spacedJumps(240, 500, 140)) })
                Assert.That(pp(map, p, da(cs: 2, ar: 10.4f, od: 11, hp: 0)), Is.LessThanOrEqualTo(pp(map, p)));
        }

        /// <summary>PP never falls with a higher OD (DA OD 0 → 11). Passes on these maps; fails now on real maps (Hashire OD 11 &lt; OD 10,
        /// Overkill), because hard time sums move times that include the 300 window — see TestHardTimeIsRealTime.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestOverallDifficultyIsMonotone(Mp2Parameters p)
        {
            foreach (var map in new[] { jumpMap(300, 300), fromObjects(stream(600, 75, 30)), sliderMap() })
            {
                double previous = 0;
                for (int od = 0; od <= 11; od++)
                {
                    double value = pp(map, p, da(od: od));
                    // 0.1%: the repetition discount compares move times, the window changes them a little.
                    Assert.That(value, Is.GreaterThanOrEqualTo(previous * 0.999), $"OD {od}");
                    previous = value;
                }
            }
        }

        /// <summary>
        /// "Hard time" is real time: on a uniform map it is at most the drain time. Fails now: share = move time = gap + Relax
        /// leniency + WindowSlack · 300 window, so a 45 s stream has 86 s of hard time at OD 0 and 54 s at OD 11 (moves overlap).
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestHardTimeIsRealTime(Mp2Parameters p)
        {
            var notes = stream(600, 75, 30);
            double length = notes.Last().Time / 1000;

            foreach (float od in new[] { 0f, 8, 11 })
            {
                var d = evaluate(fromObjects(notes), p, da(od: od));
                Assert.That(d.HardSeconds, Is.LessThanOrEqualTo(length * 1.05 + 1), $"OD {od}");
            }
        }

        /// <summary>OD 0 → 11 on a 200 BPM 1/4 stream: at most +30%. In a stream the late-hit slack cannot be used on every note
        /// (arriving late at one note leaves less time for the next), so the window must not act as extra move time per note.
        /// Fails now after the hard-time fix (1.67 with WindowSlack 1; 1.32 today only because the hard-time bug compensates).</summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestOverallDifficultyEffectIsBounded(Mp2Parameters p)
        {
            var map = fromObjects(stream(600, 75, 30));
            Assert.That(pp(map, p, da(od: 11)) / pp(map, p, da(od: 0)), Is.LessThanOrEqualTo(1.3));
        }

        /// <summary>Low AR (more notes on screen) and very high AR (reaction) are bounded: AR 0 at most +15%, AR −10 (extended) at most
        /// +30%, AR 11 at most +15% over AR 9; an easy map gains nothing from AR 0 (at most +5%). Passes on these maps (real dense maps:
        /// ganbare AR 0 = 1.67, AR −10 = 1.88 — check those in the fit).</summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestApproachRateEffectIsBounded(Mp2Parameters p)
        {
            var map = jumpMap(300, 300);
            double nm = pp(map, p);
            Assert.That(pp(map, p, da(ar: 0)) / nm, Is.LessThanOrEqualTo(1.15), "AR 0");
            Assert.That(pp(map, p, da(ar: -10)) / nm, Is.LessThanOrEqualTo(1.3), "AR -10");
            // 1.4: reaction at high AR is what makes HR ≈ ×1.3 on easy maps (debate consensus, accepted by the owner).
            Assert.That(pp(map, p, da(ar: 11)) / nm, Is.LessThanOrEqualTo(1.4), "AR 11");
            var easy = fromObjects(spacedJumps(240, 500, 140));
            Assert.That(pp(easy, p, da(ar: 0)) / pp(easy, p), Is.LessThanOrEqualTo(1.05), "easy AR 0");
        }

        /// <summary>HR is exactly DA with CS × 1.3, AR / OD × 1.4 (capped at 10) — physics only, no extra rule; MR is exactly NM.
        /// Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestHardRockAndMirrorArePhysicsOnly(Mp2Parameters p)
        {
            var map = jumpMap(300, 300);
            Assert.That(pp(map, p, new OsuModHardRock()) / pp(map, p, da(cs: 5.2f, ar: 10, od: 10)), Is.EqualTo(1).Within(0.01));
            Assert.That(pp(map, p, new OsuModMirror()) / pp(map, p), Is.EqualTo(1).Within(1e-6));
        }

        /// <summary>HD: +0–15%. FL: never below NM, at most +15% on a stream (the next note is always inside the light) and at most
        /// +45% on jumps. Fails now: FL is a flat +0.3 reading → ×1.56 on everything (streams, easy maps, stacks).</summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestHiddenAndFlashlightAreBounded(Mp2Parameters p)
        {
            var jumps = jumpMap(300, 300);
            var streamMap = fromObjects(stream(600, 75, 30));
            double hd = pp(jumps, p, new OsuModHidden()) / pp(jumps, p);
            Assert.That(hd, Is.InRange(1.0, 1.15), "HD");
            Assert.That(pp(jumps, p, new OsuModFlashlight()) / pp(jumps, p), Is.InRange(1.0, 1.45), "FL jumps");
            Assert.That(pp(streamMap, p, new OsuModFlashlight()) / pp(streamMap, p), Is.InRange(1.0, 1.15), "FL stream");
        }

        // ================================================================ 2B, stacks, sliders, spinners, off-screen

        /// <summary>
        /// 2B chords. A section where every other note has a simultaneous partner is worth at most 2× the same notes with the partner
        /// moved 1/4 later (a chord costs at most like a 1/4 at the local rhythm). Fails now: ×3.3 (×12 over the map without the
        /// chords, ×24 when every note is a chord) — the move time is only Relax leniency + 300 window (≈ 30 ms) and the LoadCap
        /// median does not help once half the neighbours are chords.
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestChordSectionsDoNotExplode(Mp2Parameters p)
        {
            var notes = jumpNotes(300, 300);
            var chords = notes.Concat(notes.Where((_, i) => i % 2 == 0).Select(n => circle(n.Time, 512 - n.Position.X, 384 - n.Position.Y))).OrderBy(n => n.Time).ToList();
            var spread = notes.Concat(notes.Where((_, i) => i % 2 == 0).Select(n => circle(n.Time + 75, 512 - n.Position.X, 384 - n.Position.Y))).OrderBy(n => n.Time).ToList();
            Assert.That(pp(fromObjects(chords), p), Is.LessThanOrEqualTo(pp(fromObjects(spread), p) * 2));
        }

        /// <summary>A chord whose notes overlap (30 px apart at CS 4, the cursor covers both) adds nothing (≤ +5%). Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestOverlappingChordsAreFree(Mp2Parameters p)
        {
            var notes = jumpNotes(300, 300);
            var withPartners = notes.Concat(notes.Select(n => circle(n.Time, n.Position.X + 30, n.Position.Y))).OrderBy(n => n.Time).ToList();
            Assert.That(pp(fromObjects(withPartners), p), Is.LessThanOrEqualTo(pp(fromObjects(notes), p) * 1.05));
        }

        /// <summary>One-point map through the real beatmap processor (stack offsets grow along the chain, so the end is a slow
        /// drift): difficulty below 1/6 of jumps, compared in D so the PP exponent does not matter (1/6 in D is ~3% of the PP).</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestOnePointMapWithStackingIsWorthAlmostNothing(Mp2Parameters p)
        {
            var onePoint = fromObjects(Enumerable.Range(0, 400).Select(i => circle(i * 75, 256, 192)));
            Assert.That(evaluate(onePoint, p).Difficulty, Is.LessThan(evaluate(jumpMap(400, 300), p).Difficulty / 6));
        }

        /// <summary>
        /// A slider that is still running when the next note must be hit is never worth more than the longest slider that ends in
        /// time (+5%). Fails now: ×215 over circles — the next note's free time is its own start time, so its move time is only
        /// leniency + window (the player really just leaves the slider). Fix: free time ≤ note time − 0.25 · head-to-head gap.
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestSliderRunningIntoTheNextNoteIsBounded(Mp2Parameters p)
        {
            var heads = jumpNotes(150, 1500);
            // 120 px span at SV 1 (0.467 px/ms) = 257 ms: 4 repeats end 214 ms before the next head, 10 repeats run 1.3 s past it.
            var legal = heads.Select(n => n with { Path = new[] { new Vector2(120, 0) }, Repeats = 4 });
            var overlapping = heads.Select(n => n with { Path = new[] { new Vector2(120, 0) }, Repeats = 10 });
            Assert.That(pp(fromObjects(overlapping), p), Is.LessThanOrEqualTo(pp(fromObjects(legal), p) * 1.05));
        }

        /// <summary>A buzz slider that fits in the follow circle (20 px, 20 repeats, ends before the next note) needs no tracking. Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestSmallBuzzSliderNeedsNoTracking(Mp2Parameters p)
        {
            var buzz = jumpNotes(150, 1500).Select(n => n with { Path = new[] { new Vector2(20, 0) }, Repeats = 20 });
            Assert.That(evaluate(fromObjects(buzz), p).SkillShares["track"], Is.LessThan(0.01));
        }

        /// <summary>
        /// Aspire sliders (SV 10, 400 × 300 px zigzag, 2 repeats, one every 1.5 s) are worth at most a map of 1/2 jumps at 200 BPM.
        /// Fails now: 1337 PP — tracking speed is linear and unbounded. Fix: saturate the tracking speed (≈ 20 CS 4 diameters / s;
        /// at 20 the case drops to 409, at 10 to 148, while Transhumanist loses 1% / 7.5%).
        /// </summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestAspireSlidersAreBounded(Mp2Parameters p)
        {
            var aspire = jumpNotes(150, 1500).Select(n => n with
            {
                Path = new[] { new Vector2(400, 0), new Vector2(0, 300), new Vector2(400, 300), new Vector2(0, 0) },
                Repeats = 2,
                Sv = 10,
            });
            Assert.That(pp(fromObjects(aspire), p), Is.LessThanOrEqualTo(pp(jumpMap(600, 150), p)));
        }

        /// <summary>Zero-length / reverse-spam sliders cost nothing beyond the circle at the head (+10% at most). Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestZeroLengthSlidersAreFree(Mp2Parameters p)
        {
            var heads = jumpNotes(200, 600);
            var zero = heads.Select(n => n with { Path = new[] { new Vector2(0.01f, 0) }, Repeats = 30, Sv = 0.1 });
            Assert.That(evaluate(fromObjects(zero), p).SkillShares["track"], Is.LessThan(0.01));
        }

        /// <summary>Replacing every 4th note with a spinner never adds PP (+1% numerical slack), and a spinner-only map is worth 0. Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestSpinnersAddNothing(Mp2Parameters p)
        {
            var notes = jumpNotes(200, 300);
            var withSpinners = notes.Select((n, i) => i % 4 == 3 ? n with { SpinnerMs = 250 } : n);
            Assert.That(pp(fromObjects(withSpinners), p), Is.LessThanOrEqualTo(pp(fromObjects(notes), p) * 1.01));
            Assert.That(pp(fromObjects(Enumerable.Range(0, 20).Select(i => new SynthObject(i * 3000, new Vector2(256, 192), SpinnerMs: 2500))), p), Is.EqualTo(0));
        }

        /// <summary>
        /// Notes outside the screen (aspire, hand-edited maps) are worth at most 3× the same back-and-forth between the playfield
        /// edges. Fails now: ×91 for ±3000 px — the agility term is linear in distance. Fix: clamp positions to the visible
        /// area (x −171…683, y −48…432 osu! px at 16:9).
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestOffScreenNotesAreBounded(Mp2Parameters p)
        {
            var edges = fromObjects(Enumerable.Range(0, 300).Select(i => circle(i * 300, i % 2 == 0 ? 0 : 512, 192)));
            var offScreen = fromObjects(Enumerable.Range(0, 300).Select(i => circle(i * 300, i % 2 == 0 ? -3000 : 3500, 192)));
            Assert.That(pp(offScreen, p), Is.LessThanOrEqualTo(pp(edges, p) * 3));
        }

        // ================================================================ length

        /// <summary>
        /// Short maps are not worth their level: 2 notes ≤ 5 PP, 3 s (10 notes) ≤ 20% of the same pattern for 1 min. Fails now:
        /// 2 notes = 413 PP, 3 s = 37% — the level is the mean of the covered part of PeakSeconds, so a map shorter than PeakSeconds
        /// is not penalised; the first note also carries the next note's agility over its artificial 1 s share. Fix:
        /// D × min(1, covered / PeakSeconds)^0.5 (3 s → 11%, 2 notes → 1.9 PP).
        /// </summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestShortMapsAreNotWorthTheirLevel(Mp2Parameters p)
        {
            var notes = jumpNotes(200, 300);
            Assert.That(pp(fromObjects(new[] { circle(0, 0, 0), circle(100, 512, 384) }), p), Is.LessThanOrEqualTo(5));
            Assert.That(pp(fromObjects(notes.Take(10)), p), Is.LessThanOrEqualTo(pp(fromObjects(notes), p) * 0.2));
        }

        /// <summary>Marathons: 10 min of the same intensity at most 1.65× 1 min (consensus: length ≈ ×1.4), and a 1 min pattern looped 10 times too. Failed before
        /// (2.58 and 2.57: LengthExponent 0.3 over (1 + hard time / 30 s), and the repetition discount only looks 4 notes back).</summary>
        [Category("Mp2FitBound")]
        [TestCaseSource(nameof(ShippedParameters))]
        public void TestMarathonsAreBounded(Mp2Parameters p)
        {
            var minute = jumpNotes(200, 300);
            double one = pp(fromObjects(minute), p);
            // Debate consensus (accepted by the owner): length adds at most ≈ ×1.4 per 10 min. 1.65: random jumps 10× longer also
            // have a harder hardest 10 s (the level), which is not length.
            Assert.That(pp(jumpMap(2000, 300), p) / one, Is.LessThanOrEqualTo(1.65), "10 min");
            var loop = Enumerable.Range(0, 10).SelectMany(k => minute.Select(n => n with { Time = n.Time + k * 60000 }));
            // Long-range repetition (a looped section) is not modelled yet: the loop is bounded like 10 unique minutes.
            // 2.1: the level takes the hardest moments of all 10 copies (long-range repetition is open).
            Assert.That(pp(fromObjects(loop), p) / one, Is.LessThanOrEqualTo(2.1), "loop");
        }

        /// <summary>Degenerate maps: empty, one note, extreme DA everywhere — finite, non-negative, no exception. Passes now.</summary>
        [Category("Mp2Invariant")]
        [TestCaseSource(nameof(AllParameters))]
        public void TestDegenerateInputsAreFinite(Mp2Parameters p)
        {
            Assert.That(pp(fromObjects(Array.Empty<SynthObject>()), p), Is.EqualTo(0));
            Assert.That(pp(fromObjects(new[] { circle(0, 100, 100) }), p), Is.EqualTo(0));

            foreach (var mod in new[] { da(cs: 0, ar: -10, od: 0), da(cs: 11, ar: 11, od: 11), da(cs: 11, ar: -10, od: 11) })
            {
                double value = pp(jumpMap(100, 150), p, mod, rateMod(2.0));
                Assert.That(double.IsFinite(value) && value >= 0);
            }
        }

        // ================================================================ real maps (lab cache)

        /// <summary>
        /// Invariants on real maps from ~/.cache/mosupp-lab/osu (or MOSUPP_LAB_OSU): monotone in rate (0.05 steps, locked and not), DT
        /// 1.49 → 1.51 within +4%, monotone in CS and OD, lock = DA equivalent (±2%). Fails now: DT 1.49 → 1.51 = +5.7% (2833124),
        /// OD 11 &lt; OD 10 (2989093, 2004799), lock −4…−14%.
        /// </summary>
        [Category("Mp2RealMaps")]
        [TestCase(2833124)]
        [TestCase(4565459)]
        [TestCase(3352759)]
        [TestCase(4654706)]
        [TestCase(4323073)]
        [TestCase(2815425)]
        [TestCase(2989093)]
        [TestCase(2004799)]
        public void TestRealMapInvariants(int beatmapId)
        {
            string dir = Environment.GetEnvironmentVariable("MOSUPP_LAB_OSU") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "mosupp-lab", "osu");
            string file = Path.Combine(dir, $"{beatmapId}.osu");
            Assume.That(File.Exists(file), $"{file} is not cached");

            var p = fitted() ?? Mp2Parameters.Default;
            Func<IBeatmap> map = () => decode(file);

            foreach (bool locked in new[] { false, true })
            {
                double previous = pp(map, p);
                for (int step = 21; step <= 40; step++)
                {
                    double value = pp(map, p, rateMod(step / 20.0, locked));
                    Assert.That(value, Is.GreaterThanOrEqualTo(previous * 0.999), $"rate {step / 20.0} lock={locked}");
                    previous = value;
                }
            }

            // No step at 1.5: the 1.49 → 1.51 ratio is like its neighbours' (discrete moves are steeper than rate^PpExponent by
            // design — spare time shrinks with the rate — so the absolute bound is a bit above rate^PpExponent).
            double ratio(double from, double to) => pp(map, p, rateMod(to)) / pp(map, p, rateMod(from));
            double middle = ratio(1.49, 1.51);
            Assert.That(middle, Is.LessThanOrEqualTo(Math.Max(ratio(1.45, 1.47), ratio(1.53, 1.55)) + 0.005), "DT 1.49 → 1.51 vs neighbours");
            Assert.That(middle, Is.LessThanOrEqualTo(Math.Pow(1.51 / 1.49, p.PpExponent + 3)), "DT 1.49 → 1.51");

            float ar = map().Difficulty.ApproachRate, od = map().Difficulty.OverallDifficulty;
            Assert.That(pp(map, p, rateMod(1.5, true)) / pp(map, p, rateMod(1.5), da(ar: lockedAr(ar, 1.5), od: lockedOd(od, 1.5))), Is.EqualTo(1).Within(0.02), "lock");

            double prevCs = 0, prevOd = 0;
            for (int i = 0; i <= 10; i++)
            {
                double cs = pp(map, p, da(cs: i));
                double o = pp(map, p, da(od: i + 1));
                // 2%: lazer's stack offsets scale with the circle, so a stack-heavy map moves less with smaller circles even with
                // every rule off (3352759: CS 4 → 5 = −1.3%), and note types follow the spacing in diameters. Not worth farming.
                Assert.That(cs, Is.GreaterThanOrEqualTo(prevCs * 0.98), $"CS {i}");
                Assert.That(o, Is.GreaterThanOrEqualTo(prevOd * 0.998), $"OD {i + 1}");
                prevCs = cs;
                prevOd = o;
            }
        }


        // ================================================================ helpers

        /// <summary>A synthetic object: circle; slider when <paramref name="Path"/> is set (points relative to the head, linear);
        /// spinner when <paramref name="SpinnerMs"/> &gt; 0.</summary>
        public record SynthObject(double Time, Vector2 Position, Vector2[]? Path = null, int Repeats = 0, double Sv = 1, double SpinnerMs = 0);

        private static SynthObject circle(double time, float x, float y) => new SynthObject(time, new Vector2(x, y));

        private static Func<IBeatmap> fromObjects(IEnumerable<SynthObject> objects, float cs = 4, float ar = 9, float od = 8, double beatLength = 300)
        {
            var list = objects.ToList();
            // A fresh beatmap per evaluation: conversion keeps the same object instances and mods / ApplyDefaults mutate them.
            return () => build(list, cs, ar, od, beatLength);
        }

        private static IBeatmap build(IEnumerable<SynthObject> objects, float cs, float ar, float od, double beatLength)
        {
            var difficulty = new BeatmapDifficulty { CircleSize = cs, ApproachRate = ar, OverallDifficulty = od, DrainRate = 5, SliderMultiplier = 1.4, SliderTickRate = 1 };
            var beatmap = new Beatmap<OsuHitObject> { Difficulty = difficulty, BeatmapInfo = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo, Difficulty = difficulty } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });

            foreach (var o in objects)
            {
                if (o.SpinnerMs > 0)
                    beatmap.HitObjects.Add(new Spinner { StartTime = o.Time, EndTime = o.Time + o.SpinnerMs, Position = new Vector2(256, 192) });
                else if (o.Path != null)
                {
                    var points = new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR) }.Concat(o.Path.Select(v => new PathControlPoint(v))).ToArray();
                    beatmap.HitObjects.Add(new Slider { StartTime = o.Time, Position = o.Position, Path = new SliderPath(points), RepeatCount = o.Repeats, SliderVelocityMultiplier = o.Sv });
                }
                else
                    beatmap.HitObjects.Add(new HitCircle { StartTime = o.Time, Position = o.Position });
            }

            return beatmap;
        }

        private static IBeatmap decode(string file)
        {
            using var stream = File.OpenRead(file);
            using var reader = new LineBufferedReader(stream);
            var beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            beatmap.BeatmapInfo.Ruleset = new OsuRuleset().RulesetInfo;
            return beatmap;
        }

        private static IBeatmap playableOf(IBeatmap beatmap, IReadOnlyList<Mod> mods) => new FlatWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods);

        private static Mp2Difficulty evaluate(Func<IBeatmap> map, Mp2Parameters p, params Mod[] mods)
        {
            IReadOnlyList<Mod> all = mods.Append(new OsuModRelax()).ToArray();
            return Mp2Model.Evaluate(Mp2Features.Extract(playableOf(map(), all), all), p);
        }

        private static double pp(Func<IBeatmap> map, Mp2Parameters p, params Mod[] mods) => evaluate(map, p, mods).PpSs;

        private static Mod[] rateMod(double rate, bool locked)
        {
            if (Math.Abs(rate - 1) < 1e-9)
                return Array.Empty<Mod>();

            return new[] { makeRate(rate, locked) };
        }

        private static Mod rateMod(double rate) => makeRate(rate, false);

        private static Mod makeRate(double rate, bool locked)
        {
            ModRateAdjust mod = rate < 1 ? new OsuModHalfTime() : new OsuModDoubleTime();
            mod.SpeedChange.Value = rate;
            mod.LockDifficultyAdjust.Value = locked;
            return mod;
        }

        private static Mod da(float? cs = null, float? ar = null, float? od = null, float? hp = null)
        {
            var mod = new OsuModDifficultyAdjust();
            mod.ExtendedLimits.Value = new[] { cs, ar, od, hp }.Any(v => v is < 0 or > 10);
            if (cs != null) mod.CircleSize.Value = cs;
            if (ar != null) mod.ApproachRate.Value = ar;
            if (od != null) mod.OverallDifficulty.Value = od;
            if (hp != null) mod.DrainRate.Value = hp;
            return mod;
        }

        /// <summary>AR / OD that the unlocked rate needs to show the locked values (same as RxLockedDifficulty).</summary>
        private static float lockedAr(float ar, double rate)
        {
            double preempt = (ar < 5 ? 1800 - 120 * ar : 1950 - 150 * ar) * rate;
            return (float)(preempt > 1200 ? (1800 - preempt) / 120 : (1950 - preempt) / 150);
        }

        private static float lockedOd(float od, double rate) => (float)((80 - (80 - 6 * od) * rate) / 6);

        /// <summary>Deterministic pseudo-random jumps inside the playfield (no repeating pattern).</summary>
        private static List<SynthObject> jumpNotes(int count, double gap, double start = 0)
        {
            var random = new Random(7);
            return Enumerable.Range(0, count).Select(i => circle(start + i * gap, 40 + (float)random.NextDouble() * 432, 40 + (float)random.NextDouble() * 304)).ToList();
        }

        private static Func<IBeatmap> jumpMap(int count, double gap) => fromObjects(jumpNotes(count, gap));

        /// <summary>Easy jumps: every note <paramref name="spacing"/> px from the previous one in a random direction.</summary>
        private static List<SynthObject> spacedJumps(int count, double gap, float spacing)
        {
            var random = new Random(3);
            var list = new List<SynthObject>();
            var position = new Vector2(256, 192);

            for (int i = 0; i < count; i++)
            {
                list.Add(circle(i * gap, position.X, position.Y));

                for (int attempt = 0; attempt < 50; attempt++)
                {
                    double angle = random.NextDouble() * Math.PI * 2;
                    var next = position + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * spacing;
                    if (next.X is > 20 and < 492 && next.Y is > 20 and < 364)
                    {
                        position = next;
                        break;
                    }
                }
            }

            return list;
        }

        /// <summary>A stream along a slowly turning curve.</summary>
        private static List<SynthObject> stream(int count, double gap, float spacing, double start = 0)
        {
            var list = new List<SynthObject>();
            var position = new Vector2(100, 192);
            double angle = 0;

            for (int i = 0; i < count; i++)
            {
                list.Add(circle(start + i * gap, position.X, position.Y));
                angle += 0.15;
                var next = position + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * spacing;
                if (next.X is < 30 or > 482 || next.Y is < 30 or > 354)
                {
                    angle += Math.PI / 2;
                    next = position + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * spacing;
                }

                position = next;
            }

            return list;
        }

        /// <summary>Jumps where every other object is a 1/2 slider (150 px, SV 1.4) — tracking and slider-end starts.</summary>
        private static Func<IBeatmap> sliderMap()
            => fromObjects(jumpNotes(300, 300).Select((n, i) => i % 2 == 0 ? n with { Path = new[] { new Vector2(70, 0) }, Sv = 1 } : n));
    }
}
