// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    /// <summary>
    /// A coloured, smoothed opponent cursor which is visible only during Ranked Play breaks.
    /// </summary>
    public partial class RankedPlayRemoteCursor : CompositeDrawable
    {
        private readonly DrawableRuleset drawableRuleset;
        private readonly IBindable<bool> isBreakTime;
        private readonly string username;
        private readonly CircularContainer cursorBody;
        private readonly OsuSpriteText label;

        private Vector2 targetPosition;
        private Vector2 displayedPosition;
        private bool hasPosition;
        private long lastSampleTimestamp;
        private byte buttons;

        public uint LastSequence { get; private set; }

        public bool HasPosition => hasPosition;

        internal static bool ShouldBeVisible(bool hasCursorPosition, bool isBreak, TimeSpan sampleAge)
            => hasCursorPosition && isBreak && sampleAge.TotalSeconds < 1;

        public RankedPlayRemoteCursor(string username, Color4 colour, DrawableRuleset drawableRuleset, IBindable<bool> isBreakTime)
        {
            this.drawableRuleset = drawableRuleset;
            this.isBreakTime = isBreakTime;
            this.username = username;

            Size = new Vector2(36);
            Origin = Anchor.Centre;
            Alpha = 0;
            AlwaysPresent = true;
            Depth = float.MinValue;

            InternalChildren = new Drawable[]
            {
                cursorBody = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 4,
                    BorderColour = colour,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                        Alpha = 0.42f,
                    }
                },
                new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(9),
                    Masking = true,
                    Colour = Color4.White,
                    Child = new Box { RelativeSizeAxes = Axes.Both },
                },
                label = new OsuSpriteText
                {
                    Position = new Vector2(27, -5),
                    Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                    Colour = colour,
                    Alpha = 0,
                }
            };

            label.Text = username;
        }

        public void Push(uint sequence, Vector2 normalisedPosition, ushort pingMilliseconds, byte buttonState)
        {
            if (hasPosition && sequence <= LastSequence)
                return;

            LastSequence = sequence;
            targetPosition = Vector2.ComponentMin(Vector2.One, Vector2.ComponentMax(Vector2.Zero, normalisedPosition));
            buttons = (byte)(buttonState & 0b11);
            lastSampleTimestamp = Stopwatch.GetTimestamp();
            label.Text = $"{username}  {pingMilliseconds} ms";

            if (!hasPosition)
            {
                displayedPosition = targetPosition;
                hasPosition = true;
            }
        }

        protected override void Update()
        {
            base.Update();

            TimeSpan sampleAge = hasPosition ? Stopwatch.GetElapsedTime(lastSampleTimestamp) : TimeSpan.MaxValue;

            if (!ShouldBeVisible(hasPosition, isBreakTime.Value, sampleAge))
            {
                Alpha = 0;
                label.Alpha = 0;
                return;
            }

            float interpolation = (float)(1 - Math.Exp(-Time.Elapsed / 1000 * 22));
            displayedPosition = Vector2.Lerp(displayedPosition, targetPosition, interpolation);

            Vector2 gamefieldPosition = displayedPosition * new Vector2(512, 384);
            Vector2 screenPosition = drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition);
            if (Parent != null)
                Position = Parent.ToLocalSpace(screenPosition);

            Alpha = 1;
            label.Alpha = Alpha;
            cursorBody.BorderThickness = buttons == 0 ? 4 : 6;
            Scale = new Vector2(buttons == 0 ? 1 : 0.82f);
        }
    }
}
