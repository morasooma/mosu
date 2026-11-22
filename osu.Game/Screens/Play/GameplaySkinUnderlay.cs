using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Screens.Play
{
    /// <summary>Draw proxies for decorative skin surfaces, behind the gameplay ruleset.</summary>
    public partial class GameplaySkinUnderlay : Container
    {
        public GameplaySkinUnderlay() { RelativeSizeAxes = Axes.Both; }
        public void RemoveProxy(Drawable proxy) => Scheduler.Add(() => Remove(proxy, true));
    }
}
