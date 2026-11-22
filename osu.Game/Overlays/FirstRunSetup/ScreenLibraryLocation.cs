using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Localisation;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(ScreenLibraryLocation), nameof(Title))]
    public partial class ScreenLibraryLocation : WizardScreen
    {
        public static LocalisableString Title => LibraryLocationStrings.Title;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private OsuTextBox pathInput = null!;
        private OsuDirectorySelector selector = null!;
        private OsuTextFlowContainer status = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                paragraph(LibraryLocationStrings.Current + "\n" + storage.GetFullPath(".")),
                button(LibraryLocationStrings.Separate, () => status.Text = LibraryLocationStrings.Kept),
                paragraph(LibraryLocationStrings.SeparateHint),
                button(LibraryLocationStrings.Lazer, useLazer),
                paragraph(LibraryLocationStrings.LazerHint),
                paragraph(LibraryLocationStrings.Other),
                pathInput = new OsuTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    PlaceholderText = LibraryLocationStrings.Path,
                },
                button(LibraryLocationStrings.Browse, () =>
                {
                    bool show = selector.Alpha == 0;
                    selector.Alpha = show ? 1 : 0;
                    selector.Height = show ? 250 : 0;
                }),
                selector = new OsuDirectorySelector
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    Alpha = 0,
                },
                button(LibraryLocationStrings.UseFolder, () => select(pathInput.Text)),
                status = paragraph(LibraryLocationStrings.Hint),
            };
            // The device list has no directory, so it must also clear any previously selected path.
            selector.CurrentPath.BindValueChanged(e => pathInput.Text = e.NewValue?.FullName ?? string.Empty);
        }

        private static OsuTextFlowContainer paragraph(LocalisableString text) => new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
        {
            Text = text,
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
        };

        private static RoundedButton button(LocalisableString text, Action action) => new RoundedButton
        {
            Text = text,
            RelativeSizeAxes = Axes.X,
            Height = 45,
            Action = action,
        };

        private void useLazer()
        {
            foreach (string root in host.UserStoragePaths)
            {
                string candidate = Path.Combine(root, "osu");
                if (!Directory.Exists(candidate))
                    continue;
                var lazerStorage = new OsuStorage(host, host.GetStorage(candidate));
                string path = lazerStorage.GetFullPath(".");
                if (!File.Exists(Path.Combine(path, "client.realm")))
                    continue;
                select(path);
                return;
            }
            status.Text = LibraryLocationStrings.NotFound;
        }

        public override LocalisableString? NextStepText => LibraryLocationStrings.Continue;

        private void select(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path.Trim()))
                    throw new InvalidOperationException(LibraryLocationStrings.InvalidPath.ToString());
                path = Path.GetFullPath(path.Trim());
                if (LibraryLocationValidator.IsInsideStableInstallation(path))
                    throw new InvalidOperationException(LibraryLocationStrings.StableFolderNotSupported.ToString());
                var target = host.GetStorage(path);
                string probe = $".mosu-write-test-{Guid.NewGuid():N}";
                using (target.CreateFileSafely(probe))
                {
                }
                target.Delete(probe);
                if (storage is not OsuStorage osuStorage)
                    throw new InvalidOperationException(LibraryLocationStrings.Unavailable.ToString());
                if (!game.RestartAppWhenExited())
                    throw new InvalidOperationException(LibraryLocationStrings.RestartUnavailable.ToString());
                osuStorage.ChangeDataPath(path);
                game.AttemptExit();
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        }
    }
}
