using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// An Argon key counter with a resizable, upward-scrolling input history.
    /// </summary>
    public partial class ScrollingKeyCounter : KeyCounter
    {
        private const float default_history_height = 180;
        private const float key_height = 45;
        private const float minimum_bar_height = 3;
        private const int maximum_bars = 256;

        public BindableFloat ScrollSpeed { get; } = new BindableFloat(90) { MinValue = 10, MaxValue = 1000, Precision = 1 };

        private readonly Container history;
        private readonly DrawablePool<PressBar> barPool;
        private readonly List<PressBar> bars = new List<PressBar>();
        private PressBar? heldBar;
        private double? lastTime;
        private double historyTime;
        private bool ready;
        private readonly SkinComponentColourProvider themeColours = new SkinComponentColourProvider();

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        private double currentTime => gameplayClock?.CurrentTime ?? Clock.CurrentTime;

        private double playbackRate => Math.Abs(gameplayClock?.Rate ?? Clock.Rate);

        // Integrate each interval separately: dividing the entire history by the current
        // rate would resize and reposition old bars whenever the playback rate changes.
        private double updateHistoryTime()
        {
            double now = currentTime;
            if (lastTime > now || gameplayClock?.IsRewinding == true)
                clearHistory();

            if (lastTime.HasValue && playbackRate > 0.0001)
                historyTime += Math.Max(0, now - lastTime.Value) / playbackRate;
            lastTime = now;
            return historyTime;
        }

        // The trigger can be inside the frame-stable ruleset, ahead of or behind the HUD's clock.
        private double eventTime
        {
            get
            {
                double time = updateHistoryTime();
                return gameplayClock != null && playbackRate > 0.0001
                    ? time + (Trigger.Clock.CurrentTime - currentTime) / playbackRate
                    : time;
            }
        }

        public ScrollingKeyCounter(InputTrigger trigger)
            : base(trigger)
        {
            Size = new Vector2(52.5f, default_history_height + key_height);

            AddInternal(barPool = new DrawablePool<PressBar>(16, maximum_bars));
            AddRange(new Drawable[]
            {
                history = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = default_history_height,
                    Masking = true,
                },
                new ArgonKeyCounter(trigger)
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                },
            });
        }

        protected override void LoadComplete()
        {
            themeColours.Bind(action => Scheduler.Add(action), () => history.Colour = themeColours.GetColour(OverlayColour.Highlight1, Colour4.White));
            ready = true;

            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek += clearHistory;

            base.LoadComplete();
        }

        protected override void Activate(bool forwardPlayback = true)
        {
            base.Activate(forwardPlayback);

            if (!ready)
                return;

            if (!forwardPlayback)
            {
                clearHistory();
                return;
            }

            double time = eventTime;
            if (heldBar != null)
                return;

            if (bars.Count == maximum_bars)
                removeBar(0);

            heldBar = barPool.Get();
            heldBar.StartTime = time;
            heldBar.EndTime = null;
            bars.Add(heldBar);
            history.Add(heldBar);
        }

        protected override void Deactivate(bool forwardPlayback = true)
        {
            base.Deactivate(forwardPlayback);

            if (!ready)
                return;

            if (!forwardPlayback)
            {
                clearHistory();
                return;
            }

            double time = eventTime;
            if (heldBar == null)
                return;

            heldBar.EndTime = Math.Max(heldBar.StartTime, time);
            heldBar = null;
        }

        protected override void Update()
        {
            base.Update();

            history.Height = Math.Max(0, DrawHeight - key_height);
            double pixelsPerMillisecond = ScrollSpeed.Value / 1000.0;
            double historyDuration = history.Height / pixelsPerMillisecond;
            // Keep pause/seek behaviour tied to gameplay, but remove DT/HT's rate
            // from the visual timeline so the setting means real pixels per second.
            double now = updateHistoryTime();

            // A key can already be held when this component is added, or after a seek.
            if (Trigger.IsActive && heldBar == null && gameplayClock?.IsRewinding != true)
                Activate();

            for (int i = bars.Count - 1; i >= 0; i--)
            {
                var bar = bars[i];
                double endTime = bar.EndTime ?? now;
                double age = Math.Max(0, now - endTime);

                if (age >= historyDuration)
                {
                    removeBar(i);
                    continue;
                }

                bar.Y = -(float)(age * pixelsPerMillisecond);
                bar.Height = (float)Math.Clamp((endTime - bar.StartTime) * pixelsPerMillisecond, Math.Min(minimum_bar_height, history.Height), history.Height);
            }
        }

        private void removeBar(int index)
        {
            if (bars[index] == heldBar)
                heldBar = null;
            history.Remove(bars[index], false);
            bars.RemoveAt(index);
        }

        private void clearHistory()
        {
            heldBar = null;
            history.Clear(false);
            bars.Clear();
            lastTime = null;
            historyTime = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek -= clearHistory;

            base.Dispose(isDisposing);
        }

        private partial class PressBar : PoolableDrawable
        {
            public double StartTime;
            public double? EndTime;

            public PressBar()
            {
                RelativeSizeAxes = Axes.X;
                Anchor = Anchor.BottomCentre;
                Origin = Anchor.BottomCentre;
                Alpha = 0.75f;
                InternalChild = new SkinComponentRoundedContainer { RelativeSizeAxes = Axes.Both };
            }
        }
    }
}
