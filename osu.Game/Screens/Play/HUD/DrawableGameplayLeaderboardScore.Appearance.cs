using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    public partial class DrawableGameplayLeaderboardScore
    {
        public Bindable<GameplayLeaderboardStyle> Style { get; } = new Bindable<GameplayLeaderboardStyle>();
        public Bindable<GameplayLeaderboardPanelStyle> PanelStyle { get; } = new Bindable<GameplayLeaderboardPanelStyle>();
        public BindableBool ShowMisses { get; } = new BindableBool();
        public BindableBool ShowPp { get; } = new BindableBool();
        public BindableBool ShowMods { get; } = new BindableBool();
        public BindableBool ShowAccuracy { get; } = new BindableBool(true);
        public BindableBool ShowCombo { get; } = new BindableBool(true);

        [Resolved]
        private RulesetStore? rulesets { get; set; }

        private readonly OverlayColourProvider theme = new OverlayColourProvider(OverlayColourScheme.Blue);
        private IBindable<int> palette = null!;
        private readonly IBindable<bool> disableShear = OsuGame.DisableShear.GetBoundCopy();
        private readonly IBindable<float> cardRadius = OverlayColourProvider.GetGeometryRadius(GeometryRole.Card, 10);
        private Container extraInformation = null!;
        private Container statisticsViewport = null!;
        private FillFlowContainer statistics = null!;
        private FillFlowContainer<ModIcon> modIcons = null!;
        private OsuSpriteText missesText = null!;
        private OsuSpriteText performanceText = null!;
        private bool extraInvalid = true;
        private int? lastMisses;
        private double? lastPp;
        private string? lastMods;
        private double nextInformationUpdate;
        private bool hasExtra => ShowMisses.Value || ShowPp.Value || ShowMods.Value;

        internal string ExtraInformation => $"{missesText.Text} {performanceText.Text}";
        internal ColourInfo PanelBackground => rightLayerGradient.Colour;
        internal float PanelRadius => scorePanel.CornerRadius;
        internal Colour4 MissesColour => missesText.Colour.TopLeft;
        internal int ModIconCount => modIcons.Count;
        internal float AvatarTop => avatarLayer.Margin.Top;
        internal float AvatarSize => avatarLayer.Width;
        internal Vector2 AvatarCentre => avatar.ScreenSpaceDrawQuad.Centre;
        internal Vector2 AvatarSlotCentre => avatarLayer.ScreenSpaceDrawQuad.Centre;
        private float currentAvatarSize => hasExtra ? Height : avatar_size;

        private void initialiseAppearance()
        {
            rightLayer.Add(extraInformation = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Height = 16,
                Y = -4,
                Padding = new MarginPadding { Left = avatar_size / 2 + 4, Right = 12 },
                Children = new Drawable[]
                {
                    statisticsViewport = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        Child = statistics = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Children = new Drawable[]
                            {
                                missesText = new OsuSpriteText { Font = OsuFont.Style.Caption2, Colour = colours.Red2 },
                                performanceText = new OsuSpriteText { Font = OsuFont.Style.Caption2 },
                            },
                        },
                    },
                    modIcons = new FillFlowContainer<ModIcon>
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Spacing = new Vector2(1, 0),
                    },
                },
            });
            Style.BindValueChanged(_ => refreshAppearance());
            PanelStyle.BindValueChanged(_ => refreshAppearance());
            ShowMisses.BindValueChanged(_ => updateInformationLayout());
            ShowPp.BindValueChanged(_ => updateInformationLayout());
            ShowMods.BindValueChanged(_ => updateInformationLayout());
            disableShear.BindValueChanged(_ => queueAppearance());
            cardRadius.BindValueChanged(_ => queueAppearance());
            palette = theme.PaletteVersion;
            palette.BindValueChanged(_ => queueAppearance());
            updateInformationLayout();
            refreshAppearance();
        }

        private void updateInformationLayout()
        {
            Height = PANEL_HEIGHT + (hasExtra ? 16 : 0);
            scoreComponents.Padding = new MarginPadding { Left = currentAvatarSize / 2 + 4, Right = 12, Top = 5, Bottom = hasExtra ? 21 : 5 };
            extraInformation.Alpha = hasExtra ? 1 : 0;
            missesText.Alpha = ShowMisses.Value ? 1 : 0;
            performanceText.Alpha = ShowPp.Value ? 1 : 0;
            modIcons.Alpha = ShowMods.Value ? 1 : 0;
            extraInvalid = true;
            updateRowGeometry();
            drawSizeLayout.Invalidate();
        }

        private void queueAppearance()
        {
            if (ThreadSafety.IsUpdateThread) refreshAppearance();
            else Scheduler.AddOnce(refreshAppearance);
        }

        private void updateRowGeometry()
        {
            if (extraInformation == null) return;
            bool modern = Style.Value == GameplayLeaderboardStyle.Morasooma;
            // A global card radius can be appropriate for large panels but turns this
            // compact row into a pill. Explicit skin overrides still take precedence.
            float radius = SkinComponentGeometry.GetInheritedOverride(this) ?? (modern ? Math.Min(cardRadius.Value, 10) : 10);
            scorePanel.CornerRadius = radius;
            float avatarSize = currentAvatarSize;
            avatarMask.CornerRadius = Math.Min(radius, avatarSize / 4);
            avatarLayer.Size = new Vector2(avatarSize);
            avatarLayer.Margin = new MarginPadding { Left = -avatarSize / 2 };
            rightLayer.Margin = new MarginPadding { Left = -avatarSize / 2 };
            extraInformation.Padding = new MarginPadding { Left = avatarSize / 2 + 4, Right = 12 };
            float maxWidth = Math.Max(0, Math.Max(0, extraInformation.DrawWidth - extraInformation.Padding.TotalHorizontal) * 0.5f);
            float scale = modIcons.Width > 0 ? Math.Min(1, maxWidth / modIcons.Width) : 1;
            modIcons.Scale = new Vector2(scale);
            float iconWidth = modIcons.DrawWidth * scale;
            statisticsViewport.Padding = new MarginPadding { Right = ShowMods.Value && iconWidth > 0 ? iconWidth + 8 : 0 };
            extraInformation.Alpha = hasExtra && rightLayer.Width >= username_score_width_cutoff ? scoreComponents.Alpha : 0;
        }

        private void refreshAppearance()
        {
            if (extraInformation == null) return;
            bool modern = Style.Value == GameplayLeaderboardStyle.Morasooma;
            var shear = OsuGame.SHEAR;
            Shear = shear;
            positionText.Shear = scoreComponents.Shear = extraInformation.Shear = -shear;
            // Counter-shear the centred mask, not the image inside a top-left mask.
            // Otherwise the upright image slides under a still-sheared clipping shape.
            avatarMask.Shear = -shear;
            avatar.Shear = Vector2.Zero;
            avatar.Scale = Vector2.One;
            updateRowGeometry();
            var primary = modern ? theme.Content1 : osuTK.Graphics.Color4.White;
            var secondary = modern ? theme.Content2 : osuTK.Graphics.Color4.White;
            scoreText.Colour = positionText.Colour = primary;
            accuracyText.Colour = comboText.Colour = performanceText.Colour = secondary;
            // Misses are a judgement, not a cosmetic theme role.
            missesText.Colour = colours.Red2;
            updatePanelState();
        }

        private void applyThemedPanel()
        {
            bool solid = PanelStyle.Value == GameplayLeaderboardPanelStyle.Solid;
            var background = (Colour4)theme.Background3;
            var elevated = (Colour4)theme.Background4;
            leftLayerGradient.Colour = elevated.Opacity(solid ? 1 : 0.75f);
            rightLayerGradient.Colour = background.Opacity(solid ? 1 : 0.6f);
            panelOutline.Colour = BackgroundColour ?? (Tracked || ScorePosition.Value == 1 ? theme.Highlight1 : theme.Background2);
            panelOutline.Thickness = 1.5f;
        }

        private void updateExtraInformation()
        {
            updateRowGeometry();
            if (!hasExtra) return;
            double now = Time.Current;
            if (!extraInvalid && now < nextInformationUpdate) return;
            nextInformationUpdate = now + 100;
            int? misses = ShowMisses.Value ? score.GetMisses() : null;
            double? pp = ShowPp.Value ? score.GetPerformance() : null;
            string mods = ShowMods.Value ? score.Mods : string.Empty;
            if (!extraInvalid && misses == lastMisses && pp == lastPp && mods == lastMods) return;
            bool modsChanged = extraInvalid || mods != lastMods;
            lastMisses = misses;
            lastPp = pp;
            lastMods = mods;
            extraInvalid = false;
            missesText.Text = ShowMisses.Value ? $"× {misses?.ToString() ?? "—"}" : string.Empty;
            performanceText.Text = ShowPp.Value ? (pp.HasValue && double.IsFinite(pp.Value) ? $"{pp.Value:0.##} pp" : "— pp") : string.Empty;
            if (modsChanged)
            {
                modIcons.Clear();
                if (ShowMods.Value)
                    foreach (var mod in score.GetDisplayMods(rulesets).AsVisible().AsOrdered())
                        modIcons.Add(new ModIcon(mod, showTooltip: false, showExtendedInformation: false) { Scale = new Vector2(0.23f) });
            }
        }
    }
}
