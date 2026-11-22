using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// Animates the row's visual content, never the row participating in settings layout.
    /// Horizontal scaling preserves auto-sized height and the positions of neighbouring rows.
    /// </summary>
    internal partial class SettingsRowAnimation : Component
    {
        private readonly Drawable row;
        private readonly Drawable content;
        private IBindable<bool>? enhanced;
        private IBindable<InterfaceAnimationMode>? mode;
        private IBindable<Visibility>? panelVisibility;
        private bool wasVisible;
        private double lastUpdateTime = double.NegativeInfinity;

        public SettingsRowAnimation(Drawable row, Drawable content)
        {
            this.row = row;
            this.content = content;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager? config, SettingsPanel? panel)
        {
            enhanced = config?.GetBindable<bool>(OsuSetting.ForkEnhancedAnimations);
            mode = InterfaceAnimation.Mode.GetBoundCopy();
            panelVisibility = panel?.State.GetBoundCopy();

            enhanced?.BindValueChanged(_ => Reset());
            mode.BindValueChanged(_ => Reset());
            panelVisibility?.BindValueChanged(_ => Reset());
        }

        protected override void Update()
        {
            base.Update();

            if (enhanced?.Value != true || !InterfaceAnimation.AllowDecorativeMovement)
            {
                wasVisible = false;
                return;
            }

            // A masked ancestor can skip this component entirely. Treat a gap in
            // updates as a fresh appearance when that ancestor becomes visible again.
            if (Clock.CurrentTime - lastUpdateTime > Clock.ElapsedFrameTime + 1)
                wasVisible = false;

            lastUpdateTime = Clock.CurrentTime;

            bool visible = row.IsPresent && row.DrawHeight > 0
                && panelVisibility?.Value != Visibility.Hidden
                && row.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(row.ComputeMaskingBounds());

            if (visible && !wasVisible)
            {
                content.ScaleTo(new Vector2(0.05f, 1));
                content.ScaleTo(Vector2.One, 220, Easing.InOutSine);
            }

            wasVisible = visible;
        }

        public void Reset()
        {
            content.FinishTransforms(targetMember: nameof(Drawable.Scale));
            content.Scale = Vector2.One;
            wasVisible = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            enhanced?.UnbindAll();
            mode?.UnbindAll();
            panelVisibility?.UnbindAll();
            base.Dispose(isDisposing);
        }
    }
}
