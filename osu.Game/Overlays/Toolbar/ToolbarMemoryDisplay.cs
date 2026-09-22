// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Performance.Debug;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarMemoryDisplay : CompositeDrawable, IHasTooltip
    {
        private readonly BindableBool showMemory = new BindableBool();
        private OsuSpriteText text = null!;
        private double lastUpdate;

        [Resolved(canBeNull: true)]
        private PerformanceDebugService? telemetry { get; set; }

        public LocalisableString TooltipText
        {
            get
            {
                PerformanceDebugSnapshot sample = telemetry?.Latest ?? default;
                return $"Process RAM: {sample.WorkingSetMb:0} MB\nGC heap: {sample.GcHeapMb:0} MB\nAllocations: {sample.AllocationRateMbPerSecond:0.0} MB/s\nGC mode: {sample.GcMode}";
            }
        }

        public ToolbarMemoryDisplay()
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkShowMemoryInToolbar, showMemory);
            InternalChild = text = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Margin = new MarginPadding { Horizontal = 10 },
                Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold, fixedWidth: true),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            showMemory.BindValueChanged(v => Alpha = v.NewValue ? 1 : 0, true);
        }

        protected override void Update()
        {
            base.Update();

            if (!showMemory.Value || telemetry == null || Time.Current - lastUpdate < 500)
                return;

            PerformanceDebugSnapshot sample = telemetry.Latest;
            text.Text = $"RAM {sample.WorkingSetMb:0}M | GC {sample.GcHeapMb:0}M";
            lastUpdate = Time.Current;
        }

        protected override void Dispose(bool isDisposing)
        {
            showMemory.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}