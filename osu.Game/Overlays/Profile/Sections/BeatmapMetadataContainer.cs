// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Sections
{
    /// <summary>
    /// Display artist/title/mapper information, commonly used as the left portion of a profile or score display row.
    /// </summary>
    public abstract partial class BeatmapMetadataContainer : OsuHoverContainer
    {
        private readonly IBeatmapInfo beatmapInfo;
        private FillFlowContainer textFlow = null!;
        private IBindable<Colour4> themeColour = null!;

        protected BeatmapMetadataContainer(IBeatmapInfo beatmapInfo)
        {
            this.beatmapInfo = beatmapInfo;

            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapSetOverlay? beatmapSetOverlay, OverlayColourProvider colourProvider)
        {
            Action = () =>
            {
                beatmapSetOverlay?.FetchAndShowBeatmap(beatmapInfo.OnlineID);
            };

            Child = textFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Children = CreateText(beatmapInfo),
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(colour =>
            {
                foreach (var text in textFlow.ChildrenOfType<SpriteText>())
                    text.Colour = colour.NewValue;
            }, true);
        }

        protected abstract Drawable[] CreateText(IBeatmapInfo beatmapInfo);
    }
}
