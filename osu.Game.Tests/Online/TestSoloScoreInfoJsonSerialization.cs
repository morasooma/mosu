// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.IO.Serialization;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Online
{
    /// <summary>
    /// Basic testing to ensure our attribute-based naming is correctly working.
    /// </summary>
    [TestFixture]
    public class TestSoloScoreInfoJsonSerialization
    {
        [Test]
        public void TestScoreSerialisationViaExtensionMethod()
        {
            var score = SoloScoreInfo.ForSubmission(TestResources.CreateTestScoreInfo());

            string serialised = score.Serialize();

            Assert.That(serialised, Contains.Substring("large_tick_hit"));
            Assert.That(serialised, Contains.Substring("\"rank\": \"S\""));
        }

        [Test]
        public void TestScoreSerialisationWithoutSettings()
        {
            var score = SoloScoreInfo.ForSubmission(TestResources.CreateTestScoreInfo());

            string serialised = JsonConvert.SerializeObject(score);

            Assert.That(serialised, Contains.Substring("large_tick_hit"));
            Assert.That(serialised, Contains.Substring("\"rank\":\"S\""));
        }

        /// <summary>
        /// Ensures that the proxy implementations of <see cref="IScoreInfo"/> by <see cref="SoloScoreInfo"/>
        /// do not get serialised to JSON.
        /// </summary>
        [Test]
        public void TestScoreSerialisationSkipsInterfaceMembers()
        {
            var score = SoloScoreInfo.ForSubmission(TestResources.CreateTestScoreInfo());

            string[] variants =
            {
                JsonConvert.SerializeObject(score),
                score.Serialize()
            };

            foreach (string serialised in variants)
            {
                Assert.That(serialised, Does.Not.Contain("\"online_id\":"));
                Assert.That(serialised, Does.Not.Contain("\"user\":"));
                Assert.That(serialised, Does.Not.Contain("\"date\":"));
                Assert.That(serialised, Does.Not.Contain("\"legacy_online_id\":"));
                Assert.That(serialised, Does.Not.Contain("\"beatmap\":"));
                Assert.That(serialised, Does.Not.Contain("\"ruleset\":"));
                Assert.That(serialised, Does.Not.Contain("\"client_state\":"));
            }
        }

        [Test]
        public void TestGameplayIntegrityReportIsSerialised()
        {
            ScoreInfo scoreInfo = TestResources.CreateTestScoreInfo();
            scoreInfo.GameplayIntegrityReport = new GameplayIntegrityReport
            {
                ReplayFrameRate = 60,
                ClockRate = 1,
                ReplayFrameCount = 123,
                ReplayActionPressCount = 42,
                ReplayActionReleaseCount = 41,
                Clock = new GameplayClockIntegrityReport
                {
                    ValidationEnabled = true,
                    PlaybackRateValid = false,
                    DiscrepancyCount = 7,
                    MaxDriftMilliseconds = 350,
                    GameplayElapsedMilliseconds = 30_000,
                    RawGameplayElapsedMilliseconds = 36_500,
                    SeekCount = 1,
                    SeekDeltaMilliseconds = 6_500,
                    AuthorisedSkips =
                    [
                        new GameplaySkipIntegrityEvent
                        {
                            Sequence = 0,
                            Kind = GameplaySkipIntegrityEvent.BREAK,
                            FromMilliseconds = 12_500,
                            ToMilliseconds = 19_000,
                            PeriodStartMilliseconds = 10_000,
                            PeriodEndMilliseconds = 20_000,
                            BreakIndex = 2,
                        },
                    ],
                },
                Difficulty = new GameplayDifficultyIntegrityReport
                {
                    OriginalApproachRate = 9,
                    AppliedApproachRate = 10,
                    CustomApproachRateEnabled = true,
                    VisualOD11Enabled = true,
                },
                Assistance = new GameplayAssistanceIntegrityReport
                {
                    AimAssistEnabled = true,
                    AimAssistAdjustedFrameCount = 50,
                    RelaxGeneratedPressCount = 12,
                },
                InputSources =
                [
                    new GameplayInputTimingReport
                    {
                        Handler = "Mouse",
                        Active = true,
                        EventCount = 456,
                        IntervalCount = 455,
                        ButtonPressCount = 42,
                        ButtonReleaseCount = 41,
                        ActiveDurationMilliseconds = 7500,
                        ExpectedIntervalMilliseconds = 1000.0 / 60,
                        MeanIntervalMilliseconds = 16.67,
                        IntervalStandardDeviationMilliseconds = 0.2,
                        MatchingIntervalRatio = 0.98,
                    },
                ],
            };

            string serialised = JsonConvert.SerializeObject(SoloScoreInfo.ForSubmission(scoreInfo));

            Assert.That(serialised, Does.Contain("\"gameplay_integrity\""));
            Assert.That(serialised, Does.Contain("\"matching_interval_ratio\":0.98"));
            Assert.That(serialised, Does.Contain("\"handler\":\"Mouse\""));
            Assert.That(serialised, Does.Contain("\"version\":4"));
            Assert.That(serialised, Does.Contain("\"button_press_count\":42"));
            Assert.That(serialised, Does.Contain("\"visual_od11_enabled\":true"));
            Assert.That(serialised, Does.Contain("\"aim_assist_adjusted_frame_count\":50"));
            Assert.That(serialised, Does.Contain("\"gameplay_elapsed_ms\":30000.0"));
            Assert.That(serialised, Does.Contain("\"raw_gameplay_elapsed_ms\":36500.0"));
            Assert.That(serialised, Does.Contain("\"seek_count\":1"));
            Assert.That(serialised, Does.Contain("\"seek_delta_ms\":6500.0"));
            Assert.That(serialised, Does.Contain("\"authorised_skips\""));
            Assert.That(serialised, Does.Contain("\"break_index\":2"));
        }
    }
}
