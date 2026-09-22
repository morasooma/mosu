// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public partial class TestSceneDodgeGameplayCompletion : ScreenTestScene
    {
        private TestReplayPlayer player = null!;

        [Test]
        [Category("ExternalRegression")]
        public void TestExportedAutoplayReachesResultsScreen()
        {
            string? mapDirectory = Environment.GetEnvironmentVariable("DODGE_REGRESSION_MAP_DIR");

            if (string.IsNullOrWhiteSpace(mapDirectory) || !Directory.Exists(mapDirectory))
            {
                Assert.Ignore("Set DODGE_REGRESSION_MAP_DIR to run exported Dodge maps.");
                return;
            }

            string[] beatmapFiles = Directory.GetFiles(mapDirectory, "*.osu", SearchOption.AllDirectories);
            string? mapFilter = Environment.GetEnvironmentVariable("DODGE_REGRESSION_MAP_FILTER");

            if (!string.IsNullOrWhiteSpace(mapFilter))
            {
                beatmapFiles = beatmapFiles.Where(path =>
                                                   Path.GetFileNameWithoutExtension(path).Contains(
                                                       mapFilter,
                                                       StringComparison.OrdinalIgnoreCase))
                                           .ToArray();
            }

            Assert.That(beatmapFiles, Is.Not.Empty, $"No matching .osu file found below {mapDirectory}.");

            string beatmapFile = beatmapFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).First();
            string sidecarFile = Path.ChangeExtension(beatmapFile, ".ruleset.json");
            Assert.That(File.Exists(sidecarFile), Is.True, $"Missing Dodge sidecar for {beatmapFile}.");

            IBeatmap exportedBeatmap = DodgeExportedMapRegressionTest.DecodeBeatmap(beatmapFile, sidecarFile);
            Storyboard exportedStoryboard = decodeStoryboard(beatmapFile);
            var autoplay = new DodgeModAutoplay();
            Score replayScore = autoplay.CreateScoreFromReplayData(exportedBeatmap, [autoplay]);

            AddStep("set exported beatmap", () =>
            {
                Beatmap.Value = new ExportedWorkingBeatmap(exportedBeatmap, exportedStoryboard, Audio, mapDirectory);
                Ruleset.Value = exportedBeatmap.BeatmapInfo.Ruleset;
                SelectedMods.Value = [autoplay];
                player = new TestReplayPlayer(replayScore, false, true);
            });
            AddStep("load replay player", () => LoadScreen(player));
            AddUntilStep("replay player loaded", () => player.IsLoaded && player.IsCurrentScreen());
            AddUntilStep("gameplay clock started", () => !player.GameplayClockContainer.IsPaused.Value && player.GameplayClockContainer.CurrentTime > 0);
            AddStep("play replay at 2x", () => ((MasterGameplayClockContainer)player.GameplayClockContainer).UserPlaybackRate.Value = 2);

            for (int segment = 1; segment <= 6; segment++)
            {
                double progress = segment / 6d;
                AddUntilStep($"play through {progress:P0}", () =>
                    Stack.CurrentScreen is ResultsScreen
                    || Beatmap.Value.Track.CurrentTime >= Beatmap.Value.Track.Length * progress - 1);
            }

            AddWaitStep("allow completion transition", 180);
            AddStep("score processor completes", () =>
            {
                int maxHits = (int)typeof(JudgementProcessor)
                                    .GetProperty("MaxHits", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .GetValue(player.ScoreProcessor)!;
                var drawableRuleset = (DrawableDodgeRuleset)player.DrawableRuleset;
                var unjudged = drawableRuleset.Playfield.AllHitObjects
                                              .Where(drawable => !drawable.Result.HasResult)
                                              .GroupBy(drawable => drawable.HitObject.GetType().Name)
                                              .Select(group => $"{group.Key}:{group.Count()}");

                string completionState =
                    $"completion: clock={player.GameplayClockContainer.CurrentTime:N3}, track={Beatmap.Value.Track.CurrentTime:N3}/{Beatmap.Value.Track.Length:N3}, "
                    + $"hits={player.ScoreProcessor.JudgedHits}/{maxHits}, unjudged=[{string.Join(", ", unjudged)}]";

                Assert.That(player.ScoreProcessor.HasCompleted.Value, Is.True, completionState);
            });
            AddUntilStep("results screen displayed", () => Stack.CurrentScreen is ResultsScreen { IsLoaded: true });
        }

        private static Storyboard decodeStoryboard(string beatmapFile)
        {
            using var stream = File.OpenRead(beatmapFile);
            using var reader = new LineBufferedReader(stream);
            return Decoder.GetDecoder<Storyboard>(reader).Decode(reader);
        }

        private sealed class ExportedWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly NativeStorage storage;
            private readonly StorageBackedResourceStore resourceStore;
            private readonly ITrackStore trackStore;
            private readonly Track audioTrack;

            public ExportedWorkingBeatmap(IBeatmap beatmap, Storyboard storyboard, AudioManager audioManager, string mapDirectory)
                : base(beatmap, storyboard, audioManager)
            {
                storage = new NativeStorage(mapDirectory);
                resourceStore = new StorageBackedResourceStore(storage);
                trackStore = audioManager.GetTrackStore(resourceStore);
                audioTrack = trackStore.Get(Path.GetFileName(beatmap.Metadata.AudioFile));
                storyboard.BeatmapInfo = BeatmapInfo;
                LoadTrack();
            }

            protected override Track GetBeatmapTrack() => audioTrack;

            public override Stream? GetStream(string storagePath) => storage.GetStream(storagePath);

            public override Texture? GetBackground() => null;

            protected override ISkin? GetSkin() => null;

            public override bool TryTransferTrack(WorkingBeatmap target) => false;
        }
    }
}
