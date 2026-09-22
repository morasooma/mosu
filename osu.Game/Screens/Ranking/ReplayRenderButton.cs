// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class ReplayRenderButton : GrayButton, IHasPopover
    {
        private readonly ScoreInfo scoreInfo;

        // The chosen render period lives here rather than on the popover: the popover instance
        // is expired while the period selector screen is open (the suspended results screen's
        // PopoverContainer drops it), and a fresh popover is created on every open.
        private double? selectedTrimStart;
        private double? selectedTrimEnd;
        private bool pendingReopen;

        public ReplayRenderButton(ScoreInfo scoreInfo)
            : base(FontAwesome.Solid.Film)
        {
            this.scoreInfo = scoreInfo;

            Size = new Vector2(75, 30);
            TooltipText = "render replay to video";
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = this.ShowPopover;
        }

        protected override void Update()
        {
            base.Update();

            // The period selector suspends this screen; reopen the popover (with the persisted
            // period) once the results screen has resumed and this button is visible again
            // (a suspended screen fades out, making this button not present).
            if (pendingReopen && IsPresent)
            {
                pendingReopen = false;
                Schedule(() => this.ShowPopover());
            }
        }

        public Popover GetPopover()
        {
            pendingReopen = false;

            return new ReplayRenderPopover(scoreInfo, selectedTrimStart, selectedTrimEnd)
            {
                OnPeriodSelected = (start, end) =>
                {
                    selectedTrimStart = start;
                    selectedTrimEnd = end;
                    pendingReopen = true;
                }
            };
        }
    }
}
