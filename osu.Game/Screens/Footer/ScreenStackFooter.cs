// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;
using osuTK;

namespace osu.Game.Screens.Footer
{
    public partial class ScreenStackFooter : CompositeDrawable
    {
        /// <summary>
        /// Called when logo tracking begins. The legacy footer keeps the logo behind its skinned chrome,
        /// while the regular footer keeps the existing frontmost behaviour.
        /// </summary>
        public Action<bool>? RequestLogoInFront { private get; init; }

        /// <summary>
        /// The back button was pressed.
        /// </summary>
        public Action? BackButtonPressed { private get; init; }

        /// <summary>
        /// The (legacy) back button.
        /// </summary>
        public readonly BackButton BackButton;

        /// <summary>
        /// The footer.
        /// </summary>
        public readonly ScreenFooter Footer;

        /// <summary>
        /// Whether the legacy back button is currently displayed.
        /// </summary>
        private readonly IBindable<bool> backButtonVisibility = new BindableBool();

        private readonly ScreenStackTracker screenTracker;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private readonly IBindable<Skin> currentSkin = new Bindable<Skin>();
        private Bindable<ForkSongSelectStyle> songSelectStyle = null!;
        private Container? legacyFooterContainer;
        private LegacyFooter? legacyFooter;
        private bool legacyFooterLoading;
        private int legacyFooterGeneration;
        private bool allowLegacyFooterSkinning;

        public ScreenStackFooter(ScreenStack screenStack, ScreenFooter.BackReceptor? backReceptor = null)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                BackButton = new BackButton(backReceptor)
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Action = () => BackButtonPressed?.Invoke(),
                },
                Footer = new ScreenFooter(backReceptor)
                {
                    RequestLogoInFront = v => RequestLogoInFront?.Invoke(v && !isLegacyFooterActive),
                    BackButtonPressed = () => BackButtonPressed?.Invoke()
                }
            };

            screenTracker = new ScreenStackTracker(screenStack);
            screenTracker.ScreenChanged += onScreenChanged;

            backButtonVisibility.ValueChanged += onBackButtonVisibilityChanged;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            currentSkin.BindTo(skins.CurrentSkin);
            currentSkin.BindValueChanged(_ => rebuildLegacyFooter());

            songSelectStyle = config.GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);
            songSelectStyle.BindValueChanged(_ => rebuildLegacyFooter());

            Footer.OverlayStateChanged += updateLegacyFooter;
        }

        private void rebuildLegacyFooter()
        {
            legacyFooterGeneration++;

            if (legacyFooterContainer != null)
            {
                RemoveInternal(legacyFooterContainer, true);
                legacyFooterContainer = null;
                legacyFooter = null;
            }

            legacyFooterLoading = false;
            updateLegacyFooter();
        }

        private void updateLegacyFooter()
        {
            bool active = isLegacyFooterActive;

            if (active)
                ensureLegacyFooterLoaded();

            legacyFooterContainer?.FadeTo(active ? 1 : 0, 120, Easing.OutQuint);
            Footer.SetDefaultChromeVisible(!active);
            RequestLogoInFront?.Invoke(!active);
        }

        private bool isLegacyFooterActive
            => allowLegacyFooterSkinning
               && songSelectStyle.Value.UsesStableStyle()
               && !Footer.HasActiveOverlay;

        private void ensureLegacyFooterLoaded()
        {
            if (legacyFooter != null || legacyFooterLoading)
                return;

            legacyFooterLoading = true;
            int generation = legacyFooterGeneration;

            var footer = new LegacyFooter
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Y = 4,
                BackAction = () => BackButtonPressed?.Invoke(),
                ModsAction = () => Footer.TriggerFooterButton(0),
                RandomAction = () => Footer.TriggerFooterButton(1),
                OptionsMenuItems = getStableOptionsMenuItems,
            };

            // Match the song-select chrome's skin coordinates even when UIScale is changed.
            // The 4:3 minimum allows the footer to select its native 1024-wide layout.
            var container = new DrawSizePreservingFillContainer
            {
                RelativeSizeAxes = Axes.Both,
                TargetDrawSize = new Vector2(1024, 768),
                Alpha = 0,
                Child = footer,
            };

            LoadComponentAsync(container, loaded =>
            {
                if (generation != legacyFooterGeneration)
                {
                    loaded.Dispose();
                    return;
                }

                legacyFooter = footer;
                legacyFooterContainer = loaded;
                legacyFooterLoading = false;
                AddInternal(loaded);
                updateLegacyFooter();
            });
        }

        private osu.Framework.Graphics.UserInterface.MenuItem[] getStableOptionsMenuItems()
        {
            if (screenTracker.LeadingScreen is not Select.ISongSelect songSelect)
                return Array.Empty<osu.Framework.Graphics.UserInterface.MenuItem>();

            var beatmapInfo = beatmap.Value?.BeatmapInfo;

            if (beatmapInfo == null)
                return Array.Empty<osu.Framework.Graphics.UserInterface.MenuItem>();

            return songSelect.GetForwardActions(beatmapInfo).Cast<osu.Framework.Graphics.UserInterface.MenuItem>().ToArray();
        }

        private void onScreenChanged(IScreen lastScreen, IScreen newScreen)
        {
            unbindScreen(lastScreen);
            bindScreen(newScreen);
        }

        private void onBackButtonVisibilityChanged(ValueChangedEvent<bool> visible)
        {
            if (visible.NewValue)
                BackButton.Show();
            else
                BackButton.Hide();
        }

        private void unbindScreen(IScreen screen)
        {
            if (screen is not OsuScreen osuScreen)
                return;

            backButtonVisibility.UnbindFrom(osuScreen.BackButtonVisibility);
        }

        private void bindScreen(IScreen screen)
        {
            if (screen is not OsuScreen osuScreen)
            {
                ((BindableBool)backButtonVisibility).Value = true;

                allowLegacyFooterSkinning = false;
                updateLegacyFooter();

                Footer.SetButtons([]);
                Footer.Hide();
                return;
            }

            allowLegacyFooterSkinning = osuScreen.ShowFooter && osuScreen.AllowLegacyFooterSkinning;
            updateLegacyFooter();

            if (osuScreen.ShowFooter)
            {
                // the legacy back button should never display while the new footer is in use, as it
                // contains its own local back button.
                ((BindableBool)backButtonVisibility).Value = false;

                Footer.Show();

                if (osuScreen.IsLoaded)
                    updateFooterButtons();
                else
                {
                    // ensure the current buttons are immediately disabled on screen change (so they can't be pressed).
                    Footer.SetButtons([]);

                    osuScreen.OnLoadComplete += _ => updateFooterButtons();
                }

                void updateFooterButtons()
                {
                    var buttons = osuScreen.CreateFooterButtons();

                    osuScreen.LoadComponentsAgainstScreenDependencies(buttons);

                    Footer.SetButtons(buttons);
                    Footer.Show();
                }
            }
            else
            {
                backButtonVisibility.BindTo(osuScreen.BackButtonVisibility);

                Footer.SetButtons([]);
                Footer.Hide();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Footer.OverlayStateChanged -= updateLegacyFooter;
            screenTracker.Dispose();
        }

        /// <summary>
        /// Recursively represents a single screen stack and any nested subscreen stack.
        /// </summary>
        private class ScreenStackTracker : IDisposable
        {
            /// <summary>
            /// Invoked when the leading screen changes.
            /// </summary>
            /// <remarks>
            /// This differs from <see cref="ScreenStack.ScreenPushed"/> and <see cref="ScreenStack.ScreenExited"/>
            /// because <c>lastScreen</c> and <c>newScreen</c> may be subscreens of the current screen stack.
            /// <br />
            /// As such, no assumptions may be made as to the relation of screens to this entry's <see cref="ScreenStack"/>.
            /// </remarks>
            public event ScreenChangedDelegate? ScreenChanged;

            /// <summary>
            /// The screen stack tracked by this entry.
            /// </summary>
            private readonly ScreenStack stack;

            /// <summary>
            /// An entry corresponding to the subscreen stack of the current screen, if any.
            /// </summary>
            private ScreenStackTracker? subScreenTracker;

            /// <summary>
            /// The screen which should be bound to the screen footer - the most nested subscreen.
            /// </summary>
            // ReSharper disable once FunctionRecursiveOnAllPaths (TODO: remove after fixed https://youtrack.jetbrains.com/issue/RIDER-135036/Incorrect-recursive-on-all-execution-paths-inspection)
            private IScreen leadingScreen => subScreenTracker?.leadingScreen ?? stack.CurrentScreen;

            /// <summary>
            /// The screen currently bound to the footer (the most nested subscreen).
            /// </summary>
            public IScreen? LeadingScreen => leadingScreen;

            public ScreenStackTracker(ScreenStack stack)
            {
                this.stack = stack;

                stack.ScreenPushed += onParentScreenChanged;
                stack.ScreenExited += onParentScreenChanged;
            }

            private void onParentScreenChanged(IScreen lastScreen, IScreen newScreen)
            {
                // The screen which we will be UNBINDING from the screen footer later on.
                IScreen lastLeadingScreen = subScreenTracker?.leadingScreen ?? lastScreen;

                // Subscreens are attached to a parent screen, so when the parent changes the subscreen must also.
                subScreenTracker?.Dispose();
                subScreenTracker = null;

                // Check if we've switched to a screen that has a subscreen.
                if (newScreen is IHasSubScreenStack newStack)
                {
                    subScreenTracker = new ScreenStackTracker(newStack.SubScreenStack);
                    subScreenTracker.ScreenChanged += onSubScreenScreenChanged;
                }

                ScreenChanged?.Invoke(lastLeadingScreen, leadingScreen);
            }

            private void onSubScreenScreenChanged(IScreen lastScreen, IScreen newScreen)
            {
                ScreenChanged?.Invoke(lastScreen, newScreen);
            }

            public void Dispose()
            {
                stack.ScreenPushed -= onParentScreenChanged;
                stack.ScreenExited -= onParentScreenChanged;

                if (subScreenTracker != null)
                {
                    subScreenTracker.ScreenChanged -= onSubScreenScreenChanged;
                    subScreenTracker.Dispose();
                }
            }
        }
    }
}
