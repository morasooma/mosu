// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// What a character says, shown beside them.
    /// </summary>
    /// <remarks>
    /// Lives in the world next to the speaker, which is the confirmed design. In normal play the
    /// camera is at 1:1, so a world unit is a screen pixel and these sizes are literal: the earlier
    /// bubble read badly because it was drawn at 13px, not because of where it was. Only the editor
    /// zooms, and reading dialogue is not what the editor is for.
    /// <para>
    /// Beside the speaker rather than over their head, because over their head is a place whose height
    /// the author chooses: a tall object put its bubble off the top of the screen. To the side it is
    /// always the same distance above the ground, however big the thing talking is.
    /// </para>
    /// </remarks>
    internal partial class DialogueBubble : OsuClickableContainer
    {
        /// <summary>
        /// The bubble's width, and the height it has when the line inside it is short. A longer line
        /// grows it taller — see <see cref="Update"/>.
        /// </summary>
        public static readonly Vector2 SIZE = new Vector2(560, 184);

        /// <summary>How far to the side of the speaker the bubble floats.</summary>
        public const float GAP = 12;

        /// <summary>How far above the speaker's feet the bottom of the bubble sits.</summary>
        public const float LIFT = 44;

        private const float padding = 26;
        private const float body_top = 52;
        private const float body_bottom = 40;

        private readonly OsuSpriteText speaker;
        private readonly OsuTextFlowContainer body;
        private readonly OsuSpriteText page;

        private int shownPage = -1;
        private string shownLine = string.Empty;

        public DialogueBubble(OsuColour colours, Color4 accent, Action advance)
        {
            Action = advance;
            Size = SIZE;
            PlaceBeside(true);
            Alpha = 0;
            Masking = true;
            CornerRadius = 16;
            BorderThickness = 3;
            BorderColour = accent;

            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.97f) },
                speaker = new OsuSpriteText
                {
                    Position = new Vector2(padding, 18),
                    Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                    Colour = accent,
                },
                // A text flow rather than a multiline sprite: a sprite breaks a long line wherever it
                // runs out of width, mid-word, while a flow lays out words and breaks between them.
                body = new OsuTextFlowContainer(text => text.Font = OsuFont.Default.With(size: 22))
                {
                    Position = new Vector2(padding, body_top),
                    Width = SIZE.X - padding * 2,
                    AutoSizeAxes = Axes.Y,
                    LineSpacing = 0.2f,
                },
                page = new OsuSpriteText
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Right = padding, Bottom = 14 },
                    Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                    Colour = colours.Gray9,
                },
            };
        }

        public void SetSpeaker(string name) => speaker.Text = name.ToUpperInvariant();

        /// <summary>
        /// Puts the bubble on one side of the speaker or the other, at a fixed height above their feet.
        /// </summary>
        public void PlaceBeside(bool toTheRight)
        {
            Anchor = toTheRight ? Anchor.BottomRight : Anchor.BottomLeft;
            Origin = toTheRight ? Anchor.BottomLeft : Anchor.BottomRight;
            Position = new Vector2(toTheRight ? GAP : -GAP, -LIFT);
        }

        /// <summary>
        /// Brings the bubble in line with the conversation, or fades it out when there is none.
        /// Called every frame, so a line rewritten in the editor reaches the screen straight away.
        /// </summary>
        /// <param name="open">Whether a conversation is being had at all.</param>
        /// <param name="line">What is being said.</param>
        /// <param name="pageIndex">Which page of the open branch is being read, from zero.</param>
        /// <param name="pageCount">How many pages the open branch has.</param>
        public void Refresh(bool open, string line, int pageIndex, int pageCount)
        {
            if (!open)
            {
                if (Alpha > 0)
                {
                    this.FadeOut(140, Easing.OutQuint);
                    shownPage = -1;
                    shownLine = string.Empty;
                }

                return;
            }

            if (Alpha < 1)
                this.FadeIn(170, Easing.OutQuint);

            if (pageIndex == shownPage && line == shownLine)
                return;

            shownPage = pageIndex;
            shownLine = line;
            body.Text = line;
            page.Text = $"{pageIndex + 1} / {pageCount}   E";
        }

        /// <summary>
        /// Grows the bubble to hold whatever was written. The document allows a page of 500 characters,
        /// which is several lines however wide the bubble is, and clipping an author's line would be
        /// worse than a taller bubble.
        /// </summary>
        protected override void Update()
        {
            base.Update();
            Height = Math.Max(SIZE.Y, body_top + body.DrawHeight + body_bottom);
        }

        internal string LineForTesting => shownLine;

        internal float TextHeightForTesting => body.DrawHeight;
    }
}
