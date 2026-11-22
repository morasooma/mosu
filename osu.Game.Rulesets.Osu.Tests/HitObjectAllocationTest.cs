using System;
using System.Linq;
using System.IO;
using System.Text;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Framework.Bindables;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class HitObjectAllocationTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";
        protected override Ruleset CreateRuleset() => new OsuRuleset();
        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuRuleset().CreateDifficultyCalculator(beatmap);

        [TestCase("none")]
        [TestCase("DT")]
        [TestCase("HR")]
        [TestCase("RX")]
        public void TestPlayableAllocation(string kind)
        {
            var working = GetBeatmap("801165");
            var ruleset = new OsuRuleset().RulesetInfo;
            Mod[] mods = kind switch
            {
                "DT" => new Mod[] { new OsuModDoubleTime() },
                "HR" => new Mod[] { new OsuModHardRock() },
                "RX" => new Mod[] { new OsuModMosuRelax() },
                _ => Array.Empty<Mod>(),
            };
            _ = working.GetPlayableBeatmap(ruleset, mods);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
            {
                var playable = working.GetPlayableBeatmap(ruleset, mods);
                GC.KeepAlive(playable);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"PLAYABLE_ALLOC kind={kind} map=801165 calls=4 bytes={bytes}");
            Assert.That(bytes, Is.LessThan(kind == "HR" ? 29_000_000 : 23_000_000));
        }

        [TestCase("none")]
        [TestCase("DT")]
        [TestCase("HR")]
        [TestCase("RX")]
        public void TestLongPlayableRetainsExactValuesAcrossGameplayBindingPreparation(string kind)
        {
            // 12,000 source objects over 20 minutes, including repeated sliders. No
            // files/network/audio. The same playable graph is calculated before and
            // after public bindings are materialised, not a different-map comparison.
            var text = new StringBuilder("osu file format v14\n[General]\nMode:0\n[Metadata]\nTitle:Allocation probe\nArtist:Test\nCreator:Test\nVersion:Long\n[Difficulty]\nHPDrainRate:5\nCircleSize:4\nOverallDifficulty:8\nApproachRate:9\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,2,1,100,1,0\n[HitObjects]\n");
            for (int i = 0; i < 12000; i++)
            {
                int x = 128 + i % 3 * 64;
                int time = 1000 + i * 100;
                text.Append(i % 4 == 0 ? $"{x},192,{time},2,0,L|{x + 200}:192,2,200\n" : $"{x},192,{time},1,0\n");
            }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
            using var reader = new LineBufferedReader(stream);
            var decoder = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader);
            ((LegacyBeatmapDecoder)decoder).ApplyOffsets = false;
            var decoded = decoder.Decode(reader);
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(decoded);
            Mod[] mods = kind switch
            {
                "DT" => new Mod[] { new OsuModDoubleTime() },
                "HR" => new Mod[] { new OsuModHardRock() },
                "RX" => new Mod[] { new OsuModMosuRelax() },
                _ => Array.Empty<Mod>(),
            };
            long before = GC.GetAllocatedBytesForCurrentThread();
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(playable.HitObjects.Count, Is.EqualTo(12000));
            Assert.That(playable.HitObjects.Last().StartTime, Is.GreaterThan(10 * 60 * 1000));
            var objects = playable.HitObjects.Cast<OsuHitObject>().SelectMany(h => new[] { h }.Concat(h.NestedHitObjects.Cast<OsuHitObject>())).ToArray();
            var values = objects.Select(h => (h.StartTime, h.StackHeight, h.StackedPosition, h.ComboIndex, h.ComboIndexWithOffsets, h.IndexInCurrentCombo)).ToArray();
            var score = ruleset.CreateScoreProcessor();
            score.Mods.Value = mods;
            score.ApplyBeatmap(playable);
            var stats = score.MaximumStatistics.ToArray();
            int maxCombo = score.MaximumCombo;
            foreach (var h in playable.HitObjects.Cast<OsuHitObject>()) h.PrepareGameplayBindings();
            Assert.That(objects.Select(h => (h.StartTime, h.StackHeight, h.StackedPosition, h.ComboIndex, h.ComboIndexWithOffsets, h.IndexInCurrentCombo)).ToArray(), Is.EqualTo(values));
            var preparedScore = ruleset.CreateScoreProcessor();
            preparedScore.Mods.Value = mods;
            preparedScore.ApplyBeatmap(playable);
            Assert.That(preparedScore.MaximumCombo, Is.EqualTo(maxCombo));
            Assert.That(preparedScore.MaximumStatistics.ToArray(), Is.EqualTo(stats));
            Assert.That(preparedScore.MaximumTotalScore, Is.EqualTo(score.MaximumTotalScore));
            TestContext.Progress.WriteLine($"LONG_PLAYABLE kind={kind} durationMs={playable.HitObjects.Last().StartTime} source=12000 totalObjects={objects.Length} bytes={bytes} maxCombo={maxCombo} exactPreparationParity=true");
        }

        [Test]
        public void TestCircleConstructionAllocation()
        {
            _ = new HitCircle();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 30000; i++) GC.KeepAlive(new HitCircle());
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"CIRCLE_CONSTRUCTION count=30000 bytes={bytes}");
            Assert.That(bytes, Is.LessThan(18_000_000));
        }

        [Test]
        public void TestRepeatedLeafDefaultsAllocation()
        {
            var circle = new HitCircle();
            var controlPoints = new ControlPointInfo();
            var difficulty = new BeatmapDifficulty();
            circle.ApplyDefaults(controlPoints, difficulty);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 30000; i++) circle.ApplyDefaults(controlPoints, difficulty);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"LEAF_DEFAULTS calls=30000 bytes={bytes}");
            Assert.That(bytes, Is.Zero);
        }

        [Test]
        public void TestNestedStartTimeMovesOnceAfterRepeatedDefaults()
        {
            var slider = createSlider();
            var controlPoints = new ControlPointInfo();
            var difficulty = new BeatmapDifficulty();
            slider.ApplyDefaults(controlPoints, difficulty);
            slider.ApplyDefaults(controlPoints, difficulty);
            double[] times = slider.NestedHitObjects.Select(n => n.StartTime).ToArray();
            slider.StartTime += 123;
            for (int i = 0; i < times.Length; i++)
                Assert.That(slider.NestedHitObjects[i].StartTime, Is.EqualTo(times[i] + 123));
            slider.StartTimeBindable.Value -= 50;
            for (int i = 0; i < times.Length; i++)
                Assert.That(slider.NestedHitObjects[i].StartTime, Is.EqualTo(times[i] + 73));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestStackHeightPropagationBeforeAndAfterBindableExposure(bool exposeFirst)
        {
            var slider = createSlider();
            if (exposeFirst) _ = slider.StackHeightBindable;
            slider.StackHeight = 2;
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            Assert.That(slider.NestedHitObjects.Cast<OsuHitObject>().All(n => n.StackHeight == 2), Is.True);
            slider.StackHeight = 3;
            Assert.That(slider.NestedHitObjects.Cast<OsuHitObject>().All(n => n.StackHeight == 3), Is.True);
            Assert.That(slider.StackHeightBindable.Value, Is.EqualTo(3));
            slider.StackHeightBindable.Disabled = true;
            Assert.Throws<InvalidOperationException>(() => slider.StackHeight = 9);
            Assert.That(slider.NestedHitObjects.Cast<OsuHitObject>().All(n => n.StackHeight == 3), Is.True);
            slider.StackHeightBindable.Disabled = false;
            slider.StackHeightBindable.Value = -1;
            Assert.That(slider.NestedHitObjects.Cast<OsuHitObject>().All(n => n.StackHeight == -1), Is.True);
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            slider.StackHeightBindable.Value = 4;
            Assert.That(slider.NestedHitObjects.Cast<OsuHitObject>().All(n => n.StackHeight == 4), Is.True);
        }

        [TestCase("combo", false)]
        [TestCase("combo", true)]
        [TestCase("offsets", false)]
        [TestCase("offsets", true)]
        [TestCase("index", false)]
        [TestCase("index", true)]
        public void TestLazyNestedComboPreservesBidirectionalBindableBehaviour(string kind, bool exposeBeforeDefaults)
        {
            var slider = createSlider();
            Bindable<int>? parent = exposeBeforeDefaults ? comboBindable(slider, kind) : null;
            setCombo(slider, kind, 7);
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var nested = slider.NestedHitObjects.Cast<OsuHitObject>().ToArray();
            foreach (var n in nested) Assert.That(getCombo(n, kind), Is.EqualTo(7));
            setCombo(nested[0], kind, 8);
            Assert.That(getCombo(slider, kind), Is.EqualTo(8));
            foreach (var n in nested) Assert.That(getCombo(n, kind), Is.EqualTo(8));
            parent ??= comboBindable(slider, kind);
            var child = comboBindable(nested[0], kind);
            Assert.That(child, Is.Not.SameAs(parent));
            Assert.That(child, Is.SameAs(comboBindable(nested[0], kind)));
            int parentChanges = 0;
            int childChanges = 0;
            parent.ValueChanged += _ => parentChanges++;
            child.ValueChanged += _ => childChanges++;
            parent.Value = 9;
            child.Value = 10;
            Assert.That(parentChanges, Is.EqualTo(2));
            Assert.That(childChanges, Is.EqualTo(2));
            foreach (var n in nested) Assert.That(getCombo(n, kind), Is.EqualTo(10));
            child.Default = 11;
            Assert.That(parent.Default, Is.EqualTo(11));
            parent.Disabled = true;
            Assert.That(child.Disabled, Is.True);
            Assert.Throws<InvalidOperationException>(() => setCombo(nested[1], kind, 12));
            child.Disabled = false;
            Assert.That(parent.Disabled, Is.False);
            parent.Value = 13;
            Assert.That(getCombo(nested[1], kind), Is.EqualTo(13));
            child.UnbindFrom(parent);
            child.Value = 14;
            Assert.That(parent.Value, Is.EqualTo(13));
            Assert.That(getCombo(nested[0], kind), Is.EqualTo(14));
            // Getter must not silently re-bind explicitly unbound child state.
            Assert.That(comboBindable(nested[0], kind).Value, Is.EqualTo(14));
            parent.Value = 15;
            Assert.That(child.Value, Is.EqualTo(14));
        }

        [TestCase("combo")]
        [TestCase("offsets")]
        [TestCase("index")]
        public void TestChildUnbindAllDetachesOnlyThatChild(string kind)
        {
            var slider = createSlider();
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var children = slider.NestedHitObjects.Cast<OsuHitObject>().ToArray();
            setCombo(slider, kind, 7);
            var child = comboBindable(children[0], kind);
            child.UnbindAll();
            setCombo(slider, kind, 8);
            Assert.That(getCombo(children[0], kind), Is.EqualTo(7));
            Assert.That(getCombo(children[1], kind), Is.EqualTo(8));
            setCombo(children[0], kind, 9);
            Assert.That(getCombo(slider, kind), Is.EqualTo(8));
        }

        [Test]
        public void TestCustomNestedComboRetainsEagerVirtualAccessors()
        {
            var slider = new CustomNestedSlider
            {
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(300, 0) }),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            Assert.That(slider.CustomChild.ComboIndex, Is.Zero);
            Assert.That(slider.CustomChild.ComboGetterCalls, Is.GreaterThan(0));
            slider.ComboIndex = 42;
            Assert.That(slider.CustomChild.ComboIndex, Is.EqualTo(42));
            slider.CustomChild.ComboIndexBindable.Value = 43;
            Assert.That(slider.ComboIndex, Is.EqualTo(43));
        }

        private sealed class CustomNestedSlider : Slider
        {
            public readonly CustomNestedCircle CustomChild = new CustomNestedCircle();
            protected override void CreateNestedHitObjects(System.Threading.CancellationToken cancellationToken)
            {
                base.CreateNestedHitObjects(cancellationToken);
                AddNested(CustomChild);
            }
        }

        private sealed class CustomNestedCircle : HitCircle
        {
            public int ComboGetterCalls;
            public override int ComboIndex
            {
                get { ComboGetterCalls++; return base.ComboIndex; }
                set => base.ComboIndex = value;
            }
        }

        [Test]
        public void TestGameplayPreparationMaterialisesOnceBeforeDrawableAcquisition()
        {
            var slider = createSlider();
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var beatmap = new osu.Game.Rulesets.Osu.Beatmaps.OsuBeatmap { HitObjects = { slider } };
            using var drawable = new OsuRuleset().CreateDrawableRulesetWith(beatmap, Array.Empty<Mod>());
            // Do not load/render/run the ruleset or play audio. Constructor is the
            // background Player boundary where calculation-only state is materialised.
            var children = slider.NestedHitObjects.Cast<OsuHitObject>().ToArray();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int repeat = 0; repeat < 1000; repeat++)
            {
                foreach (var child in children)
                {
                    _ = child.ComboIndexBindable;
                    _ = child.ComboIndexWithOffsetsBindable;
                    _ = child.IndexInCurrentComboBindable;
                    _ = child.StackHeightBindable;
                }
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            // Materialised child retains only the original weak bindable links, not
            // a strong holder reference to the parent's state after gameplay setup.
            var holder = typeof(OsuHitObject).GetField("sharedComboInformation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            foreach (var child in children) Assert.That(holder.GetValue(child), Is.Null);
            slider.ComboIndexBindable.UnbindBindings();
            slider.ComboIndex = 42;
            foreach (var child in slider.NestedHitObjects.Cast<OsuHitObject>()) Assert.That(child.ComboIndex, Is.Not.EqualTo(42));
        }

        [TestCase("combo")]
        [TestCase("offsets")]
        [TestCase("index")]
        public void TestParentUnbindAlsoDetachesPreviouslyUnobservedChildren(string kind)
        {
            var slider = createSlider();
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var children = slider.NestedHitObjects.Cast<OsuHitObject>().ToArray();
            setCombo(slider, kind, 7);
            // Exposing parent materialises the full original graph before unbinding.
            var parent = comboBindable(slider, kind);
            parent.UnbindBindings();
            parent.Value = 8;
            foreach (var child in children) Assert.That(getCombo(child, kind), Is.EqualTo(7));
            setCombo(children[0], kind, 9);
            Assert.That(parent.Value, Is.EqualTo(8));
            Assert.That(getCombo(children[1], kind), Is.EqualTo(7));
        }

        [Test]
        public void TestRepeatedDefaultsKeepOldAndNewChildrenInSyncWithoutParentReferences()
        {
            var slider = createSlider();
            var controlPoints = new ControlPointInfo();
            var difficulty = new BeatmapDifficulty();
            slider.ApplyDefaults(controlPoints, difficulty);
            var oldChild = (OsuHitObject)slider.NestedHitObjects[0];
            var oldBindable = oldChild.ComboIndexBindable;
            slider.ApplyDefaults(controlPoints, difficulty);
            var newChild = (OsuHitObject)slider.NestedHitObjects[0];
            Assert.That(newChild, Is.Not.SameAs(oldChild));
            slider.ComboIndex = 10;
            Assert.That(oldBindable.Value, Is.EqualTo(10));
            Assert.That(newChild.ComboIndex, Is.EqualTo(10));
            newChild.ComboIndex = 11;
            Assert.That(slider.ComboIndex, Is.EqualTo(11));
            Assert.That(oldBindable.Value, Is.EqualTo(11));
        }

        [Test]
        public void TestCustomSliderRetainsOriginalEagerBindingPath()
        {
            var slider = new CustomSlider
            {
                StartTime = 1000,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(300, 0) }),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            Assert.That(slider.ParentGetterCalls, Is.GreaterThan(0));
            Assert.That(slider.NestedGetterCalls, Is.EqualTo(slider.NestedHitObjects.Count));
            slider.ComboIndexBindable.Value = 42;
            Assert.That(slider.NestedHitObjects.Cast<OsuHitObject>().All(n => n.ComboIndex == 42), Is.True);
        }

        private sealed class CustomSlider : Slider
        {
            public int ParentGetterCalls;
            public int NestedGetterCalls;
            protected override void BindNestedComboInformation(IHasComboInformation nested, IHasComboInformation parent)
            {
                _ = parent.ComboIndexBindable;
                ParentGetterCalls++;
                _ = nested.ComboIndexBindable;
                NestedGetterCalls++;
                base.BindNestedComboInformation(nested, parent);
            }
        }

        private static Bindable<int> comboBindable(OsuHitObject obj, string kind) => kind switch
        {
            "combo" => obj.ComboIndexBindable,
            "offsets" => obj.ComboIndexWithOffsetsBindable,
            _ => obj.IndexInCurrentComboBindable,
        };

        private static int getCombo(OsuHitObject obj, string kind) => kind switch
        {
            "combo" => obj.ComboIndex,
            "offsets" => obj.ComboIndexWithOffsets,
            _ => obj.IndexInCurrentCombo,
        };

        private static void setCombo(OsuHitObject obj, string kind, int value)
        {
            switch (kind)
            {
                case "combo": obj.ComboIndex = value; break;
                case "offsets": obj.ComboIndexWithOffsets = value; break;
                default: obj.IndexInCurrentCombo = value; break;
            }
        }

        private static Slider createSlider() => new Slider
        {
            StartTime = 1000,
            RepeatCount = 1,
            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(300, 0) }),
        };
    }
}
