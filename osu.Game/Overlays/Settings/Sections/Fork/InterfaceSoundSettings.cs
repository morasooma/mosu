using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>Inline companion to FontSettingsPanel, using the same settings controls.</summary>
    public partial class SoundSettingsPanel : FillFlowContainer
    {
        private readonly InterfaceSoundSettingsPanel? editor;
        private readonly bool showEditor;
        private InterfaceSoundPackDropdown packs = null!;
        private SettingsButtonV2 import = null!;
        private SettingsButtonV2 export = null!;
        private bool busy;

        [Resolved(canBeNull: true)] private InterfaceSoundManager manager { get; set; } = null!;
        [Resolved] private Storage storage { get; set; } = null!;
        [Resolved] private GameHost host { get; set; } = null!;
        [Resolved(canBeNull: true)] private SettingsOverlay? settingsOverlay { get; set; }
        [Resolved(canBeNull: true)] private INotificationOverlay? notifications { get; set; }

        public SoundSettingsPanel(InterfaceSoundSettingsPanel? editor = null, bool showEditor = true)
        {
            this.editor = editor;
            this.showEditor = showEditor;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new osuTK.Vector2(0, 5);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Minimal standalone theme/font previews may not host audio at all.
            if (manager == null) return;
            Add(new SettingsItemV2(packs = new InterfaceSoundPackDropdown(manager)
            {
                Caption = InterfaceSoundStrings.Pack,
                HintText = InterfaceSoundStrings.ScopeHint,
            }) { FavouriteId = "OsuSetting.InterfaceSoundPack" });
            // Legacy test/compatibility control only; production settings use GameResourceSettings.
            import = new ImportSoundButton(file => run(async () =>
            {
                string id = await Task.Run(() =>
                {
                    using var stream = file.OpenRead();
                    return manager.Library.Import(stream, Path.GetFileNameWithoutExtension(file.Name)).Id;
                }).ConfigureAwait(false);
                schedule(() => manager.Select(id));
            })) { Text = InterfaceSoundStrings.Import, TooltipText = InterfaceSoundStrings.ImportHint };
            Add(import);
            Add(export = new SettingsButtonV2
            {
                Text = InterfaceSoundStrings.Export,
                Action = () => run(async () =>
                {
                    await manager.Export().ConfigureAwait(false);
                    schedule(() => storage.GetStorageForDirectory("exports").PresentExternally());
                }),
            });
            Add(new SettingsButtonV2 { Text = InterfaceSoundStrings.Folder, Action = manager.Library.PresentExternally });
            if (showEditor)
                Add(new SettingsButtonV2
                {
                    Text = InterfaceSoundStrings.Edit,
                    Keywords = new[] { "sounds", "interface", "morafx", "звуки", "интерфейс" },
                    Action = () =>
                    {
                        if (editor != null) editor.Show();
                        else settingsOverlay?.ShowSoundEditor();
                    },
                });
        }

        private void run(Func<Task> operation)
        {
            if (busy) return;
            busy = true;
            packs.Current.Disabled = true;
            import.Enabled.Value = export.Enabled.Value = false;
            _ = perform(operation);
        }

        private async Task perform(Func<Task> operation)
        {
            try { await operation().ConfigureAwait(false); }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, "Sound package operation failed.");
                schedule(() => notifications?.Post(new SimpleErrorNotification { Text = InterfaceSoundStrings.ImportFailed }));
            }
            finally
            {
                schedule(() =>
                {
                    busy = false;
                    packs.Current.Disabled = false;
                    packs.Refresh();
                    import.Enabled.Value = export.Enabled.Value = true;
                });
            }
        }

        // File choosers and scrolling can mask this panel while IO is in flight.
        // Complete model/UI changes on the host, not a culled drawable's scheduler.
        private void schedule(Action action) => host.UpdateThread.Scheduler.Add(() =>
        {
            if (!IsDisposed) action();
        });

        public partial class ImportSoundButton : SettingsButtonV2, IHasPopover
        {
            public readonly Bindable<FileInfo?> SelectedFile = new Bindable<FileInfo?>();
            public ImportSoundButton(Action<FileInfo> import)
            {
                Action = this.ShowPopover;
                SelectedFile.BindValueChanged(e =>
                {
                    if (e.NewValue == null) return;
                    this.HidePopover();
                    import(e.NewValue);
                    SelectedFile.Value = null;
                });
            }
            public Popover GetPopover() => new FormFileSelector.FileChooserPopover(new[] { InterfaceSoundLibrary.Extension }, SelectedFile, null, allowClear: false);
        }
    }

    public partial class InterfaceSoundPackDropdown : FormDropdown<string>
    {
        private readonly InterfaceSoundManager manager;
        private InterfaceSoundPack[] custom = Array.Empty<InterfaceSoundPack>();
        private IBindable<int>? changed;
        private bool refreshing;

        public InterfaceSoundPackDropdown(InterfaceSoundManager manager)
        {
            this.manager = manager;
            Current.Default = InterfaceSoundLibrary.DefaultPack;
            Current.BindValueChanged(e => { if (!refreshing) manager.Select(e.NewValue); });
            Refresh();
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            changed = manager.Changed.GetBoundCopy();
            changed.BindValueChanged(_ => host.UpdateThread.Scheduler.Add(() => { if (!IsDisposed) Refresh(); }));
            Refresh();
        }

        protected override void Dispose(bool isDisposing)
        {
            changed?.UnbindAll();
            base.Dispose(isDisposing);
        }

        public void Refresh()
        {
            refreshing = true;
            bool wasDisabled = Current.Disabled;
            Current.Disabled = false;
            try
            {
                custom = manager.Library.GetPacks().ToArray();
                Items = new[] { InterfaceSoundLibrary.DefaultPack }.Concat(custom.Select(p => p.Id));
                Current.Value = Items.Contains(manager.SelectedId) ? manager.SelectedId : InterfaceSoundLibrary.DefaultPack;
            }
            finally { Current.Disabled = wasDisabled; refreshing = false; }
        }

        protected override LocalisableString GenerateItemText(string item) => item == InterfaceSoundLibrary.DefaultPack
            ? InterfaceSoundStrings.Default : custom.FirstOrDefault(p => p.Id == item)?.Name ?? item;
    }
}
