using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class UnsupportedFontDialog : PopupDialog
    {
        public UnsupportedFontDialog(Action revertAction)
        {
            HeaderText = ForkSettingsStrings.UnsupportedFontHeader;
            BodyText = ForkSettingsStrings.UnsupportedFontBody;
            Icon = FontAwesome.Solid.ExclamationTriangle;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = ForkSettingsStrings.UnsupportedFontGuideButton,
                    Action = () =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = $"{MosuServerEnvironment.PublicServerUrl}/news/shrift",
                                UseShellExecute = true
                            });
                        }
                        catch { }
                        
                        revertAction?.Invoke();
                    }
                },
                new PopupDialogCancelButton
                {
                    Text = ForkSettingsStrings.UnsupportedFontCancelButton,
                    Action = () => revertAction?.Invoke()
                }
            };
        }
    }
}
