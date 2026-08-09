// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Edit.Design;
using osu.Game.Storyboards;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeStoryboardGeometryTest
    {
        [Test]
        public void TestArenaIsCentredInsideNormalStoryboardCanvas()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DodgeStoryboardCanvas.NORMAL_WIDTH, Is.EqualTo(640));
                Assert.That(DodgeStoryboardCanvas.CANVAS_HEIGHT, Is.EqualTo(480));
                Assert.That(DodgeStoryboardCanvas.ARENA_SIZE, Is.EqualTo(new Vector2(512, 384)));
                Assert.That((DodgeStoryboardCanvas.NORMAL_WIDTH - DodgeStoryboardCanvas.ARENA_SIZE.X) / 2, Is.EqualTo(64));
                Assert.That((DodgeStoryboardCanvas.CANVAS_HEIGHT - DodgeStoryboardCanvas.ARENA_SIZE.Y) / 2, Is.EqualTo(48));
            });
        }

        [Test]
        public void TestWidescreenKeepsFullStoryboardHeight()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DodgeStoryboardCanvas.WIDESCREEN_WIDTH, Is.EqualTo(480 * 16 / 9f));
                Assert.That(DodgeStoryboardCanvas.WIDESCREEN_WIDTH, Is.GreaterThan(DodgeStoryboardCanvas.NORMAL_WIDTH));
                Assert.That(DodgeStoryboardCanvas.CANVAS_HEIGHT, Is.EqualTo(480));
            });
        }

        [TestCase("animation0.png", "animation.png")]
        [TestCase("folder/animation12.png", "folder/animation.png")]
        [TestCase("folder/still.png", "folder/still.png")]
        public void TestAnimationBasePathDetection(string selectedFrame, string expectedBase)
            => Assert.That(DodgeDesignScreen.GetAnimationBasePathForTesting(selectedFrame), Is.EqualTo(expectedBase));

        [Test]
        public void TestNewObjectHasExplicitVisibleRange()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));

            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1234, 2000);

            Assert.Multiple(() =>
            {
                Assert.That(sprite.StartTime, Is.EqualTo(1234));
                Assert.That(sprite.EndTimeForDisplay, Is.EqualTo(3234));
                Assert.That(sprite.Commands.Alpha.Single().StartTime, Is.EqualTo(1234));
                Assert.That(sprite.Commands.Alpha.Single().EndTime, Is.EqualTo(3234));
                Assert.That(sprite.Commands.VectorScale, Is.Empty);
                Assert.That(sprite.Commands.X, Is.Empty);
                Assert.That(sprite.Commands.Y, Is.Empty);
                Assert.That(sprite.Commands.Rotation, Is.Empty);
            });
        }

        [Test]
        public void TestObjectRangeDragRetimesEveryCommandWithoutChangingValues()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            sprite.Commands.AddX(Easing.InOutQuad, 1500, 2500, 100, 500);

            StoryboardSprite retimed = DodgeStoryboardEditing.Retime(sprite, 5000, 9000);

            Assert.Multiple(() =>
            {
                Assert.That(retimed.StartTime, Is.EqualTo(5000));
                Assert.That(retimed.EndTimeForDisplay, Is.EqualTo(9000));
                Assert.That(retimed.Commands.X.Single().StartTime, Is.EqualTo(6000));
                Assert.That(retimed.Commands.X.Single().EndTime, Is.EqualTo(8000));
                Assert.That(retimed.Commands.X.Single().StartValue, Is.EqualTo(100));
                Assert.That(retimed.Commands.X.Single().EndValue, Is.EqualTo(500));
                Assert.That(retimed.Commands.X.Single().Easing, Is.EqualTo(Easing.InOutQuad));
                Assert.That(sprite.StartTime, Is.EqualTo(1000), "The immutable source is not edited in place.");
            });
        }

        [Test]
        public void TestCommandDragOnlyRetimesSelectedCommand()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            sprite.Commands.AddX(Easing.None, 1200, 1800, 100, 200);
            var alpha = sprite.Commands.Alpha.Single();

            StoryboardSprite retimed = DodgeStoryboardEditing.RetimeCommand(sprite, alpha, 1250, 2750);

            Assert.Multiple(() =>
            {
                Assert.That(retimed.Commands.Alpha.Single().StartTime, Is.EqualTo(1250));
                Assert.That(retimed.Commands.Alpha.Single().EndTime, Is.EqualTo(2750));
                Assert.That(retimed.Commands.X.Single().StartTime, Is.EqualTo(1200));
                Assert.That(retimed.Commands.X.Single().EndTime, Is.EqualTo(1800));
                Assert.That(retimed.Commands.Alpha.Single().StartValue, Is.EqualTo(1));
                Assert.That(retimed.Commands.Alpha.Single().EndValue, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestPositionEditSplitsExistingMoveAtPlayhead()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            sprite.Commands.AddX(Easing.None, 1000, 3000, 100, 300);
            sprite.Commands.AddY(Easing.None, 1000, 3000, 200, 400);

            StoryboardSprite edited = DodgeStoryboardEditing.SetPositionAt(sprite, 2000, new Vector2(250, 275), Easing.InOutQuad);

            Assert.Multiple(() =>
            {
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 2000).Position.X, Is.EqualTo(250).Within(0.001f));
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 2000).Position.Y, Is.EqualTo(275).Within(0.001f));
                Assert.That(edited.Commands.X, Has.Count.EqualTo(2));
                Assert.That(edited.Commands.Y, Has.Count.EqualTo(2));
                Assert.That(edited.Commands.Alpha.Single().StartTime, Is.EqualTo(1000));
                Assert.That(edited.Commands.Alpha.Single().EndTime, Is.EqualTo(3000));
                Assert.That(sprite.Commands.X, Has.Count.EqualTo(1), "The immutable source must remain unchanged.");
            });
        }

        [Test]
        public void TestScaleEditCreatesOnlyVectorScaleKey()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);

            StoryboardSprite edited = DodgeStoryboardEditing.SetEffectiveScaleAt(sprite, 1500, new Vector2(1.5f, 0.75f), Easing.Out);

            Assert.Multiple(() =>
            {
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 1500).EffectiveScale.X, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 1500).EffectiveScale.Y, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(edited.Commands.VectorScale, Has.Count.EqualTo(1));
                Assert.That(edited.Commands.Scale, Is.Empty);
                Assert.That(edited.Commands.Alpha.Single().StartTime, Is.EqualTo(1000));
                Assert.That(edited.Commands.Alpha.Single().EndTime, Is.EqualTo(3000));
            });
        }

        [Test]
        public void TestOpacityEditPreservesVisibleLifetime()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);

            StoryboardSprite edited = DodgeStoryboardEditing.SetOpacityAt(sprite, 2000, 0.4f, Easing.In);

            Assert.Multiple(() =>
            {
                Assert.That(edited.Commands.Alpha, Has.Count.EqualTo(1));
                Assert.That(edited.Commands.Alpha.Single().StartTime, Is.EqualTo(1000));
                Assert.That(edited.Commands.Alpha.Single().EndTime, Is.EqualTo(3000));
                Assert.That(edited.Commands.Alpha.Single().StartValue, Is.EqualTo(0.4f));
                Assert.That(edited.Commands.Alpha.Single().EndValue, Is.EqualTo(0.4f));
            });
        }

        [Test]
        public void TestWholeObjectTranslationMovesCompleteAnimatedPathWithoutAddingKeys()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            sprite.Commands.AddX(Easing.InOutQuad, 1000, 3000, 100, 500);
            sprite.Commands.AddY(Easing.InOutQuad, 1000, 3000, 200, 350);

            StoryboardSprite edited = DodgeStoryboardEditing.TranslateWholeObject(sprite, new Vector2(32, -16));

            Assert.Multiple(() =>
            {
                Assert.That(edited.Commands.X, Has.Count.EqualTo(1));
                Assert.That(edited.Commands.Y, Has.Count.EqualTo(1));
                Assert.That(edited.Commands.X.Single().StartValue, Is.EqualTo(132));
                Assert.That(edited.Commands.X.Single().EndValue, Is.EqualTo(532));
                Assert.That(edited.Commands.Y.Single().StartValue, Is.EqualTo(184));
                Assert.That(edited.Commands.Y.Single().EndValue, Is.EqualTo(334));
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 2000).Position,
                    Is.EqualTo(DodgeStoryboardEditing.StateAt(sprite, 2000).Position + new Vector2(32, -16)));
            });
        }

        [Test]
        public void TestWholeObjectScaleCreatesConstantRangeInsteadOfPlayheadKey()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);

            StoryboardSprite edited = DodgeStoryboardEditing.ScaleWholeObjectAt(sprite, 1750, new Vector2(1.5f, 0.75f));

            Assert.Multiple(() =>
            {
                Assert.That(edited.Commands.VectorScale, Has.Count.EqualTo(1));
                Assert.That(edited.Commands.VectorScale.Single().StartTime, Is.EqualTo(1000));
                Assert.That(edited.Commands.VectorScale.Single().EndTime, Is.EqualTo(3000));
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 1000).EffectiveScale, Is.EqualTo(new Vector2(1.5f, 0.75f)));
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 3000).EffectiveScale, Is.EqualTo(new Vector2(1.5f, 0.75f)));
            });
        }

        [Test]
        public void TestWholeObjectRotationOffsetsExistingAnimation()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            sprite.Commands.AddRotation(Easing.None, 1000, 3000, 10, 50);

            StoryboardSprite edited = DodgeStoryboardEditing.RotateWholeObjectAt(sprite, 2000, 60);

            Assert.Multiple(() =>
            {
                Assert.That(edited.Commands.Rotation, Has.Count.EqualTo(1));
                Assert.That(edited.Commands.Rotation.Single().StartValue, Is.EqualTo(40).Within(0.001f));
                Assert.That(edited.Commands.Rotation.Single().EndValue, Is.EqualTo(80).Within(0.001f));
                Assert.That(DodgeStoryboardEditing.StateAt(edited, 2000).Rotation, Is.EqualTo(60).Within(0.001f));
            });
        }

        [Test]
        public void TestBrokenDurationCanBeRepaired()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            sprite.Commands.AddAlpha(Easing.None, 9821, 9821, 1, 1);

            StoryboardSprite repaired = DodgeStoryboardEditing.RepairVisibleRange(sprite);

            Assert.Multiple(() =>
            {
                Assert.That(repaired.StartTime, Is.EqualTo(9821));
                Assert.That(repaired.EndTimeForDisplay, Is.EqualTo(11821));
                Assert.That(repaired.Commands.Alpha, Has.Count.EqualTo(1));
                Assert.That(repaired.Commands.Alpha.Single().StartValue, Is.EqualTo(1));
                Assert.That(repaired.Commands.Alpha.Single().EndValue, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestSelectionPresentationChangesOnlyWhenCrossingLifetime()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            var tracker = new StoryboardSelectionActivityTracker();
            StoryboardSprite[] sprites = { sprite };

            tracker.Capture(sprites, 500);

            Assert.Multiple(() =>
            {
                Assert.That(StoryboardSelectionState.Resolve(sprite, 500, true, true, true),
                    Is.EqualTo(StoryboardSelectionPresentation.OutsideRangeNotice));
                Assert.That(tracker.Update(sprites, 750), Is.False, "Seeking while remaining outside must not rebuild selection.");

                Assert.That(tracker.Update(sprites, 1000), Is.True, "Crossing StartTime must switch the notice to a drawable overlay.");
                Assert.That(StoryboardSelectionState.Resolve(sprite, 1000, true, true, true),
                    Is.EqualTo(StoryboardSelectionPresentation.DrawableOverlay));
                Assert.That(tracker.Update(sprites, 2500), Is.False, "Seeking within the lifetime must not rebuild selection.");
                Assert.That(tracker.Update(sprites, 3000), Is.False, "EndTime is inclusive.");

                Assert.That(tracker.Update(sprites, 3000.1), Is.True, "Crossing EndTime must switch the drawable overlay back to a notice.");
                Assert.That(StoryboardSelectionState.Resolve(sprite, 3000.1, true, true, true),
                    Is.EqualTo(StoryboardSelectionPresentation.OutsideRangeNotice));
                Assert.That(tracker.Update(sprites, 5000), Is.False, "Further seeking outside must not rebuild selection.");

                Assert.That(tracker.Update(sprites, 2000), Is.True, "Seeking backwards into the lifetime must also switch immediately.");
                Assert.That(StoryboardSelectionState.Resolve(sprite, 2000, true, true, true),
                    Is.EqualTo(StoryboardSelectionPresentation.DrawableOverlay));
            });
        }

        [Test]
        public void TestCornerScaleKeepsOppositeCornerFixedForEveryOriginAndHandle()
        {
            Anchor[] origins =
            {
                Anchor.TopLeft,
                Anchor.TopCentre,
                Anchor.TopRight,
                Anchor.CentreLeft,
                Anchor.Centre,
                Anchor.CentreRight,
                Anchor.BottomLeft,
                Anchor.BottomCentre,
                Anchor.BottomRight,
            };
            Vector2[] textureCorners =
            {
                new Vector2(0, 0),
                new Vector2(200, 0),
                new Vector2(200, 100),
                new Vector2(0, 100),
            };
            var startPosition = new Vector2(320, 240);
            var startScale = new Vector2(1.2f, 0.8f);
            var requestedRatio = new Vector2(1.4f, 0.6f);
            const float rotation = 27;

            foreach (Anchor origin in origins)
            {
                Vector2 originPixel = originPosition(origin, new Vector2(200, 100));
                Vector2[] corners = textureCorners
                                    .Select(corner => startPosition + rotate((corner - originPixel) * startScale, rotation))
                                    .ToArray();

                for (int draggedCorner = 0; draggedCorner < corners.Length; draggedCorner++)
                {
                    int fixedCornerIndex = (draggedCorner + 2) % corners.Length;
                    Vector2 fixedCorner = corners[fixedCornerIndex];
                    Vector2 startPointer = corners[draggedCorner];
                    Vector2 localDiagonal = rotate(startPointer - fixedCorner, -rotation);
                    Vector2 currentPointer = fixedCorner + rotate(localDiagonal * requestedRatio, rotation);

                    DodgeStoryboardEditing.CornerScaleResult result = DodgeStoryboardEditing.CalculateCornerScale(
                        startScale,
                        startPosition,
                        startPosition,
                        fixedCorner,
                        startPointer,
                        currentPointer,
                        rotation,
                        false);

                    Vector2 reconstructedFixedCorner =
                        result.Position + rotate((textureCorners[fixedCornerIndex] - originPixel) * result.EffectiveScale, rotation);
                    Vector2 reconstructedDraggedCorner =
                        result.Position + rotate((textureCorners[draggedCorner] - originPixel) * result.EffectiveScale, rotation);

                    Assert.Multiple(() =>
                    {
                        Assert.That(result.EffectiveScale.X, Is.EqualTo(startScale.X * requestedRatio.X).Within(0.001f),
                            $"{origin}, handle {draggedCorner}: X scale");
                        Assert.That(result.EffectiveScale.Y, Is.EqualTo(startScale.Y * requestedRatio.Y).Within(0.001f),
                            $"{origin}, handle {draggedCorner}: Y scale");
                        Assert.That(reconstructedFixedCorner.X, Is.EqualTo(fixedCorner.X).Within(0.001f),
                            $"{origin}, handle {draggedCorner}: fixed X");
                        Assert.That(reconstructedFixedCorner.Y, Is.EqualTo(fixedCorner.Y).Within(0.001f),
                            $"{origin}, handle {draggedCorner}: fixed Y");
                        Assert.That(reconstructedDraggedCorner.X, Is.EqualTo(currentPointer.X).Within(0.001f),
                            $"{origin}, handle {draggedCorner}: dragged X");
                        Assert.That(reconstructedDraggedCorner.Y, Is.EqualTo(currentPointer.Y).Within(0.001f),
                            $"{origin}, handle {draggedCorner}: dragged Y");
                    });
                }
            }
        }

        [Test]
        public void TestTimelineViewContainsObjectAndPlayhead()
        {
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "sprite.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 18000, 2000);

            (double start, double end) = DodgeStoryboardEditing.CalculateTimelineView(sprite, 5000);

            Assert.Multiple(() =>
            {
                Assert.That(start, Is.LessThanOrEqualTo(5000));
                Assert.That(end, Is.GreaterThanOrEqualTo(20000));
            });
        }

        private static Vector2 originPosition(Anchor origin, Vector2 size)
        {
            float x = origin switch
            {
                Anchor.TopCentre or Anchor.Centre or Anchor.BottomCentre => size.X / 2,
                Anchor.TopRight or Anchor.CentreRight or Anchor.BottomRight => size.X,
                _ => 0,
            };
            float y = origin switch
            {
                Anchor.CentreLeft or Anchor.Centre or Anchor.CentreRight => size.Y / 2,
                Anchor.BottomLeft or Anchor.BottomCentre or Anchor.BottomRight => size.Y,
                _ => 0,
            };
            return new Vector2(x, y);
        }

        private static Vector2 rotate(Vector2 value, float degrees)
        {
            float radians = MathHelper.DegreesToRadians(degrees);
            float sin = MathF.Sin(radians);
            float cos = MathF.Cos(radians);
            return new Vector2(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
        }
    }
}
