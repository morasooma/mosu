using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Overlays.CustomTheme;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Overlays.Dialog
{
    public partial class CrashReportDialog : PopupDialog
    {
        private readonly CrashReportStore store;
        private readonly CrashReport report;
        private bool exporting;
        private RoundedButton exportButton = null!;
        private OsuTextFlowContainer status = null!;

        public override float PreferredWidth => 850;
        protected override bool CompactAppearance => true;
        protected override bool HorizontalButtons => true;

        public CrashReportDialog(CrashReportStore store, CrashReport report)
        {
            this.store = store;
            this.report = report;
            Icon = FontAwesome.Solid.ExclamationTriangle;
            HeaderText = CrashReportStrings.Header;
            BodyText = CrashReportStrings.Body;
        }

        [BackgroundDependencyLoader]
        private void load(Clipboard clipboard, Storage storage, OsuGame game)
        {
            var colours = new OverlayColourProvider(OverlayColourScheme.Purple);
            build(clipboard, storage, game, colours);
        }

        private void build(Clipboard clipboard, Storage storage, OsuGame game, OverlayColourProvider colours)
        {
            MainContent.Padding = new MarginPadding { Horizontal = 25 };
            MainContent.Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    new OsuTextFlowContainer(t => t.Font = t.Font.With(size: 16))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = $"{report.Timestamp.ToLocalTime():g} · {report.Version}",
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 170,
                        Masking = true,
                        CornerRadius = 8,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                            new BasicScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding(12),
                                Child = new OsuTextFlowContainer(t => t.Font = t.Font.With(size: 15))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = report.Exception,
                                },
                            },
                        },
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Children = new Drawable[]
                        {
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Width = 0.48f, Height = 40,
                                Text = CrashReportStrings.Copy,
                                Action = () =>
                                {
                                    try
                                    {
                                        clipboard.SetText(report.ToText());
                                        status.Text = CrashReportStrings.Copied;
                                    }
                                    catch (Exception)
                                    {
                                        status.Text = CrashReportStrings.CopyFailed;
                                    }
                                },
                            },
                            exportButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Width = 0.48f, Height = 40,
                                Text = CrashReportStrings.Export,
                                Action = () => export(storage),
                            },
                        },
                    },
                    status = new OsuTextFlowContainer(t => t.Font = t.Font.With(size: 16))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = CrashReportStrings.ExportHint,
                    },
                    new OsuTextFlowContainer(t => t.Font = t.Font.With(size: 18))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = CrashReportStrings.DiscordHint,
                    },
                },
            };
            Buttons = new PopupDialogButton[]
            {
                new CrashBackButton { Text = CrashReportStrings.Back },
                new PopupDialogButton
                {
                    Text = CrashReportStrings.Discord,
                    ButtonColour = colours.Colour3,
                    HideDialogOnAction = false,
                    Action = () => game.OpenUrlExternally("https://discord.gg/bRSDssrgUE"),
                },
            };
            State.BindValueChanged(e =>
            {
                if (e.NewValue == Visibility.Hidden)
                    store.Acknowledge(report);
            });
        }

        private partial class CrashBackButton : PopupDialogCancelButton
        {
            protected override void LoadComplete()
            {
                base.LoadComplete();
                ButtonColour = OverlayColourProvider.GetEffectiveContextColour(
                    ThemeSurfaceContext.FooterBack, SurfaceColourRole.ControlIdle,
                    Color4Extensions.FromHex("#DE31AE"));
            }
        }

        private async void export(Storage storage)
        {
            if (exporting) return;
            exporting = true;
            exportButton.Enabled.Value = false;
            status.Text = CrashReportStrings.Exporting;
            try
            {
                string path = await Task.Run(() => store.Export(report)).ConfigureAwait(false);
                Schedule(() =>
                {
                    status.Text = CrashReportStrings.Exported;
                    try
                    {
                        storage.GetStorageForDirectory("exports").PresentFileExternally(Path.GetFileName(path));
                    }
                    catch (Exception)
                    {
                        // The ZIP was saved even if the platform cannot launch a file manager.
                    }
                });
            }
            catch (Exception)
            {
                Schedule(() => status.Text = CrashReportStrings.ExportFailed);
            }
            finally
            {
                Schedule(() =>
                {
                    exporting = false;
                    exportButton.Enabled.Value = true;
                });
            }
        }
    }
}
