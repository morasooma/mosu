// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osuTK;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A drawable which can be skinned via an <see cref="ISkinSource"/>.
    /// </summary>
    public partial class SkinnableDrawable : SkinReloadableDrawable
    {
        /// <summary>
        /// The displayed component.
        /// </summary>
        public Drawable Drawable { get; private set; } = null!;

        /// <summary>
        /// Whether the drawable component should be centered in available space.
        /// Defaults to true.
        /// </summary>
        public bool CentreComponent = true;

        public new Axes AutoSizeAxes
        {
            get => base.AutoSizeAxes;
            set => base.AutoSizeAxes = value;
        }

        protected readonly ISkinComponentLookup ComponentLookup;

        private readonly ConfineMode confineMode;

        /// <summary>
        /// Create a new skinnable drawable.
        /// </summary>
        /// <param name="lookup">The namespace-complete resource name for this skinnable element.</param>
        /// <param name="defaultImplementation">A function to create the default skin implementation of this element.</param>
        /// <param name="confineMode">How (if at all) the <see cref="Drawable"/> should be resize to fit within our own bounds.</param>
        public SkinnableDrawable(ISkinComponentLookup lookup, Func<ISkinComponentLookup, Drawable>? defaultImplementation = null, ConfineMode confineMode = ConfineMode.NoScaling)
            : this(lookup, confineMode)
        {
            createDefault = defaultImplementation;
        }

        protected SkinnableDrawable(ISkinComponentLookup lookup, ConfineMode confineMode = ConfineMode.NoScaling)
        {
            ComponentLookup = lookup;
            this.confineMode = confineMode;

            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Seeks to the 0-th frame if the content of this <see cref="SkinnableDrawable"/> is an <see cref="IFramedAnimation"/>.
        /// </summary>
        public void ResetAnimation() => (Drawable as IFramedAnimation)?.GotoFrame(0);

        private readonly Func<ISkinComponentLookup, Drawable>? createDefault;

        private readonly Cached scaling = new Cached();

        private bool isDefault;

        protected virtual Drawable CreateDefault(ISkinComponentLookup lookup) => createDefault?.Invoke(lookup) ?? Empty();

        /// <summary>
        /// Whether to apply size restrictions (specified via <see cref="confineMode"/>) to the default implementation.
        /// </summary>
        protected virtual bool ApplySizeRestrictionsToDefault => false;

        protected override void SkinChanged(ISkinSource skin)
        {
            var retrieved = skin.GetDrawableComponent(ComponentLookup);

            if (retrieved == null)
            {
                Drawable = CreateDefault(ComponentLookup);
                isDefault = true;
            }
            else
            {
                Drawable = retrieved;
                isDefault = false;
            }

            scaling.Invalidate();

            if (CentreComponent)
            {
                Drawable.Origin = Anchor.Centre;
                Drawable.Anchor = Anchor.Centre;
            }

            InternalChild = Drawable;
            ResetLifetimeStability();
        }

        private bool directChildrenLifeStable;

        public void ResetLifetimeStability()
        {
            directChildrenLifeStable = false;
        }

        protected override bool CheckChildrenLife()
        {
            if (directChildrenLifeStable)
                return false;

            bool aliveChanged = base.CheckChildrenLife();
            directChildrenLifeStable = canSkipDirectChildLifeChecks();
            return aliveChanged;
        }

        private bool canSkipDirectChildLifeChecks()
        {
            if (InternalChildren.Count != AliveInternalChildren.Count)
                return false;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];

                if (!child.IsAlive && child.LifetimeStart > Time.Current)
                    return false;

                if (child.IsAlive && child.LifetimeEnd < double.MaxValue)
                    return false;
            }

            return true;
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

        protected override void Update()
        {
            base.Update();

            if (!scaling.IsValid)
            {
                try
                {
                    if (isDefault && !ApplySizeRestrictionsToDefault) return;

                    switch (confineMode)
                    {
                        case ConfineMode.ScaleToFit:
                            Drawable.RelativeSizeAxes = Axes.Both;
                            Drawable.Size = Vector2.One;
                            Drawable.Scale = Vector2.One;
                            Drawable.FillMode = FillMode.Fit;
                            break;
                    }
                }
                finally
                {
                    scaling.Validate();
                }
            }
        }
    }

    public enum ConfineMode
    {
        /// <summary>
        /// Don't apply any scaling. This allows the user element to be of any size, exceeding specified bounds.
        /// </summary>
        NoScaling,
        ScaleToFit,
    }
}
