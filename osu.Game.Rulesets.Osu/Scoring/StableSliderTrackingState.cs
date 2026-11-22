using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Scoring
{
    /// <summary>
    /// Stable's button transition history is global input state, not slider head state.
    /// Only changes in the held button mask advance the two previous masks.
    /// </summary>
    internal sealed class StableButtonState
    {
        public int Buttons { get; private set; }
        public int PreviousButtons { get; private set; }
        public int PreviousButtons2 { get; private set; }
        public int NewButtons { get; private set; }

        public void Update(int buttons)
        {
            NewButtons = buttons & ~Buttons;
            if (buttons == Buttons)
                return;

            PreviousButtons2 = PreviousButtons;
            PreviousButtons = Buttons;
            Buttons = buttons;
        }
    }

    /// <summary>
    /// Stable acquires tracking independently of the head judgement and records when
    /// tracking begins. A checkpoint passed before acquisition cannot be recovered.
    /// </summary>
    internal sealed class StableSliderTrackingState
    {
        private int downButton;
        private readonly List<Snapshot> history = new List<Snapshot>();

        public bool Tracking { get; private set; }
        public double SlideStart { get; private set; } = double.NegativeInfinity;

        public void Update(double time, double startTime, bool judged, bool inFollowArea, StableButtonState buttons, bool relax)
        {
            // Gameplay clock values in stable are integer milliseconds, including
            // live input. Do not make replay playback the only integer-timed path.
            time = (int)time;

            // Stable returns before touching the slider's held-button state before
            // StartTime. Otherwise a pre-held key incorrectly becomes the slider key.
            if (time < startTime || judged)
                return;

            bool swapAllowed = buttons.Buttons != 0 && !(buttons.PreviousButtons == 3 && buttons.PreviousButtons2 == buttons.Buttons);
            bool held = false;
            if (buttons.Buttons == 0)
                downButton = 0;
            else if (downButton == 0 || (buttons.Buttons != 3 && swapAllowed))
            {
                downButton = (buttons.NewButtons & 1) != 0 ? 1
                    : (buttons.NewButtons & 2) != 0 ? 2 : buttons.Buttons;
                held = true;
            }
            else
                held = (buttons.Buttons & downButton) != 0;

            bool trackingNow = inFollowArea && (held || swapAllowed || relax);
            if (trackingNow && !Tracking)
                SlideStart = time;
            Tracking = trackingNow;
            record(time);
        }

        public void HandleHeadPress(OsuAction action, double time)
        {
            downButton = action == OsuAction.LeftButton ? 1 : 2;
            record((int)time);
        }

        public bool CanHitCheckpoint(double time) => Tracking && SlideStart <= time;

        public void Rewind(double time)
        {
            while (history.Count > 0 && history[^1].Time > time)
                history.RemoveAt(history.Count - 1);
            if (history.Count == 0)
            {
                downButton = 0;
                Tracking = false;
                SlideStart = double.NegativeInfinity;
            }
            else
            {
                var snapshot = history[^1];
                downButton = snapshot.Button;
                Tracking = snapshot.Tracking;
                SlideStart = snapshot.SlideStart;
            }
        }

        private void record(double time)
        {
            var snapshot = new Snapshot(time, downButton, Tracking, SlideStart);
            if (history.Count > 0)
            {
                var previous = history[^1];
                if (previous.Button == downButton && previous.Tracking == Tracking && previous.SlideStart == SlideStart)
                    return;
            }
            history.Add(snapshot);
        }

        private readonly record struct Snapshot(double Time, int Button, bool Tracking, double SlideStart);
    }
}
