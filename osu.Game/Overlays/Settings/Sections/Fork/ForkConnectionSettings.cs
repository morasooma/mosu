// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkConnectionSettings : SettingsSubsection
    {
        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.ConnectionHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Bindable<bool> useProxy = config.GetBindable<bool>(OsuSetting.ForkUseConnectionProxy);
            bool revertingChange = false;

            useProxy.BindValueChanged(change =>
            {
                if (revertingChange || change.NewValue == change.OldValue)
                    return;

                Scheduler.Add(() =>
                {
                    if (game == null)
                        return;

                    dialogOverlay?.Push(new ConfirmDialog(
                        ForkSettingsStrings.ConnectionProxyRestartBody,
                        restartGame,
                        () =>
                        {
                            revertingChange = true;
                            useProxy.Value = change.OldValue;
                            revertingChange = false;
                        }));
                });
            });

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ConnectionProxyCaption,
                    HintText = ForkSettingsStrings.ConnectionProxyHint,
                    Current = useProxy
                })
                {
                    Keywords = new[] { @"proxy", @"connection", @"Russia", @"RF", @"резервный", @"прокси", @"соединение" }
                }
            };
        }

        private void restartGame()
        {
            if (game == null)
                return;

            game.RestartOnExitAction = () =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = false,
                });
            };
            game.Exit();
        }
    }
}
