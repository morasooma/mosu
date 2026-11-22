namespace osu.Game.Graphics.Fonts
{
    /// <summary>Explicit user action; constructing the settings panel never changes OS associations.</summary>
    public interface IFontFileAssociation
    {
        bool IsSupported { get; }
        void Register();
    }
}
