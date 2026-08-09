// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Performance;

namespace osu.Game.Screens.Play
{
    public partial class ScrollingMessage : CompositeDrawable
    {
        private const float scroll_speed = 0.05f;

        private readonly Drawable messageContent;

        /// <summary>
        /// Unquantised scroll position, so quantising the applied <see cref="Drawable.X"/> does not change speed.
        /// </summary>
        private float scrollOffset;

        public ScrollingMessage(Drawable messageContent)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = this.messageContent = messageContent;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            messageContent.FadeInFromZero(2000, Easing.OutQuint);
            resetMessagePosition();
        }

        protected override void Update()
        {
            base.Update();

            if (messageContent.X + messageContent.DrawWidth > 0)
                scrollBy((float)Clock.ElapsedFrameTime * scroll_speed);
            else
                resetMessagePosition();
        }

        /// <summary>
        /// Advances the marquee, snapping the applied position to whole pixels.
        /// </summary>
        /// <remarks>
        /// Assigning <see cref="Drawable.X"/> invalidates <see cref="Invalidation.DrawInfo"/> down the entire
        /// glyph subtree of the message. At uncapped frame rates the untruncated movement is well under a tenth
        /// of a pixel per frame, so the vast majority of those invalidation sweeps produce no visible change.
        /// Accumulating in <see cref="scrollOffset"/> and only writing when the whole-pixel position actually
        /// moves keeps the on-screen speed identical while collapsing the invalidation storms.
        ///
        /// This was measured as the single largest invalidation source in the gameplay scene, though note it is
        /// only ever constructed by <see cref="ReplayPlayer"/> and <see cref="SpectatorPlayer"/> -- it does not
        /// exist during normal local play.
        /// </remarks>
        private void scrollBy(float amount)
        {
            if (!MosuOptimisationToggles.QuantiseScrollingMessage)
            {
                messageContent.X -= amount;
                return;
            }

            scrollOffset -= amount;

            float quantised = MathF.Round(scrollOffset);

            if (quantised != messageContent.X)
                messageContent.X = quantised;
        }

        private void resetMessagePosition()
        {
            scrollOffset = DrawWidth + 10;
            messageContent.X = MosuOptimisationToggles.QuantiseScrollingMessage ? MathF.Round(scrollOffset) : scrollOffset;
        }
    }
}
