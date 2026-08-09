// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Edit.Design;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Storyboards;
using osu.Framework.Graphics;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeBeatmapSerializerTest
    {
        [Test]
        public void TestCustomDifficultySettingsSurviveBeatmapInfoReplacement()
        {
            var ruleset = new DodgeRuleset();
            var beatmap = new Beatmap<DodgeHitObject>();

            DodgeBeatmapSettings.SetPlayerSize(beatmap.Difficulty, 31);
            DodgeBeatmapSettings.SetForceStoryboard(beatmap.Difficulty, true);
            DodgeBeatmapSettings.SetForceBeatmapSkin(beatmap.Difficulty, true);

            BeatmapDifficulty originalDifficulty = beatmap.Difficulty;
            beatmap.BeatmapInfo = beatmap.BeatmapInfo;
            ruleset.CopyCustomDifficultySettings(originalDifficulty, beatmap.Difficulty);

            Assert.Multiple(() =>
            {
                Assert.That(DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty), Is.EqualTo(31));
                Assert.That(DodgeBeatmapSettings.GetForceStoryboard(beatmap.Difficulty), Is.True);
                Assert.That(DodgeBeatmapSettings.GetForceBeatmapSkin(beatmap.Difficulty), Is.True);
            });
        }

        [Test]
        public void TestCompatibilityAndSidecarRoundTrip()
        {
            var source = new Beatmap<DodgeHitObject>
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = new RulesetInfo("dodge", "Dodge", string.Empty, DodgeRuleset.ONLINE_ID),
                    DifficultyName = "Test",
                },
            };

            source.Metadata.Title = "Sidecar test";
            source.Metadata.Artist = "Test artist";
            source.Metadata.Author.Username = "Test mapper";
            source.Difficulty.ApproachRate = DodgeBeatmapSettings.GetApproachRate(1100);
            source.Difficulty.CircleSize = DodgeBeatmapSettings.GetCircleSize(24);
            source.Difficulty.OverallDifficulty = DodgeBeatmapSettings.GetOverallDifficulty(360);
            DodgeBeatmapSettings.SetPlayerSize(source.Difficulty, 26);
            DodgeBeatmapSettings.SetForceStoryboard(source.Difficulty, true);
            DodgeBeatmapSettings.SetForceBeatmapSkin(source.Difficulty, true);
            source.Difficulty.SliderMultiplier = DodgeBeatmapSettings.GetSliderMultiplier(28);
            source.Difficulty.SliderTickRate = DodgeBeatmapSettings.GetSliderTickRate(250);
            source.Difficulty.DrainRate = 7;
            source.HitObjects.Add(new DodgeBullet
            {
                StartTime = 1234,
                Duration = 500,
                Position = new Vector2(100, 120),
                EndPosition = new Vector2(400, 280),
                Shape = DodgeBulletShape.Diamond,
                ContinueUntilExit = true,
                Colour = Colour4.FromHex("#35A7FF"),
                OutlineColour = Colour4.FromHex("#0A2440"),
                Opacity = 0.65f,
                OutlineThickness = 2.5f,
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 48,
                WaveCycles = 3,
                WavePhase = 25,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.FullPath,
            });
            source.HitObjects.Add(new DodgeArenaChange
            {
                StartTime = 2000,
                Duration = 750,
                TargetPosition = new Vector2(50, 40),
                TargetSize = new Vector2(300, 200),
                Colour = Colour4.FromHex("#18304A"),
                Opacity = 0.45f,
                OutlineColour = Colour4.FromHex("#F2A65A"),
                BorderOpacity = 0.7f,
            });
            source.HitObjects.Add(new DodgeEmitter
            {
                StartTime = 3000,
                Duration = 1250,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(456, 192),
                MovementEndPosition = new Vector2(256, 320),
                BulletCount = 7,
                SpreadAngle = 180,
                BurstCount = 6,
                BurstInterval = 150,
                BurstBeatDivisor = (int)DodgeEmitterBeatDivisor.Third,
                MoveSource = true,
                Shape = DodgeBulletShape.Triangle,
                ContinueUntilExit = true,
                Colour = Colour4.FromHex("#FF5577"),
                OutlineColour = Colour4.FromHex("#220811"),
                Opacity = 0.8f,
                OutlineThickness = 1.5f,
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = -36,
                WaveCycles = 4,
                WavePhase = -15,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Hidden,
            });

            using var compatibilityStream = new MemoryStream();
            using var sidecarStream = new MemoryStream();

            DodgeBeatmapSerializer.EncodeCompatibilityBeatmap(source, null, null, compatibilityStream);
            DodgeBeatmapSerializer.Serialize(source, sidecarStream);

            string compatibilityText = Encoding.UTF8.GetString(compatibilityStream.ToArray());
            string sidecarText = Encoding.UTF8.GetString(sidecarStream.ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(compatibilityText, Does.Contain("Mode: 0"));
                Assert.That(compatibilityText, Does.Contain("[HitObjects]"));
                Assert.That(compatibilityText, Does.Contain("// DodgeSidecarSHA256:"));
                Assert.That(sidecarText, Does.Contain("\"rulesetOnlineId\": 10"));
                Assert.That(sidecarText, Does.Contain($"\"version\": {DodgeBeatmapSerializer.CURRENT_VERSION}"));
                Assert.That(sidecarText, Does.Contain("\"appearanceDuration\": 1100"));
                Assert.That(sidecarText, Does.Contain("\"bulletSize\": 24"));
                Assert.That(sidecarText, Does.Contain("\"hpDrain\": 7"));
                Assert.That(sidecarText, Does.Contain("\"playerSpeed\": 360"));
                Assert.That(sidecarText, Does.Contain("\"playerSize\": 26"));
                Assert.That(sidecarText, Does.Contain("\"grazeDistance\": 28"));
                Assert.That(sidecarText, Does.Contain("\"grazeScore\": 250"));
                Assert.That(sidecarText, Does.Contain("\"forceStoryboard\": true"));
                Assert.That(sidecarText, Does.Contain("\"forceBeatmapSkin\": true"));
                Assert.That(sidecarText, Does.Contain("\"shape\": \"Diamond\""));
                Assert.That(sidecarText, Does.Contain("\"shape\": \"Triangle\""));
                Assert.That(sidecarText, Does.Contain("\"arenaChanges\""));
                Assert.That(sidecarText, Does.Contain("\"backgroundColour\": \"#18304A\""));
                Assert.That(sidecarText, Does.Contain("\"backgroundOpacity\": 0.45"));
                Assert.That(sidecarText, Does.Contain("\"borderColour\": \"#F2A65A\""));
                Assert.That(sidecarText, Does.Contain("\"borderOpacity\": 0.7"));
                Assert.That(sidecarText, Does.Contain("\"emitters\""));
                Assert.That(sidecarText, Does.Contain("\"bulletCount\": 7"));
                Assert.That(sidecarText, Does.Contain("\"burstCount\": 6"));
                Assert.That(sidecarText, Does.Contain("\"burstInterval\": 150"));
                Assert.That(sidecarText, Does.Contain("\"burstBeatDivisor\": 3"));
                Assert.That(sidecarText, Does.Contain("\"moveSource\": true"));
                Assert.That(sidecarText, Does.Contain("\"continueUntilExit\": true"));
                Assert.That(sidecarText, Does.Contain("\"movementType\": \"Sine\""));
                Assert.That(sidecarText, Does.Contain("\"trajectoryGuideStyle\": \"FullPath\""));
                Assert.That(sidecarText, Does.Contain("\"trajectoryGuideStyle\": \"Hidden\""));
                Assert.That(sidecarText, Does.Not.Contain("travelDistancePerBeat"));
            });

            compatibilityStream.Seek(0, SeekOrigin.Begin);

            Beatmap compatibility;

            using (var reader = new LineBufferedReader(compatibilityStream, leaveOpen: true))
                compatibility = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            sidecarStream.Seek(0, SeekOrigin.Begin);
            IBeatmap decoded = DodgeBeatmapSerializer.DecodeCompatibilityBeatmap(compatibility, sidecarStream);
            var bullet = decoded.HitObjects.OfType<DodgeBullet>().Single();
            var arenaChange = decoded.HitObjects.OfType<DodgeArenaChange>().Single();
            var emitter = decoded.HitObjects.OfType<DodgeEmitter>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(compatibility.BeatmapInfo.Ruleset.OnlineID, Is.Zero);
                Assert.That(compatibility.HitObjects, Has.Count.EqualTo(2));
                Assert.That(decoded.HitObjects, Has.Count.EqualTo(3));
                Assert.That(bullet.StartTime, Is.EqualTo(1234));
                Assert.That(bullet.Duration, Is.EqualTo(500));
                Assert.That(bullet.Position, Is.EqualTo(new Vector2(100, 120)));
                Assert.That(bullet.EndPosition, Is.EqualTo(new Vector2(400, 280)));
                Assert.That(bullet.Shape, Is.EqualTo(DodgeBulletShape.Diamond));
                Assert.That(bullet.ContinueUntilExit, Is.True);
                Assert.That(bullet.Colour, Is.EqualTo(Colour4.FromHex("#35A7FF")));
                Assert.That(bullet.OutlineColour, Is.EqualTo(Colour4.FromHex("#0A2440")));
                Assert.That(bullet.Opacity, Is.EqualTo(0.65f));
                Assert.That(bullet.OutlineThickness, Is.EqualTo(2.5f));
                Assert.That(bullet.MovementType, Is.EqualTo(DodgeMovementType.Sine));
                Assert.That(bullet.WaveAmplitude, Is.EqualTo(48));
                Assert.That(bullet.WaveCycles, Is.EqualTo(3));
                Assert.That(bullet.WavePhase, Is.EqualTo(25));
                Assert.That(bullet.TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.FullPath));
                Assert.That(DodgeBeatmapSettings.GetAppearanceDuration(decoded.Difficulty), Is.EqualTo(1100).Within(0.001));
                Assert.That(DodgeBeatmapSettings.GetBulletSize(decoded.Difficulty), Is.EqualTo(24).Within(0.001));
                Assert.That(DodgeBeatmapSettings.GetPlayerSpeed(decoded.Difficulty), Is.EqualTo(360).Within(0.001));
                Assert.That(DodgeBeatmapSettings.GetPlayerSize(decoded.Difficulty), Is.EqualTo(26).Within(0.001));
                Assert.That(DodgeBeatmapSettings.GetGrazeDistance(decoded.Difficulty), Is.EqualTo(28).Within(0.001));
                Assert.That(DodgeBeatmapSettings.GetGrazeScore(decoded.Difficulty), Is.EqualTo(250).Within(0.001));
                Assert.That(DodgeBeatmapSettings.GetForceStoryboard(decoded.Difficulty), Is.True);
                Assert.That(DodgeBeatmapSettings.GetForceBeatmapSkin(decoded.Difficulty), Is.True);
                Assert.That(decoded.Difficulty.DrainRate, Is.EqualTo(7).Within(0.001));
                Assert.That(arenaChange.StartTime, Is.EqualTo(2000));
                Assert.That(arenaChange.Duration, Is.EqualTo(750));
                Assert.That(arenaChange.TargetPosition, Is.EqualTo(new Vector2(50, 40)));
                Assert.That(arenaChange.TargetSize, Is.EqualTo(new Vector2(300, 200)));
                Assert.That(arenaChange.Colour, Is.EqualTo(Colour4.FromHex("#18304A")));
                Assert.That(arenaChange.Opacity, Is.EqualTo(0.45f));
                Assert.That(arenaChange.OutlineColour, Is.EqualTo(Colour4.FromHex("#F2A65A")));
                Assert.That(arenaChange.BorderOpacity, Is.EqualTo(0.7f));
                Assert.That(emitter.StartTime, Is.EqualTo(3000));
                Assert.That(emitter.Duration, Is.EqualTo(1250));
                Assert.That(emitter.Position, Is.EqualTo(new Vector2(256, 192)));
                Assert.That(emitter.AimPosition, Is.EqualTo(new Vector2(456, 192)));
                Assert.That(emitter.MovementEndPosition, Is.EqualTo(new Vector2(256, 320)));
                Assert.That(emitter.BulletCount, Is.EqualTo(7));
                Assert.That(emitter.SpreadAngle, Is.EqualTo(180));
                Assert.That(emitter.BurstCount, Is.EqualTo(6));
                Assert.That(emitter.BurstInterval, Is.EqualTo(150));
                Assert.That(emitter.BurstBeatDivisor, Is.EqualTo(3));
                Assert.That(emitter.MoveSource, Is.True);
                Assert.That(emitter.Shape, Is.EqualTo(DodgeBulletShape.Triangle));
                Assert.That(emitter.ContinueUntilExit, Is.True);
                Assert.That(emitter.Colour, Is.EqualTo(Colour4.FromHex("#FF5577")));
                Assert.That(emitter.OutlineColour, Is.EqualTo(Colour4.FromHex("#220811")));
                Assert.That(emitter.Opacity, Is.EqualTo(0.8f));
                Assert.That(emitter.OutlineThickness, Is.EqualTo(1.5f));
                Assert.That(emitter.MovementType, Is.EqualTo(DodgeMovementType.Sine));
                Assert.That(emitter.WaveAmplitude, Is.EqualTo(-36));
                Assert.That(emitter.WaveCycles, Is.EqualTo(4));
                Assert.That(emitter.WavePhase, Is.EqualTo(-15));
                Assert.That(emitter.TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.Hidden));
            });
        }

        [Test]
        public void TestVersionNineMigratesVisualFlagsToFalse()
        {
            const string versionNine = """
                                       {
                                         "format": "dodge",
                                         "rulesetOnlineId": 10,
                                         "version": 9,
                                         "bullets": []
                                       }
                                       """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(versionNine));
            IBeatmap decoded = DodgeBeatmapSerializer.DecodeCompatibilityBeatmap(new Beatmap(), stream);

            Assert.Multiple(() =>
            {
                Assert.That(DodgeBeatmapSettings.GetForceStoryboard(decoded.Difficulty), Is.False);
                Assert.That(DodgeBeatmapSettings.GetForceBeatmapSkin(decoded.Difficulty), Is.False);
            });
        }

        [Test]
        public void TestVersionElevenArenaUsesLegacyAppearance()
        {
            const string versionEleven = """
                                         {
                                           "format": "dodge",
                                           "rulesetOnlineId": 10,
                                           "version": 11,
                                           "arenaChanges": [
                                             {
                                               "startTime": 0,
                                               "duration": 500,
                                               "targetX": 0,
                                               "targetY": 0,
                                               "width": 512,
                                               "height": 384
                                             }
                                           ]
                                         }
                                         """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(versionEleven));
            DodgeArenaChange arena = DodgeBeatmapSerializer.DeserializeHitObjects(stream).OfType<DodgeArenaChange>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(arena.Colour, Is.EqualTo(Colour4.Black));
                Assert.That(arena.Opacity, Is.EqualTo(1));
                Assert.That(arena.OutlineColour, Is.EqualTo(Colour4.White));
                Assert.That(arena.BorderOpacity, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestVersionTenMigratesProjectileMotionDefaults()
        {
            const string versionTen = """
                                      {
                                        "format": "dodge",
                                        "rulesetOnlineId": 10,
                                        "version": 10,
                                        "bullets": [
                                          {
                                            "startTime": 1000,
                                            "duration": 500,
                                            "startX": 10,
                                            "startY": 20,
                                            "endX": 30,
                                            "endY": 40
                                          }
                                        ],
                                        "emitters": [
                                          {
                                            "startTime": 2000,
                                            "duration": 500,
                                            "sourceX": 10,
                                            "sourceY": 20,
                                            "aimX": 110,
                                            "aimY": 20
                                          }
                                        ]
                                      }
                                      """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(versionTen));
            DodgeHitObject[] decoded = DodgeBeatmapSerializer.DeserializeHitObjects(stream).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(decoded, Has.All.Property(nameof(DodgeHitObject.MovementType)).EqualTo(DodgeMovementType.Linear));
                Assert.That(decoded.OfType<DodgeBullet>().Single().TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.Arrow));
                Assert.That(decoded.OfType<DodgeEmitter>().Single().TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.Path));
                Assert.That(decoded, Has.All.Property(nameof(DodgeHitObject.WaveAmplitude)).EqualTo(DodgeHitObject.DEFAULT_WAVE_AMPLITUDE));
                Assert.That(decoded, Has.All.Property(nameof(DodgeHitObject.WaveCycles)).EqualTo(DodgeHitObject.DEFAULT_WAVE_CYCLES));
            });
        }

        [Test]
        public void TestStoryboardIsIncludedInCompositeState()
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            var storyboard = new Storyboard
            {
                Beatmap = beatmap,
                BeatmapInfo = beatmap.BeatmapInfo,
            };
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "outside-arena.png", Anchor.Centre, new Vector2(20, 20));
            sprite.Commands.AddAlpha(Easing.None, 100, 200, 0, 1);
            storyboard.GetLayer("Overlay").Add(sprite);

            using var stream = new MemoryStream();
            DodgeBeatmapSerializer.Serialize(beatmap, storyboard, stream);
            string state = Encoding.UTF8.GetString(stream.ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(state, Does.Contain("\"storyboard\""));
                Assert.That(state, Does.Contain("outside-arena.png"));
                Assert.That(state, Does.Contain("Overlay"));
            });
        }

        [Test]
        public void TestRetimedStoryboardCommandIsPersisted()
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            var storyboard = new Storyboard
            {
                Beatmap = beatmap,
                BeatmapInfo = beatmap.BeatmapInfo,
            };
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "timed.png", Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
            StoryboardSprite retimed = DodgeStoryboardEditing.RetimeCommand(sprite, sprite.Commands.Alpha.Single(), 1250, 2750);
            storyboard.GetLayer("Foreground").Add(retimed);

            using var stream = new MemoryStream();
            DodgeBeatmapSerializer.Serialize(beatmap, storyboard, stream);
            string state = Encoding.UTF8.GetString(stream.ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(state, Does.Contain(@"F,0,1250,2750,1"));
                Assert.That(state, Does.Not.Contain(@"V,0,1000,3000,1,1"),
                    "A newly placed sprite should not receive a redundant scale command.");
            });
        }

        [Test]
        public void TestCompositeEditorUndoRestoresStoryboardAndVisualFlags()
        {
            var source = new Beatmap<DodgeHitObject>
            {
                BeatmapInfo =
                {
                    Ruleset = new DodgeRuleset().RulesetInfo,
                },
            };
            var beatmap = new EditorBeatmap(source);
            var changeHandler = new BeatmapEditorChangeHandler(beatmap);
            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, "overlay.png", Anchor.Centre, new Vector2(700, 240));

            changeHandler.BeginChange();
            beatmap.Storyboard.GetLayer("Overlay").Add(sprite);
            DodgeBeatmapSettings.SetForceStoryboard(beatmap.Difficulty, true);
            DodgeBeatmapSettings.SetForceBeatmapSkin(beatmap.Difficulty, true);
            changeHandler.EndChange();

            Assert.That(changeHandler.CanUndo.Value, Is.True);
            changeHandler.RestoreState(-1);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.Storyboard.GetLayer("Overlay").Elements, Is.Empty);
                Assert.That(DodgeBeatmapSettings.GetForceStoryboard(beatmap.Difficulty), Is.False);
                Assert.That(DodgeBeatmapSettings.GetForceBeatmapSkin(beatmap.Difficulty), Is.False);
            });

            changeHandler.RestoreState(1);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.Storyboard.GetLayer("Overlay").Elements.OfType<StoryboardSprite>().Single().Path, Is.EqualTo("overlay.png"));
                Assert.That(DodgeBeatmapSettings.GetForceStoryboard(beatmap.Difficulty), Is.True);
                Assert.That(DodgeBeatmapSettings.GetForceBeatmapSkin(beatmap.Difficulty), Is.True);
            });
        }

        [Test]
        public void TestVersionOneSidecarRemainsReadable()
        {
            const string versionOne = """
                                      {
                                        "format": "dodge",
                                        "rulesetOnlineId": 10,
                                        "version": 1,
                                        "bullets": [
                                          {
                                            "startTime": 1000,
                                            "duration": 500,
                                            "startX": 10,
                                            "startY": 20,
                                            "endX": 30,
                                            "endY": 40
                                          }
                                        ]
                                      }
                                      """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(versionOne));
            var bullet = DodgeBeatmapSerializer.Deserialize(stream).Single();

            Assert.That(bullet.Position, Is.EqualTo(new Vector2(10, 20)));
            Assert.That(bullet.EndPosition, Is.EqualTo(new Vector2(30, 40)));
            Assert.That(bullet.ContinueUntilExit, Is.False);
            Assert.That(bullet.Shape, Is.EqualTo(DodgeBulletShape.Circle));
        }

        [Test]
        public void TestVersionEightMovingEmitterMigratesWithoutBpmSync()
        {
            const string versionEight = """
                                        {
                                          "format": "dodge",
                                          "rulesetOnlineId": 10,
                                          "version": 8,
                                          "emitters": [
                                            {
                                              "startTime": 1000,
                                              "duration": 500,
                                              "sourceX": 10,
                                              "sourceY": 20,
                                              "aimX": 110,
                                              "aimY": 20,
                                              "movementEndX": 10,
                                              "movementEndY": 220,
                                              "burstCount": 4,
                                              "burstInterval": 137
                                            }
                                          ]
                                        }
                                        """;

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(versionEight));
            var emitter = DodgeBeatmapSerializer.DeserializeHitObjects(stream).OfType<DodgeEmitter>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(emitter.MoveSource, Is.True);
                Assert.That(emitter.BurstCount, Is.EqualTo(4));
                Assert.That(emitter.BurstInterval, Is.EqualTo(137));
                Assert.That(emitter.BurstBeatDivisor, Is.Zero);
            });
        }
    }
}
