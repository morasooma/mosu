// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Osu.UI.TagCoop;
using osu.Game.Rulesets.Replays;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TagCoopCompatibilityReplayTest : OsuTestScene
    {
        [Test]
        public void TestHeldButtonIsReleasedBetweenOwners()
        {
            IReadOnlyList<ReplayFrame>? frames = null;

            AddStep("create compatibility replay", () =>
            {
                var beatmap = new OsuBeatmap
                {
                    HitObjects =
                    {
                        new HitCircle { StartTime = 1000, ComboIndex = 0, Position = new Vector2(128, 192) },
                        new HitCircle { StartTime = 1100, ComboIndex = 1, Position = new Vector2(384, 192) },
                    }
                };

                foreach (OsuHitObject hitObject in beatmap.HitObjects)
                    hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                Child = new DrawableOsuRuleset(new OsuRuleset(), beatmap);
                frames = OsuTagCoopGameplayController.CreateCompatibilityReplayFrames((DrawableOsuRuleset)Child, new TagCoopReplayMetadata
                {
                    Players =
                    [
                        new TagCoopReplayPlayer { UserID = 1, Username = "first" },
                        new TagCoopReplayPlayer { UserID = 2, Username = "second" },
                    ],
                    Frames =
                    [
                        new TagCoopReplayFrame { UserID = 1, Sequence = 1, GameplayTime = 1000, X = 0.25f, Y = 0.5f, ButtonState = 1 },
                        new TagCoopReplayFrame { UserID = 2, Sequence = 1, GameplayTime = 1100, X = 0.75f, Y = 0.5f, ButtonState = 1 },
                    ]
                });
            });

            AddAssert("release inserted between owner presses", () => frames!.OfType<OsuReplayFrame>().Select(frame => frame.Actions.Count).ToArray(),
                () => Is.EqualTo(new[] { 1, 0, 1 }));
        }
    }
}
