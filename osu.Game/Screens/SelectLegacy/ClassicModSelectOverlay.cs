using osu.Game.Overlays.Mods;

namespace osu.Game.Screens.SelectLegacy
{
    /// <summary>
    /// Keeps the current mod selection logic while restoring classic overlay sounds.
    /// </summary>
    public partial class ClassicModSelectOverlay : UserModSelectOverlay
    {
        protected override string PopInSampleName => "SongSelectClassic/overlay-pop-in";
        protected override string PopOutSampleName => "SongSelectClassic/overlay-pop-out";
        protected override string ColumnAppearSampleName => string.Empty;
    }
}
