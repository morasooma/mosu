// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    public partial class CheatLikenessStatistic : CompositeDrawable
    {
        private readonly GameplayPerformanceSnapshot.CheatLikenessReport report;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuSpriteText riskLabel = null!;
        private OsuSpriteText confidenceLabel = null!;
        private OsuTextFlowContainer summaryText = null!;
        private IBindable<Colour4> themeColour = null!;

        public CheatLikenessStatistic(GameplayPerformanceSnapshot.CheatLikenessReport report)
        {
            this.report = report;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    createSummaryCard(),
                    createSummaryText(),
                    createDetailsTable(),
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            if (OverlayColourProvider.IsLightTheme)
            {
                riskLabel.Colour = colourProvider.Content1;
                confidenceLabel.Colour = colourProvider.Content1.Opacity(0.85f);
            }
            else
            {
                riskLabel.Colour = Color4.White;
                confidenceLabel.Colour = Color4.White.Opacity(0.85f);
            }

            if (summaryText != null)
                summaryText.Colour = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White;
        }

        private Drawable createSummaryCard()
        {
            Color4 accent = getAccentColour();

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = accent.Opacity(0.16f),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(14),
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = report.IsAvailable ? $"{report.Percent:0}%" : "N/A",
                                Font = OsuFont.Torus.With(size: 34, weight: FontWeight.Bold),
                                Colour = accent,
                            },
                            riskLabel = new OsuSpriteText
                            {
                                Text = report.IsAvailable ? report.RiskLabel : "Analysis inactive",
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                Colour = Color4.White,
                            },
                            confidenceLabel = new OsuSpriteText
                            {
                                Text = report.IsAvailable
                                    ? $"{report.ConfidenceLabel} ({report.ConfidencePercent:0}%)"
                                    : "Online-only signal",
                                Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold),
                                Colour = Color4.White.Opacity(0.85f),
                            }
                        }
                    }
                }
            };
        }

        private Drawable createSummaryText() => summaryText = new OsuTextFlowContainer(cp => cp.Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = report.Summary,
        };

        private Drawable createDetailsTable()
        {
            List<SimpleStatisticItem> items = report.IsAvailable
                ? createAvailableItems()
                : createUnavailableItems();

            foreach (SimpleStatisticItem item in items)
                item.FontSize = StatisticItem.FONT_SIZE;

            return new SimpleStatisticTable(2, items);
        }

        private List<SimpleStatisticItem> createAvailableItems() =>
        [
            createItem("Risk", report.RiskLabel),
            createItem("Confidence", report.ConfidenceLabel),
            createItem("Monitored", $"{report.MonitoringTime:0.0}s"),
            createItem("Shifted targets", report.ShiftedTargetCount.ToString()),
            createItem("Peak bait score", $"{report.PeakScore:0.00}"),
            createItem("Average bait score", $"{report.AverageScore:0.00}"),
            createItem("Suspicious share", $"{report.SuspiciousRatio * 100:0.0}%"),
            createItem("Divergent share", $"{report.DivergentRatio * 100:0.0}%"),
            createItem("Press evidence", report.PressEvidenceCount.ToString()),
            createItem("Last runtime state", report.LastRuntimeState),
        ];

        private List<SimpleStatisticItem> createUnavailableItems() =>
        [
            createItem("Status", "Inactive"),
            createItem("Reason", "Offline or uploads disabled"),
        ];

        private static SimpleStatisticItem createItem(string name, string value)
            => new SimpleStatisticItem<string>(name)
            {
                Value = value
            };

        private Color4 getAccentColour()
        {
            if (!report.IsAvailable)
                return new Color4(140, 160, 180, 255);

            if (report.Percent >= 85)
                return new Color4(255, 90, 90, 255);

            if (report.Percent >= 65)
                return new Color4(255, 150, 80, 255);

            if (report.Percent >= 40)
                return new Color4(255, 205, 90, 255);

            return new Color4(110, 220, 170, 255);
        }
    }
}
