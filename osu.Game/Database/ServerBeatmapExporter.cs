using osu.Framework.Platform;

namespace osu.Game.Database
{
    /// <summary>
    /// Uses legacy beatmap contents, but Unicode ZIP names for Mosu uploads.
    /// Shift-JIS cannot represent all filenames referenced by UTF-8 .osu files.
    /// </summary>
    public class ServerBeatmapExporter : LegacyBeatmapExporter
    {
        public ServerBeatmapExporter(Storage storage)
            : base(storage)
        {
        }

        protected override bool UseFixedEncoding => false;
    }
}
