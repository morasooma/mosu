// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps.Formats
{
    /// <summary>
    /// Provides a sidecar format for rulesets whose objects cannot be represented in a legacy .osu file.
    /// </summary>
    public interface ICustomBeatmapFormat
    {
        void EncodeCompatibilityBeatmap(IBeatmap beatmap, ISkin? skin, Storyboard? storyboard, Stream stream);

        /// <summary>
        /// Encodes all ruleset-specific state. Implementations may include editor
        /// resources such as storyboard or map-skin metadata when required.
        /// </summary>
        void EncodeSidecar(IBeatmap beatmap, ISkin? skin, Storyboard? storyboard, Stream stream);

        IBeatmap DecodeSidecar(IBeatmap compatibilityBeatmap, Stream stream);

        /// <summary>
        /// Copies ruleset-specific difficulty state when the core replaces a beatmap's
        /// <see cref="BeatmapInfo"/> while saving it.
        /// </summary>
        /// <remarks>
        /// Standard difficulty fields are copied by <see cref="BeatmapDifficulty.CopyTo"/>,
        /// but rulesets may keep additional settings outside that type.
        /// </remarks>
        void CopyCustomDifficultySettings(BeatmapDifficulty source, BeatmapDifficulty target)
        {
        }
    }

    public static class CustomBeatmapFormat
    {
        public const string SIDECAR_EXTENSION = ".ruleset.json";

        public static string GetSidecarFilename(string beatmapFilename)
            => Path.ChangeExtension(beatmapFilename, SIDECAR_EXTENSION);
    }
}
