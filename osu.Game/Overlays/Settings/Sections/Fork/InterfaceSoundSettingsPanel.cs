using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class InterfaceSoundSettingsPanel : SettingsSubPanel
    {
        private InterfaceSoundPackDropdown packs = null!;
        private SoundActionButton resetAll = null!;
        private readonly List<InterfaceSoundRow> rows = new List<InterfaceSoundRow>();
        private IBindable<int> changed = null!;
        private SampleChannel? previewChannel;
        internal SampleChannel? PreviewChannel => previewChannel;
        private bool busy;
        private int previewRequest;

        [Resolved]
        private InterfaceSoundManager manager { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private NotificationOverlay? notifications { get; set; }

        protected override Drawable CreateHeader() => new SettingsHeader(InterfaceSoundStrings.Header, InterfaceSoundStrings.ScopeHint);

        [BackgroundDependencyLoader]
        private void load()
        {
            AddSection(new SoundSection(InterfaceSoundStrings.Pack, FontAwesome.Solid.VolumeUp,
                new SettingsItemV2(packs = new InterfaceSoundPackDropdown(manager)
                {
                    Caption = InterfaceSoundStrings.Pack,
                    HintText = InterfaceSoundStrings.Hint,
                }) { ShowRevertToDefaultButton = false },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 44,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        resetAll = new SoundActionButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Width = 1,
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Text = InterfaceSoundStrings.ResetAllShort,
                            TooltipText = InterfaceSoundStrings.ResetAll,
                            Height = 36,
                            Action = () => run(() =>
                            {
                                manager.Library.ResetAll(manager.SelectedId);
                                return manager.SelectedId;
                            }),
                        },
                    },
                }));

            foreach (var category in manager.Events.GroupBy(p => p.Split('/')[0]).OrderBy(g => categoryOrder(g.Key)))
            {
                var categoryRows = category.OrderBy(eventOrder).ThenBy(key => key, StringComparer.Ordinal)
                                           .Select(key => new InterfaceSoundRow(key, preview, replace, reset)).ToArray();
                rows.AddRange(categoryRows);
                AddSection(new SoundSection(InterfaceSoundStrings.Category(category.Key), categoryIcon(category.Key), categoryRows));
            }

            changed = manager.Changed.GetBoundCopy();
            changed.BindValueChanged(_ => Schedule(refresh));
            State.BindValueChanged(e =>
            {
                if (e.NewValue != Visibility.Visible)
                    stopPreview();
            });
            refresh();
        }

        private static int eventOrder(string key) => key switch
        {
            "UI/default-select" => 0, "UI/default-hover" => 1, "UI/check-on" => 2, "UI/check-off" => 3,
            "UI/screen-back" => 4, "UI/notch-tick" => 5, "UI/dropdown-open" => 6, "UI/dropdown-close" => 7,
            _ => 8,
        };

        private static int categoryOrder(string category) => category switch
        {
            "UI" => 0, "Menu" => 1, "SongSelect" => 2, "SongSelectClassic" => 3,
            "Keyboard" => 4, "Intro" => 5, "Results" => 6, "Multiplayer" => 7,
            "DailyChallenge" => 8, _ => 9,
        };

        private static IconUsage categoryIcon(string category) => category switch
        {
            "Keyboard" => FontAwesome.Solid.Keyboard,
            "SongSelect" or "SongSelectClassic" => FontAwesome.Solid.Music,
            "Multiplayer" => FontAwesome.Solid.Users,
            "Results" or "MedalSplash" => FontAwesome.Solid.Trophy,
            _ => FontAwesome.Solid.VolumeUp,
        };

        private void replace(string key, FileInfo file)
        {
            string id = manager.SelectedId;
            run(() =>
            {
                using var stream = file.OpenRead();
                return manager.Library.Replace(id, key, file.Name, stream).Id;
            });
        }

        private void reset(string key)
        {
            string id = manager.SelectedId;
            run(() =>
            {
                manager.Library.Reset(id, key);
                return id;
            });
        }

        private void run(Func<string> operation)
        {
            if (busy)
                return;
            busy = true;
            stopPreview();
            refresh();
            _ = perform(operation);
        }

        private async Task perform(Func<string> operation)
        {
            try
            {
                string id = await Task.Run(operation).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (manager.SelectedId == id)
                        manager.Reload();
                    else
                        manager.Select(id);
                });
            }
            catch (Exception)
            {
                Schedule(() => notifications?.Post(new SimpleErrorNotification { Text = InterfaceSoundStrings.ImportFailed }));
            }
            finally
            {
                Schedule(() =>
                {
                    busy = false;
                    packs.Refresh();
                    refresh();
                });
            }
        }

        private void refresh()
        {
            stopPreview();
            var pack = manager.Library.Find(manager.SelectedId);
            foreach (var row in rows)
                row.Refresh(pack?.Sounds.ContainsKey(row.EventKey) == true, busy);
            packs.Current.Disabled = busy;
            resetAll.Enabled.Value = !busy && pack?.Sounds.Count > 0;
        }

        private void preview(string key)
        {
            stopPreview();
            int request = previewRequest;
            _ = preparePreview(key, request);
        }

        private async Task preparePreview(string key, int request)
        {
            Sample? sample = await Task.Run(() => audio.Samples.Get(key)).ConfigureAwait(false);
            Schedule(() =>
            {
                if (request != previewRequest || State.Value != Visibility.Visible || busy)
                {
                    sample?.Dispose();
                    return;
                }
                previewChannel = sample?.Play();
            });
        }

        private void stopPreview()
        {
            previewRequest++;
            previewChannel?.Stop();
            previewChannel = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            stopPreview();
            changed?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private partial class SoundSection : SettingsSection
        {
            public override LocalisableString Header { get; }
            private readonly IconUsage icon;
            public override Drawable CreateIcon() => new SpriteIcon { Icon = icon };

            public SoundSection(LocalisableString header, IconUsage icon, params Drawable[] children)
            {
                Header = header;
                this.icon = icon;
                Children = children;
            }
        }
    }

    /// <summary>One event per row; no nested sound-producing click targets.</summary>
    public partial class InterfaceSoundRow : CompositeDrawable, IFilterable, IHasTooltip
    {
        public string EventKey { get; }
        public LocalisableString TooltipText => EventKey;
        public SoundActionButton PreviewButton { get; }
        public SoundFileButton ReplaceButton { get; }
        public SoundActionButton ResetButton { get; }
        private readonly OsuSpriteText status;
        private OverlayColourProvider colours = null!;
        private IBindable<Colour4>? theme;
        private bool overridden;

        public IEnumerable<LocalisableString> FilterTerms => new[] { InterfaceSoundStrings.EventLabel(EventKey), EventKey };
        public bool MatchingFilter { set => Alpha = value ? 1 : 0; }
        public bool FilteringActive { get; set; }

        public InterfaceSoundRow(string key, Action<string> preview, Action<string, FileInfo> replace, Action<string> reset)
        {
            EventKey = key;
            RelativeSizeAxes = Axes.X;
            Height = 68;
            Padding = new MarginPadding { Left = SettingsPanel.CONTENT_PADDING.Left, Right = SettingsPanel.CONTENT_PADDING.Right, Vertical = 3 };
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Right = 128 },
                    Children = new Drawable[]
                    {
                        new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Y = -9,
                            Text = InterfaceSoundStrings.EventLabel(key),
                            MaximumSize = new Vector2(float.MaxValue, 40),
                        },
                        status = new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Y = 17,
                            Font = OsuFont.GetFont(size: 11),
                        },
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Children = new Drawable[]
                    {
                        PreviewButton = new SoundActionButton
                        {
                            Size = new Vector2(36),
                            Icon = FontAwesome.Solid.Play,
                            TooltipText = InterfaceSoundStrings.Preview,
                            Action = () => preview(key),
                        },
                        ReplaceButton = new SoundFileButton(new[] { ".wav", ".mp3" }, file => replace(key, file))
                        {
                            Size = new Vector2(36),
                            Icon = FontAwesome.Solid.FolderOpen,
                            TooltipText = InterfaceSoundStrings.Replace,
                        },
                        ResetButton = new SoundActionButton
                        {
                            Size = new Vector2(36),
                            Icon = FontAwesome.Solid.Undo,
                            TooltipText = InterfaceSoundStrings.Reset,
                            Action = () => reset(key),
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            this.colours = colours;
            theme = colours.GetColourBindable(OverlayColour.Content2);
            theme.BindValueChanged(_ => updateStatus(), true);
        }

        public void Refresh(bool hasOverride, bool busy)
        {
            overridden = hasOverride;
            PreviewButton.Enabled.Value = !busy;
            ReplaceButton.Enabled.Value = !busy;
            ResetButton.Enabled.Value = !busy && hasOverride;
            updateStatus();
        }

        private void updateStatus()
        {
            status.Text = overridden ? InterfaceSoundStrings.CustomSound : InterfaceSoundStrings.BaseSound;
            if (colours != null)
                status.Colour = overridden ? colours.Content1 : colours.Content2;
        }
    }

    /// <summary>Uses the client's button visuals, but installs no hover or click samples.</summary>
    public partial class SoundActionButton : OsuButton, IHasTooltip
    {
        public SoundActionButton() : base(hoverSounds: null) { }
        public LocalisableString TooltipText { get; set; }
        public IconUsage? Icon { get; init; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            DefaultBackgroundColour = colours.Colour3;
            SpriteText.Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold);
            if (Icon is IconUsage icon)
            {
                SpriteText.Hide();
                Add(new SpriteIcon { Icon = icon, Size = new Vector2(14), Anchor = Anchor.Centre, Origin = Anchor.Centre });
            }
        }
    }

    public partial class SoundFileButton : SoundActionButton, IHasPopover
    {
        public readonly Bindable<FileInfo?> SelectedFile = new Bindable<FileInfo?>();
        private readonly string[] extensions;
        private string? directory;

        public SoundFileButton(string[] extensions, Action<FileInfo> selected)
        {
            this.extensions = extensions;
            Action = this.ShowPopover;
            SelectedFile.BindValueChanged(e =>
            {
                if (e.NewValue == null)
                    return;
                this.HidePopover();
                directory = e.NewValue.DirectoryName;
                selected(e.NewValue);
                SelectedFile.Value = null;
            });
        }

        public Popover GetPopover() => new FormFileSelector.FileChooserPopover(extensions, SelectedFile, directory, allowClear: false);
    }
}
