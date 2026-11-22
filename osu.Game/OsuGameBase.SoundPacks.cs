using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game
{
    public partial class OsuGameBase
    {
        public async Task ImportSoundPack(Stream source, string name)
        {
            var manager = interfaceSounds ?? throw new InvalidOperationException("Sound manager is not loaded.");
            var pack = await Task.Run(() => manager.Library.Import(source, name)).ConfigureAwait(false);
            var selected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Schedule(() =>
            {
                try
                {
                    manager.Select(pack.Id);
                    BeatmapManager?.PostNotification?.Invoke(new SimpleNotification { Text = InterfaceSoundStrings.Imported });
                    selected.SetResult();
                }
                catch (Exception ex) { selected.SetException(ex); }
            });
            await selected.Task.ConfigureAwait(false);
            await manager.Ready.ConfigureAwait(false);
        }

        private sealed class SoundImporter : ICanAcceptFiles
        {
            private readonly OsuGameBase game;
            public SoundImporter(OsuGameBase game) => this.game = game;
            public IEnumerable<string> HandledExtensions => new[] { InterfaceSoundLibrary.Extension };
            public Task Import(params string[] paths) => Import(paths.Select(p => new ImportTask(p)).ToArray());
            public async Task Import(ImportTask[] tasks, ImportParameters parameters = default)
            {
                foreach (var task in tasks)
                {
                    try
                    {
                        using var stream = task.Stream ?? File.OpenRead(task.Path);
                        await game.ImportSoundPack(stream, Path.GetFileNameWithoutExtension(task.Path)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        osu.Framework.Logging.Logger.Error(ex, "Could not import .morafx package.");
                        game.Schedule(() => game.BeatmapManager?.PostNotification?.Invoke(new SimpleErrorNotification { Text = InterfaceSoundStrings.ImportFailed }));
                    }
                }
            }
        }
    }
}
