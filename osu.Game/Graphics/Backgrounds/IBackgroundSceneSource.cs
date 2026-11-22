using osu.Framework.Graphics.Sprites;

namespace osu.Game.Graphics.Backgrounds
{
    /// <summary>
    /// The image actually displayed behind the current screen, with its screen-space geometry.
    /// </summary>
    public interface IBackgroundSceneSource
    {
        Sprite? BackgroundSprite { get; }
    }
}
