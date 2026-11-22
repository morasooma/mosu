using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ManiaEditorStrings
    {
        private const string prefix = "osu.Game.Localisation.ManiaEditor";

        public static LocalisableString AutoSeekOnPlacementCaption => new TranslatableString($"{prefix}:auto_seek_on_placement_caption", "Auto-scroll when placing notes in mania");
        public static LocalisableString AutoSeekOnPlacementHint => new TranslatableString($"{prefix}:auto_seek_on_placement_hint", "Jump to the start of a newly placed note if it is ahead of the current position. When off, placing notes leaves the editor where it is.");
    }
}
