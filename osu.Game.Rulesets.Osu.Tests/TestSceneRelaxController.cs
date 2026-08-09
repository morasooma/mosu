// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneRelaxController : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private OsuInputManager osuInputManager = null!;
        private OsuPlayfield playfield = null!;
        private DrawableHitCircle hitCircle = null!;
        private AimAssistController aimAssistController = null!;
        private RelaxController relaxController = null!;
        private RecordedActionListener actionListener = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create test scene", () =>
            {
                var hitObject = new HitCircle
                {
                    Position = OsuPlayfield.BASE_SIZE / 2,
                    StartTime = Time.Current + 1000
                };

                hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Children = new Drawable[]
                {
                    new SkinProvidingContainer(new TrianglesSkin(null!))
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = osuInputManager = new OsuInputManager(new OsuRuleset().RulesetInfo)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = playfield = new OsuPlayfield
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = OsuPlayfield.BASE_SIZE
                                }
                            }
                        }
                    }
                };

                playfield.HitObjectContainer.Add(hitCircle = new DrawableHitCircle(hitObject));
                osuInputManager.KeyBindingContainer.Add(actionListener = new RecordedActionListener());
            });

            AddStep("reset fork config", resetRelaxConfig);
            AddUntilStep("scene loaded", () => osuInputManager.IsLoaded && hitCircle.IsLoaded);
            AddUntilStep("aim assist controller loaded", () =>
            {
                AimAssistController? controller = playfield.ChildrenOfType<AimAssistController>().SingleOrDefault();

                if (controller?.IsLoaded != true)
                    return false;

                aimAssistController = controller;
                return true;
            });
            AddUntilStep("relax controller loaded", () =>
            {
                RelaxController? controller = playfield.ChildrenOfType<RelaxController>().SingleOrDefault();

                if (controller?.IsLoaded != true)
                    return false;

                relaxController = controller;
                return true;
            });
            AddStep("refresh hit circle timing", () => hitCircle.HitObject.StartTime = relaxController.Time.Current + 1000);
            AddStep("clear recorded relax events", () => relaxController.ClearInputEvents());
            AddStep("clear recorded input actions", () => actionListener.Clear());
        }

        [Test]
        public void TestPositionalMissEvent()
        {
            int positionalMissCount = 0;
            double reportedOffset = double.NaN;
            Drawable? handledBy = null;
            DrawableHitCircle.HitReceptor positionalMissTarget = null!;

            AddStep("listen for positional misses", () => osuInputManager.NewPositionalMiss += offset =>
            {
                positionalMissCount++;
                reportedOffset = offset;
            });
            AddStep("move cursor outside note", () => osuInputManager.HandleUserCursorMovement(hitCircle.ScreenSpaceDrawQuad.Centre + new Vector2(150, 0)));
            AddUntilStep("cursor is outside note", () => !hitCircle.HitArea.IsHovered);

            AddStep("press outside hit window", () => osuInputManager.KeyBindingContainer.TriggerPressed(OsuAction.LeftButton));
            AddStep("release", () => osuInputManager.KeyBindingContainer.TriggerReleased(OsuAction.LeftButton));
            AddAssert("no positional miss outside hit window", () => positionalMissCount == 0);

            AddStep("add positional miss target", () => osuInputManager.KeyBindingContainer.Add(positionalMissTarget = new DrawableHitCircle.HitReceptor
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(64),
                CanBeHit = () => true,
                GetPositionalMissTimeOffset = () => -50,
                Hit = () => { }
            }));
            AddUntilStep("positional miss target loaded", () => positionalMissTarget.IsLoaded);
            AddStep("move cursor outside target", () => osuInputManager.HandleUserCursorMovement(positionalMissTarget.ScreenSpaceDrawQuad.Centre + new Vector2(150, 0)));
            AddUntilStep("cursor is outside target", () => !positionalMissTarget.IsHovered);
            AddAssert("target is in input queue", () => osuInputManager.CheckScreenSpaceActionPressJudgeable(positionalMissTarget.ScreenSpaceDrawQuad.Centre));
            AddAssert("action is released", () => !osuInputManager.PressedActions.Contains(OsuAction.LeftButton));
            AddStep("press outside note", () => handledBy = osuInputManager.KeyBindingContainer.TriggerPressed(OsuAction.LeftButton));
            AddAssert("press was unhandled", () => handledBy == null);
            AddStep("release", () => osuInputManager.KeyBindingContainer.TriggerReleased(OsuAction.LeftButton));
            AddAssert("positional miss reported", () => positionalMissCount == 1);
            AddAssert("timing offset reported", () => reportedOffset == -50);
        }

        [Test]
        public void TestRelaxUsesConfiguredTimingAndHold()
        {
            double startTime = 0;

            configureRelax(baseOffset: 20, timingVariance: 0, holdTime: 30, syncRadius: 0, maxSyncDelay: 0);

            AddStep("store start time", () => startTime = hitCircle.HitObject.StartTime);
            AddStep("move cursor to gameplay target", () => osuInputManager.HandleUserCursorMovement(playfield.GamefieldToScreenSpace(hitCircle.HitObject.StackedPosition)));
            AddUntilStep("press and release logged", () => relaxController.InputEvents.Count >= 2);
            AddAssert("press uses configured offset", () => firstPress().Time - startTime, () => Is.EqualTo(20).Within(35));
            AddAssert("release uses configured hold", () => firstRelease().Time - firstPress().Time, () => Is.EqualTo(30).Within(20));
        }

        [Test]
        public void TestRelaxStableBpmWindowScalesWithGameplayRate()
        {
            Assert.That(RelaxController.ConvertStableBpmToGameplayTapWindow(300, 1.5), Is.EqualTo(75).Within(0.01));
            Assert.That(RelaxController.ConvertStableBpmToGameplayTapWindow(300, 0.75), Is.EqualTo(37.5).Within(0.01));
        }

        [Test]
        public void TestRelaxJumpSingleTapWindowClampsSafelyForShortStableWindow()
        {
            double stableTapWindow = RelaxController.ConvertStableBpmToGameplayTapWindow(300, 0.75);

            Assert.That(
                RelaxController.GetDerivedJumpSingleTapWindow(stableTapWindow, 0.3),
                Is.EqualTo(stableTapWindow + 60).Within(0.01));
        }

        [Test]
        public void TestRelaxDerivedPeakBpmHandlesHighStableBpm()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RelaxController.GetDerivedPeakBpm(450), Is.EqualTo(468).Within(0.01));
                Assert.That(RelaxController.GetDerivedLivePeakBpm(325), Is.EqualTo(361).Within(0.01));
            });
        }

        public void TestRelaxForcesPressAfterMaxSyncDelay()
        {
            double startTime = 0;
            float localRadius = 0;
            Vector2 localPosition = Vector2.Zero;

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 18, syncRadius: 60, maxSyncDelay: 45);

            AddStep("store timing and local radius", () =>
            {
                startTime = hitCircle.HitObject.StartTime;
                localRadius = (float)hitCircle.HitObject.Radius;
                localPosition = hitCircle.HitObject.StackedPosition;
            });
            AddStep("move cursor far from note first", () => osuInputManager.HandleUserCursorMovement(playfield.GamefieldToScreenSpace(localPosition + new Vector2(localRadius + 80, 0))));
            AddStep("stay inside sync ring", () => osuInputManager.HandleUserCursorMovement(playfield.GamefieldToScreenSpace(localPosition + new Vector2(localRadius + 18, 0))));
            AddUntilStep("press logged after sync timeout", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("press is delayed inside sync ring", () => firstPress().Time - startTime, () => Is.GreaterThan(10));
            AddAssert("press stays within timeout envelope", () => firstPress().Time - startTime, () => Is.LessThan(90));
        }

        [Test]
        public void TestRelaxDoesNotOvertakeDelayedQueueHead()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            double firstStartTime = 0;

            AddStep("replace with close pair", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(210, 192), start),
                    second = createCircle(new Vector2(255, 192), start + 45));
            });
            AddUntilStep("pair loaded", () => first.IsLoaded && second.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 70, maxSyncDelay: 80,
                alternateThreshold: 100, misaltProbability: 0);

            AddStep("store first start time", () => firstStartTime = first.HitObject.StartTime);
            AddStep("move cursor to second note", () => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("first press logged", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("earlier note stays queue head", () => firstPress().TargetStartTime, () => Is.EqualTo(firstStartTime).Within(1));
            AddAssert("queue head waited instead of being overtaken", () => firstPress().Time - firstStartTime, () => Is.GreaterThan(20));
        }

        [Test]
        public void TestRelaxAlternatesOnStreams()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;

            AddStep("replace with stream circles", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(150, 192), start),
                    second = createCircle(new Vector2(210, 192), start + 70),
                    third = createCircle(new Vector2(270, 192), start + 140),
                    fourth = createCircle(new Vector2(330, 192), start + 210));
            });
            AddUntilStep("stream circles loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 100, misaltProbability: 0);

            AddUntilStep("four presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 4);
            AddAssert("stream alternates left right", () =>
                relaxController.InputEvents.Where(e => e.IsPress).Take(4).Select(e => e.Action).SequenceEqual(new[]
                {
                    OsuAction.LeftButton,
                    OsuAction.RightButton,
                    OsuAction.LeftButton,
                    OsuAction.RightButton,
                }));
        }

        [Test]
        public void TestRelaxStableBpmExhaustsIntoAlternation()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;
            DrawableHitCircle fifth = null!;
            DrawableHitCircle sixth = null!;

            AddStep("replace with draining burst", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(144, 192), start),
                    second = createCircle(new Vector2(204, 192), start + 110),
                    third = createCircle(new Vector2(264, 192), start + 220),
                    fourth = createCircle(new Vector2(324, 192), start + 330),
                    fifth = createCircle(new Vector2(384, 192), start + 440),
                    sixth = createCircle(new Vector2(444, 192), start + 550));
            });
            AddUntilStep("draining burst loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded && fifth.IsLoaded && sixth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 0, stableBpm: 120, misaltProbability: 0);

            AddUntilStep("six burst presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 6);
            AddAssert("burst starts as singletap then falls into alternation", () =>
            {
                OsuAction[] actions = relaxController.InputEvents.Where(e => e.IsPress).Take(6).Select(e => e.Action).ToArray();
                return actions[0] == OsuAction.LeftButton
                       && actions[1] == OsuAction.LeftButton
                       && actions.Skip(2).Any(a => a == OsuAction.RightButton);
            });
        }

        [Test]
        public void TestRelaxStableBpmRecoversAfterBreak()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;
            DrawableHitCircle fifth = null!;
            DrawableHitCircle sixth = null!;

            AddStep("replace with draining burst", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(124, 192), start),
                    second = createCircle(new Vector2(184, 192), start + 110),
                    third = createCircle(new Vector2(244, 192), start + 220),
                    fourth = createCircle(new Vector2(304, 192), start + 330));
            });
            AddUntilStep("draining burst loaded", () =>
                first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 120, misaltProbability: 0);

            AddUntilStep("draining burst presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 4);
            AddStep("replace with recovery pair after a break", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    fifth = createCircle(new Vector2(196, 128), start),
                    sixth = createCircle(new Vector2(286, 128), start + 110));
            });
            AddUntilStep("recovery pair loaded", () => fifth.IsLoaded && sixth.IsLoaded);
            AddUntilStep("recovery pair presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 6);
            AddAssert("after a long break relax returns to one finger", () =>
            {
                OsuAction[] actions = relaxController.InputEvents.Where(e => e.IsPress).Take(6).Select(e => e.Action).ToArray();
                return actions[4] == OsuAction.RightButton && actions[5] == OsuAction.RightButton;
            });
        }

        [Test]
        public void TestRelaxWideJumpsDoNotConsumeStreamStamina()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;

            AddStep("replace with wide jump trio", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(72, 192), start),
                    second = createCircle(new Vector2(256, 124), start + 140),
                    third = createCircle(new Vector2(440, 260), start + 280));
            });
            AddUntilStep("wide jump trio loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 18, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 120, misaltProbability: 0);

            AddUntilStep("three jump presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("wide jumps do not drain stream stamina", () => relaxController.CurrentTapStamina, () => Is.GreaterThan(0.95));
        }

        [Test]
        public void TestRelaxStableBpmCannotSustainExtremeStream()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;
            DrawableHitCircle fifth = null!;
            DrawableHitCircle sixth = null!;
            double sixthPressDelta = double.NaN;

            AddStep("replace with extreme stream", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(124, 192), start),
                    second = createCircle(new Vector2(174, 192), start + 75),
                    third = createCircle(new Vector2(224, 192), start + 150),
                    fourth = createCircle(new Vector2(274, 192), start + 225),
                    fifth = createCircle(new Vector2(324, 192), start + 300),
                    sixth = createCircle(new Vector2(374, 192), start + 375));
            });
            AddUntilStep("extreme stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded && fifth.IsLoaded && sixth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 120, misaltProbability: 0);

            AddUntilStep("six extreme stream presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 6);
            AddStep("store sixth press delta", () =>
                sixthPressDelta = relaxController.InputEvents.Where(e => e.IsPress).Skip(5).First().Time - first.HitObject.StartTime);
            AddAssert("extreme stream gets delayed by stamina", () => sixthPressDelta, () => Is.GreaterThan(390));
        }

        [Test]
        public void TestRelaxStableBpmStillAllowsShortPeakBurst()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            double thirdPressDelta = double.NaN;

            AddStep("replace with short peak burst", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(144, 192), start),
                    second = createCircle(new Vector2(194, 192), start + 75),
                    third = createCircle(new Vector2(244, 192), start + 150));
            });
            AddUntilStep("short peak burst loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 120, misaltProbability: 0);

            AddUntilStep("three burst presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddStep("store third burst delta", () =>
                thirdPressDelta = relaxController.InputEvents.Where(e => e.IsPress).Skip(2).First().Time - first.HitObject.StartTime);
            AddAssert("short burst exceeds stable rate while fresh", () => thirdPressDelta, () => Is.LessThan(260));
        }

        [Test]
        public void TestRelaxFatigueGraduallySlowsAndShiftsStreamTimingEarlier()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;
            DrawableHitCircle fifth = null!;
            DrawableHitCircle sixth = null!;
            DrawableHitCircle seventh = null!;
            double sixthPressOffset = double.NaN;
            double seventhScheduledOffset = double.NaN;

            AddStep("replace with long draining stream", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(124, 192), start),
                    second = createCircle(new Vector2(174, 192), start + 75),
                    third = createCircle(new Vector2(224, 192), start + 150),
                    fourth = createCircle(new Vector2(274, 192), start + 225),
                    fifth = createCircle(new Vector2(324, 192), start + 300),
                    sixth = createCircle(new Vector2(374, 192), start + 375),
                    seventh = createCircle(new Vector2(424, 192), start + 450));
            });
            AddUntilStep("long draining stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded && fifth.IsLoaded && sixth.IsLoaded && seventh.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 120, misaltProbability: 0);

            AddUntilStep("six draining stream presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 6);
            AddStep("sample fatigue cadence", () =>
            {
                RelaxController.RelaxInputEvent[] presses = relaxController.InputEvents.Where(e => e.IsPress).Take(6).ToArray();
                sixthPressOffset = presses[5].Time - sixth.HitObject.StartTime;

                if (relaxController.TryGetTargetScheduledPressTime(seventh.HitObject, out double scheduledPressTime))
                    seventhScheduledOffset = scheduledPressTime - seventh.HitObject.StartTime;
            });
            AddAssert("fatigue still slows the collapsing stream", () => sixthPressOffset, () => Is.GreaterThan(4));
            AddAssert("fatigue starts biasing scheduled stream timing earlier", () => seventhScheduledOffset, () => Is.LessThan(-2));
        }

        [Test]
        public void TestRelaxHighStableBpmAlternatesRealStreamFromSecondNote()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;

            AddStep("replace with 200 bpm stream", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(150, 192), start),
                    second = createCircle(new Vector2(210, 192), start + 75),
                    third = createCircle(new Vector2(270, 192), start + 150),
                    fourth = createCircle(new Vector2(330, 192), start + 225));
            });
            AddUntilStep("200 bpm stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 300, misaltProbability: 0);

            AddUntilStep("four 200 bpm presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 4);
            AddAssert("200 bpm stream alternates from the second note even at high stable bpm", () =>
            {
                OsuAction[] actions = relaxController.InputEvents.Where(e => e.IsPress).Take(4).Select(e => e.Action).ToArray();
                return actions.SequenceEqual(new[]
                {
                    OsuAction.LeftButton,
                    OsuAction.RightButton,
                    OsuAction.LeftButton,
                    OsuAction.RightButton,
                });
            });
        }

        [Test]
        public void TestRelaxHighStableBpmDoesNotCapTwoHundredStream()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;
            DrawableHitCircle fifth = null!;
            DrawableHitCircle sixth = null!;
            double sixthPressDelta = double.NaN;

            AddStep("replace with long 200 bpm stream", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(124, 192), start),
                    second = createCircle(new Vector2(174, 192), start + 75),
                    third = createCircle(new Vector2(224, 192), start + 150),
                    fourth = createCircle(new Vector2(274, 192), start + 225),
                    fifth = createCircle(new Vector2(324, 192), start + 300),
                    sixth = createCircle(new Vector2(374, 192), start + 375));
            });
            AddUntilStep("long 200 bpm stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded && fifth.IsLoaded && sixth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 300, misaltProbability: 0);

            AddUntilStep("six 200 bpm presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 6);
            AddStep("store sixth 200 bpm delta", () =>
                sixthPressDelta = relaxController.InputEvents.Where(e => e.IsPress).Skip(5).First().Time - first.HitObject.StartTime);
            AddAssert("300 stable bpm does not cap 200 bpm stream", () => sixthPressDelta, () => Is.LessThan(440));
        }

        [Test]
        public void TestRelaxAlternateThresholdForcesAlternationForCommittedStreams()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;

            AddStep("replace with 180 bpm committed stream", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(140, 192), start),
                    second = createCircle(new Vector2(210, 192), start + 83),
                    third = createCircle(new Vector2(280, 192), start + 166),
                    fourth = createCircle(new Vector2(350, 192), start + 249));
            });
            AddUntilStep("180 bpm committed stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 170, stableBpm: 300, misaltProbability: 0);

            AddUntilStep("four forced-alt stream presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 4);
            AddAssert("alternate threshold forces two-finger stream pattern", () =>
                string.Join(",", relaxController.InputEvents.Where(e => e.IsPress).Take(4).Select(e => e.Action)),
                () => Is.EqualTo("LeftButton,RightButton,LeftButton,RightButton"));
        }

        [Test]
        public void TestRelaxAlternateThresholdForcesAlternationForFastFlowBurstFromSecondNote()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;

            AddStep("replace with fast flow burst", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(140, 192), start),
                    second = createCircle(new Vector2(210, 192), start + 83),
                    third = createCircle(new Vector2(280, 192), start + 166));
            });
            AddUntilStep("fast flow burst loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 170, stableBpm: 300, misaltProbability: 0);

            AddUntilStep("three forced-alt burst presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("alternate threshold forces burst alternation from the second note", () =>
                string.Join(",", relaxController.InputEvents.Where(e => e.IsPress).Take(3).Select(e => e.Action)),
                () => Is.EqualTo("LeftButton,RightButton,LeftButton"));
        }

        [Test]
        public void TestRelaxAlternatesFastWideJumpsBeyondSingleTapWindow()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;

            AddStep("replace with fast wide jump trio", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(60, 192), start),
                    second = createCircle(new Vector2(332, 112), start + 130),
                    third = createCircle(new Vector2(444, 272), start + 260));
            });
            AddUntilStep("fast wide jumps loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 24, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 145, stableBpm: 103.5, misaltProbability: 0);

            AddUntilStep("three jump presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("fast wide jumps alternate hands", () =>
                string.Join(",", relaxController.InputEvents.Where(e => e.IsPress).Take(3).Select(e => e.Action)),
                () => Is.EqualTo("LeftButton,RightButton,LeftButton"));
        }

        [Test]
        public void TestRelaxSingleTapsModerateJumpsWhenTheyAreSingletapFriendly()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            double observedVarianceScale = 1;
            string observedMode = string.Empty;

            AddStep("replace with moderate jump trio", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(92, 192), start),
                    second = createCircle(new Vector2(332, 136), start + 158),
                    third = createCircle(new Vector2(120, 252), start + 316));
            });
            AddUntilStep("moderate jump trio loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 14, holdTime: 24, syncRadius: 0, maxSyncDelay: 0,
                stableBpm: 180, misaltProbability: 0);

            AddUntilStep("singletap jump plan detected", () =>
            {
                observedVarianceScale = relaxController.CurrentTimingVarianceScale;
                observedMode = relaxController.CurrentModeName;
                return observedMode == "jump-singletap" && observedVarianceScale < 0.6;
            });
            AddUntilStep("three moderate jump presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("moderate jumps stay on single tap", () =>
                relaxController.InputEvents.Where(e => e.IsPress).Take(3).Select(e => e.Action).Distinct().Count(), () => Is.EqualTo(1));
            AddAssert("singletap jumps reduce timing randomness scale", () => observedVarianceScale, () => Is.LessThan(0.6));
            AddAssert("diagnostic mode reflects singletap jump", () => observedMode, () => Is.EqualTo("jump-singletap"));
            AddUntilStep("controller returns to idle after jump trio", () => relaxController.CurrentModeName == "idle");
            AddAssert("timing variance scale resets in idle", () => relaxController.CurrentTimingVarianceScale, () => Is.EqualTo(1));
        }

        [Test]
        public void TestRelaxWideJumpUsesExtendedSyncEnvelope()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            double secondStartTime = 0;
            float secondRadius = 0;
            Vector2 waitingPosition = Vector2.Zero;

            AddStep("replace with wide jump pair", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(92, 192), start),
                    second = createCircle(new Vector2(420, 132), start + 240));
            });
            AddUntilStep("wide jump pair loaded", () => first.IsLoaded && second.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 24, syncRadius: 16, maxSyncDelay: 18,
                alternateThreshold: 170, misaltProbability: 0);

            AddStep("store second note timing", () =>
            {
                secondStartTime = second.HitObject.StartTime;
                secondRadius = getHitCircleScreenRadius(second);
                waitingPosition = second.ScreenSpaceDrawQuad.Centre + new Vector2(secondRadius + 24, 0);
            });
            AddStep("move to opening note", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre));
            AddStep("schedule jump approach", () =>
            {
                double outerDelay = Math.Max(0, second.HitObject.StartTime - 8 - relaxController.Time.Current);
                double centreDelay = Math.Max(0, second.HitObject.StartTime + 10 - relaxController.Time.Current);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(waitingPosition), outerDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre), centreDelay);
            });
            AddUntilStep("second jump press logged", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - second.HitObject.StartTime) < 1));
            AddAssert("second jump waits for jump sync window", () =>
                relaxController.InputEvents.First(e => e.IsPress && Math.Abs(e.TargetStartTime - second.HitObject.StartTime) < 1).Time - secondStartTime,
                () => Is.GreaterThan(4).And.LessThan(95));
        }

        [Test]
        public void TestRelaxMisaltCanBreakAlternation()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;

            AddStep("replace with three note burst", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(170, 192), start),
                    second = createCircle(new Vector2(230, 192), start + 70),
                    third = createCircle(new Vector2(290, 192), start + 140));
            });
            AddUntilStep("burst circles loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 0, misaltProbability: 1);

            AddUntilStep("three presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("misalt repeats previous finger", () =>
            {
                OsuAction[] actions = relaxController.InputEvents.Where(e => e.IsPress).Take(3).Select(e => e.Action).ToArray();
                return actions[0] == actions[1] && actions[1] == actions[2];
            });
        }

        [Test]
        public void TestRelaxSliderTailOffsetExtendsHold()
        {
            DrawableSlider slider = null!;
            double expectedRelease = 0;

            AddStep("replace with slider", () =>
            {
                double start = relaxController.Time.Current + 900;

                slider = createSlider(new Vector2(132, 192), start, 300, new Vector2(240, 0));
                replaceHitObjects(slider);
                expectedRelease = slider.HitObject.EndTime + 40;
            });
            AddUntilStep("slider loaded", () => slider.IsLoaded && slider.HeadCircle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, sliderTailOffset: 40, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move cursor to slider head", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("press and release logged", () => relaxController.InputEvents.Count >= 2);
            AddAssert("slider release respects tail offset", () => firstRelease().Time, () => Is.EqualTo(expectedRelease).Within(35));
        }

        [Test]
        public void TestRelaxDefaultSliderReleaseHasNaturalTailHold()
        {
            DrawableSlider slider = null!;

            AddStep("replace with slider for default tail hold", () =>
            {
                double start = relaxController.Time.Current + 1800;

                slider = createSlider(new Vector2(132, 192), start, 300, new Vector2(240, 0));
                replaceHitObjects(slider);
            });
            AddUntilStep("default tail slider loaded", () => slider.IsLoaded && slider.HeadCircle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 0, sliderTailOffset: 0, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move cursor to slider head", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("default tail press and release logged", () => relaxController.InputEvents.Count >= 2);
            AddAssert("isolated slider only presses once", () =>
                relaxController.InputEvents.Count(e => e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1), () => Is.EqualTo(1));
            AddAssert("isolated slider only releases once", () =>
                relaxController.InputEvents.Count(e => !e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1), () => Is.EqualTo(1));
            AddAssert("default slider release is not instant at tail", () => firstRelease().Time - slider.HitObject.EndTime,
                () => Is.GreaterThan(18).And.LessThan(75));
        }

        [Test]
        public void TestRelaxPreservesHeldSliderActionForOverlappingCircle()
        {
            DrawableSlider slider = null!;
            DrawableHitCircle circle = null!;
            OsuAction sliderAction = default;
            OsuAction overlapAction = default;
            double sliderReleaseTime = double.NaN;

            AddStep("replace with slider into overlapping circle", () =>
            {
                double start = relaxController.Time.Current + 1800;

                replaceHitObjects(
                    slider = createSlider(new Vector2(132, 192), start, 520, new Vector2(260, 0)),
                    circle = createCircle(new Vector2(312, 188), start + 140));
            });
            AddUntilStep("slider overlap pattern loaded", () => slider.IsLoaded && slider.HeadCircle.IsLoaded && circle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 24, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move cursor to slider head", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddStep("schedule move to overlapping circle", () =>
            {
                double moveDelay = Math.Max(0, circle.HitObject.StartTime - 26 - relaxController.Time.Current);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(circle.ScreenSpaceDrawQuad.Centre), moveDelay);
            });
            AddUntilStep("slider and circle presses logged", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1)
                && relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - circle.HitObject.StartTime) < 1));
            AddStep("store overlap actions", () =>
            {
                sliderAction = relaxController.InputEvents.First(e => e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1).Action;
                overlapAction = relaxController.InputEvents.First(e => e.IsPress && Math.Abs(e.TargetStartTime - circle.HitObject.StartTime) < 1).Action;
            });
            AddUntilStep("slider release logged", () =>
                relaxController.InputEvents.Any(e => !e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1));
            AddStep("store slider release time", () =>
                sliderReleaseTime = relaxController.InputEvents.First(e => !e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1).Time);
            AddAssert("overlapping circle uses opposite action to held slider", () => overlapAction, () => Is.Not.EqualTo(sliderAction));
            AddAssert("slider hold survives overlapping circle", () => sliderReleaseTime, () => Is.GreaterThan(slider.HitObject.EndTime - 18));
        }

        [Test]
        public void TestRelaxLeavesSpinnerCursorToPlayer()
        {
            DrawableSpinner spinner = null!;
            Vector2 rawSpinnerPosition = Vector2.Zero;

            AddStep("replace with spinner", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(spinner = createSpinner(start, 1300));
            });
            AddUntilStep("spinner loaded", () => spinner.IsLoaded && spinner.Body.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 28, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move raw cursor away from spinner centre", () =>
            {
                rawSpinnerPosition = spinner.Body.ScreenSpaceDrawQuad.Centre + new Vector2(180, 90);
                InputManager.MoveMouseTo(rawSpinnerPosition);
            });
            AddUntilStep("spinner press logged", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddWaitStep("let spinner run a few frames", 8);
            AddAssert("relax does not take cursor on spinner", () =>
                (osuInputManager.CurrentState.Mouse.Position - rawSpinnerPosition).Length, () => Is.LessThan(2f));
            AddUntilStep("spinner release logged", () => relaxController.InputEvents.Any(e => !e.IsPress));
            AddAssert("spinner release stays near end", () => firstRelease().Time, () => Is.GreaterThan(spinner.HitObject.EndTime - 45));
        }

        [Test]
        public void TestRelaxKeepsHoldingAcrossConsecutiveSpinners()
        {
            DrawableSpinner first = null!;
            DrawableSpinner second = null!;

            AddStep("replace with consecutive spinners", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createSpinner(start, 600),
                    second = createSpinner(start + 620, 540));
            });
            AddUntilStep("spinner chain loaded", () => first.IsLoaded && second.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 28, syncRadius: 0, maxSyncDelay: 0);

            AddUntilStep("spinner chain completed", () => relaxController.InputEvents.Count >= 2);
            AddAssert("spinner chain uses a single held press", () => relaxController.InputEvents.Count(e => e.IsPress), () => Is.EqualTo(1));
            AddAssert("spinner chain has no release between spinners", () => relaxController.InputEvents.Count(e => !e.IsPress), () => Is.EqualTo(1));
            AddAssert("spinner chain release happens after second spinner", () => firstRelease().Time, () => Is.GreaterThan(second.HitObject.EndTime - 45));
        }

        [Test]
        public void TestRelaxIgnoresShortSpinnerWhileSliderIsHeld()
        {
            DrawableSlider slider = null!;
            DrawableSpinner spinner = null!;

            AddStep("replace with slider and short overlapping spinner", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    slider = createSlider(new Vector2(132, 192), start, 520, new Vector2(260, 0)),
                    spinner = createSpinner(start + 120, 180));
            });
            AddUntilStep("overlap set loaded", () => slider.IsLoaded && slider.HeadCircle.IsLoaded && spinner.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 18, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move cursor to slider head", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("slider interaction finished", () => relaxController.InputEvents.Count >= 2);
            AddAssert("short overlapping spinner is ignored", () =>
                relaxController.InputEvents.Any(e => Math.Abs(e.TargetStartTime - spinner.HitObject.StartTime) < 1), () => Is.False);
            AddAssert("slider still receives opening press", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - slider.HitObject.StartTime) < 1));
        }

        [Test]
        public void TestAimAssistReleasesVirtualCursorOnSpinner()
        {
            DrawableHitCircle first = null!;
            DrawableSpinner spinner = null!;
            Vector2 rawSpinnerPosition = Vector2.Zero;

            AddStep("replace with circle into spinner", () =>
            {
                double start = relaxController.Time.Current + 700;

                replaceHitObjects(
                    first = createCircle(new Vector2(170, 192), start),
                    spinner = createSpinner(start + 520, 1200));
            });
            AddUntilStep("circle and spinner loaded", () => first.IsLoaded && spinner.IsLoaded && spinner.Body.IsLoaded);

            configureAimAssist(strength: 1, fovRadius: 240, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 28, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move raw near opening circle", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(52, 0)));
            AddUntilStep("aim assist drives before spinner", () =>
                osuInputManager.IsVirtualCursorActive
                && (osuInputManager.CurrentState.Mouse.Position - osuInputManager.OriginalUserCursorPosition).Length > 8f);
            AddStep("move raw far from spinner orbit", () =>
            {
                rawSpinnerPosition = spinner.Body.ScreenSpaceDrawQuad.Centre + new Vector2(220, 120);
                InputManager.MoveMouseTo(rawSpinnerPosition);
            });
            AddUntilStep("spinner press logged", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - spinner.HitObject.StartTime) < 1));
            AddWaitStep("let spinner state settle", 4);
            AddAssert("aim assist idles on spinner", () => aimAssistController.CurrentModeName, () => Is.EqualTo("idle"));
            AddAssert("spinner keeps raw cursor position", () =>
                (osuInputManager.CurrentState.Mouse.Position - rawSpinnerPosition).Length, () => Is.LessThan(2f));
        }

        [Test]
        public void TestRelaxLateSliderPressStillHolds()
        {
            DrawableSlider slider = null!;
            double startTime = 0;

            AddStep("replace with short slider", () =>
            {
                double start = relaxController.Time.Current + 900;

                slider = createSlider(new Vector2(132, 192), start, 100, new Vector2(180, 0));
                replaceHitObjects(slider);
            });
            AddUntilStep("short slider loaded", () => slider.IsLoaded && slider.HeadCircle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 30, sliderTailOffset: 0, syncRadius: 70, maxSyncDelay: 160);

            AddStep("store slider start time", () => startTime = slider.HitObject.StartTime);
            AddStep("stay just outside slider head", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre + new Vector2(40, 0)));
            AddUntilStep("late press and release logged", () => relaxController.InputEvents.Count >= 2);
            AddAssert("press happens after slider end", () => firstPress().Time - startTime, () => Is.GreaterThan(100));
            AddAssert("late slider press still keeps hold", () => firstRelease().Time - firstPress().Time, () => Is.GreaterThan(20));
        }

        [Test]
        public void TestRelaxTapOnlyOnOverlappingSliders()
        {
            DrawableSlider first = null!;
            DrawableSlider second = null!;
            double firstReleaseTime = double.NaN;

            AddStep("replace with overlapping sliders", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createSlider(new Vector2(132, 192), start, 520, new Vector2(220, 0)),
                    second = createSlider(new Vector2(260, 160), start + 110, 520, new Vector2(0, 180)));
            });
            AddUntilStep("overlapping sliders loaded", () => first.IsLoaded && first.HeadCircle.IsLoaded && second.IsLoaded && second.HeadCircle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 24, syncRadius: 0, maxSyncDelay: 0);

            AddStep("move cursor to first slider head", () => InputManager.MoveMouseTo(first.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("first slider release logged", () =>
                relaxController.InputEvents.Any(e => !e.IsPress && Math.Abs(e.TargetStartTime - first.HitObject.StartTime) < 1));
            AddStep("store first slider release", () =>
                firstReleaseTime = relaxController.InputEvents.First(e => !e.IsPress && Math.Abs(e.TargetStartTime - first.HitObject.StartTime) < 1).Time);
            AddAssert("overlapping slider is tapped instead of fully held", () => firstReleaseTime, () => Is.LessThan(first.HitObject.EndTime - 150));
            AddAssert("second overlapping slider still gets head press", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - second.HitObject.StartTime) < 1));
        }

        [Test]
        public void TestRelaxKeepsPendingPressAfterDrawableRemoval()
        {
            DrawableHitCircle circle = null!;
            double startTime = 0;
            float radius = 0;

            AddStep("replace with removable circle", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(circle = createCircle(new Vector2(256, 192), start));
            });
            AddUntilStep("removable circle loaded", () => circle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 12, syncRadius: 60, maxSyncDelay: 70);

            AddStep("store timing and radius", () =>
            {
                startTime = circle.HitObject.StartTime;
                radius = getHitCircleScreenRadius(circle);
            });
            AddStep("stay in sync ring", () => InputManager.MoveMouseTo(circle.ScreenSpaceDrawQuad.Centre + new Vector2(radius + 6, 0)));
            AddUntilStep("sync delay started", () => relaxController.CurrentPendingSyncDelay > 0);
            AddStep("remove drawable before timeout", () => playfield.HitObjectContainer.Remove(circle));
            AddUntilStep("press still logged after removal", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("pending click survives drawable removal", () => firstPress().Time - startTime, () => Is.GreaterThan(35));
        }

        [Test]
        public void TestRelaxDoesNotTreatEdgeContactAsFullyAimed()
        {
            DrawableHitCircle circle = null!;
            double startTime = 0;
            float radius = 0;

            AddStep("replace with edge-sensitive circle", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(circle = createCircle(new Vector2(256, 192), start));
            });
            AddUntilStep("edge-sensitive circle loaded", () => circle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 18, syncRadius: 32, maxSyncDelay: 40);

            AddStep("store timing and radius", () =>
            {
                startTime = circle.HitObject.StartTime;
                radius = getHitCircleScreenRadius(circle);
            });
            AddStep("move cursor away before edge approach", () => InputManager.MoveMouseTo(circle.ScreenSpaceDrawQuad.Centre + new Vector2(180, 0)));
            AddStep("schedule edge then center aim", () =>
            {
                double edgeDelay = Math.Max(0, circle.HitObject.StartTime - 18 - relaxController.Time.Current);
                double centreDelay = Math.Max(0, circle.HitObject.StartTime + 12 - relaxController.Time.Current);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(circle.ScreenSpaceDrawQuad.Centre + new Vector2(radius - 1, 0)), edgeDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(circle.ScreenSpaceDrawQuad.Centre), centreDelay);
            });
            AddUntilStep("edge-sensitive press logged", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("edge contact does not trigger immediate synced press", () => firstPress().Time - startTime,
                () => Is.GreaterThan(6).And.LessThan(45));
        }

        [Test]
        public void TestRelaxStableInteriorHoldCountsAsCommittedAim()
        {
            DrawableHitCircle circle = null!;
            double startTime = 0;
            float radius = 0;

            AddStep("replace with committed hold circle", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(circle = createCircle(new Vector2(256, 192), start));
            });
            AddUntilStep("committed hold circle loaded", () => circle.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 18, syncRadius: 32, maxSyncDelay: 45);

            AddStep("store timing and radius", () =>
            {
                startTime = circle.HitObject.StartTime;
                radius = getHitCircleScreenRadius(circle);
            });
            AddStep("move cursor away before interior hold", () => InputManager.MoveMouseTo(circle.ScreenSpaceDrawQuad.Centre + new Vector2(180, 0)));
            AddStep("schedule committed interior hold", () =>
            {
                double holdDelay = Math.Max(0, circle.HitObject.StartTime - 18 - relaxController.Time.Current);
                Vector2 committedPosition = circle.ScreenSpaceDrawQuad.Centre + new Vector2(radius * 0.78f, 0);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(committedPosition), holdDelay);
            });
            AddUntilStep("committed hold press logged", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("stable interior hold does not wait for perfect centre", () => firstPress().Time - startTime,
                () => Is.LessThan(30).And.GreaterThanOrEqualTo(-2));
        }

        [Test]
        public void TestRelaxReleasesAndRepressesSameActionAsActualInput()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;

            AddStep("replace with same-finger pair", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(180, 192), start),
                    second = createCircle(new Vector2(260, 192), start + 80));
            });
            AddUntilStep("same-finger pair loaded", () => first.IsLoaded && second.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 160, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 50, misaltProbability: 0);

            AddUntilStep("actual input sequence recorded", () => actionListener.Events.Count >= 4);
            AddAssert("actual input sees release and re-press", () =>
                actionListener.Events.Take(4).Select(e => $"{(e.IsPress ? "P" : "R")}:{e.Action}").SequenceEqual(new[]
                {
                    "P:LeftButton",
                    "R:LeftButton",
                    "P:LeftButton",
                    "R:LeftButton",
                }));
            AddAssert("controller leaves a visible repress gap", () =>
            {
                RelaxController.RelaxInputEvent[] events = relaxController.InputEvents.Where(e => e.Action == OsuAction.LeftButton).Take(4).ToArray();
                return events[2].Time - events[1].Time;
            }, () => Is.GreaterThan(20));
        }

        [Test]
        public void TestRelaxStreamBlindModeIgnoresSyncDelayOffTrajectory()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            double startTime = 0;
            float radius = 0;

            AddStep("replace with stream", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createCircle(new Vector2(170, 192), start),
                    second = createCircle(new Vector2(230, 192), start + 70),
                    third = createCircle(new Vector2(290, 192), start + 140));
            });
            AddUntilStep("stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 60, maxSyncDelay: 70,
                alternateThreshold: 100, streamBlindMode: true);

            AddStep("store timing and radius", () =>
            {
                startTime = first.HitObject.StartTime;
                radius = getHitCircleScreenRadius(first);
            });
            AddStep("move clearly off stream trajectory but inside sync ring", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(0, radius + 42)));
            AddUntilStep("press logged", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("stream blind mode heavily reduces sync wait", () => firstPress().Time - startTime, () => Is.LessThan(56));
        }

        [Test]
        public void TestRelaxStreamBlindModeAllowsCornerCutting()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            double startTime = 0;
            float radius = 0;

            AddStep("replace with another stream", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createCircle(new Vector2(170, 192), start),
                    second = createCircle(new Vector2(230, 192), start + 70),
                    third = createCircle(new Vector2(290, 192), start + 140));
            });
            AddUntilStep("second stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 10, syncRadius: 60, maxSyncDelay: 70,
                alternateThreshold: 100, streamBlindMode: true);

            AddStep("store timing and radius", () =>
            {
                startTime = first.HitObject.StartTime;
                radius = getHitCircleScreenRadius(first);
            });
            AddStep("move to mild corner-cut position", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(0, radius + 8)));
            AddUntilStep("press logged with mild offset", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("mild corner cut keeps sync delay", () => firstPress().Time - startTime, () => Is.GreaterThan(30));
        }

        [Test]
        public void TestRelaxUsesAimAssistVirtualCursorForSync()
        {
            double startTime = 0;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 28, syncRadius: 60, maxSyncDelay: 70,
                alternateThreshold: 135, misaltProbability: 0);

            AddStep("move note into assist settle window", () => hitCircle.HitObject.StartTime = relaxController.Time.Current + 320);
            AddStep("store start time", () => startTime = hitCircle.HitObject.StartTime);
            AddStep("move raw just outside circle", () =>
            {
                rawPosition = hitCircle.ScreenSpaceDrawQuad.Centre + new Vector2(42, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("aim assist pulled gameplay cursor close to circle", () =>
                (osuInputManager.CurrentState.Mouse.Position - hitCircle.ScreenSpaceDrawQuad.Centre).Length < 20f);
            AddUntilStep("press logged with assist cursor", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddAssert("assist prevents full sync timeout", () => firstPress().Time - startTime, () => Is.LessThan(45));
        }

        [Test]
        public void TestAimAssistKeepsPendingRelaxTargetInsteadOfLeadingNext()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            string sampledMode = string.Empty;
            bool? sampledAssistPointHasValue = null;
            int sampledPressCount = -1;
            Vector2 sampledTargetPosition = Vector2.Zero;

            AddStep("replace with assisted stream", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createCircle(new Vector2(150, 192), start),
                    second = createCircle(new Vector2(250, 150), start + 170),
                    third = createCircle(new Vector2(360, 230), start + 340));
            });
            AddUntilStep("assisted stream loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureAimAssist(strength: 1, fovRadius: 260, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            configureRelax(baseOffset: 80, timingVariance: 0, holdTime: 18, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 170, misaltProbability: 0);

            AddStep("move raw to first note", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre));
            AddStep("schedule raw pull and state sample", () =>
            {
                double rawPullDelay = Math.Max(0, first.HitObject.StartTime + 110 - relaxController.Time.Current);
                double sampleDelay = Math.Max(0, second.HitObject.StartTime - 20 - relaxController.Time.Current);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(third.ScreenSpaceDrawQuad.Centre), rawPullDelay);
                Scheduler.AddDelayed(() =>
                {
                    sampledMode = aimAssistController.CurrentModeName;
                    sampledAssistPointHasValue = aimAssistController.CurrentAssistPointPosition.HasValue;
                    sampledTargetPosition = aimAssistController.CurrentTargetPosition ?? Vector2.Zero;
                    sampledPressCount = pressCount();
                }, sampleDelay);
            });
            AddUntilStep("sample captured", () => sampledAssistPointHasValue.HasValue);
            AddAssert("sample happens before second press", () => sampledPressCount, () => Is.EqualTo(1));
            AddAssert("assist keeps tracking the pending stream note", () =>
                (sampledTargetPosition - second.ScreenSpaceDrawQuad.Centre).Length, () => Is.LessThan(18f));
            AddAssert("assist may keep flow mode while relax waits mid-stream", () => sampledMode, () => Is.Not.EqualTo("idle"));
        }

        [Test]
        public void TestRelaxAssistDrivenAimStillKeepsDelay()
        {
            DrawableHitCircle assistDrivenCircle = null!;
            double assistDrivenDelta = double.NaN;

            AddStep("replace with one aim timing note", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(assistDrivenCircle = createCircle(new Vector2(180, 192), start));
            });
            AddUntilStep("timing note loaded", () => assistDrivenCircle.IsLoaded);
            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            configureRelax(baseOffset: 40, timingVariance: 0, holdTime: 18, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 170, misaltProbability: 0);

            AddStep("move raw just outside assist-driven target", () =>
                InputManager.MoveMouseTo(assistDrivenCircle.ScreenSpaceDrawQuad.Centre + new Vector2(72, 0)));
            AddUntilStep("assist-driven press logged", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - assistDrivenCircle.HitObject.StartTime) < 1));
            AddStep("store assist-driven delta", () =>
                assistDrivenDelta = relaxController.InputEvents.First(e => e.IsPress && Math.Abs(e.TargetStartTime - assistDrivenCircle.HitObject.StartTime) < 1).Time
                                    - assistDrivenCircle.HitObject.StartTime);
            AddAssert("assist-only aim still keeps some configured delay", () => assistDrivenDelta, () => Is.GreaterThan(12));
        }

        [Test]
        public void TestRelaxTimingFeedbackPullsNextNoteEarlierAfterLateTail()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            DrawableHitCircle fourth = null!;
            float firstRadius = 0;
            float secondRadius = 0;
            float thirdRadius = 0;

            AddStep("replace with feedback chain", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createCircle(new Vector2(150, 192), start),
                    second = createCircle(new Vector2(228, 192), start + 220),
                    third = createCircle(new Vector2(306, 192), start + 440),
                    fourth = createCircle(new Vector2(384, 192), start + 660));
            });
            AddUntilStep("feedback chain loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded && fourth.IsLoaded);

            configureRelax(baseOffset: 22, timingVariance: 0, holdTime: 14, syncRadius: 44, maxSyncDelay: 48,
                stableBpm: 180, misaltProbability: 0);

            AddStep("store radii", () =>
            {
                firstRadius = getHitCircleScreenRadius(first);
                secondRadius = getHitCircleScreenRadius(second);
                thirdRadius = getHitCircleScreenRadius(third);
            });
            AddStep("schedule repeated late acquisitions", () =>
            {
                double firstRingDelay = Math.Max(0, first.HitObject.StartTime - 80 - relaxController.Time.Current);
                double firstCentreDelay = Math.Max(0, first.HitObject.StartTime + 26 - relaxController.Time.Current);
                double secondRingDelay = Math.Max(0, second.HitObject.StartTime - 70 - relaxController.Time.Current);
                double secondCentreDelay = Math.Max(0, second.HitObject.StartTime + 28 - relaxController.Time.Current);
                double thirdRingDelay = Math.Max(0, third.HitObject.StartTime - 70 - relaxController.Time.Current);
                double thirdCentreDelay = Math.Max(0, third.HitObject.StartTime + 28 - relaxController.Time.Current);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(firstRadius + 10, 0)), firstRingDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre), firstCentreDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre + new Vector2(secondRadius + 10, 0)), secondRingDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre), secondCentreDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(third.ScreenSpaceDrawQuad.Centre + new Vector2(thirdRadius + 10, 0)), thirdRingDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(third.ScreenSpaceDrawQuad.Centre), thirdCentreDelay);
            });
            AddUntilStep("three late presses logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("timing feedback becomes earlier after late tail", () => relaxController.CurrentAdaptiveTimingCorrection,
                () => Is.LessThan(-4));
            AddAssert("next note schedule is pulled earlier", () =>
            {
                if (!relaxController.TryGetTargetScheduledPressTime(fourth.HitObject, out double pressTime))
                    return double.NaN;

                return pressTime - fourth.HitObject.StartTime;
            }, () => Is.LessThan(18));
        }

        [Test]
        public void TestRelaxWideJumpAssistAimStillKeepsDelay()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            double secondDelta = double.NaN;

            AddStep("replace with assisted wide jump pair", () =>
            {
                double start = relaxController.Time.Current + 850;

                replaceHitObjects(
                    first = createCircle(new Vector2(110, 192), start),
                    second = createCircle(new Vector2(414, 138), start + 260));
            });
            AddUntilStep("assisted wide jump pair loaded", () => first.IsLoaded && second.IsLoaded);

            configureAimAssist(strength: 1, fovRadius: 240, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            configureRelax(baseOffset: 30, timingVariance: 0, holdTime: 18, syncRadius: 0, maxSyncDelay: 0,
                alternateThreshold: 180, misaltProbability: 0);

            AddStep("move raw to first note", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre));
            AddStep("schedule raw near second note but outside hitbox", () =>
            {
                double delay = Math.Max(0, second.HitObject.StartTime - 180 - relaxController.Time.Current);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre + new Vector2(72, 0)), delay);
            });
            AddUntilStep("second jump press logged", () =>
                relaxController.InputEvents.Any(e => e.IsPress && Math.Abs(e.TargetStartTime - second.HitObject.StartTime) < 1));
            AddStep("store second jump delta", () =>
                secondDelta = relaxController.InputEvents.First(e => e.IsPress && Math.Abs(e.TargetStartTime - second.HitObject.StartTime) < 1).Time
                              - second.HitObject.StartTime);
            AddAssert("assist-only jump aim does not erase jump offset", () => secondDelta, () => Is.GreaterThan(18));
        }

        [Test]
        public void TestRelaxPlayerEarlyAimPullsNextJumpScheduleEarlier()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            double thirdScheduledDelta = double.NaN;

            AddStep("replace with jump timing chain", () =>
            {
                double start = relaxController.Time.Current + 920;

                replaceHitObjects(
                    first = createCircle(new Vector2(132, 192), start),
                    second = createCircle(new Vector2(372, 124), start + 240),
                    third = createCircle(new Vector2(116, 266), start + 480));
            });
            AddUntilStep("jump timing chain loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 22, timingVariance: 0, holdTime: 18, syncRadius: 40, maxSyncDelay: 32,
                stableBpm: 180, misaltProbability: 0);

            AddStep("move raw away first", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(-110, 0)));
            AddStep("schedule normal then early jump aim", () =>
            {
                double firstAimDelay = Math.Max(0, first.HitObject.StartTime - 4 - relaxController.Time.Current);
                double secondAimDelay = Math.Max(0, second.HitObject.StartTime - 34 - relaxController.Time.Current);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre), firstAimDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre), secondAimDelay);
            });
            AddUntilStep("second press logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 2);
            AddUntilStep("player timing correction turned early", () => relaxController.CurrentPlayerTimingCorrection < -3.5);
            AddStep("store third scheduled delta", () =>
            {
                if (relaxController.TryGetTargetScheduledPressTime(third.HitObject, out double pressTime))
                    thirdScheduledDelta = pressTime - third.HitObject.StartTime;
            });
            AddAssert("player timing correction is early", () => relaxController.CurrentPlayerTimingCorrection, () => Is.LessThan(-3.5));
            AddAssert("next jump schedule is pulled earlier", () => thirdScheduledDelta, () => Is.LessThan(18));
        }

        [Test]
        public void TestRelaxPlayerTimingCorrectionDoesNotDriftEarlierWhenRawAimSlows()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            double correctionAfterEarlyAim = double.NaN;

            AddStep("replace with recovery timing chain", () =>
            {
                double start = relaxController.Time.Current + 920;

                replaceHitObjects(
                    first = createCircle(new Vector2(132, 192), start),
                    second = createCircle(new Vector2(372, 124), start + 240),
                    third = createCircle(new Vector2(116, 266), start + 480));
            });
            AddUntilStep("recovery timing chain loaded", () => first.IsLoaded && second.IsLoaded && third.IsLoaded);

            configureRelax(baseOffset: 22, timingVariance: 0, holdTime: 18, syncRadius: 40, maxSyncDelay: 32,
                stableBpm: 180, misaltProbability: 0);

            AddStep("move raw away before chain", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(-110, 0)));
            AddStep("schedule early then slower raw aim", () =>
            {
                double firstAimDelay = Math.Max(0, first.HitObject.StartTime - 4 - relaxController.Time.Current);
                double secondAimDelay = Math.Max(0, second.HitObject.StartTime - 34 - relaxController.Time.Current);
                double thirdResetDelay = Math.Max(0, third.HitObject.StartTime - 120 - relaxController.Time.Current);
                double thirdAimDelay = Math.Max(0, third.HitObject.StartTime + 34 - relaxController.Time.Current);

                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre), firstAimDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre), secondAimDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(third.ScreenSpaceDrawQuad.Centre - new Vector2(118, 0)), thirdResetDelay);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(third.ScreenSpaceDrawQuad.Centre), thirdAimDelay);
            });
            AddUntilStep("early correction established", () =>
            {
                if (relaxController.InputEvents.Count(e => e.IsPress) < 2 || relaxController.CurrentPlayerTimingCorrection >= -3.5)
                    return false;

                correctionAfterEarlyAim = relaxController.CurrentPlayerTimingCorrection;
                return true;
            });
            AddUntilStep("third press logged", () => relaxController.InputEvents.Count(e => e.IsPress) >= 3);
            AddAssert("player timing correction does not drift even earlier", () => relaxController.CurrentPlayerTimingCorrection,
                () => Is.GreaterThanOrEqualTo(correctionAfterEarlyAim));
        }

        [Test]
        public void TestRelaxDoesNotPressCurrentJumpEarlyFromParkedAim()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            double firstPressDelta = double.NaN;

            AddStep("replace with jump pair", () =>
            {
                double start = relaxController.Time.Current + 900;

                replaceHitObjects(
                    first = createCircle(new Vector2(144, 192), start),
                    second = createCircle(new Vector2(380, 128), start + 240));
            });
            AddUntilStep("jump pair loaded", () => first.IsLoaded && second.IsLoaded);

            configureRelax(baseOffset: 22, timingVariance: 0, holdTime: 18, syncRadius: 40, maxSyncDelay: 32,
                stableBpm: 180, misaltProbability: 0);

            AddStep("move raw away before jump", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre - new Vector2(118, 0)));
            AddStep("park raw on first jump far too early", () =>
            {
                double earlyAimDelay = Math.Max(0, first.HitObject.StartTime - 120 - relaxController.Time.Current);
                Scheduler.AddDelayed(() => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre), earlyAimDelay);
            });
            AddUntilStep("first jump press logged", () => relaxController.InputEvents.Any(e => e.IsPress));
            AddStep("store first press delta", () => firstPressDelta = firstPress().Time - first.HitObject.StartTime);
            AddAssert("parked early aim does not pull current jump before schedule", () => firstPressDelta, () => Is.GreaterThan(14));
        }

        [Test]
        public void TestRelaxDoesNotCancelAimAssistVirtualCursor()
        {
            configureAimAssist(strength: 1, fovRadius: 240, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            configureRelax(baseOffset: 0, timingVariance: 0, holdTime: 42, syncRadius: 30, maxSyncDelay: 20,
                alternateThreshold: 170, misaltProbability: 0);

            AddStep("move note into assist engage window", () => hitCircle.HitObject.StartTime = relaxController.Time.Current + 340);
            AddStep("move raw away from note", () => InputManager.MoveMouseTo(hitCircle.ScreenSpaceDrawQuad.Centre + new Vector2(96, 0)));
            AddUntilStep("assist engages virtual cursor", () =>
                osuInputManager.IsVirtualCursorActive
                && (osuInputManager.CurrentState.Mouse.Position - osuInputManager.OriginalUserCursorPosition).Length > 8f);
            AddWaitStep("let relax update another frame", 5);
            AddAssert("relax keeps assist cursor active", () => osuInputManager.IsVirtualCursorActive);
            AddAssert("cursor stays offset from raw input", () =>
                (osuInputManager.CurrentState.Mouse.Position - osuInputManager.OriginalUserCursorPosition).Length, () => Is.GreaterThan(5f));
        }

        private void configureRelax(double baseOffset = 0, double timingVariance = 0, double dynamicDrift = 0, double holdTime = 18,
                                    double sliderTailOffset = 0, float syncRadius = 0, double maxSyncDelay = 0,
                                    double alternateThreshold = 100, double misaltProbability = 0, double? stableBpm = null, bool streamBlindMode = false)
        {
            AddStep("configure relax", () =>
            {
                double resolvedStableBpm = stableBpm ?? (alternateThreshold > 0 ? 15000.0 / alternateThreshold : 180.0);
                config.SetValue(OsuSetting.ForkRelaxBaseOffset, baseOffset);
                config.SetValue(OsuSetting.ForkRelaxTimingVariance, timingVariance);
                config.SetValue(OsuSetting.ForkRelaxDynamicDrift, dynamicDrift);
                config.SetValue(OsuSetting.ForkRelaxHoldTime, holdTime);
                config.SetValue(OsuSetting.ForkRelaxSliderTailOffset, sliderTailOffset);
                config.SetValue(OsuSetting.ForkRelaxSyncRadius, syncRadius);
                config.SetValue(OsuSetting.ForkRelaxMaxSyncDelay, maxSyncDelay);
                config.SetValue(OsuSetting.ForkRelaxStableBpm, resolvedStableBpm);
                config.SetValue(OsuSetting.ForkRelaxAlternateThreshold, alternateThreshold);
                config.SetValue(OsuSetting.ForkRelaxMisaltProbability, misaltProbability);
                config.SetValue(OsuSetting.ForkRelaxStreamBlindMode, streamBlindMode);
                config.SetValue(OsuSetting.ForkRelaxEnabled, true);
                relaxController.ClearInputEvents();
            });
        }

        private void configureAimAssist(double strength = 1, float fovRadius = 200, double intentThreshold = -1, double dynamicFriction = 0,
                                        double antiJitterMs = 0, float overshootAllowance = 0, double centerBias = 1)
        {
            AddStep("configure aim assist", () =>
            {
                config.SetValue(OsuSetting.ForkAimAssistStrength, strength);
                config.SetValue(OsuSetting.ForkAimAssistFovRadius, fovRadius);
                config.SetValue(OsuSetting.ForkAimAssistIntentThreshold, intentThreshold);
                config.SetValue(OsuSetting.ForkAimAssistDynamicFriction, dynamicFriction);
                config.SetValue(OsuSetting.ForkAimAssistAntiJitterMs, antiJitterMs);
                config.SetValue(OsuSetting.ForkAimAssistOvershootAllowance, overshootAllowance);
                config.SetValue(OsuSetting.ForkAimAssistCenterBias, centerBias);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, true);
            });
        }

        private void resetRelaxConfig()
        {
            config.SetValue(OsuSetting.ForkShowInput, false);
            config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
            config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
            config.SetValue(OsuSetting.ForkRelaxEnabled, false);
            config.SetValue(OsuSetting.ForkRelaxBaseOffset, 0.0);
            config.SetValue(OsuSetting.ForkRelaxTimingVariance, 10.0);
            config.SetValue(OsuSetting.ForkRelaxDynamicDrift, 0.0);
            config.SetValue(OsuSetting.ForkRelaxHoldTime, 42.0);
            config.SetValue(OsuSetting.ForkRelaxHoldVariance, 4.0);
            config.SetValue(OsuSetting.ForkRelaxSliderTailOffset, 0.0);
            config.SetValue(OsuSetting.ForkRelaxSyncRadius, 16f);
            config.SetValue(OsuSetting.ForkRelaxMaxSyncDelay, 14.0);
            config.SetValue(OsuSetting.ForkRelaxBlindTapThreshold, 160f);
            config.SetValue(OsuSetting.ForkRelaxStableBpm, 180.0);
            config.SetValue(OsuSetting.ForkRelaxAlternateThreshold, 170.0);
            config.SetValue(OsuSetting.ForkRelaxPrimaryFingerReset, 220.0);
            config.SetValue(OsuSetting.ForkRelaxMisaltProbability, 0.0);
            config.SetValue(OsuSetting.ForkRelaxStackVarianceMultiplier, 1.35);
            config.SetValue(OsuSetting.ForkRelaxStreamBlindMode, false);
        }

        private DrawableHitCircle createCircle(Vector2 position, double startTime)
        {
            var hitObject = new HitCircle
            {
                Position = position,
                StartTime = startTime
            };

            hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return new DrawableHitCircle(hitObject);
        }

        private DrawableSlider createSlider(Vector2 position, double startTime, double duration, Vector2 pathEnd)
        {
            var slider = new Slider
            {
                Position = position,
                StartTime = startTime,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    Vector2.Zero,
                    pathEnd,
                }),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            slider.Path.ExpectedDistance.Value = slider.Velocity * duration;
            return new DrawableSlider(slider);
        }

        private DrawableSpinner createSpinner(double startTime, double duration)
        {
            var spinner = new Spinner
            {
                StartTime = startTime,
                Duration = duration,
            };

            spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return new DrawableSpinner(spinner);
        }

        private void replaceHitObjects(params DrawableHitObject[] objects)
        {
            foreach (DrawableHitObject existing in playfield.HitObjectContainer.Objects.OfType<DrawableHitObject>().ToList())
                playfield.HitObjectContainer.Remove(existing);

            foreach (DrawableHitObject drawable in objects)
                playfield.HitObjectContainer.Add(drawable);
        }

        private RelaxController.RelaxInputEvent firstPress() => relaxController.InputEvents.First(e => e.IsPress);

        private RelaxController.RelaxInputEvent firstRelease() => relaxController.InputEvents.First(e => !e.IsPress);

        private int pressCount()
        {
            int count = 0;

            for (int i = 0; i < relaxController.InputEvents.Count; i++)
            {
                if (relaxController.InputEvents[i].IsPress)
                    count++;
            }

            return count;
        }

        private static float getHitCircleScreenRadius(DrawableHitCircle circle)
        {
            float width = (circle.HitArea.ScreenSpaceDrawQuad.TopRight - circle.HitArea.ScreenSpaceDrawQuad.TopLeft).Length;
            float height = (circle.HitArea.ScreenSpaceDrawQuad.BottomLeft - circle.HitArea.ScreenSpaceDrawQuad.TopLeft).Length;
            return Math.Min(width, height) * 0.5f;
        }

        private partial class RecordedActionListener : Component, IKeyBindingHandler<OsuAction>
        {
            public readonly List<RecordedActionEvent> Events = new List<RecordedActionEvent>();

            public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
            {
                Events.Add(new RecordedActionEvent(true, e.Action));
                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
                => Events.Add(new RecordedActionEvent(false, e.Action));

            public void Clear() => Events.Clear();
        }

        private readonly record struct RecordedActionEvent(bool IsPress, OsuAction Action);
    }
}
