using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneScrollingKeyCounter : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly InputCountController controller = new InputCountController();

        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };
        [Cached(typeof(IGameplayClock))]
        private readonly TestGameplayClock gameplayClock;
        private readonly Container clockContainer;
        private readonly TestTrigger reverseTrigger = new TestTrigger();
        private ScrollingKeyCounterDisplay display = null!;

        public TestSceneScrollingKeyCounter()
        {
            var triggers = new InputTrigger[]
            {
                new KeyCounterKeyboardTrigger(Key.Z),
                new KeyCounterKeyboardTrigger(Key.X),
                reverseTrigger,
            };

            gameplayClock = new TestGameplayClock(manualClock);
            foreach (var trigger in triggers)
                trigger.Clock = gameplayClock;
            Add(controller);
            AddRange(triggers);
            controller.AddRange(triggers);

            Add(clockContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = gameplayClock,
            });
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset display and clock", () =>
            {
                InputManager.ReleaseKey(Key.Z);
                InputManager.ReleaseKey(Key.X);
                reverseTrigger.Release();
                manualClock.CurrentTime = 0;
                manualClock.Rate = 1;
                clockContainer.Child = display = new ScrollingKeyCounterDisplay
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            });
            AddUntilStep("keys loaded", () => display.ChildrenOfType<ScrollingKeyCounter>().Count() == 3);
        }

        [Test]
        public void TestHoldReleaseAndExpiry()
        {
            seek(100);
            AddStep("hold Z", () => InputManager.PressKey(Key.Z));
            seek(600);
            AddAssert("hold grows from the key", () => bars(0).Single().Height, () => Is.EqualTo(45).Within(0.1));
            AddAssert("held bar stays attached", () => bars(0).Single().Y, () => Is.Zero);
            AddStep("release Z", () => InputManager.ReleaseKey(Key.Z));
            seek(1100);
            AddAssert("released bar scrolls upward", () => bars(0).Single().Y, () => Is.EqualTo(-45).Within(0.1));
            AddAssert("released duration stays fixed", () => bars(0).Single().Height, () => Is.EqualTo(45).Within(0.1));
            seek(2600);
            AddAssert("old press removed", () => bars(0), () => Is.Empty);
        }

        [TestCase(1.5)]
        [TestCase(0.75)]
        [TestCase(2.0)]
        public void TestPlaybackRateDoesNotChangeScrollSpeed(double rate)
        {
            AddStep("set playback rate", () => manualClock.Rate = rate);
            AddStep("hold Z", () => InputManager.PressKey(Key.Z));
            seek(500 * rate);
            AddAssert("half real second hold is 45px", () => bars(0).Single().Height, () => Is.EqualTo(45).Within(0.1));
            AddStep("release Z", () => InputManager.ReleaseKey(Key.Z));
            seek(1000 * rate);
            AddAssert("half real second scroll is 45px", () => bars(0).Single().Y, () => Is.EqualTo(-45).Within(0.1));
            AddStep("return to normal speed", () => manualClock.Rate = 1);
            AddAssert("old duration unchanged by rate toggle", () => bars(0).Single().Height, () => Is.EqualTo(45).Within(0.1));
            AddAssert("old position unchanged by rate toggle", () => bars(0).Single().Y, () => Is.EqualTo(-45).Within(0.1));
            seek(1000 * rate + 500);
            AddAssert("scroll continues at same real speed", () => bars(0).Single().Y, () => Is.EqualTo(-90).Within(0.1));
            AddWaitStep("pause clock", 3);
            AddAssert("pause freezes history", () => bars(0).Single().Y, () => Is.EqualTo(-90).Within(0.1));
            seek(1000 * rate + 1500);
            AddAssert("expires after two real seconds", () => bars(0), () => Is.Empty);
        }

        [Test]
        public void TestHeightAndSpeedAreIndependent()
        {
            AddStep("resize history vertically", () => display.Height = 405);
            AddAssert("width remains automatic, height manual", () => display.AutoSizeAxes, () => Is.EqualTo(Axes.X));
            AddUntilStep("all keys fill height", () => display.ChildrenOfType<ScrollingKeyCounter>().All(c => c.DrawHeight == 405));
            AddStep("hold Z", () => InputManager.PressKey(Key.Z));
            seek(500);
            AddAssert("resize does not alter speed", () => bars(0).Single().Height, () => Is.EqualTo(45).Within(0.1));
            AddStep("release Z", () => InputManager.ReleaseKey(Key.Z));
            seek(3000);
            AddAssert("taller history keeps older presses", () => bars(0).Single().Y, () => Is.EqualTo(-225).Within(0.1));
            AddStep("double scroll speed", () => display.ScrollSpeed.Value = 180);
            AddAssert("press expires at faster speed", () => bars(0), () => Is.Empty);
            AddStep("hold X at new speed", () => InputManager.PressKey(Key.X));
            seek(3500);
            AddAssert("duration uses new speed", () => bars(1).Single().Height, () => Is.EqualTo(90).Within(0.1));
            AddStep("release X", () => InputManager.ReleaseKey(Key.X));
            seek(4000);
            AddAssert("release moves at new speed", () => bars(1).Single().Y, () => Is.EqualTo(-90).Within(0.1));
            AddStep("slow down live", () => display.ScrollSpeed.Value = 45);
            AddAssert("existing bar adopts new speed", () => bars(1).Single().Y, () => Is.EqualTo(-22.5f).Within(0.1));
            AddStep("shrink below key height", () => display.Height = 20);
            AddAssert("zero history handled", () => bars(1), () => Is.Empty);
            AddStep("restore height", () => display.Height = 225);
            AddStep("tap after resize", () => InputManager.Key(Key.Z));
            AddAssert("pool still works", () => bars(0).Length, () => Is.EqualTo(1));
        }

        [Test]
        public void TestLoadedLayoutRoundTrip()
        {
            AddStep("set height and speed", () =>
            {
                display.Height = 385;
                display.ScrollSpeed.Value = 240;
            });
            AddStep("save and reload", () => clockContainer.Child = display = (ScrollingKeyCounterDisplay)display.CreateSerialisedInfo().CreateInstance());
            AddUntilStep("resized keys restored", () => display.ChildrenOfType<ScrollingKeyCounter>().Count(c => c.DrawHeight == 385) == 3);
            AddAssert("saved speed restored", () => display.ScrollSpeed.Value, () => Is.EqualTo(240));
            AddStep("hold key", () => InputManager.PressKey(Key.Z));
            seek(500);
            AddAssert("restored speed reaches keys", () => bars(0).Single().Height, () => Is.EqualTo(120).Within(0.1));
        }

        [Test]
        public void TestTapAndIndependentKeys()
        {
            AddStep("tap Z within one frame", () => InputManager.Key(Key.Z));
            AddAssert("short tap remains visible", () => bars(0).Single().Height, () => Is.EqualTo(3));
            AddStep("hold X", () => InputManager.PressKey(Key.X));
            seek(500);
            AddAssert("X grows independently", () => bars(1).Single().Height, () => Is.EqualTo(45).Within(0.1));
            AddAssert("Z tap is not extended", () => bars(0).Single().Height, () => Is.EqualTo(3));
            AddAssert("Z tap has scrolled", () => bars(0).Single().Y, () => Is.EqualTo(-45).Within(0.1));
        }

        [Test]
        public void TestLongHoldAndPausedClock()
        {
            AddStep("hold Z", () => InputManager.PressKey(Key.Z));
            seek(5000);
            AddAssert("long hold fills history", () => bars(0).Single().Height, () => Is.EqualTo(180));
            AddWaitStep("clock remains stopped", 3);
            AddAssert("stopped clock does not move history", () => bars(0).Single().Y, () => Is.Zero);
            AddStep("release Z", () => InputManager.ReleaseKey(Key.Z));
            seek(6000);
            AddAssert("long hold scrolls after release", () => bars(0).Single().Y, () => Is.EqualTo(-90).Within(0.1));
            seek(7000);
            AddAssert("long hold expires", () => bars(0), () => Is.Empty);
        }

        [Test]
        public void TestRewindClearsFutureHistory()
        {
            seek(1000);
            AddStep("tap Z", () => InputManager.Key(Key.Z));
            AddAssert("press recorded", () => bars(0).Length, () => Is.EqualTo(1));
            seek(500);
            AddAssert("future press removed", () => bars(0), () => Is.Empty);
            AddStep("hold Z", () => InputManager.PressKey(Key.Z));
            seek(700);
            seek(600);
            AddAssert("held key restarts at rewind position", () => bars(0).Single().Height, () => Is.EqualTo(3));
        }

        [Test]
        public void TestReversePlaybackDoesNotCreatePresses()
        {
            AddStep("reverse activation", () => reverseTrigger.Press(false));
            AddStep("reverse deactivation", () => reverseTrigger.Release(false));
            AddAssert("no phantom press", () => bars(2), () => Is.Empty);
        }

        [Test]
        public void TestRapidInputIsBoundedAndReused()
        {
            AddStep("many taps", () =>
            {
                for (int i = 0; i < 1000; i++)
                    InputManager.Key(Key.Z);
            });
            AddAssert("history is bounded", () => bars(0).Length, () => Is.EqualTo(256));
            seek(2000);
            AddAssert("all old taps removed", () => bars(0), () => Is.Empty);
            AddStep("tap after pool reuse", () => InputManager.Key(Key.Z));
            AddAssert("reused bar starts at key", () => bars(0).Single().Y, () => Is.Zero);
            AddAssert("reused bar has fresh duration", () => bars(0).Single().Height, () => Is.EqualTo(3));
        }

        [Test]
        public void TestAddedWhileHeldAndRecreated()
        {
            AddStep("hold Z", () => InputManager.PressKey(Key.Z));
            seek(500);
            AddStep("recreate display while held", () => clockContainer.Child = display = new ScrollingKeyCounterDisplay());
            AddUntilStep("held key picked up", () => bars(0).Length == 1);
            seek(1000);
            AddAssert("new display tracks hold", () => bars(0).Single().Height, () => Is.EqualTo(45).Within(0.1));
            AddStep("release Z", () => InputManager.ReleaseKey(Key.Z));
            seek(1500);
            AddAssert("new display handles release", () => bars(0).Single().Y, () => Is.EqualTo(-45).Within(0.1));
        }

        [Test]
        public void TestInternalRoundingIncludesPooledBars()
        {
            AddStep("hold key", () => InputManager.PressKey(Key.Z));
            seek(600);
            AddStep("square internal bars", () => SkinComponentGeometry.Restore(display, 0));
            AddAssert("layout is not clipped", () => !display.Masking);
            AddAssert("bar itself is square", () => bars(0).Single().ChildrenOfType<SkinComponentRoundedContainer>().Single().CornerRadius, () => Is.Zero);
            AddStep("round bars", () => SkinComponentGeometry.For(display).Radius.Value = 7);
            AddAssert("bar has individual radius", () => bars(0).Single().ChildrenOfType<SkinComponentRoundedContainer>().Single().CornerRadius, () => Is.EqualTo(7));
            AddStep("release key", () => InputManager.ReleaseKey(Key.Z));
            seek(3000);
            AddStep("new pooled press", () => InputManager.PressKey(Key.Z));
            seek(3600);
            AddAssert("pooled bar inherits radius", () => bars(0).Single().ChildrenOfType<SkinComponentRoundedContainer>().Single().CornerRadius, () => Is.EqualTo(7));
            AddStep("restore default capsule", () => SkinComponentGeometry.For(display).Reset());
            AddAssert("original capsule restored", () => bars(0).Single().ChildrenOfType<SkinComponentRoundedContainer>().Single().CornerRadius, () => Is.EqualTo(26.25f));
        }

        private PoolableDrawable[] bars(int key) => display.ChildrenOfType<ScrollingKeyCounter>()
                                                          .ElementAt(key).ChildrenOfType<PoolableDrawable>()
                                                          .Where(b => b.IsInUse).ToArray();

        private void seek(double time)
        {
            AddStep($"set clock to {time}", () => manualClock.CurrentTime = time);
            AddWaitStep("update history", 1);
        }

        private class TestGameplayClock : FramedClock, IGameplayClock
        {
            public TestGameplayClock(IClock source)
                : base(source)
            {
            }

            public double StartTime => 0;
            public double GameplayStartTime => 0;
            public IAdjustableAudioComponent AdjustmentsFromMods => null!;
            public IBindable<bool> IsPaused { get; } = new BindableBool();
            public bool IsRewinding => Rate < 0;
        }

        private partial class TestTrigger : InputTrigger
        {
            public TestTrigger()
                : base("B1")
            {
            }

            public void Press(bool forward = true) => Activate(forward);

            public void Release(bool forward = true) => Deactivate(forward);
        }
    }
}
