using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    /// <summary>
    /// The same persisted GPU choice is shown in Graphics and Morasooma performance settings.
    /// Changing it never restarts the game until the user presses Apply.
    /// </summary>
    public partial class GraphicsAdapterSettings : FillFlowContainer
    {
        private Bindable<string> preferred = null!;
        private Bindable<string> displayed = null!;
        private string startupPreference = string.Empty;

        public GraphicsAdapterSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);
        }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config, GameHost host, OsuGame? game, IDialogOverlay? dialogs)
        {
            bool supported = host.SupportsGraphicsAdapterSelection;
            preferred = config.GetBindable<string>(FrameworkSetting.GraphicsAdapter);
            startupPreference = host.AppliedGraphicsAdapterPreference;
            displayed = supported ? preferred.GetBoundCopy() : new Bindable<string> { Disabled = true };

            var names = host.AvailableGraphicsAdapters.ToDictionary(a => a.Id, a => a.Name);
            var items = new List<string> { string.Empty };
            if (supported)
            {
                items.AddRange(names.Keys);
                if (!items.Contains(preferred.Value))
                    items.Add(preferred.Value);
            }

            var item = new SettingsItemV2(new AdapterDropdown(names)
            {
                Caption = GraphicsAdapterStrings.Caption,
                HintText = supported ? GraphicsAdapterStrings.RestartHint : GraphicsAdapterStrings.UnsupportedHint,
                Items = items,
                Current = displayed,
            })
            {
                FavouriteId = "ui:GraphicsAdapterStrings.Caption",
                Keywords = new[] { "gpu", "graphics", "adapter", "video card", "видеокарта", "графика", "рендер" },
            };
            item.Note.Value = new SettingsNote.Data(GraphicsAdapterStrings.Active(
                string.IsNullOrEmpty(host.GraphicsAdapterName) ? GraphicsAdapterStrings.Unknown : host.GraphicsAdapterName), SettingsNote.Type.Informational);
            Add(item);

            if (supported)
            {
                var restart = new SettingsButtonV2
                {
                    Text = GraphicsAdapterStrings.Apply,
                    Action = () =>
                    {
                        if (game?.RestartAppWhenExited() == true)
                            game.AttemptExit();
                        else
                            dialogs?.Push(new ConfirmDialog(GraphicsAdapterStrings.ManualRestart, () => game?.AttemptExit()));
                    },
                };
                Add(restart);
                preferred.BindValueChanged(v => restart.Enabled.Value = v.NewValue != startupPreference, true);
            }
            else if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Add(new SettingsButtonV2
                {
                    Text = GraphicsAdapterStrings.WindowsSettings,
                    Action = () => host.OpenFileExternally("ms-settings:display-advancedgraphics"),
                    Keywords = new[] { "gpu", "opengl", "видеокарта" },
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            preferred?.UnbindAll();
            displayed?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private partial class AdapterDropdown : FormDropdown<string>
        {
            private readonly IReadOnlyDictionary<string, string> names;

            public AdapterDropdown(IReadOnlyDictionary<string, string> names) => this.names = names;

            protected override LocalisableString GenerateItemText(string item)
                => string.IsNullOrEmpty(item) ? GraphicsAdapterStrings.Automatic : names.GetValueOrDefault(item, item);
        }
    }
}
