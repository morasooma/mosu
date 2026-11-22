using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Play.HUD
{
    public enum GameplayLeaderboardStyle
    {
        [LocalisableDescription(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.LeaderboardClassic))]
        Classic,
        [LocalisableDescription(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.LeaderboardMorasooma))]
        Morasooma,
    }

    public enum GameplayLeaderboardPanelStyle
    {
        [LocalisableDescription(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.LeaderboardTransparent))]
        Transparent,
        [LocalisableDescription(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.LeaderboardSolid))]
        Solid,
    }
}
