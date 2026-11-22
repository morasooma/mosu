using osu.Framework.Bindables;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Controls presentation only. Hidden settings still participate in serialisation and cloning.
    /// </summary>
    public interface IHasConditionalSettings
    {
        IBindable<bool>? GetSettingVisibility(string propertyName);
    }
}
