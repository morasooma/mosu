// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions;
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

        public Popover GetPopover() => new ReplayRenderPopover(scoreInfo);
    }
}
