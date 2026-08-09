// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays
{
    public partial class SupporterQuestsOverlay : WaveOverlayContainer, INamedOverlayComponent
    {
        public IconUsage Icon => FontAwesome.Solid.Crown;
        public LocalisableString Title => ForkSettingsStrings.SupporterQuestTitle;
        public LocalisableString Description => ForkSettingsStrings.SupporterQuestDescription;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private OsuSpriteText overallProgressText = null!;
        private OsuSpriteText overallProgressDetailText = null!;
        private Box overallProgressBarFill = null!;
        private Box background = null!;
        private Container panel = null!;
        private Box progressBackground = null!;
        private FillFlowContainer benefits = null!;
        private OsuSpriteText headerText = null!;
        private FillFlowContainer<Drawable> questListFlow = null!;
        private LoadingLayer loadingLayer = null!;
        private IBindable<Colour4> themeColour = null!;

        public event Action<int>? QuestsLoaded;

        public SupporterQuestsOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            });

            Add(panel = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Width = 0.85f,
                Height = 0.85f,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Masking = true,
                CornerRadius = 15,
                BorderThickness = 2,
                EdgeEffect = new EdgeEffectParameters
                {
                    Colour = Color4.Black.Opacity(0.5f),
                    Type = EdgeEffectType.Shadow,
                    Radius = 15,
                },
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Relative, 0.35f),
                            new Dimension(GridSizeMode.Relative, 0.65f),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                // Left Panel (Overall progress and Telegram button)
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Horizontal = 30, Vertical = 40 },
                                    Child = new FillFlowContainer
                                    {
                                        Direction = FillDirection.Vertical,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Spacing = new Vector2(0, 30),
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Children = new Drawable[]
                                        {
                                            headerText = new OsuSpriteText
                                            {
                                                Text = ForkSettingsStrings.SupporterQuestHeader,
                                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                            },
                                            overallProgressText = new OsuSpriteText
                                            {
                                                Text = "0%",
                                                Font = OsuFont.GetFont(size: 36, weight: FontWeight.Bold),
                                                Colour = colourProvider.Content1,
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                            },
                                            overallProgressDetailText = new OsuSpriteText
                                            {
                                                Text = "(0 / 0)",
                                                Font = OsuFont.GetFont(size: 14),
                                                Colour = colourProvider.Content2,
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                            },
                                            // Overall Progress Bar
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Height = 15,
                                                Masking = true,
                                                CornerRadius = 7.5f,
                                                Children = new Drawable[]
                                                {
                                                    progressBackground = new Box
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                    overallProgressBarFill = new Box
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Width = 0f,
                                                        Colour = Colour4.FromHex("#A472E8")
                                                    }
                                                }
                                            },
                                            // Benefits list
                                            benefits = new FillFlowContainer
                                            {
                                                Direction = FillDirection.Vertical,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Spacing = new Vector2(0, 6),
                                                Padding = new MarginPadding { Top = 10, Bottom = 5 },
                                                Children = new Drawable[]
                                                {
                                                    new OsuSpriteText
                                                    {
                                                        Text = ForkSettingsStrings.SupporterQuestFeaturesHeader,
                                                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                                                        Colour = Colour4.FromHex("#A472E8"),
                                                        Margin = new MarginPadding { Bottom = 2 }
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Text = ForkSettingsStrings.SupporterQuestFeature1,
                                                        Font = OsuFont.GetFont(size: 13),
                                                        Colour = colourProvider.Content1
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Text = ForkSettingsStrings.SupporterQuestFeature2,
                                                        Font = OsuFont.GetFont(size: 13),
                                                        Colour = colourProvider.Content1
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Text = ForkSettingsStrings.SupporterQuestFeature3,
                                                        Font = OsuFont.GetFont(size: 13),
                                                        Colour = colourProvider.Content1
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Text = ForkSettingsStrings.SupporterQuestFeature4,
                                                        Font = OsuFont.GetFont(size: 13),
                                                        Colour = colourProvider.Content1
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Text = ForkSettingsStrings.SupporterQuestFeature5,
                                                        Font = OsuFont.GetFont(size: 13),
                                                        Colour = colourProvider.Content1
                                                    }
                                                }
                                            },
                                            // Skip Button
                                            new RoundedButton
                                            {
                                                Text = ForkSettingsStrings.SupporterQuestSkipBtn,
                                                BackgroundColour = Colour4.FromHex("#F9DB32"),
                                                Colour = Color4.Black,
                                                Height = 50,
                                                RelativeSizeAxes = Axes.X,
                                                Action = () => game.HandleLink("https://t.me/Matvey_gay_ebal_v_rot_bot?start=RZ-12")
                                            }
                                        }
                                    }
                                },
                                // Right Panel (Scrollable list of tasks)
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Right = 20, Vertical = 40 },
                                    Children = new Drawable[]
                                    {
                                        new OverlayScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Child = questListFlow = new FillFlowContainer<Drawable>
                                            {
                                                Direction = FillDirection.Vertical,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Spacing = new Vector2(0, 15),
                                                Padding = new MarginPadding { Right = 15 }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });

            Add(loadingLayer = new LoadingLayer(true));

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            background.Colour = colourProvider.Background5;
            panel.BorderColour = colourProvider.Background3;
            headerText.Colour = colourProvider.Content1;
            overallProgressText.Colour = colourProvider.Content1;
            overallProgressDetailText.Colour = colourProvider.Content2;
            progressBackground.Colour = colourProvider.Background4;

            foreach (var text in benefits.OfType<OsuSpriteText>().Skip(1))
                text.Colour = colourProvider.Content1;
        }

        protected override void PopIn()
        {
            base.PopIn();
            fetchQuests();
        }

        private void fetchQuests()
        {
            loadingLayer.Show();

            var req = new GetSupporterQuestsRequest();
            req.Success += response => Schedule(() =>
            {
                loadingLayer.Hide();
                updateUI(response);
            });
            req.Failure += ex => Schedule(() =>
            {
                loadingLayer.Hide();
                // Handle API error gracefully
            });

            api.PerformAsync(req);
        }

        private void updateUI(List<APISupporterQuest> quests)
        {
            questListFlow.Clear();

            if (quests == null || quests.Count == 0)
                return;

            // Find first uncompleted quest (active quest)
            int activeIndex = quests.FindIndex(q => !q.Completed);

            int completedCount = quests.Count(q => q.Completed);
            QuestsLoaded?.Invoke(completedCount);
            float progressPercentage = (float)completedCount / quests.Count;

            overallProgressText.Text = $"{(progressPercentage * 100):0}%";
            overallProgressDetailText.Text = $"({completedCount} / {quests.Count})";
            overallProgressBarFill.ResizeWidthTo(progressPercentage, 500, Easing.OutQuint);

            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                bool active = i == activeIndex;
                bool locked = activeIndex != -1 && i > activeIndex;

                if (locked && i > activeIndex + 2)
                    continue;

                questListFlow.Add(new SupporterQuestCard(q, active, locked, colourProvider));
            }

            if (activeIndex != -1 && quests.Count > activeIndex + 3)
            {
                int remaining = quests.Count - (activeIndex + 3);
                questListFlow.Add(new SupporterQuestPlaceholderCard(remaining, colourProvider));
            }
        }
    }

    public partial class SupporterQuestCard : Container
    {
        private readonly IBindable<Colour4> themeColour;

        public SupporterQuestCard(APISupporterQuest quest, bool active, bool locked, OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;

            Box background;
            SpriteIcon statusIcon;
            OsuSpriteText titleText;
            OsuTextFlowContainer descText;
            OsuSpriteText progressText;
            Box barFill;
            Box barBackground;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(15),
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Distributed),
                            new Dimension(GridSizeMode.Absolute, 120),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                statusIcon = new SpriteIcon
                                {
                                    Size = new Vector2(24),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5),
                                    Padding = new MarginPadding { Right = 15 },
                                    Children = new Drawable[]
                                    {
                                        titleText = new OsuSpriteText
                                        {
                                            Text = ForkSettingsStrings.GetQuestName(quest.Id, quest.Name),
                                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                            Colour = Color4.White
                                        },
                                        descText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                                        {
                                            Text = ForkSettingsStrings.GetQuestDesc(quest.Id, quest.Description),
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Colour = colourProvider.Content2
                                        }
                                    }
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5),
                                    Children = new Drawable[]
                                    {
                                        progressText = new OsuSpriteText
                                        {
                                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                                            Colour = Color4.White,
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 6,
                                            Masking = true,
                                            CornerRadius = 3,
                                            Children = new Drawable[]
                                            {
                                                barBackground = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = colourProvider.Background3
                                                },
                                                barFill = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = colourProvider.Highlight1
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // Style Card depending on its status (Locked, Active, Completed)
            if (quest.Completed)
            {
                statusIcon.Icon = FontAwesome.Solid.Check;
                statusIcon.Colour = Colour4.FromHex("#70F08B");
                progressText.Text = $"{(int)quest.TargetValue} / {(int)quest.TargetValue}";
                barFill.Width = 1f;
                barFill.Colour = Colour4.FromHex("#70F08B");
                background.Colour = colourProvider.Background4.Lighten(0.05f);
            }
            else if (active)
            {
                statusIcon.Icon = FontAwesome.Solid.Star;
                statusIcon.Colour = Colour4.FromHex("#F9DB32");
                progressText.Text = $"{formatProgress(quest.CurrentValue)} / {formatProgress(quest.TargetValue)}";
                barFill.Width = quest.TargetValue > 0 ? (quest.CurrentValue / quest.TargetValue) : 0f;
                // Add border or highlight to active card
                BorderThickness = 1.5f;
                BorderColour = Colour4.FromHex("#A472E8");
            }
            else if (locked)
            {
                statusIcon.Icon = FontAwesome.Solid.Lock;
                statusIcon.Colour = colourProvider.Content2;
                progressText.Text = $"0 / {formatProgress(quest.TargetValue)}";
                barFill.Width = 0f;
                Alpha = 0.4f;
            }

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = quest.Completed ? colourProvider.Background4.Lighten(0.05f) : colourProvider.Background4;
                titleText.Colour = colourProvider.Content1;
                descText.Colour = colourProvider.Content2;
                progressText.Colour = colourProvider.Content1;
                barBackground.Colour = colourProvider.Background3;

                if (locked)
                    statusIcon.Colour = colourProvider.Content2;

                if (!quest.Completed)
                    barFill.Colour = colourProvider.Highlight1;
            }, true);
        }

        private string formatProgress(float val)
        {
            // Format time values or decimals nicely
            if (val == (int)val)
                return ((int)val).ToString();
            return val.ToString("0.##");
        }
    }

    public partial class SupporterQuestPlaceholderCard : Container
    {
        private readonly IBindable<Colour4> themeColour;

        public SupporterQuestPlaceholderCard(int remainingCount, OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            Height = 60;
            Masking = true;
            CornerRadius = 8;

            Box background;
            SpriteIcon icon;
            OsuSpriteText text;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(colourProvider.Background4.Opacity(0.6f), colourProvider.Background4.Opacity(0.0f))
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        icon = new SpriteIcon
                        {
                            Icon = FontAwesome.Solid.Lock,
                            Size = new Vector2(16),
                            Colour = colourProvider.Content2,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft
                        },
                        text = new OsuSpriteText
                        {
                            Text = ForkSettingsStrings.SupporterQuestRemaining(remainingCount),
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                            Colour = colourProvider.Content2,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft
                        }
                    }
                }
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = ColourInfo.GradientVertical(colourProvider.Background4.Opacity(0.6f), colourProvider.Background4.Opacity(0));
                icon.Colour = text.Colour = colourProvider.Content2;
            }, true);
        }
    }
}
