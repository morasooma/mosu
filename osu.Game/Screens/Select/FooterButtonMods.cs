// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play.HUD;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public partial class FooterButtonMods : ScreenFooterButton
    {
        public Action? RequestDeselectAllMods { get; init; }

        public const float BAR_HEIGHT = 30f;

        private const float mod_display_portion = 0.65f;

        private readonly BindableWithCurrent<IReadOnlyList<Mod>> mods = new BindableWithCurrent<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        public Bindable<IReadOnlyList<Mod>> Mods
        {
            get => mods.Current;
            set => mods.Current = value;
        }

        private readonly BindableWithCurrent<RulesetInfo?> ruleset = new BindableWithCurrent<RulesetInfo?>();

        public Bindable<RulesetInfo?> Ruleset
        {
            get => ruleset.Current;
            set => ruleset.Current = value;
        }

        public readonly IBindable<WorkingBeatmap> Beatmap = new Bindable<WorkingBeatmap>();

        private Container modDisplayBar = null!;

        private Drawable unrankedBadge = null!;

        private ModDisplay modDisplay = null!;

        private OsuSpriteText multiplierText { get; set; } = null!;

        private Container modContainer = null!;

        private ModCountText overflowModCountDisplay = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private IBindable<Language> currentLanguage = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private CancellationTokenSource? difficultyCancellation;
        private IBindable<StarDifficulty>? starDifficulty;

        public override osu.Game.Overlays.CustomTheme.ThemeSurfaceContext? ButtonContext => osu.Game.Overlays.CustomTheme.ThemeSurfaceContext.FooterMods;

        public FooterButtonMods(ModSelectOverlay overlay)
            : base(overlay)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Text = SongSelectStrings.Mods;
            Icon = FontAwesome.Solid.ExchangeAlt;
            AccentColour = colours.Lime1;

            AddRange(new[]
            {
                unrankedBadge = new UnrankedBadge(),
                modDisplayBar = new InputBlockingContainer
                {
                    Y = -5f,
                    Depth = float.MaxValue,
                    Origin = Anchor.BottomLeft,
                    Shear = OsuGame.SHEAR,
                    CornerRadius = CORNER_RADIUS,
                    Size = new Vector2(BUTTON_WIDTH, BAR_HEIGHT),
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 4,
                        // Figma says 50% opacity, but it does not match up visually if taken at face value, and looks bad.
                        Colour = Colour4.Black.Opacity(0.25f),
                        Offset = new Vector2(0, 2),
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colourProvider.Background4,
                            RelativeSizeAxes = Axes.Both,
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            RelativeSizeAxes = Axes.Both,
                            Width = 1f - mod_display_portion,
                            Masking = true,
                            Child = multiplierText = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Shear = -OsuGame.SHEAR,
                                UseFullGlyphHeight = false,
                                Font = OsuFont.Torus.With(size: 14f, weight: FontWeight.Bold)
                            }
                        },
                        modContainer = new Container
                        {
                            CornerRadius = CORNER_RADIUS,
                            RelativeSizeAxes = Axes.Both,
                            Width = mod_display_portion,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    Colour = colourProvider.Background3,
                                    RelativeSizeAxes = Axes.Both,
                                },
                                modDisplay = new ModDisplay(showExtendedInformation: true)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Shear = -OsuGame.SHEAR,
                                    Scale = new Vector2(0.5f),
                                    Current = { BindTarget = Mods },
                                    ExpansionMode = ExpansionMode.AlwaysContracted,
                                },
                                overflowModCountDisplay = new ModCountText { Mods = { BindTarget = Mods }, },
                            }
                        },
                    }
                },
            });
        }

        private ModSettingChangeTracker? modSettingChangeTracker;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentLanguage = game.CurrentLanguage.GetBoundCopy();
            currentLanguage.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));

            Ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            beatmap.BindValueChanged(_ => Scheduler.AddOnce(updateStarDifficulty));
            Mods.BindValueChanged(m =>
            {
                updateModSettingTracker(m.NewValue);
                Scheduler.AddOnce(updateDisplay);
            });

            Beatmap.BindValueChanged(_ => Scheduler.AddOnce(updateStarDifficulty));

            // Initial layout must be available immediately. Later changes are
            // coalesced until Update: legacy chrome can leave this footer hidden
            // for the entire session, so it must not accumulate idle transforms.
            updateModSettingTracker(Mods.Value);
            updateStarDifficulty();
            updateDisplay();
            FinishTransforms(true);
        }

        private void updateStarDifficulty()
        {
            difficultyCancellation?.Cancel();
            difficultyCancellation?.Dispose();
            starDifficulty?.UnbindAll();
            difficultyCancellation = new CancellationTokenSource();
            starDifficulty = difficultyCache.GetBindableDifficulty((Beatmap.Value ?? beatmap.Value).BeatmapInfo, difficultyCancellation.Token,
                computationDelay: SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE, calculatePerformance: false);
            starDifficulty.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            difficultyCancellation?.Cancel();
            difficultyCancellation?.Dispose();
            starDifficulty?.UnbindAll();
            modSettingChangeTracker?.Dispose();
            base.Dispose(isDisposing);
        }

        private void updateModSettingTracker(IReadOnlyList<Mod>? selectedMods)
        {
            modSettingChangeTracker?.Dispose();
            modSettingChangeTracker = null;
            if (selectedMods == null)
                return;

            modSettingChangeTracker = new ModSettingChangeTracker(selectedMods);
            modSettingChangeTracker.SettingChanged += _ => Scheduler.AddOnce(updateDisplay);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            // should probably be OnClick but right mouse button clicks isn't setup well.
            if (e.Button == MouseButton.Right)
            {
                RequestDeselectAllMods?.Invoke();
                return true;
            }

            return base.OnMouseDown(e);
        }

        private const double duration = 240;
        private const Easing easing = Easing.OutQuint;

        private void updateDisplay()
        {
            if (Mods.Value.Count == 0)
            {
                modDisplayBar.MoveToY(20, duration, easing);
                modDisplayBar.FadeOut(duration, easing);
                modDisplay.FadeOut(duration, easing);
                overflowModCountDisplay.FadeOut(duration, easing);
            }
            else
            {
                modDisplayBar.MoveToY(-5, duration, Easing.OutQuint);
                modDisplayBar.FadeIn(duration, easing);
                modDisplay.FadeIn(duration, easing);
            }

            var selectedBeatmap = (Beatmap.Value ?? beatmap.Value).BeatmapInfo;
            bool unranked = !ModPerformancePointHelper.ModsAwardPerformancePoints(selectedBeatmap, Mods.Value)
                            || !ModPerformancePointHelper.IsWithinRankedStarLimit(Ruleset.Value, Mods.Value,
                                starDifficulty?.Value.Stars ?? selectedBeatmap.StarRating);

            // Without mods there is no mod bar to reserve space for beside the badge.
            float badgeOffset = Mods.Value.Count > 0 ? BUTTON_WIDTH + 5 : 0;
            unrankedBadge.Margin = new MarginPadding { Left = badgeOffset };

            if (unranked)
            {
                unrankedBadge.MoveToX(0, duration, easing);
                unrankedBadge.MoveToY(-5, duration, easing);
                unrankedBadge.FadeIn(duration, easing);
                this.ResizeWidthTo(Math.Max(BUTTON_WIDTH, badgeOffset + unrankedBadge.DrawWidth), duration, easing);
            }
            else
            {
                unrankedBadge.MoveToX(-unrankedBadge.DrawWidth, duration, easing);
                unrankedBadge.MoveToY(Mods.Value.Count == 0 ? 20 : -5, duration, easing);
                unrankedBadge.FadeOut(duration, easing);
                // Let the indicator hide before shrinking the no-mod button.
                this.Delay(Mods.Value.Count == 0 ? duration : 0).ResizeWidthTo(BUTTON_WIDTH, duration, easing);
            }

            var currentRuleset = Ruleset.Value?.CreateInstance();
            double multiplier = currentRuleset != null
                ? ModUtils.CalculateScoreMultiplierForDisplay(currentRuleset, beatmap.Value.BeatmapInfo, Mods.Value)
                : 1;
            multiplierText.Text = ModUtils.FormatScoreMultiplier(multiplier);

            if (multiplier > 1)
                multiplierText.FadeColour(colours.Red1, duration, easing);
            else if (multiplier < 1)
                multiplierText.FadeColour(colours.Lime1, duration, easing);
            else
                multiplierText.FadeColour(Color4.White, duration, easing);
        }

        protected override void Update()
        {
            base.Update();

            if (Mods.Value.Count == 0)
                return;

            if (modDisplay.DrawWidth * modDisplay.Scale.X > modContainer.DrawWidth)
                overflowModCountDisplay.Show();
            else
                overflowModCountDisplay.Hide();
        }

        protected override void UpdateRadius()
        {
            base.UpdateRadius();

            float target = ButtonRadiusBindable?.Value ?? CORNER_RADIUS;
            float radius = !OsuGame.DisableShear.Value ? Math.Min(target, 12f) : target;
            if (modDisplayBar != null)
                modDisplayBar.CornerRadius = radius;
            if (modContainer != null)
                modContainer.CornerRadius = radius;
            if (unrankedBadge is Container unrankedContainer)
                unrankedContainer.CornerRadius = !OsuGame.DisableShear.Value ? Math.Min(target, 10f) : target;
        }

        public partial class ModCountText : VisibilityContainer, IHasCustomTooltip<IReadOnlyList<Mod>>
        {
            public readonly Bindable<IReadOnlyList<Mod>> Mods = new Bindable<IReadOnlyList<Mod>>();

            private LocalisableString? customText;

            /// <summary>
            /// When set, this will be shown instead of a mod count.
            /// </summary>
            public LocalisableString? CustomText
            {
                get => customText;
                set
                {
                    customText = value;
                    if (IsLoaded)
                        updateText();
                }
            }

            private OsuSpriteText text = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                RelativeSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Colour = colourProvider.Background3,
                        Alpha = 0.8f,
                        RelativeSizeAxes = Axes.Both,
                    },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.Torus.With(size: 14f, weight: FontWeight.Bold),
                        Shear = -OsuGame.SHEAR,
                    }
                };

                Mods.BindValueChanged(_ => updateText(), true);
            }

            public ITooltip<IReadOnlyList<Mod>> GetCustomTooltip() => new ModOverflowTooltip(colourProvider);

            public IReadOnlyList<Mod>? TooltipContent => Mods.Value;

            protected override void PopIn() => this.FadeIn(300, Easing.OutExpo);
            protected override void PopOut() => this.FadeOut(300, Easing.OutExpo);

            private void updateText()
            {
                if (CustomText != null)
                    text.Text = CustomText.Value;
                else
                    text.Text = ModSelectOverlayStrings.Mods(Mods.Value.Count).ToUpper();
            }

            public partial class ModOverflowTooltip : VisibilityContainer, ITooltip<IReadOnlyList<Mod>>
            {
                private ModFlowDisplay extendedModDisplay = null!;

                [Cached]
                private OverlayColourProvider colourProvider;

                public ModOverflowTooltip(OverlayColourProvider colourProvider)
                {
                    this.colourProvider = colourProvider;
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AutoSizeAxes = Axes.Both;
                    CornerRadius = CORNER_RADIUS;
                    Masking = true;

                    InternalChildren = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5,
                        },
                        extendedModDisplay = new ModFlowDisplay
                        {
                            AutoSizeAxes = Axes.Both,
                            MaximumSize = new Vector2(400, 0),
                            Margin = new MarginPadding { Vertical = 2f, Horizontal = 10f },
                            Scale = new Vector2(0.6f),
                        },
                    };
                }

                public void SetContent(IReadOnlyList<Mod> content)
                {
                    extendedModDisplay.Current.Value = content;
                }

                public void Move(Vector2 pos) => Position = pos;

                protected override void PopIn() => this.FadeIn(240, Easing.OutQuint);
                protected override void PopOut() => this.FadeOut(240, Easing.OutQuint);
            }
        }

        internal partial class UnrankedBadge : InputBlockingContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public UnrankedBadge()
            {
                Margin = new MarginPadding { Left = BUTTON_WIDTH + 5f };
                Y = -5f;
                Depth = float.MaxValue;
                Origin = Anchor.BottomLeft;
                Shear = OsuGame.SHEAR;
                CornerRadius = !OsuGame.DisableShear.Value ? 8f : CORNER_RADIUS;
                AutoSizeAxes = Axes.X;
                Height = BAR_HEIGHT;
                Masking = true;
                BorderColour = Color4.White;
                BorderThickness = 2f;
                TooltipText = ModSelectOverlayStrings.UnrankedExplanation;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Colour = colours.Orange2,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Shear = -OsuGame.SHEAR,
                        Text = ModSelectOverlayStrings.Unranked.ToUpper(),
                        Margin = new MarginPadding { Horizontal = 15 },
                        UseFullGlyphHeight = false,
                        Font = OsuFont.Torus.With(size: 14f, weight: FontWeight.Bold),
                        Colour = Color4.Black,
                    }
                };
            }
        }
    }
}
