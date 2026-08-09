// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public class LegacyJudgementPiecePerformanceTest
    {
        [Test]
        public void TestOldJudgementStopsFramedAnimationInSkinPerformanceMode()
        {
            assertAnimationStoppedInPerformanceMode(createAnimation =>
                new LegacyJudgementPieceOld(HitResult.Ok, () => createAnimation));
        }

        [Test]
        public void TestNewJudgementStopsFramedAnimationInSkinPerformanceMode()
        {
            assertAnimationStoppedInPerformanceMode(createAnimation =>
                new LegacyJudgementPieceNew(HitResult.Ok, () => createAnimation, null));
        }

        private static void assertAnimationStoppedInPerformanceMode(System.Func<DrawableAnimation, Drawable> createJudgement)
        {
            bool wasEnabled = SkinPerformanceMode.Enabled;

            try
            {
                SkinPerformanceMode.Enabled = true;

                var animation = new DrawableAnimation();
                animation.AddFrame(new Box());
                animation.AddFrame(new Box());
                var judgement = createJudgement(animation);

                animation.IsPlaying = true;
                (judgement as IAnimatableJudgement)?.PlayAnimation();

                Assert.That(animation.CurrentFrameIndex, Is.EqualTo(0));
                Assert.That(animation.IsPlaying, Is.False);
            }
            finally
            {
                SkinPerformanceMode.Enabled = wasEnabled;
            }
        }
    }
}
