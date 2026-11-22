// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Overlays;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Beatmaps
{
    public partial class TestSceneBeatmapCardThumbnail : OsuManualInputManagerTestScene
    {
        private PlayButton playButton => this.ChildrenOfType<PlayButton>().Single();

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Test]
        public void TestThumbnailPreview()
        {
            BeatmapCardThumbnail thumbnail = null;
            osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager tracks = null;

            AddStep("create thumbnail", () =>
            {
                // Exercise the actual PlayButton without depending on an online
                // asset or making sound on the developer's machine.
                tracks = new osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager(new osu.Framework.Audio.AudioAdjustments());
                var beatmapSet = CreateAPIBeatmapSet(Ruleset.Value);
                beatmapSet.OnlineID = 241526; // ID hardcoded to ensure that the preview track exists online.

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (System.Type, object)[] { (typeof(osu.Game.Audio.PreviewTrackManager), tracks) },
                    Children = new Drawable[]
                    {
                        tracks,
                        thumbnail = new BeatmapCardThumbnail(beatmapSet, beatmapSet)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(200)
                        }
                    }
                };
            });
            AddStep("enable dim", () => thumbnail.Dimmed.Value = true);
            AddUntilStep("button visible", () => playButton.Alpha == 1);

            AddStep("click button", () =>
            {
                InputManager.MoveMouseTo(playButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("wait for start", () => playButton.Playing.Value && playButton.Enabled.Value);
            iconIs(FontAwesome.Solid.Stop);

            AddStep("click again", () =>
            {
                InputManager.MoveMouseTo(playButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("wait for stop", () => !playButton.Playing.Value && playButton.Enabled.Value);
            iconIs(FontAwesome.Solid.Play);

            AddStep("click again", () =>
            {
                InputManager.MoveMouseTo(playButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("wait for start", () => playButton.Playing.Value && playButton.Enabled.Value);
            iconIs(FontAwesome.Solid.Stop);

            AddStep("disable dim", () => thumbnail.Dimmed.Value = false);
            AddWaitStep("wait some", 3);
            AddAssert("button still visible", () => playButton.Alpha == 1);

            // The track plays in real-time, so we need to check for progress in increments to avoid timeout.
            AddStep("seek virtual preview to 30%", () => ((osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager.TestPreviewTrack)
                tracks.CurrentTrack).Track.Seek(30000));
            AddUntilStep("progress > 0.25", () => thumbnail.ChildrenOfType<PlayButton>().Single().Progress.Value > 0.25);
            AddStep("seek virtual preview to 60%", () => ((osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager.TestPreviewTrack)
                tracks.CurrentTrack).Track.Seek(60000));
            AddUntilStep("progress > 0.5", () => thumbnail.ChildrenOfType<PlayButton>().Single().Progress.Value > 0.5);
            AddStep("seek virtual preview to 90%", () => ((osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager.TestPreviewTrack)
                tracks.CurrentTrack).Track.Seek(90000));
            AddUntilStep("progress > 0.75", () => thumbnail.ChildrenOfType<PlayButton>().Single().Progress.Value > 0.75);
            AddStep("finish virtual preview", () => tracks.CurrentTrack.Stop());

            AddUntilStep("wait for track to end", () => !playButton.Playing.Value);
            AddUntilStep("button hidden", () => playButton.Alpha == 0);
        }

        private void iconIs(IconUsage usage) => AddUntilStep("icon is correct", () => playButton.ChildrenOfType<SpriteIcon>().Any(icon => icon.Icon.Equals(usage)));
    }
}
