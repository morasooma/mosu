using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Updater;
using osu.Game.Database;
using osu.Game.Online;
using osuTK;
using osuTK.Graphics;
using Velopack;
using Velopack.Sources;

namespace osu.Desktop.Updater
{
    // This standalone Game uses framework fonts; OsuSpriteText requires the full osu font setup.
#pragma warning disable RS0030
    /// <summary>
    /// A standalone recovery UI: deliberately not an OsuGameBase, so no game database,
    /// account, beatmap manager or automatic database recovery is loaded.
    /// </summary>
    public partial class DatabaseUpdateGame : StartupGame
    {
        private readonly DatabaseCompatibilityException failure;
        private readonly Storage dataStorage;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private SpriteText status = null!;
        private StartupButton checkButton = null!;
        private StartupButton installButton = null!;
        private Velopack.UpdateManager? updater;
        private UpdateInfo? readyUpdate;
        private Action? applyOnExit;
        private int checking;

        public DatabaseUpdateGame(DatabaseCompatibilityException failure, Storage dataStorage)
        {
            this.failure = failure;
            this.dataStorage = dataStorage;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 18),
                Children = new Drawable[]
                {
                    new SpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5380, Font = new FontUsage(size: 28), Colour = Color4.White },
                    new SpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5381 },
                    new SpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5382 },
                    new SpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5383 },
                    status = new SpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5384, Font = new FontUsage(size: 18) },
                    checkButton = new StartupButton
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J5385, Size = new Vector2(340, 45),
                        Action = () => _ = checkAsync(),
                    },
                    installButton = new StartupButton
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J5386, Size = new Vector2(340, 45), Enabled = { Value = false },
                        Action = install,
                    },
                    new StartupButton
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J5387, Size = new Vector2(340, 45),
                        Action = () => Host.OpenUrlExternally(MosuServerEnvironment.PublicServerUrl),
                    },
                    new StartupButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J5388, Size = new Vector2(340, 45), Action = () => Host.Exit() },
                },
            });

            Logger.Error(failure, "Game startup blocked; database preserved.");
            _ = checkAsync();
        }

        private async Task checkAsync()
        {
            if (Interlocked.Exchange(ref checking, 1) != 0)
                return;

            Schedule(() => checkButton.Enabled.Value = false);
            try
            {
                if (OsuGameDesktop.IsManualMacOSBundle)
                {
                    setStatus("Replace Morasooma.app with a newer macOS ZIP build. Your library is unchanged.");
                    return;
                }

                if (OsuGameDesktop.IsPackageManaged)
                {
                    setStatus("Update Morasooma using your package manager. Your library is unchanged.");
                    return;
                }

                const string channel = "stable";
                string feed = MosuServerEnvironment.PublicServerUrl + MosuServerEnvironment.UpdateFeedPath;
                IUpdateSource source = StableMirrorUpdateSource.Create(feed);
                updater = new Velopack.UpdateManager(source,
                    new UpdateOptions { ExplicitChannel = channel, AllowVersionDowngrade = false });
                if (!updater.IsInstalled)
                {
                    setStatus("Automatic updates are unavailable for this build. Please download the latest client.");
                    return;
                }

                setStatus("Checking for updates…");
                UpdateInfo? update = await updater.CheckForUpdatesAsync().ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();
                if (update == null)
                {
                    setStatus("No updates found. A compatible version may not have been released yet.");
                    return;
                }

                setStatus($"Update {update.TargetFullRelease.Version} found. Downloading…");
                await updater.DownloadUpdatesAsync(update, p => setStatus($"Downloading update: {p}%"), cancellation.Token).ConfigureAwait(false);
                cancellation.Token.ThrowIfCancellationRequested();
                readyUpdate = update;
                Schedule(() =>
                {
                    status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J5395;
                    installButton.Enabled.Value = true;
                });
            }
            catch (OperationCanceledException)
            {
                // Window closed; never touch the database on cancellation.
            }
            catch (Exception e)
            {
                Logger.Error(e, "Database recovery update check failed.");
                setStatus("Update failed. Check your connection. Dev Build requires a saved login and active supporter status.");
            }
            finally
            {
                Interlocked.Exchange(ref checking, 0);
                Schedule(() => checkButton.Enabled.Value = readyUpdate == null);
            }
        }

        // The protected feed checks active/supporter/restriction status server-side for
        // every manifest and package request, just like the in-game Dev Build updater.

        private void setStatus(string text) => Schedule(() => status.Text = text);

        private void install()
        {
            if (updater == null || readyUpdate == null)
                return;

            var manager = updater;
            var release = readyUpdate.TargetFullRelease;
            applyOnExit = () => manager.WaitExitThenApplyUpdates(release);
            Host.Exit();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
            applyOnExit?.Invoke();
        }
    }
}
