using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Linq;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osuTK;

namespace osu.Game.Tests.Mods
{
    [TestFixture]
    public class SegmentRateModsTest
    {
        public static IEnumerable<Ruleset> Rulesets => new Ruleset[] { new OsuRuleset(), new TaikoRuleset(), new CatchRuleset(), new ManiaRuleset() };

        [TestCaseSource(nameof(Rulesets))]
        public void TestRegistrationRoundTripAndCompatibility(Ruleset ruleset)
        {
            var glitch = ruleset.AllMods.OfType<ModGlitch>().Single();
            var flatline = ruleset.AllMods.OfType<ModFlatline>().Single();
            glitch.Amount.Value = 60;
            flatline.Direction.Value = FlatlineDirection.Both;
            foreach (var mod in new ModSegmentRate[] { glitch, flatline })
            {
                var api = JsonConvert.DeserializeObject<APIMod>(JsonConvert.SerializeObject(new APIMod(mod)))!;
                var restored = api.ToMod(ruleset);
                Assert.That(restored, Is.TypeOf(mod.GetType()));
                Assert.That(mod.Ranked, Is.False);
                Assert.That(mod.HasImplementation, Is.True);
                Assert.That(mod.ValidForMultiplayerAsFreeMod, Is.False);
                Assert.That(mod.ValidForFreestyleAsRequiredMod, Is.True);
                Assert.That(api.Settings.Keys, Does.Not.Contain("speed"));
                Assert.That(api.Settings.Keys, Does.Not.Contain("rate_plan"));
                foreach (var other in ruleset.AllMods.OfType<IApplicableToRate>().Cast<Mod>())
                    Assert.That(mod.IncompatibleMods.Any(t => t.IsInstanceOfType(other)), Is.True, other.Acronym);
            }
            Assert.That(((ModGlitch)new APIMod(glitch).ToMod(ruleset)).Amount.Value, Is.EqualTo(60));
            Assert.That(((ModFlatline)new APIMod(flatline).ToMod(ruleset)).Direction.Value, Is.EqualTo(FlatlineDirection.Both));
        }

        [Test]
        public void TestFlatlineLimitAndSharedApproachSetting()
        {
            var mod = new OsuModFlatline();
            Assert.That(mod.BakedTiming.Value, Is.True);
            Assert.That(new OsuModGlitch().BakedTiming.Value, Is.True);
            Assert.That(mod.IconScale, Is.EqualTo(0.7f));
            Assert.That(new OsuModGlitch().IconScale, Is.EqualTo(0.7f));
            Assert.That(new OsuModDoubleTime().IconScale, Is.EqualTo(1));
            mod.MaxRate.Value = 5;
            Assert.That(mod.MaxRate.Value, Is.EqualTo(3));
            mod.BakedTiming.Value = false;
            var restored = (ModFlatline)new APIMod(mod).ToMod(new OsuRuleset());
            Assert.That(restored.BakedTiming.Value, Is.False);
            Assert.That(restored.MaxRate.MaxValue, Is.EqualTo(3));
        }

        [Test]
        public void TestMappedTimelineRoundTripIncludingNegativeSegments()
        {
            var plan = new SegmentRatePlan(new[]
            {
                new SegmentRatePlan.Segment(-500, 500, 1.5),
                new SegmentRatePlan.Segment(1000, 2000, 0.75),
                new SegmentRatePlan.Segment(2000, 3000, 3),
            });
            Assert.That(plan.Map(0), Is.Zero);
            for (double t = -1000; t <= 5000; t += 13.7)
            {
                Assert.That(plan.Unmap(plan.Map(t)), Is.EqualTo(t).Within(1e-8));
                Assert.That(plan.Map(t), Is.EqualTo(plan.Elapsed(0, t)).Within(1e-8));
            }
        }

        [Test]
        public void TestMappedClockTempoSeekPauseAndRestart()
        {
            var plan = new SegmentRatePlan(new[] { new SegmentRatePlan.Segment(1000, 2000, 2) });
            var source = new AdjustableTestClock();
            var clock = new SegmentTrackClock(source, plan, t => source.Rate = plan.RateAt(t));
            source.CurrentTime = 1500;
            clock.ProcessFrame();
            Assert.That(clock.CurrentTime, Is.EqualTo(1250));
            Assert.That(clock.Rate, Is.EqualTo(1));
            clock.Seek(1400);
            Assert.That(source.CurrentTime, Is.EqualTo(1800));
            Assert.That(source.Rate, Is.EqualTo(2));
            clock.Stop();
            Assert.That(clock.IsRunning, Is.False);
            clock.Start();
            Assert.That(clock.IsRunning, Is.True);
            clock.Seek(2500);
            Assert.That(source.CurrentTime, Is.EqualTo(3000));
            Assert.That(source.Rate, Is.EqualTo(1));
            clock.Seek(-1000);
            Assert.That(source.CurrentTime, Is.EqualTo(-1000));
        }

        [Test]
        public void TestBakedOverlappingHoldsKeepMappedHeadsAndTails()
        {
            var raw = new Beatmap<ManiaHitObject>();
            raw.Difficulty.CircleSize = 4;
            raw.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            raw.HitObjects.AddRange(new ManiaHitObject[]
            {
                new HoldNote { StartTime = 1000, Duration = 850, Column = 0 },
                new HoldNote { StartTime = 1750, Duration = 800, Column = 1 },
                new Note { StartTime = 3000, Column = 0 },
                new Note { StartTime = 4000, Column = 1 },
            });
            var mod = new ManiaModGlitch { Amount = { Value = 100 } };
            var playable = new FlatWorkingBeatmap(raw).GetPlayableBeatmap(new ManiaRuleset().RulesetInfo, new Mod[] { mod });
            var holds = playable.HitObjects.OfType<HoldNote>().ToArray();
            Assert.That(holds[0].Head.StartTime, Is.EqualTo(mod.RatePlan.Map(1000)).Within(1e-6));
            Assert.That(holds[0].Tail.StartTime, Is.EqualTo(mod.RatePlan.Map(1850)).Within(1e-6));
            Assert.That(holds[1].Head.StartTime, Is.EqualTo(mod.RatePlan.Map(1750)).Within(1e-6));
            Assert.That(holds[1].Tail.StartTime, Is.EqualTo(mod.RatePlan.Map(2550)).Within(1e-6));
            Assert.That(raw.HitObjects[1].StartTime, Is.EqualTo(1750));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBakedSliderTimelineDoesNotMutateSourceOrRetry(bool flatline)
        {
            var raw = difficultyMap();
            var slider = new Slider
            {
                StartTime = 2000, Position = new Vector2(100), RepeatCount = 3,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(200, 0)) }, 200),
            };
            raw.HitObjects.Add(slider);
            raw.HitObjects = raw.HitObjects.OrderBy(h => h.StartTime).ToList();
            foreach (var obj in raw.HitObjects)
                obj.ApplyDefaults(raw.ControlPointInfo, raw.Difficulty);
            var original = raw.HitObjects.Select(h => (h.StartTime, h.GetEndTime())).ToArray();
            var nested = slider.NestedHitObjects.Select(h => h.StartTime).ToArray();
            var working = new FlatWorkingBeatmap(raw);
            ModSegmentRate mod = flatline ? new OsuModFlatline() : new OsuModGlitch { Amount = { Value = 100 } };
            var playable = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, new Mod[] { mod });
            var mappedSlider = playable.HitObjects.OfType<Slider>().Single();
            Assert.That(mappedSlider, Is.Not.SameAs(slider));
            Assert.That(raw.HitObjects.Select(h => (h.StartTime, h.GetEndTime())), Is.EqualTo(original));
            Assert.That(mappedSlider.StartTime, Is.EqualTo(mod.RatePlan.Map(2000)).Within(1e-6));
            Assert.That(mappedSlider.EndTime, Is.EqualTo(mod.RatePlan.Map(slider.EndTime)).Within(1e-6));
            Assert.That(mappedSlider.RepeatCount, Is.EqualTo(3));
            Assert.That(mappedSlider.NestedHitObjects.Count, Is.EqualTo(nested.Length));
            for (int i = 0; i < nested.Length; i++)
                Assert.That(mappedSlider.NestedHitObjects[i].StartTime, Is.EqualTo(mod.RatePlan.Map(nested[i])).Within(1e-6));
            Assert.That(mappedSlider.TimePreempt, Is.EqualTo(slider.TimePreempt));
            Assert.That(mod.ApplyToRate(mappedSlider.StartTime), Is.EqualTo(1));
            var expected = playable.HitObjects.Select(h => (h.StartTime, h.GetEndTime())).ToArray();
            var retryMod = (ModSegmentRate)new APIMod(mod).ToMod(new OsuRuleset());
            var retry = working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, new Mod[] { retryMod });
            Assert.That(retry.HitObjects.Select(h => (h.StartTime, h.GetEndTime())), Is.EqualTo(expected));
            Assert.That(raw.HitObjects.Select(h => (h.StartTime, h.GetEndTime())), Is.EqualTo(original));
        }

        [Test]
        public void TestFlatlineDoesNotTreatSkillWarmupAsEasyIntro()
        {
            var map = new Beatmap<OsuHitObject>();
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 160; i++)
                map.HitObjects.Add(new HitCircle { StartTime = 2000 + i * 125, Position = new Vector2(100 + i % 2 * 200, 100) });
            foreach (var obj in map.HitObjects)
                obj.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
            var mod = new OsuModFlatline();
            mod.ApplyToBeatmap(map);
            TestContext.Out.WriteLine(string.Join("; ", mod.RatePlan.Segments.Select(s => $"{s.Start:F0}-{s.End:F0}: {s.Rate:F3}x")));
            Assert.That(mod.RatePlan.RateAt(2500), Is.LessThan(1.2), "Identical repeating jumps must not get an artificial accelerated intro.");
        }

        [Test]
        public void TestRateBoundariesAndWallClockDuration()
        {
            var plan = new SegmentRatePlan(new[] { new SegmentRatePlan.Segment(1000, 2000, 2), new SegmentRatePlan.Segment(2000, 3000, 0.5) });
            Assert.That(plan.RateAt(999), Is.EqualTo(1));
            Assert.That(plan.RateAt(1000), Is.EqualTo(2));
            Assert.That(plan.RateAt(2000), Is.EqualTo(0.5));
            Assert.That(plan.RateAt(3000), Is.EqualTo(1));
            Assert.That(plan.Elapsed(0, 4000), Is.EqualTo(4500));
            Assert.That(plan.Elapsed(4000, 0), Is.EqualTo(-4500));
            Assert.Throws<ArgumentException>(() => new SegmentRatePlan(new[] { new SegmentRatePlan.Segment(1, 5, 1), new SegmentRatePlan.Segment(4, 8, 2) }));
        }

        [Test]
        public void TestGlitchDeterministicIncludingReapplyAndSettingsRoundTrip()
        {
            var map = circleMap();
            var mod = new OsuModGlitch { BakedTiming = { Value = false }, Amount = { Value = 60 } };
            mod.ApplyToBeatmap(map);
            var expected = mod.RatePlan.Segments.ToArray();
            Assert.That(expected, Is.Not.Empty);
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments, Is.EqualTo(expected));
            var restored = (ModGlitch)new APIMod(mod).ToMod(new OsuRuleset());
            restored.ApplyToBeatmap(map);
            Assert.That(restored.RatePlan.Segments, Is.EqualTo(expected));
            foreach (double time in new[] { 2000, 5000, 1000, 3000, -1000, 12000 })
                Assert.That(mod.ApplyToRate(time), Is.EqualTo(restored.ApplyToRate(time)));
            mod.Amount.Value = 0;
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments, Is.Empty);
        }

        [Test]
        public void TestGlitchSubdivisionSettings()
        {
            var mod = new OsuModGlitch { Amount = { Value = 100 }, Thirds = { Value = false }, Fifths = { Value = false } };
            mod.ApplyToBeatmap(circleMap());
            Assert.That(mod.RatePlan.Segments, Is.Not.Empty);
            Assert.That(mod.RatePlan.Segments.All(s => s.Rate == 1.5), Is.True);
            mod.Sixths.Value = false;
            mod.ApplyToBeatmap(circleMap());
            Assert.That(mod.RatePlan.Segments, Is.Empty);
            mod.Sixths.Value = true;
            mod.Strength.Value = 0;
            mod.ApplyToBeatmap(circleMap());
            Assert.That(mod.RatePlan.Segments, Is.Empty);
        }

        [Test]
        public void TestOverlappingHoldsAreNotSplitAndBreaksStayNormal()
        {
            var map = new Beatmap<ManiaHitObject>();
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            map.HitObjects.AddRange(new ManiaHitObject[]
            {
                new HoldNote { StartTime = 1000, Duration = 850, Column = 0 },
                new HoldNote { StartTime = 1750, Duration = 800, Column = 1 },
                new Note { StartTime = 3000, Column = 0 },
                new Note { StartTime = 5000, Column = 1 },
            });
            map.Breaks.Add(new BreakPeriod(3200, 4500));
            var mod = new ManiaModGlitch { BakedTiming = { Value = false }, Amount = { Value = 100 } };
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments.Any(s => s.Start == 1000 && s.End >= 2550), Is.True);
            Assert.That(mod.ApplyToRate(1000), Is.EqualTo(mod.ApplyToRate(2500)));
            Assert.That(mod.ApplyToRate(4000), Is.EqualTo(1));
            Assert.That(mod.RatePlan.Segments.Any(s => s.Start < 4500 && s.End > 3200), Is.False);
        }

        [Test]
        public void TestGlitchKeepsRepeatingSliderAndSpinnerIntact()
        {
            var map = circleMap();
            var slider = new Slider
            {
                StartTime = 2000, Position = new Vector2(100), RepeatCount = 3,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(200, 0)) }, 200),
            };
            slider.ApplyDefaults(map.ControlPointInfo, map.Difficulty);
            map.HitObjects.Add(slider);
            var spinner = new Spinner { StartTime = 8000, Duration = 1000 };
            map.HitObjects.Add(spinner);
            map.HitObjects = map.HitObjects.OrderBy(h => h.StartTime).ToList();
            var mod = new OsuModGlitch { BakedTiming = { Value = false }, Amount = { Value = 100 } };
            mod.ApplyToBeatmap(map);
            double end = slider.GetEndTime();
            Assert.That(mod.RatePlan.Segments.Any(s => s.Start == slider.StartTime && s.End >= end), Is.True);
            foreach (var s in mod.RatePlan.Segments)
            {
                Assert.That(s.Start > slider.StartTime && s.Start < end, Is.False);
                Assert.That(s.End > slider.StartTime && s.End < end, Is.False);
                Assert.That(s.Start < 9000 && s.End > 8000, Is.False);
            }
            Assert.That(slider.RepeatCount, Is.EqualTo(3));
            Assert.That(slider.StartTime, Is.EqualTo(2000));
        }

        [TestCase(FlatlineDirection.Harder)]
        [TestCase(FlatlineDirection.Easier)]
        [TestCase(FlatlineDirection.Both)]
        public void TestFlatlineUsesNativeSkillsAndRespectsDirection(FlatlineDirection direction)
        {
            var ruleset = new OsuRuleset();
            var raw = difficultyMap();
            var working = new FlatWorkingBeatmap(raw);
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo);
            var mod = new OsuModFlatline { BakedTiming = { Value = false }, Direction = { Value = direction } };
            var originals = playable.HitObjects.Select(h => (h.StartTime, h.GetEndTime())).ToArray();
            mod.ApplyToBeatmap(playable);
            Assert.That(mod.RatePlan.Segments, Is.Not.Empty);
            Assert.That(playable.HitObjects.Select(h => (h.StartTime, h.GetEndTime())), Is.EqualTo(originals));
            Assert.That(mod.RatePlan.Segments.All(s => s.Rate >= 0.5 && s.Rate <= 3), Is.True);
            if (direction == FlatlineDirection.Harder)
            {
                Assert.That(mod.RatePlan.Segments.All(s => s.Rate >= 1), Is.True);
                Assert.That(mod.RatePlan.Segments.Any(s => s.Rate > 1), Is.True);
            }
            if (direction == FlatlineDirection.Easier)
            {
                Assert.That(mod.RatePlan.Segments.All(s => s.Rate <= 1), Is.True);
                Assert.That(mod.RatePlan.Segments.Any(s => s.Rate < 1), Is.True);
            }
            if (direction == FlatlineDirection.Both)
            {
                Assert.That(mod.RatePlan.Segments.Any(s => s.Rate < 1), Is.True);
                Assert.That(mod.RatePlan.Segments.Any(s => s.Rate > 1), Is.True);
            }
            Assert.That(mod.ApplyToRate(-1000), Is.EqualTo(1));
            Assert.That(mod.ApplyToRate(100000), Is.EqualTo(1));
            var expected = mod.RatePlan.Segments.ToArray();
            mod.ApplyToBeatmap(playable);
            Assert.That(mod.RatePlan.Segments, Is.EqualTo(expected));
            TestContext.Out.WriteLine($"{direction}: {expected.Length} segments, {expected.Min(s => s.Rate):F3}–{expected.Max(s => s.Rate):F3}x");
        }

        [Test]
        public void TestTrackTempoWithoutPitchAndSeekReset()
        {
            var mod = new OsuModGlitch { Amount = { Value = 100 } };
            mod.ApplyToBeatmap(circleMap());
            var track = new Mock<IAdjustableAudioComponent>(MockBehavior.Strict);
            IBindable<double>? speed = null;
            track.Setup(t => t.AddAdjustment(AdjustableProperty.Tempo, It.IsAny<IBindable<double>>()))
                 .Callback<AdjustableProperty, IBindable<double>>((_, value) => speed = value);
            mod.ApplyToTrack(track.Object);
            Assert.That(speed!.Value, Is.EqualTo(1));
            var segment = mod.RatePlan.Segments[0];
            mod.UpdateRate(segment.Start + 1);
            Assert.That(speed.Value, Is.EqualTo(segment.Rate));
            mod.UpdateRate(-1000);
            Assert.That(speed.Value, Is.EqualTo(1));
            mod.UpdateRate(segment.Start + 1);
            Assert.That(speed.Value, Is.EqualTo(segment.Rate));
            mod.ApplyToSample(new Mock<IAdjustableAudioComponent>(MockBehavior.Strict).Object);
            track.VerifyAll();
        }

        [Test]
        public void TestSafeAudioBindingSurvivesGarbageCollection()
        {
            var mod = new OsuModGlitch { Amount = { Value = 100 } };
            mod.ApplyToBeatmap(circleMap());
            var track = new Mock<IAdjustableAudioComponent>();
            IBindable<double>? propagatedSpeed = null;
            track.Setup(t => t.AddAdjustment(AdjustableProperty.Tempo, It.IsAny<IBindable<double>>()))
                 .Callback<AdjustableProperty, IBindable<double>>((_, value) => propagatedSpeed = value);
            attachTemporarySafeWrapper(mod, track.Object);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var segment = mod.RatePlan.Segments[0];
            mod.UpdateRate(segment.Start + 1);
            Assert.That(propagatedSpeed!.Value, Is.EqualTo(segment.Rate));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void attachTemporarySafeWrapper(ModSegmentRate mod, IAdjustableAudioComponent component)
            => mod.ApplyToTrack(new SafeRateAdjustableAudioComponent(component));

        [Test]
        public void TestCustomIconsAreEmbeddedAndAssigned()
        {
            var assembly = typeof(ModGlitch).Assembly;
            foreach (var name in new[] { "aim-assist", "anti-aim-assist", "static-bpm", "target-difficulty", "malevich" })
            {
                string resource = $"osu.Game.Resources.Textures.Icons.Mods.mod-mosu-{name}.png";
                using var stream = assembly.GetManifestResourceStream(resource);
                Assert.That(stream, Is.Not.Null, resource);
                using var reader = new BinaryReader(stream!);
                Assert.That(reader.ReadBytes(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
            }
            Mod[] mods = { new OsuModMosuAimAssist(), new OsuModMosuAntiAimAssist(), new OsuModMosuStaticBpm(), new OsuModMosuTargetDifficulty(), new OsuModMosuMalevich() };
            Assert.That(mods.All(m => m.Icon != null), Is.True);
            Assert.That(mods.Select(m => m.Icon!.Value).Distinct().Count(), Is.EqualTo(5));
        }

        [TestCaseSource(nameof(Rulesets))]
        public void TestNativeBeatmapPipelineForEveryRuleset(Ruleset ruleset)
        {
            IBeatmap raw;
            switch (ruleset)
            {
                case ManiaRuleset:
                    var mania = new Beatmap<ManiaHitObject>();
                    mania.Difficulty.CircleSize = 4;
                    for (int i = 0; i < 80; i++)
                        mania.HitObjects.Add(new Note { StartTime = 1000 + i * (i < 30 ? 200 : 80), Column = i % 4 });
                    mania.HitObjects = mania.HitObjects.OrderBy(h => h.StartTime).ToList();
                    raw = mania;
                    break;
                case CatchRuleset:
                    var catchMap = new Beatmap<CatchHitObject>();
                    for (int i = 0; i < 80; i++)
                        catchMap.HitObjects.Add(new Fruit { StartTime = 1000 + i * 150, X = 40 + i % 3 * 200 });
                    raw = catchMap;
                    break;
                case TaikoRuleset:
                    var taiko = new Beatmap<TaikoHitObject>();
                    for (int i = 0; i < 80; i++)
                        taiko.HitObjects.Add(new Hit { StartTime = 1000 + i * 100, Type = i % 3 == 0 ? HitType.Rim : HitType.Centre });
                    raw = taiko;
                    break;
                default:
                    raw = difficultyMap();
                    break;
            }
            raw.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
            raw.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            var working = new FlatWorkingBeatmap(raw);
            foreach (var mod in ruleset.AllMods.OfType<ModSegmentRate>())
            {
                var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, new Mod[] { mod });
                Assert.That(playable.HitObjects.Count, Is.GreaterThan(1));
                Assert.That(mod.RatePlan.Segments, Is.Not.Empty, $"{ruleset.ShortName}/{mod.Acronym}");
                var calculator = ruleset.CreateDifficultyCalculator(working);
                var samples = calculator.CalculateLocalDifficulty(playable);
                Assert.That(samples, Is.Not.Empty);
                Assert.That(samples.All(s => double.IsFinite(s.Difficulty) && s.Difficulty >= 0), Is.True);
                TestContext.Out.WriteLine($"{ruleset.ShortName}/{mod.Acronym}: {mod.RatePlan.Segments.Count} segments, {samples.Count} native samples");
            }
        }

        [Test]
        public void TestMissingTimingPointsAndNegativeTimes()
        {
            var map = circleMap();
            map.ControlPointInfo.Clear();
            var mod = new OsuModGlitch { Amount = { Value = 100 } };
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments, Is.Not.Empty);
            foreach (var obj in map.HitObjects)
                obj.StartTime -= 5000;
            map.ControlPointInfo.Add(1000, new TimingControlPoint { BeatLength = 500 });
            mod.ApplyToBeatmap(map);
            Assert.That(mod.RatePlan.Segments.Any(s => s.Start < 0), Is.True);
        }

        [Test]
        public void TestRussianStringsUseRuntimeResolver()
        {
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            manager.AddLanguage("en", new ResourceManagerLocalisationStore("en"));
            manager.AddLanguage("ru", new ResourceManagerLocalisationStore("ru"));
            config.SetValue(FrameworkSetting.Locale, "ru");
            Assert.That(manager.GetLocalisedString(SegmentRateModsStrings.Direction), Is.EqualTo("Направление выравнивания"));
            Assert.That(manager.GetLocalisedString(SegmentRateModsStrings.GlitchDescription), Does.StartWith("Синхронно"));
            Assert.That(manager.GetLocalisedString(SegmentRateModsStrings.Both), Is.EqualTo("В обе стороны"));
            Assert.That(manager.GetLocalisedString(SegmentRateModsStrings.BakedTiming), Is.EqualTo("Постоянный AR"));
            string hint = manager.GetLocalisedString(SegmentRateModsStrings.BakedTimingHint);
            Assert.That(hint, Does.StartWith("Сохранять эффективный AR"));
            Assert.That(hint, Does.Not.Contain("скрипт"));
        }

        private static Beatmap<OsuHitObject> circleMap()
        {
            var map = new Beatmap<OsuHitObject>();
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 80; i++)
                map.HitObjects.Add(new HitCircle { StartTime = 1000 + i * 125, Position = new Vector2(100 + i % 2 * 150, 100) });
            return map;
        }

        private static Beatmap<OsuHitObject> difficultyMap()
        {
            var map = new Beatmap<OsuHitObject>();
            map.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            for (int i = 0; i < 32; i++)
                map.HitObjects.Add(new HitCircle { StartTime = 1000 + i * 250, Position = new Vector2(100 + i % 2 * 30, 100) });
            for (int i = 0; i < 96; i++)
                map.HitObjects.Add(new HitCircle { StartTime = 9000 + i * 80, Position = new Vector2(70 + i % 2 * 350, 100 + i % 3 * 80) });
            return map;
        }

        private sealed class AdjustableTestClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public bool Seek(double position) { CurrentTime = position; return true; }
            public void Reset() { Stop(); CurrentTime = 0; }
            public void ResetSpeedAdjustments() => Rate = 1;
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;
            public TestConfig() : base(null!) { }
        }
    }
}
