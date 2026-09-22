// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.IO;
using osuTK;
using osu.Framework.Graphics;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    /// <summary>
    /// Serialises data which cannot be represented by the legacy .osu format.
    /// This is also used by editor undo/redo, so both paths share one object schema.
    /// </summary>
    public static class DodgeBeatmapSerializer
    {
        public const int CURRENT_VERSION = 20;
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        public static void Serialize(IBeatmap beatmap, Stream stream)
            => Serialize(beatmap, (beatmap as EditorBeatmap)?.Storyboard, stream);

        public static void Serialize(IBeatmap beatmap, Storyboard? storyboard, Stream stream)
        {
            var document = new DodgeBeatmapDocument
            {
                Format = "dodge",
                RulesetOnlineId = DodgeRuleset.ONLINE_ID,
                Version = CURRENT_VERSION,
                Settings = new DodgeMapSettingsData
                {
                    AppearanceDuration = Math.Round(DodgeBeatmapSettings.GetAppearanceDuration(beatmap.Difficulty), 3),
                    BulletSize = Math.Round(DodgeBeatmapSettings.GetBulletSize(beatmap.Difficulty), 3),
                    HpDrain = Math.Round(beatmap.Difficulty.DrainRate, 3),
                    PlayerSpeed = Math.Round(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty), 3),
                    PlayerSize = Math.Round(DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty), 3),
                    GrazeDistance = Math.Round(DodgeBeatmapSettings.GetGrazeDistance(beatmap.Difficulty), 3),
                    GrazeScore = Math.Round(DodgeBeatmapSettings.GetGrazeScore(beatmap.Difficulty), 3),
                    ForceStoryboard = DodgeBeatmapSettings.GetForceStoryboard(beatmap.Difficulty),
                    ForceBeatmapSkin = DodgeBeatmapSettings.GetForceBeatmapSkin(beatmap.Difficulty),
                },
                Storyboard = storyboard == null ? null : serializeStoryboard(storyboard),
                Bullets = beatmap.HitObjects.OfType<DodgeBullet>().Select(b => new DodgeBulletData
                {
                    StartTime = b.StartTime,
                    Duration = b.Duration,
                    StartX = b.Position.X,
                    StartY = b.Position.Y,
                    EndX = b.EndPosition.X,
                    EndY = b.EndPosition.Y,
                    Shape = b.Shape.ToString(),
                    ContinueUntilExit = b.ContinueUntilExit,
                    Colour = b.Colour.ToHex(),
                    OutlineColour = b.OutlineColour.ToHex(),
                    Opacity = b.Opacity,
                    OutlineThickness = b.OutlineThickness,
                    MovementType = b.MovementType.ToString(),
                    MovementEasing = b.MovementEasing.ToString(),
                    WaveAmplitude = b.WaveAmplitude,
                    WaveCycles = b.WaveCycles,
                    WavePhase = b.WavePhase,
                    TrajectoryGuideStyle = b.TrajectoryGuideStyle.ToString(),
                }).ToList(),
                Emitters = beatmap.HitObjects.OfType<DodgeEmitter>().Select(emitter => new DodgeEmitterData
                {
                    StartTime = emitter.StartTime,
                    Duration = emitter.Duration,
                    SourceX = emitter.Position.X,
                    SourceY = emitter.Position.Y,
                    AimX = emitter.AimPosition.X,
                    AimY = emitter.AimPosition.Y,
                    BulletCount = emitter.EffectiveBulletCount,
                    SpreadAngle = emitter.EffectiveSpreadAngle,
                    Shape = emitter.Shape.ToString(),
                    ContinueUntilExit = emitter.ContinueUntilExit,
                    MovementEndX = emitter.MoveSource ? emitter.MovementEndPosition.X : emitter.Position.X,
                    MovementEndY = emitter.MoveSource ? emitter.MovementEndPosition.Y : emitter.Position.Y,
                    MoveSource = emitter.MoveSource,
                    BurstCount = emitter.EffectiveBurstCount,
                    BurstInterval = emitter.EffectiveBurstInterval,
                    BurstBeatDivisor = emitter.BurstBeatDivisor,
                    BurstRotation = emitter.BurstRotation,
                    Colour = emitter.Colour.ToHex(),
                    OutlineColour = emitter.OutlineColour.ToHex(),
                    Opacity = emitter.Opacity,
                    OutlineThickness = emitter.OutlineThickness,
                    MovementType = emitter.MovementType.ToString(),
                    MovementEasing = emitter.MovementEasing.ToString(),
                    WaveAmplitude = emitter.WaveAmplitude,
                    WaveCycles = emitter.WaveCycles,
                    WavePhase = emitter.WavePhase,
                    TrajectoryGuideStyle = emitter.TrajectoryGuideStyle.ToString(),
                }).ToList(),
                CameraChanges = beatmap.HitObjects.OfType<DodgeCameraChange>().Select(change => new DodgeCameraChangeData
                {
                    StartTime = change.StartTime,
                    Duration = change.Duration,
                    StartX = change.Position.X,
                    StartY = change.Position.Y,
                    EndX = change.EndPosition.X,
                    EndY = change.EndPosition.Y,
                    Continuous = change.Continuous,
                    Easing = change.Easing.ToString(),
                }).ToList(),
                ArenaChanges = beatmap.HitObjects.OfType<DodgeArenaChange>().Select(change => new DodgeArenaChangeData
                {
                    StartTime = change.StartTime,
                    Duration = change.Duration,
                    TargetX = change.TargetPosition.X,
                    TargetY = change.TargetPosition.Y,
                    Width = change.TargetSize.X,
                    Height = change.TargetSize.Y,
                    Rotation = change.TargetRotation,
                    KiaiShakeAngle = change.KiaiShakeAngle,
                    BackgroundColour = change.Colour.ToHex(),
                    BackgroundOpacity = change.Opacity,
                    BorderColour = change.OutlineColour.ToHex(),
                    BorderOpacity = change.BorderOpacity,
                    Easing = change.Easing.ToString(),
                }).ToList(),
                Beams = beatmap.HitObjects.OfType<DodgeBeam>().Select(beam => new DodgeBeamData
                {
                    StartTime = beam.StartTime,
                    Duration = beam.Duration,
                    StartX = beam.Position.X,
                    StartY = beam.Position.Y,
                    EndX = beam.EndPosition.X,
                    EndY = beam.EndPosition.Y,
                    Width = beam.BeamWidth,
                    Colour = beam.Colour.ToHex(),
                    OutlineColour = beam.OutlineColour.ToHex(),
                    Opacity = beam.Opacity,
                    OutlineThickness = beam.OutlineThickness,
                }).ToList(),
                Triggers = beatmap.HitObjects.OfType<DodgeTrigger>().Select(trigger => new DodgeTriggerData
                {
                    StartTime = trigger.StartTime,
                    Duration = trigger.Duration,
                    Action = trigger.Action.ToString(),
                    Strength = trigger.Strength,
                    X = trigger.Position.X,
                    Y = trigger.Position.Y,
                    Colour = trigger.Colour.ToHex(),
                }).ToList(),
            };

            JsonSerializer.Serialize(stream, document, options);
        }

        private static DodgeStoryboardData serializeStoryboard(Storyboard storyboard)
        {
            var encoder = new LegacyStoryboardEncoder(storyboard);
            using var beatmapWriter = new StringWriter();
            beatmapWriter.WriteLine("osu file format v14");
            beatmapWriter.WriteLine("[General]");
            encoder.EncodeGeneralToBeatmap(beatmapWriter);
            beatmapWriter.WriteLine("[Events]");
            encoder.EncodeEventsToBeatmap(beatmapWriter);

            using var sharedWriter = new StringWriter();
            encoder.EncodeStandaloneStoryboard(sharedWriter);

            return new DodgeStoryboardData
            {
                BeatmapEvents = beatmapWriter.ToString(),
                SharedEvents = sharedWriter.ToString(),
            };
        }

        public static List<DodgeBullet> Deserialize(Stream stream)
        {
            DodgeBeatmapDocument document = deserializeDocument(stream);
            return createBullets(document);
        }

        public static List<DodgeHitObject> DeserializeHitObjects(Stream stream)
        {
            DodgeBeatmapDocument document = deserializeDocument(stream);
            return createHitObjects(document);
        }

        private static DodgeBeatmapDocument deserializeDocument(Stream stream)
        {
            var document = JsonSerializer.Deserialize<DodgeBeatmapDocument>(stream, options)
                           ?? throw new InvalidDataException("Dodge sidecar is empty.");

            if (document.Version < 1 || document.Version > CURRENT_VERSION)
                throw new InvalidDataException($"Unsupported Dodge sidecar version {document.Version}.");

            return document;
        }

        private static List<DodgeBullet> createBullets(DodgeBeatmapDocument document)
            => document.Bullets.Select(b => new DodgeBullet
            {
                StartTime = b.StartTime,
                Duration = b.Duration,
                Position = new osuTK.Vector2(b.StartX, b.StartY),
                EndPosition = new osuTK.Vector2(b.EndX, b.EndY),
                Shape = parseShape(b.Shape),
                ContinueUntilExit = b.ContinueUntilExit,
                Colour = parseColour(b.Colour),
                OutlineColour = parseColour(b.OutlineColour),
                Opacity = Math.Clamp(b.Opacity, 0, 1),
                OutlineThickness = Math.Clamp(b.OutlineThickness, 0, 8),
                MovementType = document.Version >= 11 ? parseMovementType(b.MovementType) : DodgeMovementType.Linear,
                MovementEasing = document.Version >= 19 ? parseMovementEasing(b.MovementEasing) : DodgeMovementEasing.Linear,
                WaveAmplitude = document.Version >= 11 ? b.WaveAmplitude : DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
                WaveCycles = document.Version >= 11 ? Math.Max(1, b.WaveCycles) : DodgeHitObject.DEFAULT_WAVE_CYCLES,
                WavePhase = document.Version >= 11 ? b.WavePhase : 0,
                TrajectoryGuideStyle = document.Version >= 11 ? parseGuideStyle(b.TrajectoryGuideStyle) : DodgeTrajectoryGuideStyle.Arrow,
            }).ToList();

        private static List<DodgeHitObject> createHitObjects(DodgeBeatmapDocument document)
            => createBullets(document).Cast<DodgeHitObject>()
                                      .Concat(document.Emitters.Select(emitter => new DodgeEmitter
                                      {
                                          StartTime = emitter.StartTime,
                                          Duration = emitter.Duration,
                                          Position = new Vector2(emitter.SourceX, emitter.SourceY),
                                          AimPosition = new Vector2(emitter.AimX, emitter.AimY),
                                          BulletCount = Math.Clamp(emitter.BulletCount, DodgeEmitter.MIN_BULLET_COUNT, DodgeEmitter.MAX_BULLET_COUNT),
                                          SpreadAngle = Math.Clamp(emitter.SpreadAngle, DodgeEmitter.MIN_SPREAD_ANGLE, DodgeEmitter.MAX_SPREAD_ANGLE),
                                          Shape = parseShape(emitter.Shape),
                                          ContinueUntilExit = emitter.ContinueUntilExit,
                                          MovementEndPosition = document.Version >= 8
                                              ? new Vector2(emitter.MovementEndX, emitter.MovementEndY)
                                              : new Vector2(emitter.SourceX, emitter.SourceY),
                                          MoveSource = document.Version >= 9
                                              ? emitter.MoveSource
                                              : document.Version >= 8 && emitter.BurstCount > DodgeEmitter.MIN_BURST_COUNT,
                                          BurstCount = document.Version >= 8
                                              ? Math.Clamp(emitter.BurstCount, DodgeEmitter.MIN_BURST_COUNT, DodgeEmitter.MAX_BURST_COUNT)
                                              : DodgeEmitter.MIN_BURST_COUNT,
                                          BurstInterval = Math.Clamp(emitter.BurstInterval, DodgeEmitter.MIN_BURST_INTERVAL, DodgeEmitter.MAX_BURST_INTERVAL),
                                          BurstBeatDivisor = document.Version >= 9 && isSupportedBeatDivisor(emitter.BurstBeatDivisor)
                                              ? emitter.BurstBeatDivisor
                                              : 0,
                                          BurstRotation = document.Version >= 19 ? emitter.BurstRotation : 0,
                                          Colour = parseColour(emitter.Colour),
                                          OutlineColour = parseColour(emitter.OutlineColour),
                                          Opacity = Math.Clamp(emitter.Opacity, 0, 1),
                                          OutlineThickness = Math.Clamp(emitter.OutlineThickness, 0, 8),
                                          MovementType = document.Version >= 11 ? parseMovementType(emitter.MovementType) : DodgeMovementType.Linear,
                                          MovementEasing = document.Version >= 19 ? parseMovementEasing(emitter.MovementEasing) : DodgeMovementEasing.Linear,
                                          WaveAmplitude = document.Version >= 11 ? emitter.WaveAmplitude : DodgeHitObject.DEFAULT_WAVE_AMPLITUDE,
                                          WaveCycles = document.Version >= 11 ? Math.Max(1, emitter.WaveCycles) : DodgeHitObject.DEFAULT_WAVE_CYCLES,
                                          WavePhase = document.Version >= 11 ? emitter.WavePhase : 0,
                                          // Emitters used full path guides before guide styles became
                                          // author-configurable in format v11. Preserve that appearance
                                          // when loading legacy maps instead of introducing arrows.
                                          TrajectoryGuideStyle = document.Version >= 11
                                              ? parseGuideStyle(emitter.TrajectoryGuideStyle, DodgeTrajectoryGuideStyle.Path)
                                              : DodgeTrajectoryGuideStyle.Path,
                                      }))
                                      .Concat((document.CameraChanges ?? Enumerable.Empty<DodgeCameraChangeData>()).Select(change => new DodgeCameraChange
                                      {
                                          StartTime = change.StartTime,
                                          Duration = change.Duration,
                                          Position = new Vector2(change.StartX, change.StartY),
                                          EndPosition = new Vector2(change.EndX, change.EndY),
                                          Continuous = document.Version >= 16 && change.Continuous,
                                          Easing = document.Version >= 17 ? parseCameraEasing(change.Easing) : DodgeCameraEasing.Linear,
                                      }))
                                      .Concat(document.ArenaChanges.Select(change => new DodgeArenaChange
                                      {
                                          StartTime = change.StartTime,
                                          Duration = change.Duration,
                                          TargetPosition = new Vector2(change.TargetX, change.TargetY),
                                          TargetSize = new Vector2(change.Width, change.Height),
                                          TargetRotation = document.Version >= 14 ? change.Rotation : 0,
                                          KiaiShakeAngle = document.Version >= 19 ? change.KiaiShakeAngle : 0,
                                          Colour = document.Version >= 12
                                              ? parseColour(change.BackgroundColour, Colour4.Black)
                                              : Colour4.Black,
                                          Opacity = document.Version >= 12 ? Math.Clamp(change.BackgroundOpacity, 0, 1) : 1,
                                          OutlineColour = document.Version >= 12 ? parseColour(change.BorderColour) : Colour4.White,
                                          BorderOpacity = document.Version >= 12 ? Math.Clamp(change.BorderOpacity, 0, 1) : 1,
                                          Easing = document.Version >= 18 ? parseCameraEasing(change.Easing) : DodgeCameraEasing.Linear,
                                      }))
                                      .Concat(document.Version >= 13 ? document.Beams.Select(beam => new DodgeBeam
                                      {
                                          StartTime = beam.StartTime,
                                          Duration = beam.Duration,
                                          Position = new Vector2(beam.StartX, beam.StartY),
                                          EndPosition = new Vector2(beam.EndX, beam.EndY),
                                          BeamWidth = Math.Clamp(beam.Width > 0 ? beam.Width : DodgeBeam.DEFAULT_WIDTH, DodgeBeam.MIN_WIDTH, DodgeBeam.MAX_WIDTH),
                                          Colour = parseColour(beam.Colour),
                                          OutlineColour = parseColour(beam.OutlineColour),
                                          Opacity = Math.Clamp(beam.Opacity, 0, 1),
                                          OutlineThickness = Math.Clamp(beam.OutlineThickness, 0, 8),
                                      }) : Array.Empty<DodgeHitObject>())
                                      .Concat(document.Version >= 20 ? document.Triggers.Select(trigger => new DodgeTrigger
                                      {
                                          StartTime = trigger.StartTime,
                                          Duration = Math.Clamp(trigger.Duration, 0, DodgeTrigger.MAX_DURATION),
                                          Action = parseTriggerAction(trigger.Action),
                                          Strength = Math.Clamp(trigger.Strength, 0, 1),
                                          Position = new Vector2(trigger.X, trigger.Y),
                                          Colour = parseColour(trigger.Colour),
                                      }) : Array.Empty<DodgeHitObject>())
                                      .OrderBy(hitObject => hitObject.StartTime)
                                      .ToList();

        public static void EncodeCompatibilityBeatmap(IBeatmap beatmap, ISkin? skin, Storyboard? storyboard, Stream stream)
        {
            var compatibilityInfo = beatmap.BeatmapInfo.Clone();
            compatibilityInfo.Ruleset = new RulesetInfo("osu", "osu!", string.Empty, 0);

            var compatibility = new Beatmap<CompatibilityHitObject>
            {
                BeatmapInfo = compatibilityInfo,
                ControlPointInfo = beatmap.ControlPointInfo,
                Breaks = beatmap.Breaks,
                AudioLeadIn = beatmap.AudioLeadIn,
                StackLeniency = beatmap.StackLeniency,
                SpecialStyle = beatmap.SpecialStyle,
                LetterboxInBreaks = beatmap.LetterboxInBreaks,
                WidescreenStoryboard = beatmap.WidescreenStoryboard,
                EpilepsyWarning = beatmap.EpilepsyWarning,
                SamplesMatchPlaybackRate = beatmap.SamplesMatchPlaybackRate,
                DistanceSpacing = beatmap.DistanceSpacing,
                GridSize = beatmap.GridSize,
                TimelineZoom = beatmap.TimelineZoom,
                Countdown = beatmap.Countdown,
                CountdownOffset = beatmap.CountdownOffset,
                Bookmarks = beatmap.Bookmarks,
                SliderVelocityPresets = beatmap.SliderVelocityPresets,
                BeatmapVersion = beatmap.BeatmapVersion,
            };

            foreach (var bullet in beatmap.HitObjects.OfType<DodgeBullet>())
            {
                compatibility.HitObjects.Add(new CompatibilityHitObject
                {
                    StartTime = bullet.StartTime,
                    Position = bullet.Position,
                    Samples = bullet.Samples,
                });
            }

            foreach (var emitter in beatmap.HitObjects.OfType<DodgeEmitter>())
            {
                compatibility.HitObjects.Add(new CompatibilityHitObject
                {
                    StartTime = emitter.StartTime,
                    Position = emitter.Position,
                    Samples = emitter.Samples,
                });
            }

            foreach (var beam in beatmap.HitObjects.OfType<DodgeBeam>())
            {
                compatibility.HitObjects.Add(new CompatibilityHitObject
                {
                    StartTime = beam.StartTime,
                    Position = beam.Position,
                    Samples = beam.Samples,
                });
            }

            using var sidecarStream = new MemoryStream();
            Serialize(beatmap, storyboard, sidecarStream);
            string sidecarHash = sidecarStream.ComputeSHA2Hash();

            using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true);
            new LegacyBeatmapEncoder(compatibility, skin, storyboard).Encode(writer);
            writer.WriteLine($"// DodgeSidecarSHA256:{sidecarHash}");
        }

        public static IBeatmap DecodeCompatibilityBeatmap(IBeatmap compatibility, Stream sidecar)
        {
            DodgeBeatmapDocument document = deserializeDocument(sidecar);
            applySettings(compatibility.Difficulty, document.Settings);

            var decoded = new Beatmap<DodgeHitObject>
            {
                BeatmapInfo = compatibility.BeatmapInfo,
                ControlPointInfo = compatibility.ControlPointInfo,
                HitObjects = createHitObjects(document),
                Breaks = compatibility.Breaks,
                AudioLeadIn = compatibility.AudioLeadIn,
                StackLeniency = compatibility.StackLeniency,
                SpecialStyle = compatibility.SpecialStyle,
                LetterboxInBreaks = compatibility.LetterboxInBreaks,
                WidescreenStoryboard = compatibility.WidescreenStoryboard,
                EpilepsyWarning = compatibility.EpilepsyWarning,
                SamplesMatchPlaybackRate = compatibility.SamplesMatchPlaybackRate,
                DistanceSpacing = compatibility.DistanceSpacing,
                GridSize = compatibility.GridSize,
                TimelineZoom = compatibility.TimelineZoom,
                Countdown = compatibility.Countdown,
                CountdownOffset = compatibility.CountdownOffset,
                Bookmarks = compatibility.Bookmarks,
                SliderVelocityPresets = compatibility.SliderVelocityPresets,
                BeatmapVersion = compatibility.BeatmapVersion,
            };

            if (document.Settings != null)
            {
                DodgeBeatmapSettings.SetPlayerSize(decoded.Difficulty, document.Settings.PlayerSize);
                DodgeBeatmapSettings.SetForceStoryboard(decoded.Difficulty, document.Settings.ForceStoryboard);
                DodgeBeatmapSettings.SetForceBeatmapSkin(decoded.Difficulty, document.Settings.ForceBeatmapSkin);
            }

            return decoded;
        }

        public static void ApplyToEditor(EditorBeatmap editorBeatmap, byte[] state)
        {
            using var stream = new MemoryStream(state);
            DodgeBeatmapDocument document = deserializeDocument(stream);
            List<DodgeHitObject> hitObjects = createHitObjects(document);

            editorBeatmap.BeginChange();
            applySettings(editorBeatmap.Difficulty, document.Settings);

            foreach (var existing in editorBeatmap.HitObjects.OfType<DodgeHitObject>().ToArray())
                editorBeatmap.Remove(existing);

            foreach (var hitObject in hitObjects)
                editorBeatmap.Add(hitObject);

            if (document.Storyboard != null)
                applyStoryboard(editorBeatmap, document.Storyboard);

            editorBeatmap.EndChange();
        }

        private static void applyStoryboard(EditorBeatmap editorBeatmap, DodgeStoryboardData data)
        {
            using var primaryStream = new MemoryStream(Encoding.UTF8.GetBytes(data.BeatmapEvents));
            using var sharedStream = new MemoryStream(Encoding.UTF8.GetBytes(data.SharedEvents));
            using var primaryReader = new LineBufferedReader(primaryStream);
            using var sharedReader = new LineBufferedReader(sharedStream);

            Storyboard decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Storyboard>(primaryReader).Decode(primaryReader, sharedReader);
            Storyboard target = editorBeatmap.Storyboard;

            foreach (StoryboardLayer layer in target.Layers)
                layer.Elements.Clear();

            foreach (StoryboardLayer layer in decoded.Layers)
                target.GetLayer(layer.Name).Elements.AddRange(layer.Elements);

            target.UseSkinSprites = decoded.UseSkinSprites;
            target.BackgroundOffset = decoded.BackgroundOffset;
            editorBeatmap.WidescreenStoryboard = decoded.Beatmap.WidescreenStoryboard;
        }

        private static void applySettings(BeatmapDifficulty difficulty, DodgeMapSettingsData? settings)
        {
            if (settings == null)
                return;

            difficulty.ApproachRate = DodgeBeatmapSettings.GetApproachRate(settings.AppearanceDuration);
            difficulty.CircleSize = DodgeBeatmapSettings.GetCircleSize(settings.BulletSize);
            difficulty.DrainRate = (float)Math.Clamp(settings.HpDrain, 0, 10);
            difficulty.OverallDifficulty = DodgeBeatmapSettings.GetOverallDifficulty(settings.PlayerSpeed);
            DodgeBeatmapSettings.SetPlayerSize(difficulty, settings.PlayerSize);
            difficulty.SliderMultiplier = DodgeBeatmapSettings.GetSliderMultiplier(settings.GrazeDistance);
            difficulty.SliderTickRate = DodgeBeatmapSettings.GetSliderTickRate(settings.GrazeScore);
            DodgeBeatmapSettings.SetForceStoryboard(difficulty, settings.ForceStoryboard);
            DodgeBeatmapSettings.SetForceBeatmapSkin(difficulty, settings.ForceBeatmapSkin);
        }

        private static DodgeBulletShape parseShape(string? shape)
            => Enum.TryParse(shape, true, out DodgeBulletShape result) ? result : DodgeBulletShape.Circle;

        private static DodgeMovementType parseMovementType(string? value)
            => Enum.TryParse(value, true, out DodgeMovementType result) ? result : DodgeMovementType.Linear;

        private static DodgeMovementEasing parseMovementEasing(string? value)
            => Enum.TryParse(value, true, out DodgeMovementEasing result) ? result : DodgeMovementEasing.Linear;

        private static DodgeCameraEasing parseCameraEasing(string? value)
            => Enum.TryParse(value, true, out DodgeCameraEasing result) ? result : DodgeCameraEasing.Linear;

        private static DodgeTriggerAction parseTriggerAction(string? value)
            => Enum.TryParse(value, true, out DodgeTriggerAction result) ? result : DodgeTriggerAction.ClearBullets;

        private static DodgeTrajectoryGuideStyle parseGuideStyle(
            string? value,
            DodgeTrajectoryGuideStyle fallback = DodgeTrajectoryGuideStyle.Arrow)
            => Enum.TryParse(value, true, out DodgeTrajectoryGuideStyle result) ? result : fallback;

        private static bool isSupportedBeatDivisor(int divisor)
            => divisor == 0 || Enum.IsDefined(typeof(DodgeEmitterBeatDivisor), divisor);

        private static Colour4 parseColour(string? colour, Colour4? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(colour))
                return fallback ?? Colour4.White;

            try
            {
                return Colour4.FromHex(colour);
            }
            catch
            {
                return fallback ?? Colour4.White;
            }
        }

        private class DodgeBeatmapDocument
        {
            public string Format { get; set; } = string.Empty;

            public int RulesetOnlineId { get; set; }

            public int Version { get; set; }

            public DodgeMapSettingsData? Settings { get; set; }

            public DodgeStoryboardData? Storyboard { get; set; }

            public List<DodgeBulletData> Bullets { get; set; } = new List<DodgeBulletData>();

            public List<DodgeEmitterData> Emitters { get; set; } = new List<DodgeEmitterData>();

            public List<DodgeArenaChangeData> ArenaChanges { get; set; } = new List<DodgeArenaChangeData>();

            public List<DodgeCameraChangeData> CameraChanges { get; set; } = new List<DodgeCameraChangeData>();

            public List<DodgeBeamData> Beams { get; set; } = new List<DodgeBeamData>();

            public List<DodgeTriggerData> Triggers { get; set; } = new List<DodgeTriggerData>();
        }

        private class DodgeMapSettingsData
        {
            public double AppearanceDuration { get; set; } = DodgeBeatmapSettings.APPEARANCE_DURATION_MID;

            public double BulletSize { get; set; } = DodgeBeatmapSettings.BULLET_SIZE_MID;

            public double HpDrain { get; set; } = 5;

            public double PlayerSpeed { get; set; } = DodgeBeatmapSettings.PLAYER_SPEED_MID;

            public double PlayerSize { get; set; } = DodgeBeatmapSettings.PLAYER_SIZE_MID;

            public double GrazeDistance { get; set; }

            public double GrazeScore { get; set; }

            public bool ForceStoryboard { get; set; }

            public bool ForceBeatmapSkin { get; set; }

        }

        private class DodgeStoryboardData
        {
            public string BeatmapEvents { get; set; } = string.Empty;

            public string SharedEvents { get; set; } = string.Empty;
        }

        private class DodgeBulletData
        {
            public double StartTime { get; set; }
            public double Duration { get; set; }
            public float StartX { get; set; }
            public float StartY { get; set; }
            public float EndX { get; set; }
            public float EndY { get; set; }
            public string Shape { get; set; } = nameof(DodgeBulletShape.Circle);
            public bool ContinueUntilExit { get; set; }
            public string Colour { get; set; } = "FFFFFF";
            public string OutlineColour { get; set; } = "FFFFFF";
            public float Opacity { get; set; } = 1;
            public float OutlineThickness { get; set; }
            public string MovementType { get; set; } = nameof(DodgeMovementType.Linear);
            public string MovementEasing { get; set; } = nameof(DodgeMovementEasing.Linear);
            public float WaveAmplitude { get; set; } = DodgeHitObject.DEFAULT_WAVE_AMPLITUDE;
            public int WaveCycles { get; set; } = DodgeHitObject.DEFAULT_WAVE_CYCLES;
            public float WavePhase { get; set; }
            public string TrajectoryGuideStyle { get; set; } = nameof(DodgeTrajectoryGuideStyle.Arrow);
        }

        private class DodgeCameraChangeData
        {
            public double StartTime { get; set; }
            public double Duration { get; set; }
            public float StartX { get; set; }
            public float StartY { get; set; }
            public float EndX { get; set; }
            public float EndY { get; set; }
            public bool Continuous { get; set; }
            public string Easing { get; set; } = nameof(DodgeCameraEasing.Linear);
        }

        private class DodgeArenaChangeData
        {
            public double StartTime { get; set; }
            public double Duration { get; set; }
            public float TargetX { get; set; }
            public float TargetY { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
            public float Rotation { get; set; }
            public float KiaiShakeAngle { get; set; }
            public string BackgroundColour { get; set; } = "000000";
            public float BackgroundOpacity { get; set; } = 1;
            public string BorderColour { get; set; } = "FFFFFF";
            public float BorderOpacity { get; set; } = 1;
            public string Easing { get; set; } = nameof(DodgeCameraEasing.Linear);
        }

        private class DodgeBeamData
        {
            public double StartTime { get; set; }
            public double Duration { get; set; }
            public float StartX { get; set; }
            public float StartY { get; set; }
            public float EndX { get; set; }
            public float EndY { get; set; }
            public float Width { get; set; } = DodgeBeam.DEFAULT_WIDTH;
            public string Colour { get; set; } = "FFFFFF";
            public string OutlineColour { get; set; } = "FFFFFF";
            public float Opacity { get; set; } = 1;
            public float OutlineThickness { get; set; }
        }

        private class DodgeTriggerData
        {
            public double StartTime { get; set; }
            public double Duration { get; set; } = 300;
            public string Action { get; set; } = nameof(DodgeTriggerAction.ClearBullets);
            public float Strength { get; set; } = 0.5f;
            public float X { get; set; } = 256;
            public float Y { get; set; } = 32;
            public string Colour { get; set; } = "FFFFFF";
        }

        private class DodgeEmitterData
        {
            public double StartTime { get; set; }
            public double Duration { get; set; }
            public float SourceX { get; set; }
            public float SourceY { get; set; }
            public float AimX { get; set; }
            public float AimY { get; set; }
            public int BulletCount { get; set; } = DodgeEmitter.DEFAULT_BULLET_COUNT;
            public float SpreadAngle { get; set; } = DodgeEmitter.DEFAULT_SPREAD_ANGLE;
            public string Shape { get; set; } = nameof(DodgeBulletShape.Circle);
            public bool ContinueUntilExit { get; set; }
            public float MovementEndX { get; set; }
            public float MovementEndY { get; set; }
            public bool MoveSource { get; set; }
            public int BurstCount { get; set; } = DodgeEmitter.MIN_BURST_COUNT;
            public double BurstInterval { get; set; } = DodgeEmitter.DEFAULT_BURST_INTERVAL;
            public int BurstBeatDivisor { get; set; } = (int)DodgeEmitterBeatDivisor.Quarter;
            public float BurstRotation { get; set; }
            public string Colour { get; set; } = "FFFFFF";
            public string OutlineColour { get; set; } = "FFFFFF";
            public float Opacity { get; set; } = 1;
            public float OutlineThickness { get; set; }
            public string MovementType { get; set; } = nameof(DodgeMovementType.Linear);
            public string MovementEasing { get; set; } = nameof(DodgeMovementEasing.Linear);
            public float WaveAmplitude { get; set; } = DodgeHitObject.DEFAULT_WAVE_AMPLITUDE;
            public int WaveCycles { get; set; } = DodgeHitObject.DEFAULT_WAVE_CYCLES;
            public float WavePhase { get; set; }
            public string TrajectoryGuideStyle { get; set; } = nameof(DodgeTrajectoryGuideStyle.Path);
        }

        private class CompatibilityHitObject : HitObject, IHasPosition
        {
            public Vector2 Position { get; set; }

            public float X
            {
                get => Position.X;
                set => Position = new Vector2(value, Y);
            }

            public float Y
            {
                get => Position.Y;
                set => Position = new Vector2(X, value);
            }

            public override Judgement CreateJudgement() => new Judgement();
        }
    }
}
