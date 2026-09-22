// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Displays labelled cursor tracks embedded in a fork Tag Co-op replay.
    /// The ordinary replay track still drives gameplay, but its merged cursor is hidden.
    /// </summary>
    public partial class TagCoopReplayOverlay : CompositeDrawable
    {
        private static readonly Color4[] palette =
        {
            new Color4(255, 102, 204, 255),
            new Color4(79, 195, 247, 255),
            new Color4(255, 202, 40, 255),
            new Color4(105, 240, 174, 255),
            new Color4(179, 136, 255, 255),
            new Color4(255, 112, 67, 255),
        };

        public TagCoopReplayOverlay(DrawableRuleset drawableRuleset, TagCoopReplayMetadata metadata)
        {
            RelativeSizeAxes = Axes.Both;
            Depth = float.MinValue;

            var framesByUser = metadata.Frames.GroupBy(frame => frame.UserID)
                                       .ToDictionary(group => group.Key, group => group.OrderBy(frame => frame.GameplayTime)
                                                                                     .ThenBy(frame => frame.Sequence)
                                                                                     .ToArray());

            var cursors = new List<Drawable>();
            var legendEntries = new List<Drawable>
            {
                new OsuSpriteText
                {
                    Text = "Tag Co-op replay",
                    Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                }
            };

            for (int i = 0; i < metadata.Players.Count; i++)
            {
                TagCoopReplayPlayer player = metadata.Players[i];
                Color4 colour = palette[i % palette.Length];

                legendEntries.Add(new OsuSpriteText
                {
                    Text = player.Username,
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    Colour = colour,
                });

                if (framesByUser.TryGetValue(player.UserID, out TagCoopReplayFrame[]? frames) && frames.Length > 0)
                    cursors.Add(new ReplayCursor(drawableRuleset, player.Username, colour, frames));
            }

            InternalChildren = cursors.Append(new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding { Top = 125, Right = 20 },
                Padding = new MarginPadding(10),
                Masking = true,
                CornerRadius = 8,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.65f,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 3),
                        Children = legendEntries,
                    }
                }
            }).ToArray();
        }

        private partial class ReplayCursor : CompositeDrawable
        {
            private const double button_flash_duration = 80;

            private readonly DrawableRuleset drawableRuleset;
            private readonly TagCoopReplayFrame[] frames;
            private int frameIndex;
            private double previousTime = double.NegativeInfinity;

            public ReplayCursor(DrawableRuleset drawableRuleset, string username, Color4 colour, TagCoopReplayFrame[] frames)
            {
                this.drawableRuleset = drawableRuleset;
                this.frames = frames;

                Size = new Vector2(34);
                Origin = Anchor.Centre;
                Alpha = 0;
                Depth = float.MinValue;

                // An alpha-zero drawable is excluded from presence processing and therefore does
                // not receive Update() calls. Replay cursors start hidden until their first frame,
                // so they must remain present long enough for Update() to make them visible.
                AlwaysPresent = true;

                InternalChildren = new Drawable[]
                {
                    new CircularContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = 4,
                        BorderColour = colour,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colour,
                            Alpha = 0.32f,
                        }
                    },
                    new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(8),
                        Masking = true,
                        Child = new Box { RelativeSizeAxes = Axes.Both },
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(25, -4),
                        Text = username,
                        Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                        Colour = colour,
                    }
                };
            }

            protected override void Update()
            {
                base.Update();

                double time = drawableRuleset.Playfield.Clock.CurrentTime;

                if (time < previousTime || frameIndex < 0 || frameIndex >= frames.Length || frames[frameIndex].GameplayTime > time)
                    frameIndex = findFrameAtOrBefore(time);
                else
                {
                    while (frameIndex < frames.Length - 1 && frames[frameIndex + 1].GameplayTime <= time)
                        frameIndex++;
                }

                previousTime = time;

                if (frameIndex < 0 || time < frames[0].GameplayTime - 500 || time > frames[^1].GameplayTime + 1000)
                {
                    Alpha = 0;
                    return;
                }

                TagCoopReplayFrame current = frames[frameIndex];
                byte currentButtons = (byte)(current.ButtonState & 0b11);
                Vector2 position = new Vector2(current.X, current.Y);

                if (frameIndex < frames.Length - 1)
                {
                    TagCoopReplayFrame next = frames[frameIndex + 1];
                    double duration = next.GameplayTime - current.GameplayTime;

                    if (duration > 0 && duration <= 500)
                    {
                        float progress = (float)Math.Clamp((time - current.GameplayTime) / duration, 0, 1);
                        position = Vector2.Lerp(position, new Vector2(next.X, next.Y), progress);
                    }
                }

                Vector2 gamefieldPosition = position * new Vector2(512, 384);
                Vector2 screenPosition = drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition);

                if (Parent != null)
                    Position = Parent.ToLocalSpace(screenPosition);

                bool showPressed = currentButtons != 0;

                // Keep very short taps visible in the cursor animation. Derive this from the
                // recorded timeline rather than mutable state so replay seeks behave correctly.
                for (int i = frameIndex; !showPressed && i >= 0 && time - frames[i].GameplayTime < button_flash_duration; i--)
                    showPressed = (frames[i].ButtonState & 0b11) != 0;

                Scale = new Vector2(showPressed ? 0.82f : 1);
                Alpha = 1;
            }

            private int findFrameAtOrBefore(double time)
            {
                int left = 0;
                int right = frames.Length - 1;
                int result = -1;

                while (left <= right)
                {
                    int middle = left + (right - left) / 2;

                    if (frames[middle].GameplayTime <= time)
                    {
                        result = middle;
                        left = middle + 1;
                    }
                    else
                        right = middle - 1;
                }

                return result;
            }
        }
    }
}
