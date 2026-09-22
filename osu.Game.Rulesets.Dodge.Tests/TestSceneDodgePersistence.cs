// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public partial class TestSceneDodgePersistence : OsuTestScene
    {
        protected override Ruleset CreateRuleset() => new DodgeRuleset();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private WorkingBeatmap dodgeBeatmap = null!;

        [Test]
        public void TestCreateSaveAndReload()
        {
            AddStep("create persisted dodge difficulty", () =>
            {
                var source = new TestBeatmap(rulesets.GetRuleset(DodgeRuleset.ONLINE_ID)!, false);
                source.Metadata.Title = Guid.NewGuid().ToString();
                var sourceWorking = new TestWorkingBeatmap(source);

                var persistedSource = new TestBeatmap(rulesets.GetRuleset(DodgeRuleset.ONLINE_ID)!, false);
                var importedSet = beatmapManager.Import(persistedSource.BeatmapInfo.BeatmapSet!)!.Value.Detach();

                dodgeBeatmap = beatmapManager.CreateNewDifficulty(importedSet, sourceWorking, rulesets.GetRuleset(DodgeRuleset.ONLINE_ID)!);
            });

            AddAssert("ruleset is dodge", () => dodgeBeatmap.BeatmapInfo.Ruleset.OnlineID == DodgeRuleset.ONLINE_ID);
            AddAssert("compatibility osu exists", () => dodgeBeatmap.BeatmapSetInfo.Files.Any(f => f.Filename.EndsWith(".osu", StringComparison.Ordinal)));
            AddAssert("sidecar exists", () => dodgeBeatmap.BeatmapSetInfo.Files.Any(f => f.Filename.EndsWith(CustomBeatmapFormat.SIDECAR_EXTENSION, StringComparison.Ordinal)));

            AddStep("save bullet", () =>
            {
                var content = (Beatmap<DodgeHitObject>)dodgeBeatmap.GetPlayableBeatmap(dodgeBeatmap.BeatmapInfo.Ruleset);
                content.Difficulty.ApproachRate = DodgeBeatmapSettings.GetApproachRate(1000);
                content.Difficulty.CircleSize = DodgeBeatmapSettings.GetCircleSize(20);
                content.HitObjects.Add(new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 500,
                    Position = new Vector2(100, 120),
                    EndPosition = new Vector2(400, 280),
                });
                content.HitObjects.Add(new DodgeArenaChange
                {
                    StartTime = 2000,
                    Duration = 1000,
                    TargetPosition = new Vector2(60, 40),
                    TargetSize = new Vector2(320, 240),
                });
                content.HitObjects.Add(new DodgeEmitter
                {
                    StartTime = 3000,
                    Duration = 750,
                    Position = new Vector2(256, 192),
                    AimPosition = new Vector2(456, 192),
                    BulletCount = 9,
                    SpreadAngle = 360,
                });

                beatmapManager.Save(dodgeBeatmap.BeatmapInfo, content);
                dodgeBeatmap = beatmapManager.GetWorkingBeatmap(dodgeBeatmap.BeatmapInfo, refetch: true);
            });

            AddAssert("reloaded one dodge bullet", () => dodgeBeatmap.Beatmap.HitObjects.OfType<DodgeBullet>().Single() is not null);
            AddAssert("trajectory reloaded", () => dodgeBeatmap.Beatmap.HitObjects.OfType<DodgeBullet>().Single().EndPosition == new Vector2(400, 280));
            AddAssert("arena change reloaded", () => dodgeBeatmap.Beatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetSize == new Vector2(320, 240));
            AddAssert("emitter reloaded", () =>
            {
                var emitter = dodgeBeatmap.Beatmap.HitObjects.OfType<DodgeEmitter>().Single();
                return emitter.AimPosition == new Vector2(456, 192)
                       && emitter.BulletCount == 9
                       && emitter.SpreadAngle == 360;
            });
            AddAssert("appearance setting reloaded", () => Math.Abs(DodgeBeatmapSettings.GetAppearanceDuration(dodgeBeatmap.Beatmap.Difficulty) - 1000) < 0.001);
            AddAssert("bullet size reloaded", () => Math.Abs(DodgeBeatmapSettings.GetBulletSize(dodgeBeatmap.Beatmap.Difficulty) - 20) < 0.001);

            AddStep("simulate legacy combined hash", () =>
            {
                string osuFilename = dodgeBeatmap.BeatmapInfo.Path!;
                string sidecarFilename = CustomBeatmapFormat.GetSidecarFilename(osuFilename);

                string osuStoragePath = dodgeBeatmap.BeatmapSetInfo.GetPathForFile(osuFilename)
                                        ?? throw new InvalidOperationException("The beatmap file is missing from the beatmap set.");
                string sidecarStoragePath = dodgeBeatmap.BeatmapSetInfo.GetPathForFile(sidecarFilename)
                                            ?? throw new InvalidOperationException("The sidecar file is missing from the beatmap set.");

                using var osuStream = dodgeBeatmap.GetStream(osuStoragePath);
                using var sidecarStream = dodgeBeatmap.GetStream(sidecarStoragePath);
                using var combinedStream = new System.IO.MemoryStream();

                osuStream!.CopyTo(combinedStream);
                sidecarStream!.CopyTo(combinedStream);
                string legacyHash = combinedStream.ComputeSHA2Hash();
                Guid beatmapId = dodgeBeatmap.BeatmapInfo.ID;

                realm.Write(r => r.Find<BeatmapInfo>(beatmapId)!.Hash = legacyHash);
                dodgeBeatmap = beatmapManager.GetWorkingBeatmap(dodgeBeatmap.BeatmapInfo, refetch: true);
            });

            AddAssert("legacy map recovered", () => dodgeBeatmap.Beatmap.HitObjects.OfType<DodgeBullet>().Single() is not null);
        }
    }
}
