// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// A device that holds a lethal line on and off forever.
    /// </summary>
    /// <remarks>
    /// The line the author is aiming is drawn here, faintly, while the editor is open — a beam whose reach
    /// is invisible until it fires cannot be placed accurately. The live beam is drawn by the view that
    /// runs the room, because when it is lethal is the simulation's answer, not this drawable's.
    /// </remarks>
    internal partial class WorldBeamDevice : WorldHazardDevice
    {
        /// <summary>How far the beam reaches, in world units.</summary>
        public float Length { get; set; } = 700;

        /// <summary>How thick the beam is, in world units.</summary>
        public new float Width { get; set; } = 46;

        /// <summary>How long the beam is lethal within each cycle, in milliseconds.</summary>
        public int ActiveMilliseconds { get; set; } = 900;

        public int Damage { get; set; } = 12;

        public override float? FixedDepth => null;
        public override string LayoutKind => "beam";

        private readonly Box aim;

        public WorldBeamDevice(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select)
            : base(colours, editing, select, FontAwesome.Solid.Bolt, colours.Purple1)
        {
            AddInternal(aim = new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreLeft,
                Colour = colours.Purple1.Opacity(0.25f),
                Alpha = 0,
                Depth = 1,
            });
        }

        protected override void Update()
        {
            base.Update();

            aim.Alpha = IsEditing ? 1 : 0;

            if (!IsEditing)
                return;

            aim.Size = new Vector2(Length, Width);
            aim.Rotation = Direction;
        }
    }
}
