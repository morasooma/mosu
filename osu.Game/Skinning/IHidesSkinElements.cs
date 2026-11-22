using osu.Game.Configuration;

namespace osu.Game.Skinning
{
    /// <summary>Replay-serialised mod restrictions, independent of local skin preferences.</summary>
    public interface IHidesSkinElements
    {
        bool Hides(OsuSetting visibilitySetting);
    }
}
