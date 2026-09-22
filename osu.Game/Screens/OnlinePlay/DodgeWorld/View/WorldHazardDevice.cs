// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// A device that repeats a hazard forever: the housing the player sees, plus what only the author does.
    /// </summary>
    /// <remarks>
    /// The housing is visible in play as well as in the editor, on purpose. A course is something to read
    /// before it is something to cross, and bullets arriving from an invisible source cannot be read at all.
    /// <para>
    /// Which way it is aimed, though, is shown to the author only. In play the aim is already told by the
    /// shots themselves, and a course covered in pointers reads worse than one that just runs.
    /// </para>
    /// <para>
    /// The device draws only itself. What it does is run by the simulation from the same numbers, so that
    /// a course behaves the same whether this client, another client, or the server is stepping it.
    /// </para>
    /// </remarks>
    internal abstract partial class WorldHazardDevice : EditableWorldEntity
    {
        /// <summary>Which way the device points, in degrees. Zero is to the right.</summary>
        public float Direction { get; set; }

        /// <summary>Degrees added to <see cref="Direction"/> on every cycle.</summary>
        public float Turn { get; set; }

        /// <summary>How long one repetition takes, in milliseconds.</summary>
        public int CycleMilliseconds { get; set; } = 2000;

        /// <summary>Offset into the cycle, for staggering several devices into a pattern.</summary>
        public int PhaseMilliseconds { get; set; }

        public override bool BlocksMovement => false;
        public override bool CanResize => false;
        public override bool CanScale => true;

        private readonly Container housing;
        private readonly Box barrel;
        private readonly OsuSpriteText phaseText;

        protected WorldHazardDevice(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select,
                                    IconUsage icon, osuTK.Graphics.Color4 accent)
            : base(editing, select, accent)
        {
            Size = new Vector2(96, 96);

            AddRangeInternal(new Drawable[]
            {
                // Drawn before the housing so the barrel appears to come out from under it.
                barrel = new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(46, 12),
                    Colour = accent.Opacity(0.85f),
                    // Hidden until the first update says otherwise, so a device placed in play never
                    // flashes its aim for a frame.
                    Alpha = 0,
                },
                housing = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(54),
                    Masking = true,
                    CornerRadius = 14,
                    BorderThickness = 3,
                    BorderColour = accent,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray2 },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = icon,
                            Size = new Vector2(24),
                            Colour = accent,
                        },
                    },
                },
                phaseText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                    Colour = colours.GrayA,
                },
            });
        }

        protected override void Update()
        {
            base.Update();

            // The aim and the timing are both authoring aids: they say what the device is about to do,
            // which is the author's question and not the player's.
            barrel.Alpha = phaseText.Alpha = IsEditing ? 1 : 0;

            if (!IsEditing)
                return;

            barrel.Rotation = Direction;
            phaseText.Text = describeTiming();
        }

        private string describeTiming()
        {
            string cycle = $"{CycleMilliseconds} мс";
            string phase = PhaseMilliseconds > 0 ? $" +{PhaseMilliseconds}" : string.Empty;
            string turn = Math.Abs(Turn) > 0.01f ? $" · {Turn:0.#}°/цикл" : string.Empty;
            return cycle + phase + turn;
        }

        /// <summary>The housing, for a subclass to reach when it needs to dress it.</summary>
        protected Container Housing => housing;
    }
}
