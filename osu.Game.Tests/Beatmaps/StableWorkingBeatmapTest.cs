// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Beatmaps
{
    [HeadlessTest]
    public partial class StableWorkingBeatmapTest : OsuTestScene
    {
        private AudioManager audio = null!;
        private GameHost host = null!;
        private TemporaryNativeStorage? stableStorage;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, GameHost host)
        {
            this.audio = audio;
            this.host = host;
        }

        [TearDown]
        public void TearDown()
        {
            StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
            stableStorage?.Dispose();
            stableStorage = null;
        }

        [Test]
        public void TestStoryboardAndVideoLoadedDirectlyFromStableFolder()
        {
            StableWorkingBeatmap? working = null;
            string videoPath = string.Empty;

            AddStep("create stable beatmap", () =>
            {
                stableStorage = new TemporaryNativeStorage("stable-working-beatmap-storyboard");

                string beatmapPath = stableStorage.GetFullPath("test.osu");
                videoPath = stableStorage.GetFullPath("video.mp4");

                File.WriteAllText(beatmapPath,
                    """
                    osu file format v14

                    [General]
                    AudioFilename: audio.mp3

                    [Metadata]
                    Title:Test title
                    Artist:Test artist
                    Creator:Test creator
                    Version:Hard

                    [Difficulty]
                    HPDrainRate:5
                    CircleSize:4
                    OverallDifficulty:5
                    ApproachRate:5
                    SliderMultiplier:1.4
                    SliderTickRate:1

                    [Events]
                    Video,0,"video.mp4"
                    Sprite,Foreground,Centre,"embedded.png",320,240
                     F,0,0,1000,1

                    [TimingPoints]
                    0,500,4,2,1,100,1,0

                    [HitObjects]
                    256,192,1000,1,0,0:0:0:0:
                    """);

                File.WriteAllText(stableStorage.GetFullPath("Test artist - Test title (Test creator).osb"),
                    """
                    osu file format v14

                    [Events]
                    Sprite,Foreground,Centre,"shared.png",320,240
                     F,0,0,1000,1
                    """);

                File.WriteAllBytes(videoPath, [1, 2, 3]);
                File.WriteAllBytes(stableStorage.GetFullPath("embedded.png"), [4, 5, 6]);
                File.WriteAllBytes(stableStorage.GetFullPath("shared.png"), [7, 8, 9]);

                var set = new BeatmapSetInfo();
                var beatmapInfo = new BeatmapInfo
                {
                    BeatmapSet = set,
                    Metadata = new BeatmapMetadata
                    {
                        Artist = "Test artist",
                        Title = "Test title",
                        Author = new RealmUser { Username = "Test creator" },
                        AudioFile = "audio.mp3",
                    },
                };
                set.Beatmaps.Add(beatmapInfo);

                StablePathManager.Replace(
                    new Dictionary<Guid, string> { [beatmapInfo.ID] = beatmapPath },
                    new Dictionary<Guid, string> { [beatmapInfo.ID] = stableStorage.GetFullPath("audio.mp3") });

                working = new StableWorkingBeatmap(beatmapInfo, audio, host);
            });

            AddAssert("embedded video loaded", () => working!.Storyboard.PrimaryVideo?.Path, () => Is.EqualTo("video.mp4"));
            AddAssert("embedded storyboard loaded", () => working!.Storyboard.Layers.SelectMany(layer => layer.Elements)
                                                                        .OfType<StoryboardSprite>()
                                                                        .Any(sprite => sprite.Source == StoryboardElementSource.Beatmap
                                                                                       && sprite.Path == "embedded.png"));
            AddAssert("shared storyboard loaded", () => working!.Storyboard.Layers.SelectMany(layer => layer.Elements)
                                                                      .OfType<StoryboardSprite>()
                                                                      .Any(sprite => sprite.Source == StoryboardElementSource.Shared
                                                                                     && sprite.Path == "shared.png"));
            AddAssert("video resource resolved", () =>
            {
                using var lookup = new DrawableStoryboard.StoryboardResourceLookupStore(working!.Storyboard, Realm, host);
                using Stream? stream = lookup.GetStream("video.mp4");
                return stream?.Length;
            }, () => Is.EqualTo(3));
        }
    }
}
