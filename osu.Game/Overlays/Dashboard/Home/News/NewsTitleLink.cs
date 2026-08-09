// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Dashboard.Home.News
{
    public partial class NewsTitleLink : OsuHoverContainer
    {
        private readonly APINewsPost post;
        private IBindable<Colour4> themeColour = null!;

        public NewsTitleLink(APINewsPost post)
        {
            this.post = post;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, OverlayColourProvider colourProvider)
        {
            Child = new TextFlowContainer(t =>
            {
                t.Font = OsuFont.GetFont(weight: FontWeight.Bold);
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = post.Title
            };

            HoverColour = colourProvider.Light1;

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                IdleColour = colourProvider.Content1;
                HoverColour = colourProvider.Light1;
            }, true);

TooltipText = CommonStrings.ViewInBrowser;
                Action = () => host.OpenUrlExternally(post.ExternalUrl ?? "https://osu.ppy.sh/home/news/" + post.Slug);
        }
    }
}
