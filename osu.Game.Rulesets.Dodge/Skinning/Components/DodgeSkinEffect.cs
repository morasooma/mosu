// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Skinning.Components
{
    /// <summary>
    /// A short, player-centred effect backed by a Dodge skin component.
    /// The effect deliberately lives outside gameplay geometry: changing its
    /// texture or animation never changes the player's collision size.
    /// </summary>
    public partial class DodgeSkinEffect : CompositeDrawable
    {
        public const float DEFAULT_SIZE = 48;
        public const double DEFAULT_DURATION = 240;

        public DodgeSkinComponents Component { get; }

        public int PlayCount { get; private set; }

        public OptionalDodgeSkinDrawable Visual { get; }

        public bool HasVisual => Visual.HasVisual;

        public DodgeSkinEffect(DodgeSkinComponents component)
        {
            if (component is not (DodgeSkinComponents.GrazeEffect or DodgeSkinComponents.CollisionEffect))
                throw new ArgumentOutOfRangeException(nameof(component), component, "Only transient Dodge effect components are supported.");

            Component = component;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(DEFAULT_SIZE);
            Alpha = 0;

            InternalChild = Visual = new OptionalDodgeSkinDrawable(component)
            {
                RelativeSizeAxes = Axes.Both,
            };
            Visual.OnSkinChanged += hideWhenMissing;
        }

        /// <summary>
        /// Restarts the effect from its first frame.
        /// </summary>
        public void Play()
        {
            if (!HasVisual)
            {
                Alpha = 0;
                return;
            }

            PlayCount++;

            FinishTransforms(true);
            Visual.ResetAnimation();
            Alpha = 1;
            Scale = new Vector2(0.65f);

            this.ScaleTo(1.55f, DEFAULT_DURATION, Easing.OutQuint);
            this.FadeOut(DEFAULT_DURATION, Easing.OutQuint);
        }

        private void hideWhenMissing()
        {
            if (HasVisual)
                return;

            FinishTransforms(true);
            Alpha = 0;
        }
    }
}
