// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Performance.Debug;
using osuTK;

namespace osu.Game.Graphics.UserInterface
{
    public partial class PerformanceDebugOverlay : CompositeDrawable
    {
        private readonly Bindable<DebugHudMode> mode = new Bindable<DebugHudMode>();
        private readonly BindableBool showAlerts = new BindableBool();
        private OsuSpriteText metricsText = null!;
        private OsuSpriteText alertText = null!;
        private Container alert = null!;
        private Container metrics = null!;
        private double lastMetricsUpdate;
        private long displayedFreezeSequence;

        [Resolved(canBeNull: true)]
        private PerformanceDebugService? telemetry { get; set; }

        public PerformanceDebugOverlay()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OsuColour colours)
        {
            config.BindWith(OsuSetting.ForkDebugHudMode, mode);
            config.BindWith(OsuSetting.ForkDebugFreezeAlerts, showAlerts);

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    alert = createPanel(colours, alertText = createText(14)),
                    metrics = createPanel(colours, metricsText = createText(13)),
                }
            };

            alert.Alpha = 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            mode.BindValueChanged(_ => updateVisibility(), true);
            showAlerts.BindValueChanged(_ => updateVisibility(), true);
        }

        protected override void Update()
        {
            base.Update();

            if (telemetry == null || (mode.Value == DebugHudMode.Disabled && !showAlerts.Value))
                return;

            if (mode.Value != DebugHudMode.Disabled && Time.Current - lastMetricsUpdate >= 250)
            {
                PerformanceDebugSnapshot s = telemetry.Latest;
                metricsText.Text = mode.Value == DebugHudMode.Detailed
                    ? PerformanceDebugStrings.DetailedMetrics(s)
                    : PerformanceDebugStrings.CompactMetrics(s);
                lastMetricsUpdate = Time.Current;
            }

            PerformanceFreezeEvent? lastFreeze = telemetry.LastFreeze;

            if (showAlerts.Value && lastFreeze is { } freeze && freeze.Sequence != displayedFreezeSequence)
            {
                displayedFreezeSequence = freeze.Sequence;
                alertText.Text = PerformanceDebugStrings.FreezeAlert(freeze);
                alert.ClearTransforms();
                alert.FadeIn(100).Delay(4000).FadeOut(300);
            }
        }

        private void updateVisibility()
        {
            metrics.Alpha = mode.Value == DebugHudMode.Disabled ? 0 : 1;
            Alpha = mode.Value != DebugHudMode.Disabled || showAlerts.Value ? 1 : 0;
        }

        private static Container createPanel(OsuColour colours, Drawable content) => new Container
        {
            AutoSizeAxes = Axes.Both,
            CornerRadius = 5,
            Masking = true,
            Children = new[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Gray0,
                    Alpha = 0.82f,
                },
                content,
            }
        };

        private static OsuSpriteText createText(float size) => new OsuSpriteText
        {
            Margin = new MarginPadding { Horizontal = 9, Vertical = 5 },
            Font = OsuFont.Default.With(size: size, fixedWidth: true, weight: FontWeight.SemiBold),
        };

        protected override void Dispose(bool isDisposing)
        {
            mode.UnbindAll();
            showAlerts.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}