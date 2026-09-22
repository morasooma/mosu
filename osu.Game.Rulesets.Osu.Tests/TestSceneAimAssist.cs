// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneAimAssist : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private OsuInputManager osuInputManager = null!;
        private OsuPlayfield playfield = null!;
        private DrawableHitCircle hitCircle = null!;
        private AimAssistController aimAssistController = null!;

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
            });

            AddStep("reset fork config", () =>
            {
                config.SetValue(OsuSetting.ForkShowInput, false);
                config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
            });

            AddUntilStep("scene loaded", () => osuInputManager.IsLoaded && hitCircle.IsLoaded);
            AddUntilStep("playfield has circle", () => playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Any());
            AddUntilStep("aim assist controller loaded", () =>
            {
                AimAssistController? controller = playfield.ChildrenOfType<AimAssistController>().SingleOrDefault();

                if (controller?.IsLoaded != true)
                    return false;

                aimAssistController = controller;
                return true;
            });
            AddStep("refresh hit circle timing", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 1000);
        }

        [Test]
        public void TestAimAssistPullsVirtualCursorTowardsTarget()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move mouse away from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(160, 0));
            });
            AddStep("move mouse towards note", () =>
            {
                rawPosition = targetPosition + new Vector2(80, 0);
                InputManager.MoveMouseTo(rawPosition);
            });

            AddAssert("raw position preserved", () => osuInputManager.OriginalUserCursorPosition, () => Is.EqualTo(rawPosition));
            AddUntilStep("controller acquired target", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddWaitStep("allow assist settle", 3);
            AddUntilStep("controller output is closer to note", () => (aimAssistController.CurrentOutputPosition - targetPosition).Length < (rawPosition - targetPosition).Length);
            AddUntilStep("virtual cursor is closer to note", () =>
            {
                Vector2 assistedPosition = osuInputManager.CurrentState.Mouse.Position;
                return assistedPosition != rawPosition
                       && (assistedPosition - targetPosition).Length < (rawPosition - targetPosition).Length;
            });
        }

        [Test]
        public void TestAimAssistStronglyPullsPointTargetsAtMaximumStrength()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 300, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move away from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(180, 0));
            });
            AddStep("move toward note", () =>
            {
                rawPosition = targetPosition + new Vector2(90, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("assist engaged", () => aimAssistController.PassedActivationFilters);
            AddWaitStep("allow assist settle", 4);
            AddAssert("point assist noticeably closes distance", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.LessThan(20f));
        }

        [Test]
        public void TestAimAssistLowCenterBiasDoesNotFullyRecentreInsideNote()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 260, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, centerBias: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move note into active window", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 140);
            AddStep("move inside note but away from centre", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                rawPosition = targetPosition + new Vector2(14, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("assist engages", () => aimAssistController.PassedActivationFilters);
            AddWaitStep("allow settle", 4);
            AddAssert("gameplay cursor does not fully recenter", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length, () => Is.GreaterThan(10f));
        }

        [Test]
        public void TestAimAssistUsesUndershootBiasedImpactPointOnWideJump()
        {
            DrawableHitCircle nextCircle = null!;
            Vector2 targetPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 260, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, centerBias: 0.4);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("create wide jump pair", () =>
            {
                double start = aimAssistController.Time.Current + 360;
                hitCircle.HitObject.Position = new Vector2(170, 192);
                hitCircle.HitObject.StartTime = start;
                playfield.HitObjectContainer.Add(nextCircle = createCircle(new Vector2(430, 192), start + 260));
            });
            AddUntilStep("wide jump pair loaded", () => nextCircle.IsLoaded);
            AddStep("move raw to undershoot side", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition - new Vector2(96, 0));
            });
            AddUntilStep("impact point exposed", () => aimAssistController.CurrentAssistPointPosition.HasValue);
            AddAssert("assist point stays on undershoot side of note", () =>
                aimAssistController.CurrentAssistPointPosition?.X ?? float.NaN, () => Is.LessThan(targetPosition.X - 4));
        }

        [Test]
        public void TestAimAssistKeepsShortDoubleInPointMode()
        {
            DrawableHitCircle nextCircle = null!;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, showFlowDebug: true);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("create close double", () =>
            {
                double start = aimAssistController.Time.Current + 360;
                hitCircle.HitObject.Position = new Vector2(186, 192);
                hitCircle.HitObject.StartTime = start;
                playfield.HitObjectContainer.Add(nextCircle = createCircle(new Vector2(248, 192), start + 78));
            });
            AddUntilStep("double loaded", () => nextCircle.IsLoaded);
            AddStep("move near later note before burst starts", () => InputManager.MoveMouseTo(nextCircle.ScreenSpaceDrawQuad.Centre + new Vector2(8, 0)));
            AddUntilStep("current target acquired", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddAssert("short double stays in point mode", () => aimAssistController.CurrentModeName, () => Is.EqualTo("point"));
            AddAssert("short double uses flow-biased assist point instead of note centre", () =>
                (aimAssistController.CurrentAssistPointPosition?.X ?? float.NaN) - aimAssistController.CurrentTargetPosition!.Value.X, () => Is.GreaterThan(3f));
        }

        [Test]
        public void TestAimAssistDoesNotRecentreAfterTargetIsAlreadyCaptured()
        {
            DrawableHitCircle nextCircle = null!;
            Vector2 rawPosition = Vector2.Zero;
            Vector2 targetPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 240, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("create close triple anchor", () =>
            {
                double start = aimAssistController.Time.Current + 320;
                hitCircle.HitObject.Position = new Vector2(186, 192);
                hitCircle.HitObject.StartTime = start;
                playfield.HitObjectContainer.Add(nextCircle = createCircle(new Vector2(248, 192), start + 78));
            });
            AddUntilStep("triple anchor loaded", () => nextCircle.IsLoaded);
            AddStep("move inside current note away from centre", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                rawPosition = targetPosition + new Vector2(14, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("captured target acquired", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddWaitStep("allow captured hold settle", 3);
            AddAssert("captured target does not get pulled hard toward centre", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length, () => Is.GreaterThan(9f));
            AddAssert("captured target stays close to raw input", () =>
                (osuInputManager.CurrentState.Mouse.Position - rawPosition).Length, () => Is.LessThan(4.5f));
        }

        [Test]
        public void TestVirtualCursorApiMovesGameplayCursor()
        {
            Vector2 rawPosition = Vector2.Zero;
            Vector2 virtualPosition = Vector2.Zero;

            AddStep("move real cursor", () =>
            {
                rawPosition = hitCircle.ScreenSpaceDrawQuad.Centre + new Vector2(140, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddAssert("raw cursor captured", () => osuInputManager.OriginalUserCursorPosition, () => Is.EqualTo(rawPosition));

            AddStep("move virtual cursor directly", () =>
            {
                virtualPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                osuInputManager.MoveVirtualCursorTo(virtualPosition);
            });

            AddAssert("raw cursor preserved", () => osuInputManager.OriginalUserCursorPosition, () => Is.EqualTo(rawPosition));
            AddAssert("gameplay cursor moved by api", () => osuInputManager.CurrentState.Mouse.Position, () => Is.EqualTo(virtualPosition));

            AddStep("release virtual cursor", () => osuInputManager.ResetVirtualCursor());
            AddAssert("gameplay cursor returned to raw input", () => osuInputManager.CurrentState.Mouse.Position, () => Is.EqualTo(rawPosition));
        }

        [Test]
        public void TestVirtualCursorApiClampsGameplayCursorToScreenBounds()
        {
            Vector2 rawPosition = Vector2.Zero;

            AddStep("move real cursor", () =>
            {
                rawPosition = hitCircle.ScreenSpaceDrawQuad.Centre + new Vector2(140, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddAssert("raw cursor captured", () => osuInputManager.OriginalUserCursorPosition, () => Is.EqualTo(rawPosition));

            AddStep("move virtual cursor far outside screen", () =>
            {
                var bounds = osuInputManager.ScreenSpaceDrawQuad.AABBFloat;
                Vector2 outsidePosition = bounds.BottomRight + new Vector2(640, 480);
                osuInputManager.MoveVirtualCursorTo(outsidePosition);
            });
            AddAssert("gameplay cursor stays within screen bounds", () =>
            {
                var bounds = osuInputManager.ScreenSpaceDrawQuad.AABBFloat;
                Vector2 clamped = osuInputManager.CurrentState.Mouse.Position;
                return clamped.X >= bounds.Left - 0.01f
                       && clamped.X <= bounds.Right + 0.01f
                       && clamped.Y >= bounds.Top - 0.01f
                       && clamped.Y <= bounds.Bottom + 0.01f;
            });

            AddStep("release virtual cursor", () => osuInputManager.ResetVirtualCursor());
            AddAssert("gameplay cursor returned to raw input", () => osuInputManager.CurrentState.Mouse.Position, () => Is.EqualTo(rawPosition));
        }

        [Test]
        public void TestAimAssistDoesNotSnapBackWhenTargetDisappears()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;
            float releaseDistance = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move mouse away from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                rawPosition = targetPosition + new Vector2(80, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("assist engaged", () => osuInputManager.CurrentState.Mouse.Position != rawPosition);
            AddStep("remove assisted target", () => playfield.HitObjectContainer.Remove(hitCircle));
            AddUntilStep("controller lost target", () => !aimAssistController.CurrentTargetPosition.HasValue);
            AddWaitStep("allow first release frame", 1);
            AddStep("store release distance", () =>
            {
                releaseDistance = (osuInputManager.CurrentState.Mouse.Position - rawPosition).Length;
            });
            AddAssert("cursor does not snap instantly to raw", () => releaseDistance, () => Is.GreaterThan(0.5f));
            AddWaitStep("allow release to continue", 6);
            AddAssert("cursor moves back toward raw", () =>
                (osuInputManager.CurrentState.Mouse.Position - rawPosition).Length,
                () => Is.LessThan(releaseDistance));
        }

        [Test]
        public void TestAimAssistKeepsOffsetWithoutFurtherInput()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;
            float releaseDistance = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move mouse away from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                rawPosition = targetPosition + new Vector2(80, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("assist engaged", () => osuInputManager.CurrentState.Mouse.Position != rawPosition);
            AddStep("move outside assist fov", () =>
            {
                rawPosition = targetPosition + new Vector2(260, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddWaitStep("allow first release frame", 1);
            AddStep("store release distance", () =>
            {
                releaseDistance = (osuInputManager.CurrentState.Mouse.Position - rawPosition).Length;
            });
            AddAssert("offset remains after leaving fov", () => releaseDistance, () => Is.GreaterThan(0.5f));
            AddWaitStep("wait without further raw movement", 8);
            AddAssert("stationary release does not move cursor", () =>
                (osuInputManager.CurrentState.Mouse.Position - rawPosition).Length,
                () => Is.EqualTo(releaseDistance).Within(0.25f));
        }

        [Test]
        public void TestAimAssistReleaseTracksPlayerMovementGradually()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 previousRaw = Vector2.Zero;
            Vector2 previousOutput = Vector2.Zero;
            Vector2 movement = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("engage assist", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                previousRaw = targetPosition + new Vector2(80, 0);
                InputManager.MoveMouseTo(previousRaw);
            });
            AddUntilStep("assist engaged", () => osuInputManager.CurrentState.Mouse.Position != previousRaw);
            AddStep("remove target", () => playfield.HitObjectContainer.Remove(hitCircle));
            AddUntilStep("controller lost target", () => !aimAssistController.CurrentTargetPosition.HasValue);
            AddWaitStep("allow release state", 1);
            AddStep("store current positions", () =>
            {
                previousRaw = osuInputManager.OriginalUserCursorPosition;
                previousOutput = osuInputManager.CurrentState.Mouse.Position;
                Vector2 currentOffset = previousOutput - previousRaw;
                movement = currentOffset.LengthSquared > 0.0001f ? currentOffset.Normalized() * 36f : new Vector2(-36, 0);
            });
            AddStep("move raw toward virtual cursor", () =>
            {
                InputManager.MoveMouseTo(previousRaw + movement);
            });
            AddWaitStep("allow release follow frame", 1);
            AddAssert("release never moves opposite to player", () =>
                Vector2.Dot(osuInputManager.CurrentState.Mouse.Position - previousOutput, movement),
                () => Is.GreaterThanOrEqualTo(-0.1f));
            AddAssert("release consumes no more than the player step", () =>
                (osuInputManager.CurrentState.Mouse.Position - previousOutput).Length,
                () => Is.LessThanOrEqualTo(movement.Length + 0.5f));
            AddAssert("player movement converges raw and assisted positions", () =>
                (osuInputManager.CurrentState.Mouse.Position - osuInputManager.OriginalUserCursorPosition).Length,
                () => Is.LessThan((previousOutput - previousRaw).Length));
        }

        [Test]
        public void TestAimAssistPreservesOneToOneMovementInsideHitbox()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawBefore = Vector2.Zero;
            Vector2 outputBefore = Vector2.Zero;
            Vector2 insideStep = new Vector2(7, 5);

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);
            AddStep("approach note from outside", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(90, 0));
                InputManager.MoveMouseTo(targetPosition + new Vector2(24, 0));
            });
            AddUntilStep("assisted cursor enters hitbox", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length < aimAssistController.CurrentBaseTargetRadius - insideStep.Length - 2);
            AddStep("store positions inside hitbox", () =>
            {
                rawBefore = osuInputManager.OriginalUserCursorPosition;
                outputBefore = osuInputManager.CurrentState.Mouse.Position;
            });
            AddStep("move freely inside hitbox", () => InputManager.MoveMouseTo(rawBefore + insideStep));
            AddWaitStep("allow inside movement", 1);
            AddAssert("inside movement remains one to one", () =>
                (osuInputManager.CurrentState.Mouse.Position - outputBefore - insideStep).Length,
                () => Is.LessThan(0.25f));
        }

        [Test]
        public void TestAimAssistRespectsFov()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(fovRadius: 30, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);

            AddStep("move mouse outside fov", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                rawPosition = targetPosition + new Vector2(100, 0);
                InputManager.MoveMouseTo(rawPosition);
            });

            AddAssert("raw position preserved", () => osuInputManager.OriginalUserCursorPosition, () => Is.EqualTo(rawPosition));
            AddAssert("gameplay cursor effectively unchanged", () =>
                (osuInputManager.CurrentState.Mouse.Position - rawPosition).Length,
                () => Is.LessThanOrEqualTo(12f));
        }

        [Test]
        public void TestAimAssistRespectsIntentThreshold()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;
            float distanceAfterConflictingMove = 0;

            configureAimAssist(fovRadius: 200, intentThreshold: 0.8, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);

            AddStep("move mouse near note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(60, 0));
            });
            AddUntilStep("cursor received initial assistance", () =>
            {
                Vector2 currentPosition = osuInputManager.CurrentState.Mouse.Position;
                return (currentPosition - targetPosition).Length < 60;
            });
            AddStep("move further away from note", () =>
            {
                rawPosition = targetPosition + new Vector2(90, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddStep("store distance after conflicting move", () =>
            {
                distanceAfterConflictingMove = (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length;
            });

            AddAssert("raw position preserved", () => osuInputManager.OriginalUserCursorPosition, () => Is.EqualTo(rawPosition));
            AddWaitStep("allow cursor update", 2);
            AddAssert("cursor did not get extra pull after intent mismatch", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.GreaterThanOrEqualTo(distanceAfterConflictingMove * 0.95f));
        }

        [Test]
        public void TestAimAssistDoesNotTeleportPastUserStep()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 farPosition = Vector2.Zero;
            Vector2 nearPosition = Vector2.Zero;
            Vector2 beforeAssist = Vector2.Zero;
            float rawStep = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move far from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                farPosition = targetPosition + new Vector2(160, 0);
                InputManager.MoveMouseTo(farPosition);
            });
            AddWaitStep("stabilise far position", 1);
            AddStep("store current gameplay position", () => beforeAssist = osuInputManager.CurrentState.Mouse.Position);
            AddStep("move near note", () =>
            {
                nearPosition = targetPosition + new Vector2(80, 0);
                rawStep = (nearPosition - farPosition).Length;
                InputManager.MoveMouseTo(nearPosition);
            });
            AddWaitStep("allow assist frame", 1);
            AddAssert("assist step is bounded", () =>
                (osuInputManager.CurrentState.Mouse.Position - beforeAssist).Length,
                () => Is.LessThanOrEqualTo(rawStep * 1.2f + 4f));
        }

        [Test]
        public void TestAimAssistDoesNotTeleportOnTinyMovement()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 farPosition = Vector2.Zero;
            Vector2 nearPosition = Vector2.Zero;
            Vector2 beforeAssist = Vector2.Zero;
            float rawStep = 0;

            configureAimAssist(strength: 0.55, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);
            AddStep("set target close to hit time", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 40);

            AddStep("move far from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                farPosition = targetPosition + new Vector2(140, 0);
                InputManager.MoveMouseTo(farPosition);
            });
            AddWaitStep("stabilise far position", 1);
            AddStep("store current gameplay position", () => beforeAssist = osuInputManager.CurrentState.Mouse.Position);
            AddStep("make tiny move toward note", () =>
            {
                nearPosition = farPosition - new Vector2(8, 0);
                rawStep = (nearPosition - farPosition).Length;
                InputManager.MoveMouseTo(nearPosition);
            });
            AddWaitStep("allow assist frame", 1);
            AddAssert("tiny input does not cause a large teleport", () =>
                (osuInputManager.CurrentState.Mouse.Position - beforeAssist).Length,
                () => Is.LessThanOrEqualTo(rawStep * 3f + 8f));
        }

        [Test]
        public void TestAimAssistAntiJitterDoesNotSnapToCentreOnClick()
        {
            Vector2 targetPosition = Vector2.Zero;
            float distanceBeforeClick = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 50, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move near note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(70, 0));
            });
            AddUntilStep("assist engaged", () => aimAssistController.PassedActivationFilters && osuInputManager.CurrentState.Mouse.Position != osuInputManager.OriginalUserCursorPosition);
            AddStep("store distance before click", () =>
            {
                distanceBeforeClick = (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length;
            });
            AddStep("click left mouse", () => InputManager.Click(MouseButton.Left));
            AddWaitStep("allow anti jitter frame", 1);
            AddAssert("click does not snap cursor to centre", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.GreaterThanOrEqualTo(distanceBeforeClick - 8f));
        }

        [Test]
        public void TestAimAssistHoldsTargetWhileStationary()
        {
            Vector2 targetPosition = Vector2.Zero;
            float distanceBeforeWait = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: 0, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move away from note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(160, 0));
            });
            AddStep("move near note", () =>
            {
                InputManager.MoveMouseTo(targetPosition + new Vector2(70, 0));
            });
            AddUntilStep("assist engaged", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length < 70f);
            AddStep("store distance before wait", () =>
            {
                distanceBeforeWait = (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length;
            });
            AddWaitStep("wait without movement", 8);
            AddAssert("cursor stays settled while stationary", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.LessThanOrEqualTo(Math.Max(distanceBeforeWait + 4f, 70.5f)));
        }

        [Test]
        public void TestAimAssistPointStrengthBuildsCloserToHitTime()
        {
            Vector2 targetPosition = Vector2.Zero;
            float farDistance = 0;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, showFlowDebug: true);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move near note early", () =>
            {
                hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 900;
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(110, 0));
            });
            AddWaitStep("allow early assist update", 2);
            AddStep("store early distance", () =>
            {
                farDistance = (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length;
            });
            AddStep("move note close to 300 timing", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 35);
            AddWaitStep("allow late assist update", 2);
            AddAssert("assist gets stronger near hit time", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.LessThan(farDistance - 6f));
        }

        [Test]
        public void TestAimAssistProvidesEarlySettlingWhenAlreadyNearTarget()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: 0, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move note to earlier settle window", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 260);
            AddStep("move close to note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                rawPosition = targetPosition + new Vector2(34, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddWaitStep("allow settle while stationary", 4);
            AddAssert("assist still nudges toward centre before 300 window", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.LessThan(30f));
        }

        [Test]
        public void TestAimAssistEarlySettlingDoesNotPullBackward()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 previousOutput = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: 0, dynamicFriction: 0.35, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move note to earlier settle window", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 260);
            AddStep("move close to note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(34, 0));
            });
            AddWaitStep("allow settle while stationary", 4);
            AddStep("store output before outward nudge", () => previousOutput = osuInputManager.CurrentState.Mouse.Position);
            AddStep("nudge raw slightly outward", () => InputManager.MoveMouseTo(targetPosition + new Vector2(40, 0)));
            AddWaitStep("allow response frame", 1);
            AddAssert("assist does not yank backward from the target", () =>
                Vector2.Dot(osuInputManager.CurrentState.Mouse.Position - previousOutput, Vector2.UnitX),
                () => Is.GreaterThanOrEqualTo(-0.5f));
        }

        [Test]
        public void TestAimAssistDropsLockAfterHit()
        {
            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move near note", () =>
            {
                Vector2 targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(70, 0));
            });
            AddUntilStep("target acquired", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddStep("force hit target", () => hitCircle.HitForcefully());
            AddUntilStep("target cleared after hit", () => !aimAssistController.CurrentTargetPosition.HasValue);
        }

        [Test]
        public void TestAimAssistDoesNotImmediatelyReacquireOverlappingFollowup()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            Vector2 sharedPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, showFlowDebug: true);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace with overlapping pair", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                double start = aimAssistController.Time.Current + 750;
                sharedPosition = new Vector2(256, 192);
                first = createCircle(sharedPosition, start);
                second = createCircle(sharedPosition, start + 70);
                playfield.HitObjectContainer.Add(first);
                playfield.HitObjectContainer.Add(second);
            });

            AddUntilStep("overlapping pair loaded", () => first.IsLoaded && second.IsLoaded);
            AddStep("move near first circle", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(70, 0)));
            AddUntilStep("first target acquired", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddStep("ensure first circle resolved", () =>
            {
                if (!first.Result.HasResult)
                    first.HitForcefully();
            });
            AddStep("move raw away from overlap", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre + new Vector2(150, 0)));
            AddWaitStep("allow overlap release frame", 2);
            AddAssert("overlapping follow-up is not immediately reacquired", () => aimAssistController.CurrentTargetPosition.HasValue, () => Is.False);
        }

        [Test]
        public void TestAimAssistCurrentTargetRadiusTracksDrawableScale()
        {
            float expectedRadius = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);
            AddUntilStep("target radius available", () => aimAssistController.CurrentTargetRadius > 0);
            AddStep("store hitarea radius", () =>
            {
                float width = (hitCircle.HitArea.ScreenSpaceDrawQuad.TopRight - hitCircle.HitArea.ScreenSpaceDrawQuad.TopLeft).Length;
                float height = (hitCircle.HitArea.ScreenSpaceDrawQuad.BottomLeft - hitCircle.HitArea.ScreenSpaceDrawQuad.TopLeft).Length;
                expectedRadius = Math.Min(width, height) * 0.5f;
            });
            AddAssert("target radius matches hitarea", () => aimAssistController.CurrentTargetRadius, () => Is.EqualTo(expectedRadius).Within(1.5f));
            AddStep("scale drawable down", () => hitCircle.Scale = new Vector2(0.5f));
            AddWaitStep("allow radius update", 1);
            AddStep("store scaled hitarea radius", () =>
            {
                float width = (hitCircle.HitArea.ScreenSpaceDrawQuad.TopRight - hitCircle.HitArea.ScreenSpaceDrawQuad.TopLeft).Length;
                float height = (hitCircle.HitArea.ScreenSpaceDrawQuad.BottomLeft - hitCircle.HitArea.ScreenSpaceDrawQuad.TopLeft).Length;
                expectedRadius = Math.Min(width, height) * 0.5f;
            });
            AddAssert("scaled target radius matches hitarea", () => aimAssistController.CurrentTargetRadius, () => Is.EqualTo(expectedRadius).Within(1.5f));
        }

        [Test]
        public void TestAimAssistAdaptiveRadiusExpandsForWideNextJump()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            float closeRadius = 0;
            float wideRadius = 0;
            float closeScale = 0;
            float wideScale = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace with close pair", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                double start = aimAssistController.Time.Current + 1000;
                first = createCircle(new Vector2(180, 192), start);
                second = createCircle(new Vector2(232, 192), start + 85);
                playfield.HitObjectContainer.Add(first);
                playfield.HitObjectContainer.Add(second);
            });
            AddUntilStep("close pair loaded", () => first.IsLoaded && second.IsLoaded);
            AddUntilStep("close radius available", () => aimAssistController.CurrentTargetRadius > 0);
            AddStep("store close radius", () =>
            {
                closeRadius = aimAssistController.CurrentTargetRadius;
                closeScale = aimAssistController.CurrentAdaptiveRadiusScale;
            });

            AddStep("replace with wide pair", () =>
            {
                playfield.HitObjectContainer.Remove(first);
                playfield.HitObjectContainer.Remove(second);

                double start = aimAssistController.Time.Current + 1000;
                first = createCircle(new Vector2(180, 192), start);
                third = createCircle(new Vector2(430, 124), start + 85);
                playfield.HitObjectContainer.Add(first);
                playfield.HitObjectContainer.Add(third);
            });
            AddUntilStep("wide pair loaded", () => first.IsLoaded && third.IsLoaded);
            AddUntilStep("wide radius available", () => aimAssistController.CurrentTargetRadius > 0);
            AddStep("store wide radius", () =>
            {
                wideRadius = aimAssistController.CurrentTargetRadius;
                wideScale = aimAssistController.CurrentAdaptiveRadiusScale;
            });

            AddAssert("wide next jump clearly increases assist radius", () => wideRadius, () => Is.GreaterThan(closeRadius + 9f));
            AddAssert("debug scale reflects adaptive radius change", () => wideScale, () => Is.GreaterThan(closeScale + 0.18f));
        }

        [Test]
        public void TestAimAssistAdaptiveRadiusRespondsQuicklyToWideJumpSwitch()
        {
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            DrawableHitCircle third = null!;
            float closeScale = 0;

            configureAimAssist(strength: 1, fovRadius: 200, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace with close pair", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                double start = aimAssistController.Time.Current + 1000;
                first = createCircle(new Vector2(180, 192), start);
                second = createCircle(new Vector2(232, 192), start + 85);
                playfield.HitObjectContainer.Add(first);
                playfield.HitObjectContainer.Add(second);
            });
            AddUntilStep("close pair loaded", () => first.IsLoaded && second.IsLoaded);
            AddUntilStep("close scale available", () => aimAssistController.CurrentAdaptiveRadiusScale > 0);
            AddStep("store close scale", () => closeScale = aimAssistController.CurrentAdaptiveRadiusScale);

            AddStep("replace with wide pair", () =>
            {
                playfield.HitObjectContainer.Remove(first);
                playfield.HitObjectContainer.Remove(second);

                double start = aimAssistController.Time.Current + 1000;
                first = createCircle(new Vector2(180, 192), start);
                third = createCircle(new Vector2(430, 124), start + 85);
                playfield.HitObjectContainer.Add(first);
                playfield.HitObjectContainer.Add(third);
            });
            AddUntilStep("wide pair loaded", () => first.IsLoaded && third.IsLoaded);
            AddWaitStep("allow quick response", 2);
            AddAssert("wide scale rises quickly after target switch", () => aimAssistController.CurrentAdaptiveRadiusScale, () => Is.GreaterThan(closeScale + 0.08f));
        }

        [Test]
        public void TestAimAssistTargetsSliderHeadBeforeSliderTracking()
        {
            DrawableSlider slider = null!;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, showFlowDebug: true);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace hit circle with slider", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                var sliderObject = new Slider
                {
                    StartTime = aimAssistController.Time.Current + 1000,
                    Position = OsuPlayfield.BASE_SIZE / 2 - new Vector2(120, 0),
                    Path = new SliderPath(PathType.LINEAR, new[]
                    {
                        Vector2.Zero,
                        new Vector2(240, 0),
                    }),
                };

                sliderObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                playfield.HitObjectContainer.Add(slider = new DrawableSlider(sliderObject));
            });

            AddUntilStep("slider loaded", () => slider?.IsLoaded == true && slider.HeadCircle.IsLoaded && slider.TailCircle.IsLoaded);
            AddStep("move near slider tail before start", () => InputManager.MoveMouseTo(slider.TailCircle.ScreenSpaceDrawQuad.Centre + new Vector2(6, 0)));
            AddUntilStep("target acquired", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddAssert("target stays on slider head before hit", () =>
                (aimAssistController.CurrentTargetPosition!.Value - slider.HeadCircle.ScreenSpaceDrawQuad.Centre).Length,
                () => Is.LessThanOrEqualTo(8f));
            AddAssert("target is not snapped to tail", () =>
                (aimAssistController.CurrentTargetPosition!.Value - slider.TailCircle.ScreenSpaceDrawQuad.Centre).Length,
                () => Is.GreaterThan(120f));
        }

        [Test]
        public void TestAimAssistTracksSliderAfterHeadHit()
        {
            DrawableSlider slider = null!;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 260, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace hit circle with longer slider", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                var sliderObject = new Slider
                {
                    StartTime = aimAssistController.Time.Current + 1800,
                    Position = OsuPlayfield.BASE_SIZE / 2 - new Vector2(130, 0),
                    Path = new SliderPath(PathType.LINEAR, new[]
                    {
                        Vector2.Zero,
                        new Vector2(260, 0),
                    }),
                };

                sliderObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                sliderObject.Path.ExpectedDistance.Value = sliderObject.Velocity * 4000;
                playfield.HitObjectContainer.Add(slider = new DrawableSlider(sliderObject));
            });

            AddUntilStep("tracking slider loaded", () => slider?.IsLoaded == true && slider.HeadCircle.IsLoaded && slider.Ball.IsLoaded);
            AddAssert("slider has meaningful duration", () => slider.HitObject.Duration, () => Is.GreaterThan(3000d));
            AddStep("move near slider head", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre + new Vector2(16, 0)));
            AddUntilStep("wait until slider pre-hit window", () => aimAssistController.Time.Current >= slider.HitObject.StartTime - 40);
            AddStep("hold primary key", () => InputManager.PressKey(Key.Z));
            AddUntilStep("wait for slider start", () => aimAssistController.Time.Current >= slider.HitObject.StartTime + 10);
            AddStep("move raw near slider ball", () =>
            {
                rawPosition = slider.Ball.ScreenSpaceDrawQuad.Centre + new Vector2(46, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddUntilStep("slider tracking mode active", () => aimAssistController.CurrentModeName is "slider" or "slider-repeat");
            AddUntilStep("slider tracking exposes assist point", () => aimAssistController.CurrentAssistPointPosition.HasValue);
            AddWaitStep("allow tracking settle", 2);
            AddAssert("virtual cursor is pulled toward slider tracking point", () =>
                (osuInputManager.CurrentState.Mouse.Position - aimAssistController.CurrentAssistPointPosition!.Value).Length,
                () => Is.LessThan((rawPosition - aimAssistController.CurrentAssistPointPosition!.Value).Length));
            AddStep("release primary key", () => InputManager.ReleaseKey(Key.Z));
        }

        [Test]
        public void TestAimAssistIgnoresSpinner()
        {
            DrawableSpinner spinner = null!;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 320, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace hit circle with spinner", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                var spinnerObject = new Spinner
                {
                    StartTime = aimAssistController.Time.Current + 650,
                    Duration = 1200,
                };

                spinnerObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                playfield.HitObjectContainer.Add(spinner = new DrawableSpinner(spinnerObject));
            });

            AddUntilStep("spinner loaded", () => spinner?.IsLoaded == true && spinner.Body.IsLoaded);
            AddStep("move near spinner ring", () =>
            {
                Vector2 spinnerCentre = spinner.Body.ScreenSpaceDrawQuad.Centre;
                rawPosition = spinnerCentre + new Vector2(140, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddWaitStep("allow spinner settle", 2);
            AddAssert("spinner is ignored as assist target", () => aimAssistController.CurrentTargetPosition.HasValue, () => Is.False);
            AddAssert("spinner keeps assist idle", () => aimAssistController.CurrentModeName, () => Is.EqualTo("idle"));
        }

        [Test]
        public void TestAimAssistHelpsQuickIsolatedPointFlick()
        {
            Vector2 targetPosition = Vector2.Zero;
            Vector2 rawPosition = Vector2.Zero;

            configureAimAssist(strength: 1, fovRadius: 260, intentThreshold: 0, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("move note into early settle window", () => hitCircle.HitObject.StartTime = aimAssistController.Time.Current + 320);
            AddStep("move far away from isolated note", () =>
            {
                targetPosition = hitCircle.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(targetPosition + new Vector2(220, 0));
            });
            AddStep("quick flick near isolated note", () =>
            {
                rawPosition = targetPosition + new Vector2(52, 0);
                InputManager.MoveMouseTo(rawPosition);
            });
            AddAssert("there is no next target", () => aimAssistController.CurrentNextTargetPosition.HasValue, () => Is.False);
            AddUntilStep("isolated point assist engages", () => aimAssistController.PassedActivationFilters);
            AddWaitStep("allow isolated settle", 2);
            AddAssert("isolated quick flick gets help", () =>
                (osuInputManager.CurrentState.Mouse.Position - targetPosition).Length,
                () => Is.LessThan(34f));
        }

        [Test]
        public void TestAimAssistDoesNotRushStreamStart()
        {
            DrawableHitCircle firstCircle = null!;
            DrawableHitCircle secondCircle = null!;
            DrawableHitCircle thirdCircle = null!;
            Vector2 sampledTargetPosition = Vector2.Zero;
            Vector2? sampledAssistPointPosition = null;
            string sampledMode = string.Empty;
            string sampledFlowMode = string.Empty;
            int sampledFlowPathPointCount = 0;

            configureAimAssist(strength: 1, fovRadius: 220, intentThreshold: -1, dynamicFriction: 0, antiJitterMs: 0, overshootAllowance: 0, showFlowDebug: true);
            AddUntilStep("aim assist enabled", () => aimAssistController.IsEnabled);

            AddStep("replace hit circle with stream", () =>
            {
                playfield.HitObjectContainer.Remove(hitCircle);

                HitCircle createCircle(Vector2 position, double startTime)
                {
                    var circle = new HitCircle
                    {
                        Position = position,
                        StartTime = startTime
                    };

                    circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    return circle;
                }

                double firstStartTime = aimAssistController.Time.Current + 1200;

                playfield.HitObjectContainer.Add(firstCircle = new DrawableHitCircle(createCircle(new Vector2(170, 192), firstStartTime)));
                playfield.HitObjectContainer.Add(secondCircle = new DrawableHitCircle(createCircle(new Vector2(228, 192), firstStartTime + 70)));
                playfield.HitObjectContainer.Add(thirdCircle = new DrawableHitCircle(createCircle(new Vector2(286, 192), firstStartTime + 140)));
            });

            AddUntilStep("stream circles loaded", () => firstCircle?.IsLoaded == true && secondCircle?.IsLoaded == true && thirdCircle?.IsLoaded == true);
            AddStep("move near later stream note before start", () => InputManager.MoveMouseTo(thirdCircle.ScreenSpaceDrawQuad.Centre + new Vector2(8, 0)));
            AddUntilStep("stream start target acquired", () => aimAssistController.CurrentTargetPosition.HasValue);
            AddAssert("sample happens before stream start", () => aimAssistController.Time.Current, () => Is.LessThan(firstCircle.HitObject.StartTime - 50));
            AddStep("capture stream start state", () =>
            {
                sampledMode = aimAssistController.CurrentModeName;
                sampledFlowMode = aimAssistController.CurrentFlowDebugModeName;
                sampledFlowPathPointCount = aimAssistController.CurrentFlowDebugPathPoints.Count;
                sampledTargetPosition = aimAssistController.CurrentTargetPosition ?? Vector2.Zero;
                sampledAssistPointPosition = aimAssistController.CurrentAssistPointPosition;
            });
            AddAssert("stream start stays in point mode", () => sampledMode, () => Is.EqualTo("point"));
            AddAssert("flow debug still recognises a flow pattern", () => sampledFlowMode, () => Is.Not.EqualTo("point"));
            AddAssert("flow debug already has a path before start", () => sampledFlowPathPointCount, () => Is.GreaterThanOrEqualTo(2));
            AddAssert("stream start uses a forward steering point", () =>
                (sampledAssistPointPosition?.X ?? float.NaN) - sampledTargetPosition.X, () => Is.GreaterThan(3f));
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

        private void configureAimAssist(double strength = 1, float fovRadius = 200, double intentThreshold = -1, double dynamicFriction = 0,
                                        double antiJitterMs = 0, float overshootAllowance = 0, double centerBias = 1, bool showFlowDebug = false)
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
                config.SetValue(OsuSetting.ForkAimAssistShowFlowDebug, showFlowDebug);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, true);
            });
        }
    }
}
