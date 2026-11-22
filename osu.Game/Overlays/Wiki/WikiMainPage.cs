// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using HtmlAgilityPack;
using osu.Framework.Allocation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Overlays.Wiki.Markdown;

namespace osu.Game.Overlays.Wiki
{
    public partial class WikiMainPage : FillFlowContainer
    {
        public string Markdown;

        public WikiMainPage()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            var html = new HtmlDocument();
            html.LoadHtml(Markdown ?? string.Empty);

            // Mosu's main page may be ordinary Markdown rather than the legacy
            // osu!web HTML template. Render that content instead of assuming panels.
            var panelNodes = html.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' wiki-main-page-panel ')]");
            if (panelNodes == null || panelNodes.Count == 0)
            {
                Child = new WikiMarkdownContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CurrentPath = $@"{api.Endpoints.WebsiteUrl}/wiki/",
                    Text = Markdown ?? string.Empty,
                    DocumentMargin = new MarginPadding(0),
                    DocumentPadding = new MarginPadding(0),
                };
                return;
            }

            var panels = createPanels(panelNodes.ToArray()).ToArray();

            Children = new Drawable[]
            {
                createBlurb(html),
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = Enumerable.Repeat(new Dimension(GridSizeMode.AutoSize), panels.Length).ToArray(),
                    Content = panels,
                },
            };
        }

        private Container createBlurb(HtmlDocument html)
        {
            var blurbNode = html.DocumentNode.SelectSingleNode("//div[contains(@class, 'wiki-main-page__blurb')]");

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding
                {
                    Vertical = 30,
                },
                Child = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(Typeface.Inter, size: 12, weight: FontWeight.Light))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = blurbNode?.InnerText ?? string.Empty,
                    TextAnchor = Anchor.TopCentre,
                }
            };
        }

        private IEnumerable<Drawable[]> createPanels(HtmlNode[] panelsNode)
        {
            int i = 0;

            while (i < panelsNode.Length)
            {
                bool isFullWidth = panelsNode[i].HasClass("wiki-main-page-panel--full");

                if (isFullWidth)
                {
                    yield return new Drawable[]
                    {
                        new WikiPanelContainer(panelsNode[i++].InnerText, true)
                        {
                            // This is required to fill up the space of "null" drawable below.
                            Width = 2,
                        },
                        null,
                    };
                }
                else
                {
                    yield return new Drawable[]
                    {
                        new WikiPanelContainer(panelsNode[i++].InnerText),
                        i < panelsNode.Length ? new WikiPanelContainer(panelsNode[i++].InnerText) : null,
                    };
                }
            }
        }
    }
}
