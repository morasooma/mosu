using osu.Game.Localisation;

namespace osu.Game.Overlays.Notifications
{
    /// <summary>
    /// One quiet toast per import/save batch. Closing the toast does not cancel background processing.
    /// </summary>
    public partial class BeatmapCalculationNotification : SimpleNotification
    {
        public override string PopInSampleName => string.Empty;
        public override string PopOutSampleName => string.Empty;

        public BeatmapCalculationNotification()
        {
            Text = BackgroundCalculationStrings.CalculatingNewMaps;
            IsImportant = false;
            Transient = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Also expire when posted while the notification panel is open (where the toast tray isn't involved).
            Scheduler.AddDelayed(() => Close(false), 2500);
        }
    }
}
