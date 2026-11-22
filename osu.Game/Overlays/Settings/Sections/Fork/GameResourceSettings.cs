using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>Selection and export only. Import uses the normal file drop/open pipeline.</summary>
    public partial class GameResourceSettings : FillFlowContainer
    {
        [Resolved(canBeNull: true)] private OsuGameBase? game { get; set; }
        private IBindable<bool>? busy;
        private IBindable<int>? changed;
        private ResourceDropdown packs = null!;
        private SettingsButtonV2 export = null!;

        public GameResourceSettings()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new osuTK.Vector2(0, 5);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            if (game == null || game.ResourcePacks == null) return;
            packs = new ResourceDropdown(game)
            {
                Caption = GameResourceStrings.Pack,
                HintText = GameResourceStrings.Hint,
            };
            Add(new SettingsItemV2(packs)
            {
                FavouriteId = "OsuSetting.GameResourcePack",
                Keywords = new[] { "resources", "pack", "morars", "textures", "sounds", "fonts", "ресурсы", "архив", "звуки", "шрифты" },
            });
            Add(export = new SettingsButtonV2
            {
                Text = GameResourceStrings.Export,
                TooltipText = GameResourceStrings.ExportHint,
                Action = () => _ = game.ExportResourcePack(),
            });
            changed = game.ResourcePacksChanged.GetBoundCopy();
            changed.BindValueChanged(_ => Schedule(() => { if (!IsDisposed) packs.Refresh(); }));
            busy = game.ResourcePackBusy.GetBoundCopy();
            busy.BindValueChanged(e =>
            {
                export.Enabled.Value = !e.NewValue;
                packs.Current.Disabled = e.NewValue;
            }, true);
            packs.Refresh();
        }

        protected override void Dispose(bool isDisposing)
        {
            busy?.UnbindAll();
            changed?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private partial class ResourceDropdown : FormDropdown<string>
        {
            private readonly OsuGameBase game;
            private GameResourcePackLibrary.InstalledPack[] installed = Array.Empty<GameResourcePackLibrary.InstalledPack>();
            private bool refreshing;
            public ResourceDropdown(OsuGameBase game)
            {
                this.game = game;
                Current.Default = GameResourcePackLibrary.DefaultPack;
                Current.BindValueChanged(e => { if (!refreshing) game.SelectResourcePack(e.NewValue); });
            }
            public void Refresh()
            {
                refreshing = true;
                bool disabled = Current.Disabled;
                Current.Disabled = false;
                try
                {
                    installed = game.ResourcePacks.Installed().ToArray();
                    Items = new[] { GameResourcePackLibrary.DefaultPack }.Concat(installed.Select(p => p.Id));
                    string selected = game.SelectedResourcePack;
                    Current.Value = Items.Contains(selected) ? selected : GameResourcePackLibrary.DefaultPack;
                }
                finally { Current.Disabled = disabled; refreshing = false; }
            }
            protected override LocalisableString GenerateItemText(string item) => item == GameResourcePackLibrary.DefaultPack
                ? GameResourceStrings.Default : installed.FirstOrDefault(p => p.Id == item)?.Name + " · " + item[..6];
        }
    }
}
