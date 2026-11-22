using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Utils;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public partial class InterfaceAnimationTest
    {
        private InterfaceAnimationMode previousMode;
        private bool previousEnhanced;

        [SetUp]
        public void SetUp()
        {
            previousMode = InterfaceAnimation.Mode.Value;
            previousEnhanced = InterfaceAnimation.Enhanced.Value;
            InterfaceAnimation.Mode.Value = InterfaceAnimationMode.Full;
            InterfaceAnimation.Enhanced.Value = false;
        }

        [TearDown]
        public void TearDown()
        {
            InterfaceAnimation.Mode.Value = previousMode;
            InterfaceAnimation.Enhanced.Value = previousEnhanced;
        }

        [TestCase(InterfaceAnimationMode.Full, 500, 500, true)]
        [TestCase(InterfaceAnimationMode.Minimal, 100, 0, false)]
        [TestCase(InterfaceAnimationMode.Disabled, 0, 0, false)]
        public void TestDurations(InterfaceAnimationMode mode, double fade, double movement, bool decorative)
        {
            InterfaceAnimation.Mode.Value = mode;
            Assert.Multiple(() =>
            {
                Assert.That(InterfaceAnimation.FadeDuration(500), Is.EqualTo(fade));
                Assert.That(InterfaceAnimation.MovementDuration(500), Is.EqualTo(movement));
                Assert.That(InterfaceAnimation.AllowDecorativeMovement, Is.EqualTo(decorative));
            });
        }

        [Test]
        public void TestConfigChangesStillReachPolicyAfterGarbageCollection()
        {
            using var storage = new TemporaryNativeStorage($"interface-animation-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            var retainedConfigCopy = config.GetBindable<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations);
            InterfaceAnimation.Mode.BindTo(retainedConfigCopy);
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var dropdown = config.GetBindable<InterfaceAnimationMode>(OsuSetting.ForkInterfaceAnimations);
                dropdown.Value = InterfaceAnimationMode.Disabled;
                Assert.That(InterfaceAnimation.Mode.Value, Is.EqualTo(InterfaceAnimationMode.Disabled));
                dropdown.Value = InterfaceAnimationMode.Minimal;
                Assert.That(InterfaceAnimation.Mode.Value, Is.EqualTo(InterfaceAnimationMode.Minimal));
                dropdown.Value = InterfaceAnimationMode.Full;
                Assert.That(InterfaceAnimation.Mode.Value, Is.EqualTo(InterfaceAnimationMode.Full));
                dropdown.UnbindAll();
            }
            finally
            {
                retainedConfigCopy.UnbindAll();
            }
        }

        [TestCase(false, InterfaceAnimationMode.Full, 600, 500, Easing.OutQuint)]
        [TestCase(true, InterfaceAnimationMode.Full, 220, 220, Easing.InOutSine)]
        [TestCase(false, InterfaceAnimationMode.Minimal, 0, 100, Easing.OutQuint)]
        [TestCase(true, InterfaceAnimationMode.Minimal, 0, 100, Easing.OutQuint)]
        [TestCase(false, InterfaceAnimationMode.Disabled, 0, 0, Easing.OutQuint)]
        [TestCase(true, InterfaceAnimationMode.Disabled, 0, 0, Easing.OutQuint)]
        public void TestEnhancedMotionRespectsOptOutAndReducedMotion(bool enhanced, InterfaceAnimationMode mode, double movement, double fade, Easing easing)
        {
            InterfaceAnimation.Enhanced.Value = enhanced;
            InterfaceAnimation.Mode.Value = mode;
            Assert.Multiple(() =>
            {
                Assert.That(InterfaceAnimation.TransitionDuration(600), Is.EqualTo(movement));
                Assert.That(InterfaceAnimation.TransitionFadeDuration(500), Is.EqualTo(fade));
                Assert.That(InterfaceAnimation.TransitionEasing(Easing.OutQuint), Is.EqualTo(easing));
                Assert.That(InterfaceAnimation.UseEnhancedMotion, Is.EqualTo(enhanced && mode == InterfaceAnimationMode.Full));
                // The general policy still preserves beat/screen timings and unrelated fades.
                Assert.That(InterfaceAnimation.MovementDuration(600), Is.EqualTo(mode == InterfaceAnimationMode.Full ? 600 : 0));
                Assert.That(InterfaceAnimation.FadeDuration(500), Is.EqualTo(mode == InterfaceAnimationMode.Full ? 500 : fade));
            });
        }

        [Test]
        public void TestEnhancedPanelFadeDoesNotFinishBeforeMovement()
        {
            InterfaceAnimation.Enhanced.Value = true;
            Assert.That(InterfaceAnimation.TransitionFadeDuration(300), Is.EqualTo(InterfaceAnimation.TransitionDuration(600)));
            Assert.That(InterfaceAnimation.TransitionFadeDuration(400), Is.EqualTo(InterfaceAnimation.TransitionDuration(400)));
        }

        [Test]
        public void TestEnhancedMotionPreservesShortTransitionsAndOriginalEasingWhenDisabled()
        {
            InterfaceAnimation.Enhanced.Value = true;
            Assert.That(InterfaceAnimation.TransitionDuration(40), Is.EqualTo(40));
            Assert.That(InterfaceAnimation.TransitionFadeDuration(40), Is.EqualTo(40));
            InterfaceAnimation.Enhanced.Value = false;
            Assert.That(InterfaceAnimation.TransitionEasing(Easing.OutElastic), Is.EqualTo(Easing.OutElastic));
        }

        [TestCase(60)]
        [TestCase(165)]
        [TestCase(330)]
        [TestCase(1000)]
        public void TestEnhancedDrawableMovementIsTimeBasedAndHasNoOvershoot(double hz)
        {
            InterfaceAnimation.Enhanced.Value = true;
            using var drawable = new MotionTestContainer();
            double duration = InterfaceAnimation.TransitionDuration(600);
            drawable.MoveToX(100, duration, InterfaceAnimation.TransitionEasing(Easing.OutQuint));
            double previous = 0;
            for (double time = 0; time < duration; time += 1000 / hz)
            {
                double position = drawable.Sample(time);
                double expected = 100 * Interpolation.ApplyEasing(Easing.InOutSine, time / duration);
                Assert.That(position, Is.EqualTo(expected).Within(0.00001));
                Assert.That(position, Is.InRange(previous, 100));
                previous = position;
            }
            Assert.That(drawable.Sample(duration), Is.EqualTo(100));
        }

        [Test]
        public void TestEnhancedMotionHasGentleStartAndStopAndBalancedMidpoint()
        {
            InterfaceAnimation.Enhanced.Value = true;
            using var drawable = new MotionTestContainer();
            double duration = InterfaceAnimation.TransitionDuration(600);
            drawable.MoveToX(100, duration, InterfaceAnimation.TransitionEasing(Easing.OutQuint));
            Assert.That(drawable.Sample(duration * 0.1), Is.InRange(1, 5));
            Assert.That(drawable.Sample(duration * 0.5), Is.EqualTo(50).Within(0.00001));
            Assert.That(drawable.Sample(duration * 0.9), Is.InRange(95, 99));
            Assert.That(drawable.Sample(duration), Is.EqualTo(100));
        }

        [Test]
        public void TestReversingEnhancedMovementDoesNotJumpPosition()
        {
            InterfaceAnimation.Enhanced.Value = true;
            using var drawable = new MotionTestContainer();
            double duration = InterfaceAnimation.TransitionDuration(600);
            drawable.MoveToX(100, duration, InterfaceAnimation.TransitionEasing(Easing.OutQuint));
            double before = drawable.Sample(80);
            drawable.MoveToX(0, duration, InterfaceAnimation.TransitionEasing(Easing.OutQuint));
            Assert.That(drawable.Sample(80), Is.EqualTo(before));
            Assert.That(drawable.Sample(100), Is.InRange(0, before));
            Assert.That(drawable.Sample(80 + duration), Is.EqualTo(0));
        }

        [Test]
        public void TestEnhancedConfigurationStillReachesPolicyAfterGarbageCollection()
        {
            using var storage = new TemporaryNativeStorage($"enhanced-motion-{Guid.NewGuid():N}");
            using var config = new OsuConfigManager(storage);
            var retainedConfigCopy = config.GetBindable<bool>(OsuSetting.ForkEnhancedAnimations);
            InterfaceAnimation.Enhanced.BindTo(retainedConfigCopy);
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                config.SetValue(OsuSetting.ForkEnhancedAnimations, false);
                Assert.That(InterfaceAnimation.TransitionDuration(600), Is.EqualTo(600));
                config.SetValue(OsuSetting.ForkEnhancedAnimations, true);
                Assert.That(InterfaceAnimation.TransitionDuration(600), Is.EqualTo(220));
            }
            finally
            {
                retainedConfigCopy.UnbindAll();
            }
        }

        private partial class MotionTestContainer : Container
        {
            private readonly ManualClock manualClock = new ManualClock();

            public MotionTestContainer() => Clock = new FramedClock(manualClock);

            public double Sample(double time)
            {
                manualClock.CurrentTime = time;
                Clock.ProcessFrame();
                UpdateTransforms();
                return X;
            }
        }

        [Test]
        public void TestShortFadesAreNotLengthened()
        {
            InterfaceAnimation.Mode.Value = InterfaceAnimationMode.Minimal;
            Assert.That(InterfaceAnimation.FadeDuration(40), Is.EqualTo(40));
        }

        [Test]
        public void TestExistingUIFinishesImmediatelyButUnobservedGameplayDoesNot()
        {
            using var ui = new Container { Clock = new FramedClock(new ManualClock()) };
            using var gameplay = new Container { Clock = new FramedClock(new ManualClock()) };
            var subscription = InterfaceAnimation.Observe(ui);
            ui.MoveToX(50, 1000);
            gameplay.MoveToX(50, 1000);

            InterfaceAnimation.Mode.Value = InterfaceAnimationMode.Disabled;

            Assert.Multiple(() =>
            {
                Assert.That(ui.X, Is.EqualTo(50));
                Assert.That(gameplay.X, Is.EqualTo(0));
            });
            subscription.UnbindAll();
        }

        [Test]
        public void TestRepeatedChangesAndChildren()
        {
            using var ui = new Container
            {
                Clock = new FramedClock(new ManualClock()),
                Child = new Container { Clock = new FramedClock(new ManualClock()) },
            };
            var subscription = InterfaceAnimation.Observe(ui, true);
            foreach (var mode in new[] { InterfaceAnimationMode.Minimal, InterfaceAnimationMode.Disabled, InterfaceAnimationMode.Minimal })
            {
                InterfaceAnimation.Mode.Value = InterfaceAnimationMode.Full;
                ui.Child.ScaleTo(2, 500);
                ui.Child.FadeOut(500);
                InterfaceAnimation.Mode.Value = mode;
                Assert.That(ui.Child.Scale, Is.EqualTo(new Vector2(2)));
                Assert.That(ui.Child.Alpha, Is.EqualTo(0));
                ui.Child.Scale = Vector2.One;
                ui.Child.Alpha = 1;
            }
            subscription.UnbindAll();
        }
    }
}
