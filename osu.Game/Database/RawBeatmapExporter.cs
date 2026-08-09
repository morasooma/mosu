// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;
using osu.Game.Beatmaps;

namespace osu.Game.Database
{
    /// <summary>
    /// Exporter that packs the original raw files of a beatmap set into a standard .osz package.
    /// This preserves backgrounds, storyboards, audio, and beatmap IDs, and runs extremely fast.
    /// </summary>
    public class RawBeatmapExporter : LegacyArchiveExporter<BeatmapSetInfo>
    {
        public RawBeatmapExporter(Storage storage)
            : base(storage)
        {
        }

        protected override string FileExtension => @".osz";
    }
}
