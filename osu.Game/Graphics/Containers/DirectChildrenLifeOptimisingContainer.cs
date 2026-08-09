// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// Skips redundant direct child-life scans once all direct children are alive and none are scheduled to expire.
    /// </summary>
    public partial class DirectChildrenLifeOptimisingContainer : Container
    {
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

        /// <summary>
        /// Whether direct child-life scans can be skipped until the direct child set changes.
        /// </summary>
        protected virtual bool canSkipDirectChildLifeChecks()
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
    }
}