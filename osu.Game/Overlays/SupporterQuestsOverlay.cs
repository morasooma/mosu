// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class SupporterQuestsOverlay : OnlineOverlay<SupporterQuestsHeader>
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        private FillFlowContainer layout = null!;
        private FillFlowContainer sidebar = null!;
        private FillFlowContainer taskColumn = null!;
        private FillFlowContainer questList = null!;
        private OsuSpriteText overallProgressText = null!;
        private OsuSpriteText overallProgressDetailText = null!;
        private SupporterQuestProgressBar overallProgress = null!;
        private IconButton closeButton = null!;
        private IBindable<Colour4> themeColour = null!;
        private GetSupporterQuestsRequest? request;

        public event Action<int>? QuestsLoaded;

        public SupporterQuestsOverlay()
            : base(OverlayColourScheme.Purple, false)
        {
        }

        protected override SupporterQuestsHeader CreateHeader() => new SupporterQuestsHeader();

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Add(closeButton = new IconButton
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-12, 8),
                Size = new Vector2(40),
                Icon = FontAwesome.Solid.Times,
                TooltipText = ForkSettingsStrings.AboutMorasoomaClose,
                Action = Hide,
            });

            Child = layout = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(24),
                Padding = new MarginPadding(32),
                Children = new Drawable[]
                {
                    sidebar = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 16),
                        Children = new Drawable[]
                        {
                            new SupporterQuestPanel(new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 12),
                                Children = new Drawable[]
                                {
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestHeader, 20, FontWeight.Bold),
                                    overallProgressText = new OsuSpriteText
                                    {
                                        Text = "—",
                                        Font = OsuFont.GetFont(size: 48, weight: FontWeight.Bold),
                                    },
                                    overallProgress = new SupporterQuestProgressBar { Height = 8 },
                                    overallProgressDetailText = new OsuSpriteText
                                    {
                                        Text = "— / —",
                                        Font = OsuFont.GetFont(size: 14),
                                    },
                                },
                            }),
                            new SupporterQuestPanel(new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 12),
                                Children = new Drawable[]
                                {
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestFeaturesHeader, 17, FontWeight.Bold),
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestFeature1, secondary: true),
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestFeature2, secondary: true),
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestFeature3, secondary: true),
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestFeature4, secondary: true),
                                    new SupporterQuestText(ForkSettingsStrings.SupporterQuestFeature5, secondary: true),
                                },
                            }),
                            new SupporterQuestText(ForkSettingsStrings.SupporterQuestContributionNote, secondary: true)
                            {
                                Padding = new MarginPadding { Horizontal = 4 },
                            },
                            new SupporterQuestButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 44,
                                Text = ForkSettingsStrings.SupporterQuestDonateBtn,
                                Action = () => game.OpenUrlExternally("https://pay.cloudtips.ru/p/72f10912"),
                            },
                        },
                    },
                    taskColumn = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 12),
                        Children = new Drawable[]
                        {
                            new SupporterQuestText(SupporterQuestsStrings.Tasks, 22, FontWeight.Bold),
                            new SupporterQuestText(ForkSettingsStrings.SupporterQuestDescription, secondary: true)
                            {
                                Margin = new MarginPadding { Bottom = 8 },
                            },
                            questList = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 12),
                            },
                        },
                    },
                },
            };

            themeColour = ColourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                overallProgressText.Colour = ColourProvider.Content1;
                overallProgressDetailText.Colour = ColourProvider.Content2;
                closeButton.IconColour = ColourProvider.Content2;
                closeButton.IconHoverColour = ColourProvider.Content1;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            // На узком окне обе колонки остаются доступны в общей прокрутке.
            float availableWidth = Math.Max(0, layout.DrawWidth - layout.Padding.TotalHorizontal);
            bool stacked = availableWidth < 720;
            sidebar.Width = stacked ? availableWidth : Math.Clamp(availableWidth * 0.32f, 260, 320);
            taskColumn.Width = stacked ? availableWidth : availableWidth - sidebar.Width - layout.Spacing.X;
        }

        protected override void PopIn()
        {
            base.PopIn();
            fetchQuests();
        }

        private void fetchQuests()
        {
            cancelRequest();
            Loading.Show();
            questList.Clear();
            overallProgressText.Text = "—";
            overallProgressDetailText.Text = "— / —";
            overallProgress.SetProgress(0);

            var currentRequest = request = new GetSupporterQuestsRequest();
            currentRequest.Success += response => Schedule(() =>
            {
                if (request != currentRequest)
                    return;

                request = null;
                Loading.Hide();
                updateQuests(response);
            });
            currentRequest.Failure += _ => Schedule(() =>
            {
                if (request != currentRequest)
                    return;

                request = null;
                Loading.Hide();
                showMessage(SupporterQuestsStrings.LoadFailed);
            });

            API.PerformAsync(currentRequest);
        }

        private void updateQuests(List<APISupporterQuest> quests)
        {
            questList.Clear();
            int completedCount = quests.Count(q => q.Completed);
            QuestsLoaded?.Invoke(completedCount);

            if (quests.Count == 0)
            {
                showMessage(SupporterQuestsStrings.Empty);
                return;
            }

            float progress = (float)completedCount / quests.Count;
            overallProgressText.Text = $"{progress * 100:0}%";
            overallProgressDetailText.Text = $"{completedCount} / {quests.Count}";
            overallProgress.SetProgress(progress, true);

            int activeIndex = quests.FindIndex(q => !q.Completed);
            int hiddenCount = 0;

            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                bool locked = !quest.Completed && activeIndex != -1 && i > activeIndex;

                if (locked && i > activeIndex + 2)
                {
                    hiddenCount++;
                    continue;
                }

                questList.Add(new SupporterQuestCard(quest, i == activeIndex, locked, ColourProvider));
            }

            if (hiddenCount > 0)
                questList.Add(new SupporterQuestPlaceholderCard(hiddenCount, ColourProvider));
        }

        private void showMessage(LocalisableString message)
        {
            questList.Add(new SupporterQuestPanel(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 16),
                Children = new Drawable[]
                {
                    new SupporterQuestText(message, secondary: true),
                    new SupporterQuestButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = SupporterQuestsStrings.Retry,
                        Action = fetchQuests,
                    },
                },
            }));
        }

        private void cancelRequest()
        {
            var previous = request;
            request = null;
            previous?.Cancel();
        }

        protected override void PopOut()
        {
            cancelRequest();
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelRequest();
            base.Dispose(isDisposing);
        }
    }

    public partial class SupporterQuestsHeader : OverlayHeader
    {
        protected override OverlayTitle CreateTitle() => new SupporterQuestsTitle();

        private partial class SupporterQuestsTitle : OverlayTitle
        {
            public SupporterQuestsTitle()
            {
                Title = ForkSettingsStrings.SupporterQuestTitle;
                Description = ForkSettingsStrings.SupporterQuestDescription;
                Icon = FontAwesome.Solid.Crown;
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
            CornerRadius = 10;
            BorderThickness = active ? 2 : 1;

            bool completed = quest.Completed;
            locked &= !completed;

            var background = new Box { RelativeSizeAxes = Axes.Both };
            var statusIcon = new SpriteIcon
            {
                Size = new Vector2(14),
                Icon = completed ? FontAwesome.Solid.Check : locked ? FontAwesome.Solid.Lock : FontAwesome.Solid.Play,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
            var statusText = new OsuSpriteText
            {
                Text = completed ? SupporterQuestsStrings.Completed : locked ? SupporterQuestsStrings.Locked : SupporterQuestsStrings.Active,
                Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
            float current = completed ? quest.TargetValue : locked ? 0 : quest.CurrentValue;
            var progressText = new SupporterQuestText($"{current:0.##} / {quest.TargetValue:0.##}", 14, FontWeight.Bold);
            var progress = new SupporterQuestProgressBar();
            progress.SetProgress(completed ? 1 : quest.TargetValue > 0 ? current / quest.TargetValue : 0);

            Children = new Drawable[]
            {
                background,
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Padding = new MarginPadding(20),
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Children = new Drawable[] { statusIcon, statusText },
                        },
                        new SupporterQuestText(ForkSettingsStrings.GetQuestName(quest.Id, quest.Name), 19, FontWeight.Bold),
                        new SupporterQuestText(ForkSettingsStrings.GetQuestDesc(quest.Id, quest.Description), secondary: true),
                        progressText,
                        progress,
                    },
                },
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = locked ? colourProvider.Background5 : colourProvider.Background4;
                BorderColour = active ? colourProvider.Highlight1 : colourProvider.Background3;
                statusIcon.Colour = statusText.Colour = locked ? colourProvider.Content2 : colourProvider.Highlight1;
            }, true);
        }
    }

    public partial class SupporterQuestPlaceholderCard : Container
    {
        private readonly IBindable<Colour4> themeColour;

        public SupporterQuestPlaceholderCard(int remainingCount, OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 10;
            BorderThickness = 1;
            Child = new SupporterQuestText(ForkSettingsStrings.SupporterQuestRemaining(remainingCount), secondary: true)
            {
                Padding = new MarginPadding(20),
                TextAnchor = Anchor.TopCentre,
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background3);
            themeColour.BindValueChanged(colour => BorderColour = colour.NewValue, true);
        }
    }

    internal partial class SupporterQuestButton : RoundedButton
    {
        private IBindable<Colour4> themeColour = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Colour3);
            themeColour.BindValueChanged(colour => BackgroundColour = colour.NewValue, true);
        }
    }

    internal partial class SupporterQuestText : OsuTextFlowContainer
    {
        private readonly bool secondary;
        private IBindable<Colour4> themeColour = null!;

        public SupporterQuestText(LocalisableString text, float size = 14, FontWeight weight = FontWeight.Regular, bool secondary = false)
            : base(t => t.Font = OsuFont.GetFont(size: size, weight: weight))
        {
            this.secondary = secondary;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Text = text;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(secondary ? OverlayColour.Content2 : OverlayColour.Content1);
            themeColour.BindValueChanged(colour => Colour = colour.NewValue, true);
        }
    }

    internal partial class SupporterQuestPanel : Container
    {
        private readonly Box background;
        private IBindable<Colour4> themeColour = null!;

        public SupporterQuestPanel(Drawable content)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 10;
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(20),
                    Child = content,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Background4);
            themeColour.BindValueChanged(colour => background.Colour = colour.NewValue, true);
        }
    }

    internal partial class SupporterQuestProgressBar : Container
    {
        private readonly Box background;
        private readonly Box fill;
        private IBindable<Colour4> themeColour = null!;

        public SupporterQuestProgressBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = 6;
            Masking = true;
            CornerRadius = 3;
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                fill = new Box { RelativeSizeAxes = Axes.Both, Width = 0 },
            };
        }

        public void SetProgress(float progress, bool animated = false)
            => fill.ResizeWidthTo(float.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0, animated ? 400 : 0, Easing.OutQuint);

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            themeColour = colourProvider.GetColourBindable(OverlayColour.Highlight1);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = colourProvider.Content2.Opacity(0.12f);
                fill.Colour = colourProvider.Highlight1;
            }, true);
        }
    }
}
