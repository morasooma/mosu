// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestScenePanelUpdateBeatmapButton : OsuTestScene
    {
        private PanelUpdateBeatmapButton button = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = button = new PanelUpdateBeatmapButton
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        });

        [Test]
        public void TestNonUpdatedBeatmap()
        {
            AddStep("non-updated beatmap", () => button.BeatmapSet = new BeatmapSetInfo
            {
                OnlineID = 123,
                Beatmaps =
                {
                    new BeatmapInfo
                    {
                        OnlineID = 456,
                        MD5Hash = "test",
                        OnlineMD5Hash = "online",
                        LastOnlineUpdate = DateTimeOffset.Now,
                    }
                }
            });

            AddAssert("button visible", () => button.Alpha == 1f);
        }

        [Test]
        public void TestNullBeatmap()
        {
            AddStep("null beatmap", () => button.BeatmapSet = null);
            AddAssert("button invisible", () => button.Alpha == 0f);
        }

        [Test]
        public void TestUpdatedBeatmap()
        {
            AddStep("updated beatmap", () => button.BeatmapSet = new BeatmapSetInfo
            {
                OnlineID = 123,
                Beatmaps = { new BeatmapInfo { OnlineID = 456 } }
            });
            AddAssert("button invisible", () => button.Alpha == 0f);
        }

        [Test]
        public void TestServerExclusiveRevisionChangeUpdatesButtonWithoutRebinding()
        {
            BeatmapInfo beatmap = null!;

            AddStep("bind current server revision", () =>
            {
                beatmap = new BeatmapInfo
                {
                    OnlineID = BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD + 1,
                    MD5Hash = "current",
                    OnlineMD5Hash = "current",
                    LastOnlineUpdate = DateTimeOffset.Now,
                };
                button.BeatmapSet = new BeatmapSetInfo
                {
                    OnlineID = BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD + 10,
                    Beatmaps = { beatmap },
                };
            });
            AddAssert("button initially invisible", () => button.Alpha == 0f);
            AddStep("online revision changes", () => beatmap.OnlineMD5Hash = "updated");
            AddUntilStep("update button becomes visible", () => button.Alpha, () => Is.EqualTo(1f));
        }
    }
}
