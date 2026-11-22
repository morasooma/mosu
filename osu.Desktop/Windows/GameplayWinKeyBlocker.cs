// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Screens.Play;

namespace osu.Desktop.Windows
{
    public partial class GameplayWinKeyBlocker : Component
    {
        private Bindable<bool> disableWinKey = null!;
        private IBindable<LocalUserPlayingState> localUserPlaying = null!;
        private IBindable<bool> isActive = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(ILocalUserPlayInfo localUserInfo, OsuConfigManager config)
        {
            localUserPlaying = localUserInfo.PlayingState.GetBoundCopy();
            isActive = host.IsActive.GetBoundCopy();
            disableWinKey = config.GetBindable<bool>(OsuSetting.GameplayDisableWinKey);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUserPlaying.BindValueChanged(_ => updateBlocking());
            isActive.BindValueChanged(_ => updateBlocking());
            disableWinKey.BindValueChanged(_ => updateBlocking(), true);
        }

        private void updateBlocking()
        {
            bool shouldDisable = isActive.Value && disableWinKey.Value && localUserPlaying.Value == LocalUserPlayingState.Playing;

            // Low-level hooks must be installed on the thread pumping native window messages.
            host.InputThread.Scheduler.Add(() =>
            {
                if (shouldDisable)
                    WindowsKey.Disable();
                else
                    WindowsKey.Enable();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                localUserPlaying?.UnbindAll();
                isActive?.UnbindAll();
                disableWinKey?.UnbindAll();
                host.InputThread.Scheduler.Add(WindowsKey.Enable);
            }

            base.Dispose(isDisposing);
        }
    }
}
