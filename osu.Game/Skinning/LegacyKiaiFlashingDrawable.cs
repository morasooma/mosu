// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    public partial class LegacyKiaiFlashingDrawable : BeatSyncedContainer
    {
        private bool directChildrenLifeStable;

        public Color4 KiaiGlowColour
        {
            get => FlashingDrawable.Colour;
            set => FlashingDrawable.Colour = value;
        }

        public readonly Drawable FlashingDrawable;

        private const float flash_opacity = 0.3f;

        public LegacyKiaiFlashingDrawable(Func<Drawable?> creationFunc)
        {
            AutoSizeAxes = Axes.Both;

            Drawable baseDrawable = (creationFunc.Invoke() ?? Empty()).With(d =>
            {
                d.Anchor = Anchor.Centre;
                d.Origin = Anchor.Centre;
            });

            if (SkinPerformanceMode.ShouldDisableKiaiFlashing)
            {
                // Kiai flashing duplicates every legacy hitcircle/overlay texture. In the
                // explicitly visual-changing performance mode keep only the base sprite.
                // This removes one drawable and one texture-backed draw opportunity per layer.
                FlashingDrawable = Empty();
                InternalChild = baseDrawable;
                return;
            }

            Children = new[]
            {
                baseDrawable,
                FlashingDrawable = (creationFunc.Invoke() ?? Empty()).With(d =>
                {
                    d.Anchor = Anchor.Centre;
                    d.Origin = Anchor.Centre;
                    d.Alpha = 0;
                    d.Blending = BlendingParameters.Additive;
                })
            };
        }

        protected override bool CheckChildrenLife()
        {
            if (directChildrenLifeStable)
                return false;

            bool aliveChanged = base.CheckChildrenLife();
            directChildrenLifeStable = InternalChildren.Count == AliveInternalChildren.Count;
            return aliveChanged;
        }

        protected override void AddInternal(Drawable drawable)
        {
            directChildrenLifeStable = false;
            base.AddInternal(drawable);
        }

        protected override bool RemoveInternal(Drawable drawable, bool disposeImmediately)
        {
            directChildrenLifeStable = false;
            return base.RemoveInternal(drawable, disposeImmediately);
        }

        protected override void ClearInternal(bool disposeChildren = true)
        {
            directChildrenLifeStable = false;
            base.ClearInternal(disposeChildren);
        }

        protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
        {
            if (SkinPerformanceMode.ShouldDisableKiaiFlashing || !effectPoint.KiaiMode)
                return;

            FlashingDrawable
                .FadeTo(flash_opacity)
                .Then()
                .FadeOut(Math.Max(80, timingPoint.BeatLength - 80), Easing.OutSine);
        }
    }
}
