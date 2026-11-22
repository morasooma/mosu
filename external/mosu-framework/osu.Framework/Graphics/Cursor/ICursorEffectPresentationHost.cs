// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Containers;

namespace osu.Framework.Graphics.Cursor
{
    /// <summary>
    /// Provides presentation layers for cursor effects which must be rendered outside their
    /// logical owner's drawable subtree (for example, outside a backdrop capture framebuffer).
    /// </summary>
    public interface ICursorEffectPresentationHost
    {
        Container<Drawable> ContextMenuLayer { get; }

        Container<Drawable> PopoverLayer { get; }

        Container<Drawable> DropdownLayer { get; }

        /// <summary>
        /// Registers an externally-presented drawable against the lifetime and visibility of its logical owner.
        /// </summary>
        IDisposable Register(Drawable drawable, Func<bool> ownerIsActive, Action hide);

        /// <summary>
        /// Moves an externally-presented drawable in front of other drawables in its layer.
        /// </summary>
        void BringToFront(Drawable drawable);
    }
}
