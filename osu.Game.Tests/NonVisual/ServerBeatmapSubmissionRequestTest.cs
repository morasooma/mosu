// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Submission;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ServerBeatmapSubmissionRequestTest
    {
        [Test]
        public void TestExistingServerExclusiveUploadContractIsUnchanged()
        {
            var request = new UploadServerBeatmapSetRequest([1, 2, 3], "set.osz");

            Assert.Multiple(() =>
            {
                Assert.That(request.BeatmapSetID, Is.Null);
                Assert.That(request.BaseRevision, Is.Null);
                Assert.That(request.SubmissionTarget, Is.Null);
                Assert.That(request.SourceBeatmapSetID, Is.Null);
                Assert.That(request.SourceBeatmapID, Is.Null);
            });
        }

        [Test]
        public void TestNewDodgeUploadCarriesPublicationAndAttribution()
        {
            var request = UploadServerBeatmapSetRequest.CreateDodge(
                [1, 2, 3],
                "dodge.osz",
                BeatmapSubmissionTarget.WIP,
                123,
                456);

            Assert.Multiple(() =>
            {
                Assert.That(request.BeatmapSetID, Is.Null);
                Assert.That(request.BaseRevision, Is.Null);
                Assert.That(request.SubmissionTarget, Is.EqualTo(BeatmapSubmissionTarget.WIP));
                Assert.That(request.SourceBeatmapSetID, Is.EqualTo(123));
                Assert.That(request.SourceBeatmapID, Is.EqualTo(456));
            });
        }

        [Test]
        public void TestDodgeUpdateCarriesManagedSetAndRevision()
        {
            var request = UploadServerBeatmapSetRequest.UpdateDodge(
                2_000_000_000,
                7,
                [1, 2, 3],
                "dodge.osz",
                BeatmapSubmissionTarget.Pending,
                null,
                null);

            Assert.Multiple(() =>
            {
                Assert.That(request.BeatmapSetID, Is.EqualTo(2_000_000_000));
                Assert.That(request.BaseRevision, Is.EqualTo(7));
                Assert.That(request.SubmissionTarget, Is.EqualTo(BeatmapSubmissionTarget.Pending));
            });
        }

        [Test]
        public void TestUploadStateDeserialisesServerContract()
        {
            const string json = """
                                {
                                  "beatmapset_id": 2000000000,
                                  "revision": 3,
                                  "status": "wip",
                                  "updates_remaining": 2,
                                  "rate_limit_reset_at": "2026-07-30T00:00:00+00:00",
                                  "source_beatmapset_id": 123,
                                  "source_beatmap_id": 456
                                }
                                """;

            var state = JsonConvert.DeserializeObject<ServerBeatmapSetUploadState>(json)!;

            Assert.Multiple(() =>
            {
                Assert.That(state.BeatmapSetID, Is.EqualTo(2_000_000_000));
                Assert.That(state.Revision, Is.EqualTo(3));
                Assert.That(state.Status, Is.EqualTo("wip"));
                Assert.That(state.UpdatesRemaining, Is.EqualTo(2));
                Assert.That(state.SourceBeatmapSetID, Is.EqualTo(123));
                Assert.That(state.SourceBeatmapID, Is.EqualTo(456));
            });
        }

        [Test]
        public void TestServerDodgeBeatmapUsesDodgeShortName()
        {
            var beatmap = new APIBeatmap { RulesetID = 10 };

            Assert.That(beatmap.Ruleset.ShortName, Is.EqualTo(RulesetInfo.DODGE_MODE_SHORTNAME));
        }

        [Test]
        public void TestSongSelectGenericUploadIsBlockedForDodge()
        {
            var osuRuleset = new RulesetInfo(RulesetInfo.OSU_MODE_SHORTNAME, "osu!", string.Empty, 0);
            var dodgeRuleset = new RulesetInfo(RulesetInfo.DODGE_MODE_SHORTNAME, "Dodge", string.Empty, 10);
            var osuBeatmap = new BeatmapInfo { Ruleset = osuRuleset };
            var dodgeBeatmap = new BeatmapInfo { Ruleset = dodgeRuleset };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapLeaderboardWedge.AllowsGenericServerUpload(osuBeatmap, osuRuleset), Is.True);
                Assert.That(BeatmapLeaderboardWedge.AllowsGenericServerUpload(osuBeatmap, dodgeRuleset), Is.False);
                Assert.That(BeatmapLeaderboardWedge.AllowsGenericServerUpload(dodgeBeatmap, dodgeRuleset), Is.False);
                Assert.That(BeatmapLeaderboardWedge.AllowsGenericServerUpload(dodgeBeatmap, osuRuleset), Is.False);
            });
        }
    }
}
