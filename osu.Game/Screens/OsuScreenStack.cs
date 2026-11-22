// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Backgrounds;

namespace osu.Game.Screens
{
    [Cached(typeof(IBackgroundSceneSource))]
    public partial class OsuScreenStack : ScreenStack, ISongSelectBackdropBlurSource, IBackgroundSceneSource
    {
        [Cached]
        private BackgroundScreenStack backgroundScreenStack;

        private readonly ParallaxContainer parallaxContainer;

        public BackdropBlurContainer BackgroundBlurSource { get; } = new BackdropBlurContainer(backgroundOnly: true);

        public osu.Framework.Graphics.Sprites.Sprite? BackgroundSprite => backgroundScreenStack.BackgroundSprite;

        IBackdropBlurSource ISongSelectBackdropBlurSource.Source => BackgroundBlurSource;

        /// <summary>Unpadded viewport used by the background, independently of screen UI layout.</summary>
        public System.Func<Drawable>? BackgroundViewport { get; set; }
        private osu.Framework.Graphics.Sprites.BufferedContainerView<Drawable>? backgroundView;

        protected override void Update()
        {
            base.Update();
            if (backgroundView != null)
                backgroundView.Alpha = BackgroundBlurSource.BufferingActive ? 1 : 0;
            if (BackgroundViewport == null)
                return;

            var viewport = BackgroundViewport();
            var quad = viewport.ScreenSpaceDrawQuad;
            var parent = BackgroundBlurSource.Parent ?? this;
            var topLeft = parent.ToLocalSpace(quad.TopLeft);
            var bottomRight = parent.ToLocalSpace(quad.BottomRight);
            BackgroundBlurSource.RelativeSizeAxes = Axes.None;
            BackgroundBlurSource.Position = topLeft;
            BackgroundBlurSource.Size = bottomRight - topLeft;
        }

        protected float ParallaxAmount => parallaxContainer.ParallaxAmount;

        public new MarginPadding Padding
        {
            get => base.Padding;
            set => base.Padding = value;
        }

        public OsuScreenStack()
        {
            // Menu overlays must sample the current background too, but hidden
            // surfaces must not enable background effects during gameplay/loading.
            BackgroundBlurSource.AllowEffects = () => CurrentScreen != null && CurrentScreen is not Play.Player and not Play.PlayerLoader;
            InternalChild = BackgroundBlurSource;
            BackgroundBlurSource.SceneContent = parallaxContainer = new ParallaxContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = backgroundScreenStack = new BackgroundScreenStack { RelativeSizeAxes = Axes.Both },
            };

            ScreenPushed += screenPushed;
            ScreenExited += ScreenChanged;
        }

        /// <summary>Keep the full background outside the UI capture; capture only its sharp view.</summary>
        public BackdropBlurContainer DetachBackgroundSource()
        {
            BackgroundBlurSource.CaptureOriginal = () => CurrentScreen is Select.ISongSelect or SelectLegacy.SongSelect;
            RemoveInternal(BackgroundBlurSource, false);
            AddInternal(backgroundView = BackgroundBlurSource.CreateOriginalView());
            return BackgroundBlurSource;
        }

        public void PushSynchronously(OsuScreen screen)
        {
            if (LoadState >= LoadState.Loading && Dependencies != null)
                screen.CreateLeasedDependencies((CurrentScreen as OsuScreen)?.Dependencies ?? Dependencies);

            Push(screen);
        }

        private void screenPushed(IScreen prev, IScreen next)
        {
            if (LoadState < LoadState.Ready)
            {
                // dependencies must be present to stay in a sane state.
                // this is generally only ever hit by test scenes.
                Schedule(() => screenPushed(prev, next));
                return;
            }

            // create dependencies synchronously to ensure leases are in a sane state.
            if (next is OsuScreen osuScreen && !osuScreen.HasLeasedDependencies)
                osuScreen.CreateLeasedDependencies((prev as OsuScreen)?.Dependencies ?? Dependencies);

            ScreenChanged(prev, next);
        }

        protected virtual void ScreenChanged(IScreen prev, IScreen? next)
        {
            setParallax(next);
        }

        private void setParallax(IScreen? next) =>
            parallaxContainer.ParallaxAmount = ParallaxContainer.DEFAULT_PARALLAX_AMOUNT * ((next as IOsuScreen)?.BackgroundParallaxAmount ?? 1.0f);
    }
}
