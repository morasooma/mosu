namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// A blur source containing only screen backgrounds, never carousel controls.
    /// </summary>
    public interface ISongSelectBackdropBlurSource
    {
        IBackdropBlurSource Source { get; }
    }
}
