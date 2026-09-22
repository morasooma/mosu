// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The floor of the current room.
    /// </summary>
    /// <remarks>
    /// Sized from its parent rather than from a constant: it used to be fixed at the default map size
    /// and pinned to the room's top left corner, so resizing a room in the editor left the floor
    /// covering the wrong part of it.
    /// </remarks>
    internal partial class RoomBackdrop : CompositeDrawable
    {
        public RoomBackdrop(OsuColour colours)
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1 };
        }
    }
}
