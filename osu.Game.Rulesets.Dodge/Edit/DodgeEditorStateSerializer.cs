using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Timing;
using osu.Game.IO;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Editor history includes the common beatmap state as well as the Dodge sidecar.
    /// This envelope is local to undo/redo and does not change the exported map format.
    /// </summary>
    internal static class DodgeEditorStateSerializer
    {
        public static void Serialize(EditorBeatmap beatmap, Stream stream)
        {
            using var compatibility = new MemoryStream();
            DodgeBeatmapSerializer.EncodeCompatibilityBeatmap(beatmap, beatmap.BeatmapSkin, beatmap.Storyboard, compatibility);
            using var sidecar = new MemoryStream();
            DodgeBeatmapSerializer.Serialize(beatmap, beatmap.Storyboard, sidecar);

            JsonSerializer.Serialize(stream, new EditorState
            {
                Compatibility = compatibility.ToArray(),
                Sidecar = sidecar.ToArray(),
                Difficulty = new[]
                {
                    (double)beatmap.Difficulty.ApproachRate, beatmap.Difficulty.CircleSize,
                    beatmap.Difficulty.DrainRate, beatmap.Difficulty.OverallDifficulty,
                    beatmap.Difficulty.SliderMultiplier, beatmap.Difficulty.SliderTickRate,
                },
                Timing = beatmap.ControlPointInfo.TimingPoints.Select(point => new TimingData(
                    point.Time, point.BeatLength, point.TimeSignature.Numerator, point.OmitFirstBarLine)).ToArray(),
                Effects = beatmap.ControlPointInfo.EffectPoints.Select(point => new EffectData(
                    point.Time, point.KiaiMode, point.ScrollSpeed)).ToArray(),
                Breaks = beatmap.Breaks.Select(period => new BreakData(
                    period.StartTime, period.EndTime, period is ManualBreakPeriod)).ToArray(),
                // Sidecar decoding sorts by time, preserving type order for equal times.
                Samples = beatmap.HitObjects.OfType<DodgeHitObject>()
                                 .OrderBy(item => item.StartTime).ThenBy(typeOrder)
                                 .Select(item => item.Samples.ToArray().Serialize()).ToArray(),
                AudioFile = beatmap.Metadata.AudioFile,
                BackgroundFile = beatmap.Metadata.BackgroundFile,
                SpecialStyle = beatmap.SpecialStyle,
                BeatmapVersion = beatmap.BeatmapVersion,
            });
        }

        public static void Apply(EditorBeatmap beatmap, byte[] state)
        {
            var snapshot = JsonSerializer.Deserialize<EditorState>(state)
                           ?? throw new InvalidDataException("Empty Dodge editor state.");
            using var compatibility = new MemoryStream(snapshot.Compatibility);
            using var reader = new LineBufferedReader(compatibility);
            var common = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            beatmap.BeginChange();
            try
            {
                // Keep the ControlPointInfo instance: timeline and timing controls bind to it.
                var points = new ControlPointInfo();
                foreach (var point in snapshot.Timing)
                {
                    points.GroupAt(point.Time, true).Add(new TimingControlPoint
                    {
                        BeatLength = point.BeatLength,
                        TimeSignature = new TimeSignature(point.Signature),
                        OmitFirstBarLine = point.OmitFirstBarLine,
                    });
                }
                foreach (var point in snapshot.Effects)
                {
                    points.GroupAt(point.Time, true).Add(new EffectControlPoint
                    {
                        KiaiMode = point.Kiai,
                        ScrollSpeed = point.ScrollSpeed,
                    });
                }
                foreach (var oldGroup in beatmap.ControlPointInfo.Groups.ToArray())
                {
                    if (!oldGroup.Equals(points.GroupAt(oldGroup.Time)))
                        beatmap.ControlPointInfo.RemoveGroup(oldGroup);
                }
                foreach (var group in points.Groups)
                {
                    if (!group.Equals(beatmap.ControlPointInfo.GroupAt(group.Time)))
                    {
                        foreach (var point in group.ControlPoints.ToArray())
                            beatmap.ControlPointInfo.GroupAt(group.Time, true).Add(point);
                    }
                }

                var source = common.Metadata;
                var target = beatmap.Metadata;
                target.Title = source.Title;
                target.TitleUnicode = source.TitleUnicode;
                target.Artist = source.Artist;
                target.ArtistUnicode = source.ArtistUnicode;
                target.Author.Username = source.Author.Username;
                target.Source = source.Source;
                target.Tags = source.Tags;
                target.AudioFile = snapshot.AudioFile;
                target.BackgroundFile = snapshot.BackgroundFile;
                beatmap.PreviewTime.Value = source.PreviewTime;
                beatmap.BeatmapInfo.DifficultyName = common.BeatmapInfo.DifficultyName;
                beatmap.BeatmapInfo.BeatDivisor = common.BeatmapInfo.BeatDivisor;
                beatmap.AudioLeadIn = common.AudioLeadIn;
                beatmap.StackLeniency = common.StackLeniency;
                beatmap.SpecialStyle = snapshot.SpecialStyle;
                beatmap.LetterboxInBreaks = common.LetterboxInBreaks;
                beatmap.WidescreenStoryboard = common.WidescreenStoryboard;
                beatmap.EpilepsyWarning = common.EpilepsyWarning;
                beatmap.SamplesMatchPlaybackRate = common.SamplesMatchPlaybackRate;
                beatmap.DistanceSpacing = common.DistanceSpacing;
                beatmap.GridSize = common.GridSize;
                beatmap.TimelineZoom = common.TimelineZoom;
                beatmap.Countdown = common.Countdown;
                beatmap.CountdownOffset = common.CountdownOffset;
                beatmap.Bookmarks.Clear();
                beatmap.Bookmarks.AddRange(common.Bookmarks);
                beatmap.SliderVelocityPresets.Clear();
                beatmap.SliderVelocityPresets.AddRange(common.SliderVelocityPresets);

                DodgeBeatmapSerializer.ApplyToEditor(beatmap, snapshot.Sidecar);
                // Preserve exact difficulty values rather than rounded sidecar UI units.
                beatmap.Difficulty.ApproachRate = (float)snapshot.Difficulty[0];
                beatmap.Difficulty.CircleSize = (float)snapshot.Difficulty[1];
                beatmap.Difficulty.DrainRate = (float)snapshot.Difficulty[2];
                beatmap.Difficulty.OverallDifficulty = (float)snapshot.Difficulty[3];
                beatmap.Difficulty.SliderMultiplier = snapshot.Difficulty[4];
                beatmap.Difficulty.SliderTickRate = snapshot.Difficulty[5];
                for (int i = 0; i < beatmap.HitObjects.Count; i++)
                    beatmap.HitObjects[i].Samples = snapshot.Samples[i].Deserialize<osu.Game.Audio.HitSampleInfo[]>();
            }
            finally
            {
                beatmap.EndChange();
            }

            // Object reprocessing regenerates breaks and updates the format version.
            beatmap.Breaks.Clear();
            beatmap.Breaks.AddRange(snapshot.Breaks.Select(period => period.Manual
                ? new ManualBreakPeriod(period.Start, period.End)
                : new BreakPeriod(period.Start, period.End)));
            beatmap.BeatmapVersion = snapshot.BeatmapVersion;
        }

        private static int typeOrder(DodgeHitObject item) => item switch
        {
            DodgeBullet => 0,
            DodgeEmitter => 1,
            DodgeCameraChange => 2,
            DodgeArenaChange => 3,
            DodgeBeam => 4,
            DodgeTrigger => 5,
            _ => throw new ArgumentException($"Unsupported Dodge object: {item.GetType().Name}"),
        };

        private class EditorState
        {
            public byte[] Compatibility { get; set; } = Array.Empty<byte>();
            public byte[] Sidecar { get; set; } = Array.Empty<byte>();
            public double[] Difficulty { get; set; } = Array.Empty<double>();
            public TimingData[] Timing { get; set; } = Array.Empty<TimingData>();
            public EffectData[] Effects { get; set; } = Array.Empty<EffectData>();
            public BreakData[] Breaks { get; set; } = Array.Empty<BreakData>();
            public string[] Samples { get; set; } = Array.Empty<string>();
            public string AudioFile { get; set; } = string.Empty;
            public string BackgroundFile { get; set; } = string.Empty;
            public bool SpecialStyle { get; set; }
            public int BeatmapVersion { get; set; }
        }

        private record TimingData(double Time, double BeatLength, int Signature, bool OmitFirstBarLine);
        private record EffectData(double Time, bool Kiai, double ScrollSpeed);
        private record BreakData(double Start, double End, bool Manual);
    }
}
