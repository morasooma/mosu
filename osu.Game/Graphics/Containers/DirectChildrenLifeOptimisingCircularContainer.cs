// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// A <see cref="CircularContainer"/> which stops scanning its direct children once
    /// their set and lifetimes are known to be static.
    /// </summary>
    public partial class DirectChildrenLifeOptimisingCircularContainer : CircularContainer
    {
        private bool directChildrenLifeStable;

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
                Drawable child = InternalChildren[i];

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
