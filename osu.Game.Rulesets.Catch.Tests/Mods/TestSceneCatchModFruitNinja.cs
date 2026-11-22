using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Skinning;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Catch.Skinning.Legacy;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Scoring;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Catch.Mods;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Objects.Drawables;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Catch.Tests.Mods
{
    public partial class TestSceneCatchModFruitNinja : OsuManualInputManagerTestScene
    {
        private DrawableFruitNinjaRuleset ruleset = null!;
        private ManualClock clock = null!;
        private readonly List<HitResult> results = new List<HitResult>();
        private FruitNinjaPlayfield playfield => (FruitNinjaPlayfield)ruleset.Playfield;
        private FruitNinjaFruit target => (FruitNinjaFruit)ruleset.Beatmap.HitObjects[0];
        private float noteY => target.PositionAt(target.StartTime).Y;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private void create(bool bomb = false, ISkin? skin = null)
        {
            results.Clear();
            clock = new ManualClock { CurrentTime = 1000 };
            var beatmap = new Beatmap<CatchHitObject>
            {
                HitObjects = new List<CatchHitObject>
                {
                    new Fruit { StartTime = 2000, X = 256 },
                },
            };
            foreach (var h in beatmap.HitObjects)
                h.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            var mod = new CatchModFruitNinja();
            mod.ApplyToBeatmap(beatmap);
            ((FruitNinjaFruit)beatmap.HitObjects[0]).IsBomb = bomb;
            ruleset = new DrawableFruitNinjaRuleset(new CatchRuleset(), beatmap, new Mod[] { mod })
            {
                Clock = new FramedClock(clock),
                ProcessCustomClock = true,
                RelativeSizeAxes = Axes.Both,
            };
            Child = skin == null ? ruleset : new SkinProvidingContainer(new osu.Game.Rulesets.Catch.Skinning.FruitNinjaSkinTransformer(
                new CatchLegacySkinTransformer(skin), new OsuLegacySkinTransformer(skin))) { Child = ruleset };
            ruleset.NewResult += r => results.Add(r.Type);
        }

        [Test]
        public void TestWorkingBeatmapConversion()
        {
            AddStep("convert through actual playable beatmap pipeline", () =>
            {
                var catchRuleset = new CatchRuleset();
                var source = new Beatmap
                {
                    BeatmapInfo = { Ruleset = catchRuleset.RulesetInfo },
                    HitObjects = { new Fruit { StartTime = 2000, X = 100, NewCombo = true } },
                };
                var working = CreateWorkingBeatmap(source);
                var mods = new Mod[] { new CatchModFruitNinja() };
                var playable = working.GetPlayableBeatmap(catchRuleset.RulesetInfo, mods);
                Assert.That(playable.HitObjects.OfType<FruitNinjaFruit>().Count(), Is.EqualTo(2));
                Assert.That(source.HitObjects[0].StartTime, Is.EqualTo(2000));
                Child = ruleset = (DrawableFruitNinjaRuleset)catchRuleset.CreateDrawableRulesetWith(playable, mods);
            });
            AddUntilStep("converted ruleset loaded", () => ruleset.IsLoaded);
        }

        [TestCase(0, false)]
        [TestCase(5, false)]
        [TestCase(10, false)]
        [TestCase(11, true)]
        public void TestApproachRateThroughPlayableBeatmap(float ar, bool doubleTime)
        {
            AddStep("convert with AR mods through playable pipeline", () =>
            {
                var catchRuleset = new CatchRuleset();
                var source = new Beatmap
                {
                    BeatmapInfo = { Ruleset = catchRuleset.RulesetInfo },
                    Difficulty = new BeatmapDifficulty { ApproachRate = 5 },
                    HitObjects = { new Fruit { StartTime = 5000, X = 256, NewCombo = true } },
                };
                var adjust = new CatchModDifficultyAdjust();
                adjust.ApproachRate.Value = ar;
                Mod[] mods = doubleTime
                    ? new Mod[] { adjust, new CatchModDoubleTime(), new CatchModFruitNinja() }
                    : new Mod[] { adjust, new CatchModFruitNinja() };
                var working = CreateWorkingBeatmap(source);
                var playable = working.GetPlayableBeatmap(catchRuleset.RulesetInfo, mods);
                double expected = IBeatmapDifficultyInfo.DifficultyRangeInt(ar, CatchHitObject.PREEMPT_RANGE);
                foreach (var fruit in playable.HitObjects.Cast<FruitNinjaFruit>())
                {
                    Assert.That(fruit.TimePreempt, Is.EqualTo(expected));
                    Assert.That(fruit.TimePreempt, Is.EqualTo(fruit.SourceObject!.TimePreempt));
                    Assert.That(fruit.LaunchTime, Is.EqualTo(fruit.StartTime - expected));
                    Assert.That(fruit.SliceResultAt(fruit.ApexTime), Is.EqualTo(HitResult.Meh));
                }
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestStarsUnchanged(bool doubleTime)
        {
            AddStep("compare actual Catch difficulty calculations", () =>
            {
                var catchRuleset = new CatchRuleset();
                var source = new Beatmap { BeatmapInfo = { Ruleset = catchRuleset.RulesetInfo } };
                for (int i = 0; i < 20; i++)
                    source.HitObjects.Add(new Fruit { StartTime = 1000 + i * 250, X = i % 2 == 0 ? 80 : 430, NewCombo = i % 4 == 0 });
                source.HitObjects.Add(new JuiceStream
                {
                    StartTime = 7000,
                    X = 100,
                    Path = new osu.Game.Rulesets.Objects.SliderPath(osu.Game.Rulesets.Objects.Types.PathType.LINEAR,
                        new[] { Vector2.Zero, new Vector2(300, 0) }),
                });
                var working = CreateWorkingBeatmap(source);
                Mod[] baselineMods = doubleTime ? new Mod[] { new CatchModDoubleTime() } : System.Array.Empty<Mod>();
                var baseline = catchRuleset.CreateDifficultyCalculator(working).Calculate(baselineMods);
                var ninja = catchRuleset.CreateDifficultyCalculator(working).Calculate(baselineMods.Append(new CatchModFruitNinja()));
                Assert.That(ninja.StarRating, Is.EqualTo(baseline.StarRating).Within(1e-10));
                Assert.That(ninja.MaxCombo, Is.EqualTo(baseline.MaxCombo));
                Assert.That(ninja.Mods.OfType<CatchModFruitNinja>().Count(), Is.EqualTo(1));
            });
        }

        [TestCase(-1100, HitResult.Meh)]
        [TestCase(-75, HitResult.Ok)]
        [TestCase(75, HitResult.Ok)]
        [TestCase(125, HitResult.Meh)]
        [TestCase(-550, HitResult.Meh)]
        [TestCase(-300, HitResult.Meh)]
        [TestCase(300, HitResult.Meh)]
        [TestCase(550, HitResult.Meh)]
        [TestCase(600, HitResult.Meh)]
        [TestCase(1100, HitResult.Meh)]
        public void TestOffBeatSwipe(double offset, HitResult expected)
        {
            AddStep("create timing target", () => create());
            AddStep("advance before slice", () => clock.CurrentTime = 2000 + offset - 10);
            AddWaitStep("target updated", 3);
            AddStep("position blade and hold", () =>
            {
                float y = ((FruitNinjaFruit)ruleset.Beatmap.HitObjects[0]).PositionAt(2000 + offset).Y;
                InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(180, y)));
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("establish held blade", () => clock.CurrentTime = 2000 + offset - 5);
            AddWaitStep("held blade updated", 2);
            AddStep("swipe off beat", () =>
            {
                float y = ((FruitNinjaFruit)ruleset.Beatmap.HitObjects[0]).PositionAt(2000 + offset).Y;
                InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(330, y)));
                clock.CurrentTime = 2000 + offset;
            });
            AddUntilStep("timing grade applied", () => results.SequenceEqual(new[] { expected }));
            AddStep("release blade", () => InputManager.ReleaseButton(MouseButton.Left));
            AddStep("advance beyond flight end", () => clock.CurrentTime = target.FlightEndTime + 100);
            AddWaitStep("expired fruit processed", 3);
            AddAssert("cut fruit never becomes a later miss", () => results.SequenceEqual(new[] { expected }));
        }

        [Test]
        public void TestFlightAndMiss()
        {
            AddStep("create fruit", () => create());
            AddStep("advance to ascent", () => clock.CurrentTime = 1700);
            AddUntilStep("fruit visible", () => playfield.AllHitObjects.OfType<DrawableFruitNinjaFruit>().Any());
            AddAssert("fruit risen above bottom", () => playfield.AllHitObjects.Single().Y < 420);
            AddAssert("not judged at launch", () => results.Count == 0);
            AddAssert("no approach rings", () => !playfield.ChildrenOfType<osu.Framework.Graphics.UserInterface.CircularProgress>().Any());
            AddStep("advance to note on ascent", () => clock.CurrentTime = target.StartTime);
            AddWaitStep("update note position", 3);
            AddAssert("note below apex and still rising", () => playfield.AllHitObjects.Single().Y > FruitNinjaFruit.SLICE_Y
                && target.PositionAt(target.StartTime + 1).Y < target.PositionAt(target.StartTime).Y);
            AddStep("advance to late apex", () => clock.CurrentTime = target.ApexTime);
            AddWaitStep("update apex", 3);
            AddAssert("apex is late", () => System.Math.Abs(playfield.AllHitObjects.Single().Y - FruitNinjaFruit.SLICE_Y) < 0.01
                && target.SliceResultAt(clock.CurrentTime) == HitResult.Meh);
            AddAssert("no horizontal guide", () => !playfield.ChildrenOfType<osu.Framework.Graphics.Shapes.Box>()
                .Any(b => b.Width == CatchPlayfield.WIDTH && b.Height == 1));
            AddAssert("not judged at apex", () => results.Count == 0);
            AddStep("let fruit fall", () => clock.CurrentTime = target.FlightEndTime + 1);
            AddUntilStep("miss recorded", () => results.SequenceEqual(new[] { HitResult.Miss }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSwipe(bool bomb)
        {
            AddStep("create target", () => create(bomb));
            AddStep("advance to note on ascent", () => clock.CurrentTime = 2000);
            AddWaitStep("load target", 3);
            AddStep("move blade left", () => InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(180, noteY))));
            AddStep("hold blade", () => InputManager.PressButton(MouseButton.Left));
            AddStep("establish held frame", () => clock.CurrentTime = 2001);
            AddWaitStep("blade held", 2);
            AddStep("fast swipe through target", () =>
            {
                InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(330, noteY)));
                clock.CurrentTime = 2010;
            });
            AddUntilStep("correct slice judgement", () => results.SequenceEqual(new[] { bomb ? HitResult.ComboBreak : HitResult.Great }));
            AddStep("release blade", () => InputManager.ReleaseButton(MouseButton.Left));
            AddStep("advance effects", () => clock.CurrentTime = 2300);
            AddWaitStep("effects update without errors", 3);
        }

        [Test]
        public void TestStationaryBladeDoesNotSlice()
        {
            AddStep("create fruit", () => create());
            AddStep("place blade at note position and hold", () =>
            {
                InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(256, noteY)));
                InputManager.PressButton(MouseButton.Left);
                clock.CurrentTime = 1990;
            });
            AddStep("fruit reaches note time with stationary blade", () => clock.CurrentTime = 2000);
            AddWaitStep("update stationary blade", 3);
            AddAssert("no passive hit", () => results.Count == 0);
            AddStep("release blade", () => InputManager.ReleaseButton(MouseButton.Left));
            AddStep("let fruit fall", () => clock.CurrentTime = target.FlightEndTime + 1);
            AddUntilStep("miss instead of passive catch", () => results.SequenceEqual(new[] { HitResult.Miss }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReplaySwipe(bool bomb)
        {
            AddStep("create replay target", () => create(bomb));
            AddStep("attach two-dimensional replay", () =>
            {
                float y = noteY;
                ruleset.SetReplayScore(new Score
                {
                    Replay = new Replay
                    {
                        Frames =
                        {
                            new CatchReplayFrame(1000, 180) { BladeY = y },
                            new CatchReplayFrame(1990, 180, true) { BladeY = y },
                            new CatchReplayFrame(2000, 180, true) { BladeY = y },
                            new CatchReplayFrame(2010, 330, true) { BladeY = y },
                            new CatchReplayFrame(2030, 330) { BladeY = y },
                        },
                    },
                });
            });
            AddStep("advance replay before swipe", () => clock.CurrentTime = 2000);
            AddWaitStep("replay held", 3);
            AddStep("advance replay across target", () => clock.CurrentTime = 2030);
            AddUntilStep("replay cuts correct target", () => results.SequenceEqual(new[] { bomb ? HitResult.ComboBreak : HitResult.Great }));
        }

        [Test]
        public void TestSkinCursorFruitAndHalves()
        {
            var skin = new NinjaTestSkin();
            float cursorWidth = 0;
            AddStep("provide distinctive skin textures", () =>
            {
                skin.Textures["cursor"] = renderer.CreateTexture(32, 32);
                skin.Textures["cursortrail"] = renderer.CreateTexture(16, 16);
                skin.Textures["fruit-pear"] = renderer.CreateTexture(40, 40);
                create(skin: skin);
                clock.CurrentTime = 2000;
            });
            AddUntilStep("skin fruit loaded", () => playfield.ChildrenOfType<LegacyCatchHitObjectPiece>().Any());
            AddAssert("skin fruit texture used", () => playfield.ChildrenOfType<LegacyCatchHitObjectPiece>()
                .SelectMany(p => p.ChildrenOfType<Sprite>()).Any(s => ReferenceEquals(s.Texture, skin.Textures["fruit-pear"])));
            AddAssert("skin cursor texture used", () => ruleset.ChildrenOfType<LegacyCursor>()
                .SelectMany(p => p.ChildrenOfType<Sprite>()).Any(s => ReferenceEquals(s.Texture, skin.Textures["cursor"])));
            AddAssert("skin trail loaded", () => ruleset.ChildrenOfType<OsuCursorContainer>().Single().ActiveTrailDrawableType == typeof(LegacyCursorTrail).FullName);
            AddAssert("cursor outside scaled playfield", () => playfield.Cursor!.Parent!.Parent == ruleset.Overlays);
            AddStep("move skin cursor", () => InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(123, 234))));
            AddWaitStep("cursor position updated", 2);
            AddAssert("skin cursor centred on blade", () =>
            {
                Vector2 actual = playfield.ToLocalSpace(playfield.Cursor!.ActiveCursor.ScreenSpaceDrawQuad.Centre);
                Assert.That(Vector2.Distance(actual, new Vector2(123, 234)), Is.LessThan(1), $"Cursor at {actual}; blade at {playfield.BladePosition}");
                return true;
            });
            AddStep("capture cursor size and resize arena", () =>
            {
                cursorWidth = ruleset.ChildrenOfType<LegacyCursor>().Single().ChildrenOfType<Sprite>().First(s => s.Texture != null).ScreenSpaceDrawQuad.Width;
                ruleset.Size = new Vector2(0.6f, 0.8f);
            });
            AddWaitStep("arena resized", 3);
            AddAssert("cursor size unaffected by arena scaling", () => System.Math.Abs(ruleset.ChildrenOfType<LegacyCursor>().Single()
                .ChildrenOfType<Sprite>().First(s => s.Texture != null).ScreenSpaceDrawQuad.Width - cursorWidth) < 0.01);
            AddStep("move cursor after resize", () => InputManager.MoveMouseTo(playfield.ToScreenSpace(new Vector2(321, 180))));
            AddWaitStep("resized cursor updated", 2);
            AddAssert("resized cursor still on blade", () => Vector2.Distance(
                playfield.ToLocalSpace(playfield.Cursor!.ActiveCursor.ScreenSpaceDrawQuad.Centre), new Vector2(321, 180)) < 1);
            AddStep("slice to create skin halves", () => playfield.ShowSlice((FruitNinjaFruit)ruleset.Beatmap.HitObjects[0], new Vector2(256, 120), osuTK.Graphics.Color4.White));
            AddWaitStep("halves loaded", 3);
            AddAssert("two halves created", () => playfield.ChildrenOfType<CaughtFruit>().Count() == 2);
            AddAssert("halves use skin fruit texture", () => playfield.ChildrenOfType<CaughtFruit>()
                .All(p => p.ChildrenOfType<Sprite>().Any(s => ReferenceEquals(s.Texture, skin.Textures["fruit-pear"]))));
        }

        private class NinjaTestSkin : ISkin
        {
            public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;
            public Texture? GetTexture(string name, WrapMode wrapModeS, WrapMode wrapModeT) => Textures.GetValueOrDefault(name);
            public ISample? GetSample(ISampleInfo sampleInfo) => null;
            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) where TLookup : notnull where TValue : notnull => null;
        }

        [Test]
        public void TestBombAvoidance()
        {
            AddStep("create bomb", () => create(true));
            AddStep("let bomb fall", () => clock.CurrentTime = target.FlightEndTime + 1);
            AddUntilStep("safe bomb ignored", () => results.SequenceEqual(new[] { HitResult.IgnoreHit }));
        }
    }
}
