// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmapSet
    {
        public partial class SpreadDisplay : OsuAnimatedButton
        {
            public Bindable<BeatmapSetInfo?> BeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

            public IBindable<HashSet<BeatmapInfo>?> VisibleBeatmaps { get; } = new Bindable<HashSet<BeatmapInfo>?>();

            public BindableBool Expanded { get; } = new BindableBool();

            protected override Colour4 DimColour => Colour4.White;

            private readonly IBindable<BeatmapSetInfo?> scopedBeatmapSet = new Bindable<BeatmapSetInfo?>();
            private readonly Bindable<bool> showConvertedBeatmaps = new Bindable<bool>();

            private const double transition_duration = 200;

            [Resolved]
            private ISongSelect? songSelect { get; set; }

            [Resolved]
            private Bindable<RulesetInfo> ruleset { get; set; } = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            [Resolved]
            private RulesetStore rulesets { get; set; } = null!;

            [Resolved]
            private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

            private readonly Dictionary<Guid, (string Checksum, double Stars)> calculatedStars = new Dictionary<Guid, (string, double)>();

            private FillFlowContainer flow = null!;
            private SpriteIcon icon = null!;

            // A pooled carousel panel changes sets repeatedly. Reuse its tiny spread
            // elements rather than creating/disposal-queueing a drawable tree each time.
            private readonly List<Circle> difficultyDots = new List<Circle>();
            private readonly List<OsuSpriteText> overflowTexts = new List<OsuSpriteText>();
            private readonly Dictionary<int, Drawable> rulesetIcons = new Dictionary<int, Drawable>();

            internal int CreatedDifficultyDots => difficultyDots.Count;

            public SpreadDisplay()
            {
                AutoSizeAxes = Axes.X;
                Height = 14;
                Content.CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager configManager)
            {
                Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5),
                    Padding = new MarginPadding { Horizontal = 5 },
                    Children = new Drawable[]
                    {
                        flow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.X,
                            RelativeSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(1),
                        },
                        icon = new SpriteIcon
                        {
                            Size = new Vector2(12),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Eye,
                            Alpha = 0,
                        }
                    }
                });

                if (songSelect != null)
                    scopedBeatmapSet.BindTo(songSelect.ScopedBeatmapSet);

                configManager.BindWith(OsuSetting.ShowConvertedBeatmaps, showConvertedBeatmaps);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                difficultyCache.NativeDifficultyUpdated += onDifficultyUpdated;
                BeatmapSet.BindValueChanged(_ =>
                {
                    calculatedStars.Clear();
                    Scheduler.AddOnce(updateBeatmapSet);
                });
                VisibleBeatmaps.BindValueChanged(_ => Scheduler.AddOnce(updateBeatmapSet));
                showConvertedBeatmaps.BindValueChanged(_ => Scheduler.AddOnce(updateBeatmapSet), true);
                ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateBeatmapSet));
                Expanded.BindValueChanged(_ => updateEnabled());
                scopedBeatmapSet.BindValueChanged(_ => updateEnabled(), true);
                scopedBeatmapSet.BindDisabledChanged(_ => updateEnabled(), true);
                Enabled.BindValueChanged(_ => updateAppearance(), true);
                FinishTransforms(true);
            }

            public override bool UpdateSubTree()
            {
                // A mode with no compatible maps (or a recycled panel) can fade us out completely.
                // Drawable skips the scheduler when absent, so pending rebuilds must run here
                // to make the spread visible again. Keep it absent from layout while hidden.
                if (IsLoaded && !IsPresent)
                    Scheduler.Update();

                return base.UpdateSubTree();
            }

            private void onDifficultyUpdated(Guid id, string checksum, double stars)
            {
                if (BeatmapSet.Value?.Beatmaps.Any(b => b.ID == id && b.MD5Hash == checksum) != true)
                    return;

                calculatedStars[id] = (checksum, stars);
                Scheduler.AddOnce(updateBeatmapSet);
            }

            private double getStars(BeatmapInfo beatmap)
                => calculatedStars.TryGetValue(beatmap.ID, out var result) && result.Checksum == beatmap.MD5Hash
                    ? result.Stars : difficultyCache.GetCachedNativeStarRating(beatmap);

            private void updateBeatmapSet()
            {
                flow.Clear(disposeChildren: false);
                int dotIndex = 0;
                int textIndex = 0;

                if (BeatmapSet.Value == null)
                {
                    this.FadeOut(transition_duration, Easing.OutQuint);
                    return;
                }

                const int max_difficulties_before_collapsing = 12;

                var beatmaps = BeatmapSet.Value.Beatmaps
                                         .Where(b => b.AllowGameplayWithRuleset(ruleset.Value, showConvertedBeatmaps.Value))
                                         .ToList();
                this.FadeTo(beatmaps.Count > 0 ? 1 : 0, transition_duration, Easing.OutQuint);

                if (beatmaps.Count == 0)
                    return;

                bool showVisible = VisibleBeatmaps.Value == null || VisibleBeatmaps.Value?.Count <= max_difficulties_before_collapsing;
                bool showHidden = beatmaps.Count <= max_difficulties_before_collapsing;

                var beatmapsByRuleset = beatmaps.GroupBy(beatmap => beatmap.Ruleset.OnlineID).OrderBy(group => group.Key);

                foreach (var rulesetGrouping in beatmapsByRuleset)
                {
                    int rulesetId = rulesetGrouping.Key;
                    if (!rulesetIcons.TryGetValue(rulesetId, out var rulesetIcon))
                    {
                        rulesetIcon = rulesets.GetRuleset(rulesetId)?.CreateInstance().CreateIcon() ?? new SpriteIcon { Icon = FontAwesome.Regular.QuestionCircle };
                        rulesetIcons.Add(rulesetId, rulesetIcon);
                    }
                    flow.Add(rulesetIcon.With(i =>
                    {
                        i.Size = new Vector2(14);
                        i.Anchor = i.Origin = Anchor.CentreLeft;
                        i.Margin = new MarginPadding { Left = flow.Count > 0 ? 9 : 0 };
                    }));

                    int overflowVisible = 0;
                    int overflowHidden = 0;
                    bool? lastBeatmapVisible = null;

                    foreach (var beatmap in rulesetGrouping.OrderBy(getStars))
                    {
                        bool visible = VisibleBeatmaps.Value?.Contains(beatmap) != false;

                        if ((visible && showVisible) || (!visible && showHidden))
                        {
                            if (dotIndex == difficultyDots.Count)
                                difficultyDots.Add(new Circle { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft });
                            var circle = difficultyDots[dotIndex++];
                            circle.Size = visible ? new Vector2(7, 12) : new Vector2(5, 10);
                            circle.Alpha = visible ? 1 : 0.5f;
                            circle.Colour = colours.ForStarDifficulty(getStars(beatmap));
                            circle.Margin = new MarginPadding { Left = lastBeatmapVisible != null && lastBeatmapVisible != visible ? 1 : 0 };
                            flow.Add(circle);

                            lastBeatmapVisible = visible;
                        }
                        else
                        {
                            if (visible)
                                overflowVisible++;
                            else
                                overflowHidden++;
                        }
                    }

                    if (overflowVisible > 0)
                    {
                        var text = getOverflowText(textIndex++);
                        text.Text = overflowVisible.ToLocalisableString();
                        text.Alpha = 1;
                        flow.Add(text);
                    }

                    if (overflowHidden > 0)
                    {
                        var text = getOverflowText(textIndex++);
                        text.Text = LocalisableString.Interpolate($@"+{overflowHidden}");
                        text.Alpha = 0.7f;
                        flow.Add(text);
                    }
                }

                Action = () => songSelect?.ScopeToBeatmapSet(BeatmapSet.Value);
                updateEnabled();
            }

            private OsuSpriteText getOverflowText(int index)
            {
                if (index == overflowTexts.Count)
                    overflowTexts.Add(new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = OsuFont.Style.Caption2,
                    });
                return overflowTexts[index];
            }

            protected override void Dispose(bool isDisposing)
            {
                if (isDisposing)
                    difficultyCache?.NativeDifficultyUpdated -= onDifficultyUpdated;
                calculatedStars.Clear();
                // Removed pooled elements are no longer owned by the drawable hierarchy.
                foreach (var drawable in difficultyDots.Cast<Drawable>().Concat(overflowTexts).Concat(rulesetIcons.Values))
                {
                    if (drawable.Parent == null)
                        drawable.Dispose();
                }
                difficultyDots.Clear();
                overflowTexts.Clear();
                rulesetIcons.Clear();
                base.Dispose(isDisposing);
            }

            private void updateEnabled()
            {
                Enabled.Value = Expanded.Value && !scopedBeatmapSet.Disabled && scopedBeatmapSet.Value == null;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (!Enabled.Value)
                    return false;

                base.OnMouseDown(e);
                return true;
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!Enabled.Value)
                    return false;

                // this is a crude workaround with an issue with `OsuAnimatedButton` that isn't easily fixable.
                // the issue is that when wanting to turn off the hover layer upon click, `HoverColour` can be set to a transparent colour,
                // *but* this has to happen *before* `base.OnClick()`.
                // this is because `base.OnClick()` uses `FlashColour()` to flash the button on click,
                // but that `FlashColour()` call implicitly copies `hoverColour` *at the point of call* into the transform that ends the flash.
                updateAppearance(false);
                return base.OnClick(e);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateAppearance();

                if (!Enabled.Value)
                    return false;

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateAppearance();

                if (!Enabled.Value)
                    return;

                base.OnHoverLost(e);
            }

            private void updateAppearance(bool? isInteractable = null)
            {
                isInteractable ??= Enabled.Value && IsHovered;

                HoverColour = isInteractable.Value ? Colour4.White.Opacity(0.1f) : Colour4.Transparent;
                icon.FadeTo(isInteractable.Value ? 1 : 0, transition_duration, Easing.OutQuint);
            }
        }
    }
}
