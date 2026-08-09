// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    public partial class ServerExclusiveBeatmapBadge : BeatmapBadge
    {
        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            BadgeText = "exclusive";
            BadgeColour = colours.Lime1;
        }
    }
}
