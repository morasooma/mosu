using osu.Game.Configuration;

namespace osu.Game.Skinning
{
    /// <summary>A visual component whose visibility can be customised without removing its gameplay owner.</summary>
    public interface IHasVisibilitySetting
    {
        OsuSetting? VisibilitySetting { get; }
    }
}
