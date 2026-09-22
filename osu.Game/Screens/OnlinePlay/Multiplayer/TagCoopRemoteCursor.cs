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
    /// A coloured, smoothed cursor belonging to another Tag Co-op player.
    /// </summary>
    public partial class TagCoopRemoteCursor : CompositeDrawable
    {
        public const byte AUTOMATED_FLAG = 1 << 7;

        private const int low_latency_enter_threshold = 170;
        private const int low_latency_leave_threshold = 200;

        private readonly int userId;
        private readonly string username;
        private readonly DrawableRuleset drawableRuleset;
        private readonly IBindable<bool> isBreakTime;
        private readonly Func<int?> activeUser;
        private readonly bool forceVisible;
        private readonly CircularContainer cursorBody;
        private readonly OsuSpriteText label;

        private Vector2 targetPosition;
        private Vector2 displayedPosition;
        private bool hasPosition;
        private bool lowLatency = true;
        private long lastSampleTimestamp;
        private ushort ping;
        private byte buttons;
        private bool automated;

        public uint LastSequence { get; private set; }

        public ushort PingMilliseconds => ping;

        public bool HasPosition => hasPosition;

        public TagCoopRemoteCursor(
            int userId,
            string username,
            Color4 colour,
            DrawableRuleset drawableRuleset,
            IBindable<bool> isBreakTime,
            Func<int?> activeUser,
            bool forceVisible = false)
        {
            this.userId = userId;
            this.username = username;
            this.drawableRuleset = drawableRuleset;
            this.isBreakTime = isBreakTime;
            this.activeUser = activeUser;
            this.forceVisible = forceVisible;

            Size = new Vector2(36);
            Origin = Anchor.Centre;
            Alpha = 0;
            // A cursor starts hidden until its first network sample. Without this, framework
            // presence optimisation stops Update() from running and it can never show itself.
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
        }

        public void Push(uint sequence, Vector2 normalisedPosition, ushort pingMilliseconds, byte buttonState)
        {
            if (hasPosition && sequence <= LastSequence)
                return;

            LastSequence = sequence;
            targetPosition = Vector2.ComponentMin(Vector2.One, Vector2.ComponentMax(Vector2.Zero, normalisedPosition));
            ping = pingMilliseconds;
            automated = (buttonState & AUTOMATED_FLAG) != 0;
            buttons = (byte)(buttonState & 0b11);
            lastSampleTimestamp = Stopwatch.GetTimestamp();

            if (!hasPosition)
            {
                displayedPosition = targetPosition;
                hasPosition = true;
            }

            if (lowLatency && ping > low_latency_leave_threshold)
                lowLatency = false;
            else if (!lowLatency && ping < low_latency_enter_threshold)
                lowLatency = true;

            label.Text = automated ? $"{username}  [BOT]  {ping} ms" : $"{username}  {ping} ms";
        }

        protected override void Update()
        {
            base.Update();

            if (!hasPosition)
                return;

            float interpolation = (float)(1 - Math.Exp(-Time.Elapsed / 1000 * 22));
            displayedPosition = Vector2.Lerp(displayedPosition, targetPosition, interpolation);

            Vector2 gamefieldPosition = displayedPosition * new Vector2(512, 384);
            Vector2 screenPosition = drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition);
            if (Parent != null)
                Position = Parent.ToLocalSpace(screenPosition);

            bool inBreak = isBreakTime.Value;
            bool fresh = Stopwatch.GetElapsedTime(lastSampleTimestamp).TotalSeconds < (inBreak ? 3 : 1);
            Alpha = fresh && (forceVisible || lowLatency || inBreak) ? 1 : 0;
            label.Alpha = (forceVisible || automated || inBreak) && Alpha > 0 ? 1 : 0;
            cursorBody.BorderThickness = activeUser() == userId ? 6 : 4;
            Scale = new Vector2(buttons == 0 ? 1 : 0.82f);
        }
    }
}
