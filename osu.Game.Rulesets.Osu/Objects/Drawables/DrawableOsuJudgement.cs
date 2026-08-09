// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class DrawableOsuJudgement : DrawableJudgement
    {
        internal Color4 AccentColour { get; private set; }

        internal SkinnableLighting? Lighting { get; private set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private Vector2? screenSpacePosition;
        private DrawableOsuHitObject? judgedOsuObject;

        public override void Apply(JudgementResult result, DrawableHitObject? judgedObject)
        {
            base.Apply(result, judgedObject);

            if (judgedObject is not DrawableOsuHitObject osuObject)
                return;

            judgedOsuObject = osuObject;

            AccentColour = osuObject.AccentColour.Value;

            switch (osuObject)
            {
                case DrawableSlider slider:
                    screenSpacePosition = slider.TailCircle.ToScreenSpace(slider.TailCircle.OriginPosition);
                    break;

                default:
                    screenSpacePosition = osuObject.ToScreenSpace(osuObject.OriginPosition);
                    break;
            }

            Scale = new Vector2(osuObject.HitObject.Scale);
        }

        protected override void PrepareForUse()
        {
            if (Lighting != null)
            {
                Lighting.ResetAnimation();
                Lighting.SetColourFrom(this, Result);
                Lighting.ClearTransforms();
                Lighting.Alpha = 0;
            }

            base.PrepareForUse();

            if (screenSpacePosition != null)
                Position = Parent!.ToLocalSpace(screenSpacePosition.Value);
        }

        protected override void ApplyHitAnimations()
        {
            bool hitLightingEnabled = config.Get<bool>(OsuSetting.HitLighting);

            if (hitLightingEnabled && !SkinPerformanceMode.Enabled)
            {
                var lighting = ensureLighting();

                lighting.ResetAnimation();
                lighting.SetColourFrom(this, Result);
                lighting.ClearTransforms();
                lighting.Alpha = 0;

                // todo: this animation changes slightly based on new/old legacy skin versions.
                lighting.ScaleTo(0.8f).ScaleTo(1.2f, 600, Easing.Out);
                lighting.FadeIn(200).Then().Delay(200).FadeOut(1000);

                // extend the lifetime to cover lighting fade
                LifetimeEnd = lighting.LatestTransformEndTime;
            }

            base.ApplyHitAnimations();
        }

        protected override void ApplyMissAnimations()
        {
            if (shouldHideSliderMissVisual())
            {
                // Prevent the miss cross ("крестик") from appearing for slider internal misses (ticks, tails, repeats).
                Alpha = 0;
                LifetimeEnd = Time.Current;
                return;
            }

            base.ApplyMissAnimations();
        }

        private bool shouldHideSliderMissVisual()
        {
            if (judgedOsuObject == null)
                return false;

            // config may be null in some test contexts
            if (config?.Get<bool>(OsuSetting.ForkHideVisualSliderMisses) != true)
                return false;

            var resultType = Result?.VisualType ?? Result?.Type ?? HitResult.None;
            if (resultType.IsHit())
                return false;

            return judgedOsuObject is DrawableSliderTick || judgedOsuObject is DrawableSliderTail || judgedOsuObject is DrawableSliderRepeat;
        }

        protected override Drawable CreateDefaultJudgement(HitResult result)
        {
            if (result.IsHit() && result.IsTick())
                return Empty();

            // Note: hiding logic for slider misses is handled dynamically in ApplyMissAnimations
            // (and at the playfield level) because of pooling - this method is called at pool item creation time
            // when the actual judged object is not yet known.
            return new OsuJudgementPiece(result);
        }

        private partial class OsuJudgementPiece : DefaultJudgementPiece
        {
            public OsuJudgementPiece(HitResult result)
                : base(result)
            {
            }

            public override void PlayAnimation()
            {
                if (SkinPerformanceMode.Enabled)
                {
                    base.PlayAnimation();
                    return;
                }

                if (Result != HitResult.Miss)
                {
                    JudgementText
                        .ScaleTo(new Vector2(0.8f, 1))
                        .ScaleTo(new Vector2(1.2f, 1), 1800, Easing.OutQuint);
                }

                base.PlayAnimation();
            }
        }

        private SkinnableLighting ensureLighting()
        {
            if (Lighting != null)
                return Lighting;

            AddInternal(Lighting = new SkinnableLighting
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Blending = BlendingParameters.Additive,
                Depth = float.MaxValue,
                Alpha = 0
            });

            return Lighting;
        }
    }
}
